// SIMULADOR DE MESA: roda missões inteiras pelo núcleo, sem tela. Serve para conferir o equilíbrio do cenário
// e prova que o motor de simulação e as regras do jogo não dependem da interface.
// uso: dotnet run --project src/MissaoMarte.Simulador [-- detalhe]
using MissaoMarte.Nucleo.Jogo;
using MissaoMarte.Nucleo.Simulacao;

bool detalhe = args.Contains("detalhe");
var profs = new[] { Profissao.Comandante, Profissao.Engenheiro, Profissao.Cientista, Profissao.Geologo, Profissao.Medico, Profissao.Piloto };

// "demo <pasta>": grava uma missão já no sol 5, na manhã do pior dia da tempestade, para abrir na apresentação
if (args.Length >= 2 && args[0] == "demo")
{
    var demo = Partida.Nova("Demonstração: a tempestade", profs.Select(p => (p.Nome(), p)), 2);
    demo.Id = "demo-tempestade";
    for (int i = 0; i < 4; i++) { Jogar(demo); demo.EncerrarSol(); }
    Directory.CreateDirectory(args[1]);
    string arquivo = Path.Combine(args[1], demo.Id + ".json");
    File.WriteAllText(arquivo, demo.ParaJson());
    Console.WriteLine($"Missão de demonstração gravada em {arquivo}: sol {demo.Sol + 1}, {Partida.NomeDoCeu(demo.Tau).ToLowerInvariant()}, bateria em {demo.BateriaFracao * 100:0}%.");
    return;
}

foreach (var (nivel, n, ativo) in new[] { (2, 6, false), (2, 6, true), (1, 6, false), (1, 6, true), (3, 6, true), (2, 2, false), (2, 3, true), (2, 4, true) })
    Rodar(nivel, n, ativo);

void Rodar(int nivel, int n, bool ativo)
{
    var pt = Partida.Nova("simulação", profs.Take(n).Select(p => (p.Nome(), p)), nivel);
    Console.WriteLine($"\n=== nível {nivel}, {n} tripulantes, {(ativo ? "jogando bem" : "sem fazer nada")} ===");
    while (pt.Fase == FaseMissao.EmAndamento)
    {
        if (ativo) Jogar(pt);
        var r = pt.EncerrarSol();
        if (!r.Ok) { Console.WriteLine("  NÃO ENCERROU: " + r.Mensagem); break; }
        var rel = pt.Relatorios[^1];
        Console.WriteLine($"  sol {rel.Sol + 1} tau {rel.Tau:0.0} | ger {rel.GeradoKwh,4:0} gasto {rel.ConsumidoKwh,4:0} bat mín {rel.BateriaMinWh / 1000,4:0} 8h {rel.BateriaFimWh / 1000,4:0}"
            + $" | CO2 {rel.Horas.Max(h => h.Ppco2Kpa):0.00} O2 {rel.Horas.Min(h => h.Ppo2Kpa):0.0} P {rel.Horas.Min(h => h.PressaoKpa):0.0} T {rel.Horas.Min(h => h.TemperaturaC):0.0}/{rel.Horas.Max(h => h.TemperaturaC):0.0} U {rel.Horas.Max(h => h.Umidade) * 100:0}%"
            + $" | água {rel.AguaPotavelFimKg:0} N2 {pt.Base.TanqueN2Kg:0} O2t {pt.Base.TanqueO2Kg:0.0} | saúde {pt.Tripulacao.Min(t => t.Saude):0}-{pt.Tripulacao.Max(t => t.Saude):0} ânimo {pt.Moral:0} | Δm {pt.DiferencaDeMassaKg:E1}");
        foreach (var it in rel.Itens.Where(i => detalhe || i.Gravidade != Gravidade.Bom))
            Console.WriteLine($"      [{it.Gravidade}] {it.Titulo}: {(detalhe ? it.Porque + " // " + it.Numeros : it.Porque)}");
    }
    Console.WriteLine($"  FIM: {pt.Fase}, {pt.Estrelas()} estrela(s). {pt.MotivoFim} Amostras {pt.AmostrasAnalisadas}; água de Marte {pt.Base.AguaExtraidaDeMarteKg:0.0} kg; mapa {pt.FracaoExplorada * 100:0}%; solo {pt.ArgilaEntregueKg:0} kg.");
    foreach (var p in pt.Tripulacao.Select(t => t.Profissao))
    { var (ok, texto) = pt.MissaoIndividual(p); Console.WriteLine($"      {p.Nome(),-14} {(ok ? "cumpriu" : "não cumpriu")}: {texto}"); }
}

// Uma equipe que joga com atenção: faz as rotinas, conserta o que quebra, economiza energia na tempestade e faz a ciência.
void Jogar(Partida pt)
{
    Tripulante? Quem(Profissao p) => pt.Tripulacao.FirstOrDefault(t => t.Profissao == p && !t.Incapacitado);
    void Log(Resultado r) { if (detalhe && r.Mensagem != "") Console.WriteLine($"        {(r.Ok ? "ok " : "NÃO")} {r.Mensagem}"); }
    var amb = pt.AmbienteDoSol();

    // energia: na tempestade ou com a bateria baixa, o opcional sai
    bool aperto = amb.Tau >= 1.5 || pt.BateriaFracao < 0.25;
    foreach (var tipo in new[] { TipoEquip.FornoRegolito, TipoEquip.CarregadorRover, TipoEquip.Iluminacao })
        if (pt.Base.Eq(tipo).Ligado == aperto) pt.Alternar(tipo);
    // no pior dia, o gerador de oxigênio descansa: o ar da base guarda oxigênio para mais de um sol
    bool poupaO2 = amb.Tau >= 3.0 && pt.Ppo2Kpa > 20.5;
    if (pt.Base.Eq(TipoEquip.GeradorO2).Ligado == poupaO2) pt.Alternar(TipoEquip.GeradorO2);

    // consertos primeiro (o mais grave na frente), com quem conserta mais rápido
    var eng = Quem(Profissao.Engenheiro) ?? pt.Tripulacao.First(t => !t.Incapacitado);
    foreach (var eq in pt.Base.Equipamentos.Where(e => e.Saude < 0.99).OrderBy(e => e.Saude).ToList())
    {
        var r = pt.Reparar(eng.Id, eq.Tipo);
        if (!r.Ok) { var outro = pt.Tripulacao.Where(t => !t.Incapacitado && t.Id != eng.Id).OrderByDescending(pt.HorasLivres).FirstOrDefault(); if (outro != null) r = pt.Reparar(outro.Id, eq.Tipo); }
        Log(r);
    }
    foreach (var t in pt.Tripulacao) Log(pt.FazerRotina(t.Id));
    if (pt.Base.SujeiraPaineis > 0.03) Log(pt.LimparPaineis(eng.Id));

    // saúde
    var med = Quem(Profissao.Medico) ?? pt.Tripulacao.First(t => !t.Incapacitado);
    foreach (var doente in pt.Tripulacao.Where(t => t.Saude < 70).OrderBy(t => t.Saude).ToList()) Log(pt.Tratar(med.Id, doente.Id));

    // ciência: analisar o que há na base
    var cien = Quem(Profissao.Cientista) ?? pt.Tripulacao.Where(t => !t.Incapacitado).OrderByDescending(pt.HorasLivres).First();
    while (pt.AmostrasNaBase > 0 && pt.AmostrasAnalisadas + pt.Servicos.Count(s => s.Tipo == "analise") < 4) { var r = pt.Analisar(cien.Id); Log(r); if (!r.Ok) break; }

    // exploração: só com céu razoável
    if (amb.Tau < 1.5)
    {
        var geo = Quem(Profissao.Geologo);
        var pil = Quem(Profissao.Piloto);
        var afloramento = new Hex(2, -1);
        var delta = new Hex(-2, 0);
        if (pil != null && pt.Base.AguaExtraidaDeMarteKg + pt.Base.RegolitoKg * pt.P.RegolitoFracaoAgua < 5 && pt.Base.RoverBateriaWh > 12000)
        {
            // viagem ao delta com o veículo: solo argiloso e uma amostra
            var ajudante = geo ?? pt.Tripulacao.Where(t => !t.Incapacitado && t.Id != pil.Id).OrderByDescending(pt.HorasLivres).First();
            if (pt.HorasLivres(pil) >= 4 && pt.HorasLivres(ajudante) >= 4)
            {
                pt.Embarcar(pil.Id, true); pt.Embarcar(ajudante.Id, true);
                Log(pt.MoverVeiculo(delta));
                Log(pt.ColetarArgila(pil.Id));
                Log(pt.ColetarAmostra(ajudante.Id));
                Log(pt.MoverVeiculo(Hex.Origem));
            }
        }
        else if (geo != null && pt.Cel(afloramento)!.Amostras > 0 && pt.HorasLivres(geo) >= 5)
        {
            Log(pt.MoverAPe(geo.Id, afloramento));
            Log(pt.ColetarAmostra(geo.Id)); Log(pt.ColetarAmostra(geo.Id));
            Log(pt.MoverAPe(geo.Id, Hex.Origem));
        }
        else if (geo == null && pt.Cel(afloramento)!.Amostras > 0 && pt.AmostrasAnalisadas + pt.AmostrasNaBase + pt.AmostrasChegando < 3)
        {
            var andarilho = pt.Tripulacao.Where(t => !t.Incapacitado).OrderByDescending(pt.HorasLivres).First();
            if (pt.HorasLivres(andarilho) >= 4.5)
            {
                Log(pt.MoverAPe(andarilho.Id, afloramento));
                Log(pt.ColetarAmostra(andarilho.Id));
                Log(pt.MoverAPe(andarilho.Id, Hex.Origem));
            }
        }
    }
    foreach (var t in pt.Tripulacao.Where(t => !t.NaBase).ToList()) Log(pt.MoverAPe(t.Id, Hex.Origem));
    if (pt.Tripulacao.Any(t => !t.NaBase)) Log(pt.RetornoDeEmergencia());
}
