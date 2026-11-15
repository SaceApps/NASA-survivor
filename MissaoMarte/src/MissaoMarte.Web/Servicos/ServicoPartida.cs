using System.Text.Json;
using MissaoMarte.Nucleo.Jogo;
using Microsoft.AspNetCore.Components;

namespace MissaoMarte.Web.Servicos;

public sealed record ResumoPartida(string Id, string Nome, int Sol, int Sols, FaseMissao Fase, int Tripulantes, DateTime SalvaEm);

/// <summary>
/// A MESA: guarda a missão em andamento, aplica as jogadas uma de cada vez e avisa todos os aparelhos ligados.
/// A partida de verdade só é tocada dentro da trava. As telas desenham uma FOTO dela, tirada depois de cada jogada
/// aceita, então nenhum aparelho lê a partida enquanto outro a altera. Cada jogada aceita é gravada em disco
/// (pasta "partidas"): fechar o navegador ou o computador não perde nada.
/// </summary>
public sealed class ServicoPartida
{
    private readonly object _trava = new();
    private readonly string _pasta;
    private readonly ILogger<ServicoPartida> _log;
    private Partida? _viva;          // a partida de verdade: só dentro da trava
    private Partida? _foto;          // cópia que as telas desenham; nunca é alterada depois de publicada
    private string? _ultimoJson;     // o estado da última jogada aceita, para desfazer uma jogada que estourou
    private long _versao;

    /// <summary>A foto mais recente da missão, só para leitura. As jogadas passam por <see cref="Fazer"/>.</summary>
    public Partida? Atual => Volatile.Read(ref _foto);
    /// <summary>Sobe a cada mudança aceita: serve para saber se o que a tela mostrou ainda vale.</summary>
    public long Versao => Interlocked.Read(ref _versao);
    public event Action? Mudou;

    public ServicoPartida(IWebHostEnvironment env, ILogger<ServicoPartida> log)
    {
        _log = log;
        _pasta = Path.Combine(env.ContentRootPath, "partidas");
        Directory.CreateDirectory(_pasta);
    }

    public Resultado Fazer(Func<Partida, Resultado> jogada)
    {
        Resultado r;
        lock (_trava)
        {
            if (_viva is null) return Resultado.Nao("Não há missão em andamento.");
            try
            {
                r = jogada(_viva);
            }
            catch (Exception ex)
            {
                // a jogada estourou no meio: a partida volta ao estado da última jogada aceita
                _log.LogError(ex, "jogada falhou");
                if (_ultimoJson != null)
                {
                    _viva = Partida.DeJson(_ultimoJson);
                    Volatile.Write(ref _foto, Partida.DeJson(_ultimoJson));
                }
                r = Resultado.Nao("Algo deu errado nesta jogada. Nada foi mudado.");
            }
            if (r.Ok && !Publicar())
                r = Resultado.Sim($"{r.Mensagem} Atenção: não foi possível gravar a missão no disco agora.".Trim());
        }
        Mudou?.Invoke();
        return r;
    }

    public RelatorioSol? Prever()
    {
        lock (_trava) { return _viva?.Prever(); }
    }

    public void Nova(string nome, IEnumerable<(string Nome, Profissao Profissao)> jogadores, int nivel)
    {
        lock (_trava) { _viva = Partida.Nova(nome, jogadores, nivel); Publicar(); }
        Mudou?.Invoke();
    }

    public bool Carregar(string id)
    {
        lock (_trava)
        {
            var arquivo = Path.Combine(_pasta, Path.GetFileName(id) + ".json");
            if (!File.Exists(arquivo)) return false;
            try { _viva = Partida.DeJson(File.ReadAllText(arquivo)); }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or IOException)
            {
                _log.LogWarning(ex, "partida ilegível: {Id}", id);
                return false;
            }
            Publicar();
        }
        Mudou?.Invoke();
        return true;
    }

    public void Sair()
    {
        lock (_trava)
        {
            _viva = null; _ultimoJson = null;
            Volatile.Write(ref _foto, null);
            Interlocked.Increment(ref _versao);
        }
        Mudou?.Invoke();
    }

    public List<ResumoPartida> Salvas()
    {
        var lista = new List<ResumoPartida>();
        foreach (var arquivo in Directory.GetFiles(_pasta, "*.json"))
        {
            try
            {
                var p = Partida.DeJson(File.ReadAllText(arquivo));
                lista.Add(new ResumoPartida(Path.GetFileNameWithoutExtension(arquivo), p.Nome, p.Sol, p.SolsDaMissao, p.Fase, p.Tripulacao.Count, File.GetLastWriteTime(arquivo)));
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or IOException) { _log.LogWarning(ex, "ignorando {Arquivo}", arquivo); }
        }
        return lista.OrderByDescending(r => r.SalvaEm).ToList();
    }

    /// <summary>Tira a foto da partida para as telas e grava no disco. Devolve false se a gravação falhou (a jogada vale mesmo assim).</summary>
    private bool Publicar()
    {
        string json = _viva!.ParaJson();
        _ultimoJson = json;
        Volatile.Write(ref _foto, Partida.DeJson(json));
        Interlocked.Increment(ref _versao);
        try
        {
            var arquivo = Path.Combine(_pasta, Path.GetFileName(_viva.Id) + ".json");
            var tmp = arquivo + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, arquivo, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.LogWarning(ex, "não foi possível gravar a partida");
            return false;
        }
    }
}

/// <summary>O que este aparelho está olhando: a aba, o tripulante escolhido, o hexágono tocado e o nível do texto.</summary>
public sealed class Visao
{
    public string Aba { get; private set; } = "comando";
    public int TripulanteId { get; private set; }
    public Hex? Hex { get; private set; }
    /// <summary>0 = segue o nível da missão; 1 = 7 a 8 anos; 2 = 9 a 10; 3 = 11 a 12.</summary>
    public int NivelTexto { get; private set; }
    public string? Mensagem { get; private set; }
    public bool MensagemOk { get; private set; }
    public event Action? Mudou;

    public void IrPara(string aba) { Aba = aba; Mudou?.Invoke(); }
    public void Escolher(int tripulanteId) { TripulanteId = tripulanteId; Mudou?.Invoke(); }
    public void Tocar(Hex? h) { Hex = h; Mudou?.Invoke(); }
    public void Nivel(int n) { NivelTexto = n; Mudou?.Invoke(); }
    public void Avisar(Resultado r)
    {
        if (string.IsNullOrEmpty(r.Mensagem)) { if (r.Ok) Mensagem = null; }
        else { Mensagem = r.Mensagem; MensagemOk = r.Ok; }
        Mudou?.Invoke();
    }
    public void Limpar() { Mensagem = null; Mudou?.Invoke(); }
}

/// <summary>Base das telas: redesenha quando a mesa muda (qualquer aparelho) ou quando a visão deste aparelho muda.</summary>
public abstract class PainelBase : ComponentBase, IDisposable
{
    [Inject] protected ServicoPartida Svc { get; set; } = default!;
    [Inject] protected Visao V { get; set; } = default!;
    /// <summary>A foto da missão para desenhar. Para jogar, use <see cref="Fazer"/>: a jogada recebe a partida de verdade.</summary>
    protected Partida P => Svc.Atual!;
    protected int NivelTexto => V.NivelTexto == 0 ? (Svc.Atual?.Nivel ?? 2) : V.NivelTexto;

    protected override void OnInitialized() { Svc.Mudou += Redesenhar; V.Mudou += Redesenhar; }
    private void Redesenhar() => _ = InvokeAsync(StateHasChanged);
    public void Dispose() { Svc.Mudou -= Redesenhar; V.Mudou -= Redesenhar; GC.SuppressFinalize(this); }

    protected void Fazer(Func<Partida, Resultado> jogada) => V.Avisar(Svc.Fazer(jogada));

    protected static string Classe(Gravidade g) => g switch { Gravidade.Perigo => "perigo", Gravidade.Atencao => "atencao", _ => "bom" };
    protected static string Rotulo(Gravidade g) => g switch { Gravidade.Perigo => "Perigo", Gravidade.Atencao => "Atenção", _ => "Tudo bem" };
    protected static string Horas(double h) => h % 1 == 0 ? $"{h:0} h" : $"{h:0.#} h";
}
