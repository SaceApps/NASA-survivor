using System.Text.Json.Serialization;
using MissaoMarte.Nucleo.Simulacao;

namespace MissaoMarte.Nucleo.Jogo;

/// <summary>
/// UMA PARTIDA: o estado do jogo e as regras. As crianças decidem (quem faz o quê, o que ligar, para onde ir);
/// o software valida a jogada, anota e, no fim do sol, roda as 24 horas do motor de simulação com tudo o que foi
/// combinado. A física mora no Motor; aqui moram as regras de jogo (horas, profissões, saúde, ânimo, objetivos).
/// </summary>
public sealed partial class Partida
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Nome { get; set; } = "Missão Jezero";
    /// <summary>1 = 7 a 8 anos; 2 = 9 a 10 anos; 3 = 11 a 12 anos. A física é a mesma: muda o roteiro e o que a tela mostra.</summary>
    public int Nivel { get; set; } = 2;
    public int Sol { get; set; }
    public int SolsDaMissao { get; set; } = 8;
    public FaseMissao Fase { get; set; } = FaseMissao.EmAndamento;
    public string MotivoFim { get; set; } = "";
    public double Moral { get; set; } = 70;
    public bool RacionamentoHigiene { get; set; }
    public List<Tripulante> Tripulacao { get; set; } = new();
    public List<Celula> Celulas { get; set; } = new();
    public Veiculo Veiculo { get; set; } = new();
    public EstadoBase Base { get; set; } = new();
    public List<EventoRoteiro> Roteiro { get; set; } = new();
    public List<double> TauPorSol { get; set; } = new();
    public List<ServicoAgendado> Servicos { get; set; } = new();
    public List<RelatorioSol> Relatorios { get; set; } = new();
    public List<string> Diario { get; set; } = new();
    public int AmostrasNaBase { get; set; }
    public int AmostrasAnalisadas { get; set; }
    public double ArgilaEntregueKg { get; set; }
    public bool PrevisaoRevelada { get; set; }
    public bool InspecaoFeita { get; set; }
    public int DescobertasNoSol { get; set; }
    public DateTime CriadaEm { get; set; } = DateTime.Now;

    [JsonIgnore] public ParametrosCenario P { get; } = ParametrosCenario.Padrao();
    [JsonIgnore] public int HorasPorSol => Moral < 30 ? 6 : Moral >= 85 ? 9 : 8;
    [JsonIgnore] public const int RaioDoMapa = 4;

    // ───────────────────────── criação ─────────────────────────
    public static Partida Nova(string nome, IEnumerable<(string Nome, Profissao Profissao)> jogadores, int nivel)
    {
        var pt = new Partida { Nome = string.IsNullOrWhiteSpace(nome) ? "Missão Jezero" : nome.Trim(), Nivel = Math.Clamp(nivel, 1, 3) };
        pt.Base = EstadoBase.Inicial(pt.P);
        pt.MassaInicialKg = pt.Base.MassaTotalKg();
        int id = 0;
        foreach (var j in jogadores)
            pt.Tripulacao.Add(new Tripulante { Id = id++, Nome = string.IsNullOrWhiteSpace(j.Nome) ? j.Profissao.Nome() : j.Nome.Trim(), Profissao = j.Profissao });
        if (pt.Tripulacao.Count < 2) throw new ArgumentException("A missão precisa de pelo menos 2 tripulantes.");
        pt.Celulas = MapaJezero.Criar();
        pt.MontarRoteiro();
        pt.Diario.Add("A tripulação chegou a Jezero. A base está funcionando.");
        return pt;
    }

    private void MontarRoteiro()
    {
        double limpo = P.TauCeuLimpo;
        // a poeira de cada sol (dado de entrada do motor): uma tempestade regional no meio da missão
        TauPorSol = Nivel == 1
            ? new() { limpo, limpo, limpo, 1.2, 2.0, 1.4, limpo, limpo }
            : new() { limpo, limpo, limpo, 1.8, 3.2, 2.4, 1.0, limpo };
        Roteiro = new()
        {
            new() { Sol = 0, Hora = 3, Equip = TipoEquip.Vedacao, NovaSaude = Nivel == 1 ? 0.8 : 0.65,
                Titulo = "Vazamento na vedação", Texto = "O frio da noite encolheu a borracha da porta e o ar começou a escapar." },
        };
        if (Nivel >= 2)
        {
            Roteiro.Add(new() { Sol = 1, Hora = 14, Equip = TipoEquip.ProcessadorAgua, NovaSaude = 0.3,
                Titulo = "Filtro de água saturado", Texto = "O filtro do processador de água encheu de sujeira e está limpando bem menos." });
            Roteiro.Add(new() { Sol = 4, Hora = 1, Equip = TipoEquip.RemovedorCo2, NovaSaude = 0.0,
                Titulo = "Removedor de CO2 parou", Texto = "A poeira fina da tempestade travou uma válvula do removedor de CO2." });
        }
        if (Nivel >= 3)
            Roteiro.Add(new() { Sol = 5, Hora = 12, Equip = TipoEquip.GeradorO2, NovaSaude = 0.4,
                Titulo = "Gerador de oxigênio fraco", Texto = "Uma célula do gerador de oxigênio desgastou e ele está produzindo menos." });
    }

    // ───────────────────────── consultas ─────────────────────────
    public Celula? Cel(Hex h) => Celulas.FirstOrDefault(c => c.Pos == h);
    public Tripulante Trip(int id) => Tripulacao.First(t => t.Id == id);
    public double HorasLivres(Tripulante t) => t.Incapacitado ? 0 : Math.Max(0, HorasPorSol - t.HorasUsadas);
    public static double HoraDoRelogio(Tripulante t) => 8 + t.HorasUsadas;
    [JsonIgnore] public double Tau => TauPorSol[Math.Min(Sol, TauPorSol.Count - 1)];
    [JsonIgnore] public double FracaoExplorada => Celulas.Count(c => c.Explorado) / (double)Celulas.Count;
    [JsonIgnore] public int AmostrasChegando => (int)Math.Round(Servicos.Where(s => s.Tipo == "amostras").Sum(s => s.Quantidade));
    [JsonIgnore] public double ArgilaChegandoKg => Servicos.Where(s => s.Tipo == "descarga").Sum(s => s.Quantidade);
    [JsonIgnore] public IEnumerable<Tripulante> NoVeiculo => Tripulacao.Where(t => t.NoVeiculo && !t.Incapacitado && t.Pos == Veiculo.Pos);

    [JsonIgnore] public double Ppo2Kpa => Fisica.PressaoParcialKpa(Base.O2Kg, Fisica.MO2, Base.TemperaturaC, P.VolumeCabineM3);
    [JsonIgnore] public double Ppco2Kpa => Fisica.PressaoParcialKpa(Base.Co2Kg, Fisica.MCO2, Base.TemperaturaC, P.VolumeCabineM3);
    [JsonIgnore] public double PressaoKpa => Motor.PressaoTotalKpa(Base, P);
    [JsonIgnore] public double Umidade => Fisica.PressaoParcialKpa(Base.VaporKg, Fisica.MH2O, Base.TemperaturaC, P.VolumeCabineM3) / Fisica.PressaoSaturacaoKpa(Base.TemperaturaC);
    [JsonIgnore] public double BateriaFracao => Base.BateriaWh / P.BateriaCapacidadeWh;

    public List<Objetivo> Objetivos() => new()
    {
        new() { Id = "sobreviver", Texto = $"Chegar ao fim do sol {SolsDaMissao} com toda a tripulação bem", Atual = Math.Min(Sol, Tripulacao.Any(t => t.Incapacitado) ? SolsDaMissao - 1 : SolsDaMissao), Meta = SolsDaMissao },
        new() { Id = "ciencia", Texto = "Analisar 3 amostras de rocha no laboratório", Atual = AmostrasAnalisadas, Meta = 3 },
        new() { Id = "agua", Texto = "Tirar 5 kg de água do solo de Marte", Atual = Math.Floor(Base.AguaExtraidaDeMarteKg * 10 + 1e-6) / 10, Meta = 5 },
        new() { Id = "explorar", Texto = "Explorar mais da metade do mapa", Atual = Math.Round(FracaoExplorada * 100), Meta = 50, Obrigatorio = false },
    };

    private void Anotar(string texto) => Diario.Add($"Sol {Sol + 1}: {texto}");

    private void Gastar(Tripulante t, double horas, bool fora)
    {
        double inicio = HoraDoRelogio(t);
        t.HorasUsadas += horas;
        if (fora) t.Fora.Add(new Intervalo { Inicio = inicio, Fim = inicio + horas });
    }

    private Resultado? Impedido(Tripulante t, double horas)
    {
        if (Fase != FaseMissao.EmAndamento) return Resultado.Nao("A missão já terminou.");
        if (t.Incapacitado) return Resultado.Nao($"{t.Nome} precisa de cuidados médicos e não pode agir.");
        if (HorasLivres(t) + 1e-9 < horas) return Resultado.Nao($"{t.Nome} só tem {HorasLivres(t):0.#} h livres neste sol e isso leva {horas:0.#} h.");
        return null;
    }

    // ───────────────────────── chaves da base ─────────────────────────
    public Resultado Alternar(TipoEquip tipo)
    {
        var eq = Base.Eq(tipo);
        if (!eq.UsaEnergia) return Resultado.Nao("A vedação não tem chave: ela precisa de conserto.");
        eq.Ligado = !eq.Ligado;
        Anotar($"{eq.Nome}: {(eq.Ligado ? "ligado" : "desligado")} pela equipe.");
        return Resultado.Sim();
    }

    public Resultado MudarPrioridade(TipoEquip tipo, int prioridade)
    {
        var eq = Base.Eq(tipo);
        if (!eq.UsaEnergia) return Resultado.Nao("A vedação não usa energia.");
        eq.Prioridade = Math.Clamp(prioridade, 1, 4);
        return Resultado.Sim();
    }

    public Resultado AlternarRacionamento()
    {
        RacionamentoHigiene = !RacionamentoHigiene;
        Anotar(RacionamentoHigiene ? "A equipe decidiu economizar água no banho." : "Fim da economia de água no banho.");
        return Resultado.Sim();
    }

    // ───────────────────────── rotinas e ações dentro da base ─────────────────────────
    public Resultado FazerRotina(int id)
    {
        var t = Trip(id);
        if (Impedido(t, 1) is { } r) return r;
        if (!t.NaBase) return Resultado.Nao("A rotina é feita na base.");
        if (t.RotinaFeita) return Resultado.Nao($"{t.Nome} já fez a rotina deste sol.");
        string feito;
        switch (t.Profissao)
        {
            case Profissao.Comandante:
                Moral = Math.Min(100, Moral + 6); feito = "reuniu a equipe e combinou o plano do dia. O ânimo subiu."; break;
            case Profissao.Engenheiro:
                InspecaoFeita = true;
                foreach (var eq in Base.Equipamentos) eq.Desgaste = Math.Max(0, eq.Desgaste - 0.25);
                feito = "inspecionou o suporte à vida. As máquinas vão durar mais."; break;
            case Profissao.Cientista:
                if (!Base.Eq(TipoEquip.Comunicacao).Ligado) return Resultado.Nao("A previsão do tempo vem pela comunicação com a Terra, que está desligada.");
                PrevisaoRevelada = true; feito = "recebeu a previsão do tempo de Marte para o próximo sol."; break;
            case Profissao.Geologo:
            {
                var novas = Celulas.Where(c => !c.Explorado && c.Pos.Vizinhos().Any(v => Cel(v)?.Explorado == true))
                    .OrderBy(c => c.Pos.Distancia(Hex.Origem)).ThenBy(c => c.Pos.Q).ThenBy(c => c.Pos.R).Take(3).ToList();
                foreach (var c in novas) c.Explorado = true;
                feito = $"estudou imagens de órbita e mapeou {novas.Count} regiões novas."; break;
            }
            case Profissao.Medico:
                foreach (var o in Tripulacao.Where(o => o.NaBase && !o.Incapacitado)) o.Saude = Math.Min(100, o.Saude + 5);
                feito = "examinou a tripulação. Todos na base ficaram um pouco melhor."; break;
            default:
                Veiculo.Revisado = true; feito = "revisou o veículo. Hoje ele gasta menos bateria."; break;
        }
        t.RotinaFeita = true;
        Gastar(t, 1, false);
        Anotar($"{t.Nome} {feito}");
        return Resultado.Sim($"{t.Nome} {feito}");
    }

    public Resultado Reparar(int id, TipoEquip tipo)
    {
        var t = Trip(id); var eq = Base.Eq(tipo);
        double horas = t.Profissao == Profissao.Engenheiro ? 3 : 6;
        if (Impedido(t, horas) is { } r) return r;
        if (!t.NaBase) return Resultado.Nao("O conserto é feito na base.");
        if (eq.Saude >= 0.999 && eq.Desgaste < 0.3) return Resultado.Nao($"{eq.Nome} está em ordem.");
        if (Servicos.Any(s => s.Tipo == "reparo" && s.Equip == tipo)) return Resultado.Nao($"{eq.Nome} já está sendo consertado.");
        double inicio = HoraDoRelogio(t);
        Gastar(t, horas, false);
        Servicos.Add(new ServicoAgendado { Tipo = "reparo", Equip = tipo, TripulanteId = id, Inicio = inicio, Fim = inicio + horas });
        string m = $"{t.Nome} vai consertar: {eq.Nome}. Fica pronto às {inicio + horas:0} h.";
        Anotar(m);
        return Resultado.Sim(m);
    }

    public Resultado LimparPaineis(int id)
    {
        var t = Trip(id);
        double horas = t.Profissao == Profissao.Engenheiro ? 2 : 4;
        if (Impedido(t, horas) is { } r) return r;
        if (!t.NaBase) return Resultado.Nao("Os painéis ficam ao lado da base.");
        if (Base.SujeiraPaineis < 0.005) return Resultado.Nao("Os painéis estão limpos.");
        if (Servicos.Any(s => s.Tipo == "limpeza")) return Resultado.Nao("Os painéis já estão sendo limpos.");
        if (SemOxigenioParaSair(HoraDoRelogio(t), horas, 1, false) is { } semO2) return semO2;
        double inicio = HoraDoRelogio(t);
        Gastar(t, horas, true);                       // é do lado de fora: de traje
        Servicos.Add(new ServicoAgendado { Tipo = "limpeza", TripulanteId = id, Inicio = inicio, Fim = inicio + horas });
        string m = $"{t.Nome} saiu de traje para limpar a poeira dos painéis solares.";
        Anotar(m);
        return Resultado.Sim(m);
    }

    public Resultado Analisar(int id)
    {
        var t = Trip(id);
        double horas = t.Profissao == Profissao.Cientista ? 2 : 4;
        if (Impedido(t, horas) is { } r) return r;
        if (!t.NaBase) return Resultado.Nao("O laboratório fica na base.");
        if (AmostrasNaBase <= 0) return Resultado.Nao("Não há amostra na base para analisar. Alguém precisa coletar lá fora.");
        var lab = Base.Eq(TipoEquip.Laboratorio);
        if (!lab.Ligado || lab.Quebrado) return Resultado.Nao("O laboratório está desligado.");
        double comeco = HoraDoRelogio(t);
        var ocupado = Servicos.FirstOrDefault(s => s.Tipo == "analise" && s.Inicio < comeco + horas - 1e-9 && s.Fim > comeco + 1e-9);
        if (ocupado != null) return Resultado.Nao($"O laboratório faz uma análise de cada vez e está ocupado até as {ocupado.Fim:0.#} h.");
        if (!Motor.UsarAguaNoLaboratorio(Base, 2)) return Resultado.Nao("A análise usa 2 kg de água potável e não há o bastante.");
        AmostrasNaBase--;
        double inicio = HoraDoRelogio(t);
        Gastar(t, horas, false);
        Servicos.Add(new ServicoAgendado { Tipo = "analise", TripulanteId = id, Inicio = inicio, Fim = inicio + horas });
        string m = $"{t.Nome} começou a analisar uma amostra no laboratório (usa energia e 2 kg de água).";
        Anotar(m);
        return Resultado.Sim(m);
    }

    public Resultado Tratar(int id, int alvoId)
    {
        var t = Trip(id); var alvo = Trip(alvoId);
        bool medico = t.Profissao == Profissao.Medico;
        double horas = medico ? 1 : 2;
        if (Impedido(t, horas) is { } r) return r;
        if (t.Pos != alvo.Pos) return Resultado.Nao("Os dois precisam estar no mesmo lugar.");
        if (alvo.Saude >= 99.5) return Resultado.Nao($"{alvo.Nome} está com a saúde em dia.");
        alvo.Saude = Math.Min(100, alvo.Saude + (medico ? 35 : 15));
        Gastar(t, horas, !t.NaBase);
        string m = $"{t.Nome} cuidou de {(alvo.Id == t.Id ? "si" : alvo.Nome)}. Saúde em {alvo.Saude:0}.";
        Anotar(m);
        return Resultado.Sim(m);
    }

    /// <summary>
    /// O oxigênio dos trajes sai do tanque de reserva. Antes de alguém sair, a conta: o tanque cobre o que já foi
    /// combinado neste sol mais esta saída (e a volta, quando é uma ida)? A taxa é a que o motor cobra por hora de traje.
    /// </summary>
    private Resultado? SemOxigenioParaSair(double inicio, double horas, int pessoas, bool contarAVolta)
    {
        double porHora = P.O2ConsumoKgCmDia * P.FatorAtividadeEva * P.PassoDia + P.TrajeVazamentoKgH * P.PassoH;
        static double HorasCheias(double de, double ate) => Math.Max(1, Math.Ceiling(ate - 1e-9) - Math.Floor(de + 1e-9));
        double jaCombinado = Tripulacao.Sum(t => t.Fora.Sum(iv => HorasCheias(iv.Inicio, iv.Fim)));
        double estaSaida = HorasCheias(inicio, inicio + horas) * pessoas * (contarAVolta ? 2 : 1);
        double precisa = (jaCombinado + estaSaida) * porHora;
        if (Base.TanqueO2Kg + 1e-9 >= precisa) return null;
        return Resultado.Nao($"O oxigênio de reserva não dá para essa saída: os trajes pediriam {precisa:0.0} kg neste sol e o tanque tem {Base.TanqueO2Kg:0.0} kg.");
    }

    // ───────────────────────── exploração ─────────────────────────
    private static double FatorAPe(Terreno t) => t switch
    { Terreno.Rochoso => 1.5, Terreno.Delta => 1.5, Terreno.Dunas => 2.0, Terreno.Cratera => 2.0, Terreno.Escarpa => 3.0, _ => 1.0 };

    private static double FatorVeiculo(Terreno t) => t switch
    { Terreno.Rochoso => 1.5, Terreno.Delta => 1.6, Terreno.Cratera => 2.0, Terreno.Dunas => 2.5, Terreno.Escarpa => double.PositiveInfinity, _ => 1.0 };

    /// <summary>O caminho mais barato entre dois hexágonos (Dijkstra), pelo custo de entrar em cada um.</summary>
    public (List<Hex> Caminho, double Custo)? Rota(Hex de, Hex para, Func<Terreno, double> fator)
    {
        if (Cel(para) is null) return null;
        var custo = new Dictionary<Hex, double> { [de] = 0 };
        var veio = new Dictionary<Hex, Hex>();
        var fila = new PriorityQueue<Hex, double>();
        fila.Enqueue(de, 0);
        while (fila.TryDequeue(out var h, out var c))
        {
            if (c > custo[h] + 1e-12) continue;
            if (h == para) break;
            foreach (var v in h.Vizinhos())
            {
                var cel = Cel(v);
                if (cel is null) continue;
                double passo = fator(cel.Terreno);
                if (double.IsInfinity(passo)) continue;
                double novo = c + passo;
                if (!custo.TryGetValue(v, out var antigo) || novo < antigo - 1e-12)
                { custo[v] = novo; veio[v] = h; fila.Enqueue(v, novo); }
            }
        }
        if (!custo.ContainsKey(para)) return null;
        var caminho = new List<Hex>();
        for (var h = para; h != de; h = veio[h]) caminho.Add(h);
        caminho.Reverse();
        return (caminho, custo[para]);
    }

    private void Revelar(Hex h, Tripulante quem)
    {
        var alcance = quem.Profissao == Profissao.Geologo ? h.Vizinhos().Prepend(h) : new[] { h };
        foreach (var x in alcance)
        {
            var c = Cel(x);
            if (c is null) continue;
            if (!c.Explorado) c.Explorado = true;
        }
        var aqui = Cel(h);
        if (aqui is { DescobertaId: not null, DescobertaVista: false })
        {
            aqui.DescobertaVista = true;
            DescobertasNoSol++;
            Moral = Math.Min(100, Moral + 4);
            Anotar($"DESCOBERTA em {aqui.Nome}: {quem.Nome} chegou lá primeiro.");
        }
    }

    private void ChegarNaBase(Tripulante t)
    {
        if (t.AmostrasNaMao > 0)
        {
            // a entrega vale a partir da hora em que a pessoa chega (a análise fica para o sol seguinte)
            Servicos.Add(new ServicoAgendado { Tipo = "amostras", Quantidade = t.AmostrasNaMao, TripulanteId = t.Id, Inicio = HoraDoRelogio(t), Fim = HoraDoRelogio(t) });
            Anotar($"{t.Nome} chegou à base com {t.AmostrasNaMao} amostra(s). Amanhã elas podem ir para o laboratório.");
            t.AmostrasNaMao = 0;
        }
        t.NoVeiculo = false;
    }

    public Resultado MoverAPe(int id, Hex destino)
    {
        var t = Trip(id);
        if (t.Pos == destino) return Resultado.Nao($"{t.Nome} já está aí.");
        var rota = Rota(t.Pos, destino, FatorAPe);
        if (rota is null) return Resultado.Nao("Não há caminho até lá.");
        double horas = rota.Value.Custo * 0.5;
        if (Impedido(t, horas) is { } r) return r;
        if (destino.Distancia(Hex.Origem) > 3 && destino.Distancia(Veiculo.Pos) > 2)
            return Resultado.Nao("A pé, a regra de segurança é ficar a até 3 km da base ou a até 2 km do veículo.");
        if (destino != Hex.Origem && SemOxigenioParaSair(HoraDoRelogio(t), horas, 1, true) is { } semO2) return semO2;
        t.NoVeiculo = false;
        Gastar(t, horas, true);
        foreach (var h in rota.Value.Caminho) Revelar(h, t);
        t.Pos = destino;
        string m = $"{t.Nome} andou {rota.Value.Caminho.Count} km de traje ({horas:0.#} h).";
        Anotar(m);
        if (t.NaBase) ChegarNaBase(t);
        return Resultado.Sim(m);
    }

    public Resultado Embarcar(int id, bool entrar)
    {
        var t = Trip(id);
        if (t.Incapacitado) return Resultado.Nao($"{t.Nome} não pode embarcar agora.");
        if (entrar && t.Pos != Veiculo.Pos) return Resultado.Nao("O veículo não está aqui.");
        t.NoVeiculo = entrar;
        return Resultado.Sim();
    }

    public Resultado MoverVeiculo(Hex destino)
    {
        if (Fase != FaseMissao.EmAndamento) return Resultado.Nao("A missão já terminou.");
        var abordo = NoVeiculo.ToList();
        if (abordo.Count == 0) return Resultado.Nao("Ninguém embarcou no veículo.");
        if (Veiculo.Pos == destino) return Resultado.Nao("O veículo já está aí.");
        var rota = Rota(Veiculo.Pos, destino, FatorVeiculo);
        if (rota is null) return Resultado.Nao("O veículo não chega lá: a escarpa do delta bloqueia o caminho.");
        bool comPiloto = abordo.Any(t => t.Profissao == Profissao.Piloto);
        double economia = Veiculo.Revisado ? 0.85 : 1.0;
        double wh = rota.Value.Custo * P.RoverWhPorKm * economia * (comPiloto ? 1.0 : 1.5);
        double horas = Math.Max(0.25, rota.Value.Custo * 0.15 * (comPiloto ? 1.0 : 2.0));
        foreach (var t in abordo) if (Impedido(t, horas) is { } r) return r;
        if (Base.RoverBateriaWh < wh) return Resultado.Nao($"A bateria do veículo ({Base.RoverBateriaWh / 1000:0.0} kWh) não dá para essa viagem ({wh / 1000:0.0} kWh).");
        if (destino != Hex.Origem)
        {
            var volta = Rota(destino, Hex.Origem, FatorVeiculo);
            double whVolta = (volta?.Custo ?? 0) * P.RoverWhPorKm * economia * (comPiloto ? 1.0 : 1.5);
            if (Base.RoverBateriaWh - wh < whVolta)
                return Resultado.Nao($"Se for até lá, a bateria não dá para voltar (sobram {(Base.RoverBateriaWh - wh) / 1000:0.0} kWh e a volta gasta {whVolta / 1000:0.0} kWh).");
            if (SemOxigenioParaSair(abordo.Max(HoraDoRelogio), horas, abordo.Count, true) is { } semO2) return semO2;
        }
        Base.RoverBateriaWh -= wh;
        foreach (var t in abordo) Gastar(t, horas, true);
        foreach (var h in rota.Value.Caminho) foreach (var t in abordo) Revelar(h, t);
        Veiculo.Pos = destino;
        foreach (var t in abordo) t.Pos = destino;
        Base.RoverNaBase = destino == Hex.Origem;
        string m = $"O veículo andou {rota.Value.Caminho.Count} km com {abordo.Count} a bordo e gastou {wh / 1000:0.0} kWh{(comPiloto ? "" : " (sem piloto gasta mais)")}.";
        Anotar(m);
        if (Base.RoverNaBase)
        {
            double chegada = abordo.Max(HoraDoRelogio);
            if (Veiculo.CargaArgilaKg > 0)
            {
                // o solo entra no forno na hora em que o veículo chega: o efeito físico vem no processamento do sol
                Servicos.Add(new ServicoAgendado { Tipo = "descarga", Quantidade = Veiculo.CargaArgilaKg, TripulanteId = abordo[0].Id, Inicio = chegada, Fim = chegada });
                Anotar($"O veículo chegou com {Veiculo.CargaArgilaKg:0} kg de solo argiloso para o forno de extração.");
                Veiculo.CargaArgilaKg = 0;
            }
            if (Veiculo.Amostras > 0)
            {
                Servicos.Add(new ServicoAgendado { Tipo = "amostras", Quantidade = Veiculo.Amostras, TripulanteId = abordo[0].Id, Inicio = chegada, Fim = chegada });
                Anotar($"O veículo chegou com {Veiculo.Amostras} amostra(s). Amanhã elas podem ir para o laboratório.");
                Veiculo.Amostras = 0;
            }
            foreach (var t in abordo) ChegarNaBase(t);
        }
        return Resultado.Sim(m);
    }

    public Resultado ColetarAmostra(int id)
    {
        var t = Trip(id); var c = Cel(t.Pos)!;
        double horas = t.Profissao == Profissao.Geologo ? 1 : 2;
        if (Impedido(t, horas) is { } r) return r;
        if (c.Amostras <= 0) return Resultado.Nao("Aqui não há rocha interessante para coletar.");
        bool noVeiculo = Veiculo.Pos == t.Pos;
        if (!noVeiculo && t.AmostrasNaMao >= 2) return Resultado.Nao($"{t.Nome} já carrega 2 amostras. Leve para a base ou traga o veículo.");
        c.Amostras--;
        if (noVeiculo) Veiculo.Amostras++; else t.AmostrasNaMao++;
        Gastar(t, horas, true);
        string m = $"{t.Nome} coletou uma amostra de rocha em {c.Nome ?? "campo"}.";
        Anotar(m);
        return Resultado.Sim(m);
    }

    public Resultado ColetarArgila(int id)
    {
        var t = Trip(id); var c = Cel(t.Pos)!;
        const double horas = 2;
        if (Impedido(t, horas) is { } r) return r;
        if (c.ArgilaKg <= 0) return Resultado.Nao("Aqui não há solo argiloso com água.");
        if (Veiculo.Pos != t.Pos) return Resultado.Nao("O solo é pesado: o veículo precisa estar aqui para carregar.");
        var ajudante = Tripulacao.FirstOrDefault(o => o.Id != t.Id && o.Pos == t.Pos && HorasLivres(o) >= horas);
        if (ajudante is null) return Resultado.Nao("Cavar e carregar o solo é trabalho para dois: falta um ajudante aqui com 2 h livres.");
        double kg = Math.Min(150, Math.Min(c.ArgilaKg, Veiculo.CargaMaxKg - Veiculo.CargaArgilaKg));
        if (kg <= 0) return Resultado.Nao("O veículo está cheio.");
        c.ArgilaKg -= kg; Veiculo.CargaArgilaKg += kg;
        Gastar(t, horas, true); Gastar(ajudante, horas, true);
        string m = $"{t.Nome} e {ajudante.Nome} carregaram {kg:0} kg de solo argiloso no veículo.";
        Anotar(m);
        return Resultado.Sim(m);
    }

    /// <summary>Retorno de emergência: quem ficou fora volta de qualquer jeito, cansado e com frio (regra de jogo).</summary>
    public Resultado RetornoDeEmergencia()
    {
        if (Fase != FaseMissao.EmAndamento) return Resultado.Nao("A missão já terminou.");
        if (Tripulacao.All(t => t.NaBase)) return Resultado.Nao("Todos já estão na base.");
        foreach (var t in Tripulacao.Where(t => !t.NaBase))
        {
            t.Saude = Math.Max(1, t.Saude - 25);
            t.Fora.Add(new Intervalo { Inicio = HoraDoRelogio(t), Fim = Math.Min(22, HoraDoRelogio(t) + 3) });
            t.Pos = Hex.Origem; t.HorasUsadas = HorasPorSol;
            ChegarNaBase(t);
            Anotar($"{t.Nome} voltou para a base no retorno de emergência e perdeu saúde.");
        }
        Moral = Math.Max(0, Moral - 5);
        return Resultado.Sim("Todos voltaram, mas o retorno de emergência custou saúde e ânimo.");
    }
}
