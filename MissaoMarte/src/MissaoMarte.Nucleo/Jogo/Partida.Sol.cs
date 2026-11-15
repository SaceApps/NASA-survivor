using System.Text.Json;
using System.Text.Json.Serialization;
using MissaoMarte.Nucleo.Dados;
using MissaoMarte.Nucleo.Simulacao;

namespace MissaoMarte.Nucleo.Jogo;

/// <summary>O céu do sol que começa: o que a tripulação sabe na reunião da manhã.</summary>
public sealed class Ambiente
{
    public double Tau { get; set; }
    public string Ceu { get; set; } = "";
    /// <summary>Quanto da energia de um dia de céu limpo os painéis devem gerar hoje (0 a 1).</summary>
    public double FracaoDoCeuLimpo { get; set; }
    public double GeracaoPrevistaKwh { get; set; }
    public double TempMinC { get; set; }
    public double TempMaxC { get; set; }
    public bool TemPrevisao { get; set; }
    public double TauAmanha { get; set; }
    public string CeuAmanha { get; set; } = "";
}

/// <summary>
/// O CICLO DO SOL. O turno vai das 8 h de um sol às 8 h do seguinte: a equipe planeja de manhã, e o processamento
/// roda as 24 horas do motor com o que foi combinado. O que acontece de noite aparece no relatório da manhã seguinte.
/// </summary>
public sealed partial class Partida
{
    public const int HoraDoDespertar = 8;
    public double MassaInicialKg { get; set; }

    /// <summary>Prova da conservação de massa: deve ser zero (a menos do arredondamento do computador).</summary>
    [JsonIgnore] public double DiferencaDeMassaKg => Base.MassaTotalKg() + Base.Livro.TotalSaidasKg - Base.Livro.TotalEntradasKg - MassaInicialKg;

    public static string NomeDoCeu(double tau) =>
        tau < 0.8 ? "Céu limpo" : tau < 1.5 ? "Poeira no ar" : tau < 3.0 ? "Tempestade de poeira" : "Tempestade de poeira forte";

    public double GeracaoPrevistaKwh(double tau)
    {
        double soma = 0;
        for (int i = 0; i < 24; i++)
        {
            int relogio = HoraDoDespertar + i;
            soma += SolMarte.IrradianciaSuperficieWM2(P, Sol + relogio / 24, relogio % 24 + 0.5, tau);
        }
        return soma * P.AreaPaineisM2 * P.EficienciaPainel * (1.0 - Base.SujeiraPaineis) * P.PassoH / 1000.0;
    }

    public Ambiente AmbienteDoSol()
    {
        double tau = Tau;
        double amanha = TauPorSol[Math.Min(Sol + 1, TauPorSol.Count - 1)];
        return new Ambiente
        {
            Tau = tau, Ceu = NomeDoCeu(tau),
            FracaoDoCeuLimpo = Math.Exp(-P.KAtenuacaoPoeira * (tau - P.TauCeuLimpo)),
            GeracaoPrevistaKwh = GeracaoPrevistaKwh(tau),
            TempMinC = P.TempExternaMinC, TempMaxC = P.TempExternaMaxC,
            TemPrevisao = PrevisaoRevelada, TauAmanha = amanha, CeuAmanha = NomeDoCeu(amanha),
        };
    }

    // ───────────────────────── salvar, carregar, prever ─────────────────────────
    private static readonly JsonSerializerOptions _json = new() { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
    public string ParaJson() => JsonSerializer.Serialize(this, _json);
    public static Partida DeJson(string json) => JsonSerializer.Deserialize<Partida>(json, _json) ?? throw new InvalidOperationException("Partida vazia.");

    /// <summary>
    /// "E se a gente fizer assim?": roda o sol numa cópia, sem os imprevistos do roteiro, e devolve o relatório.
    /// O motor é determinístico, então, se nada inesperado acontecer, é exatamente isso que vai sair.
    /// </summary>
    public RelatorioSol Prever()
    {
        var copia = DeJson(ParaJson());
        copia.Roteiro.Clear();
        return copia.ProcessarSol();
    }

    // ───────────────────────── encerrar o sol ─────────────────────────
    /// <summary>Encerra o sol que a pessoa viu na tela. Se outro aparelho (ou um segundo toque) já encerrou esse sol, recusa.</summary>
    public Resultado EncerrarSol(int solVisto) =>
        solVisto == Sol ? EncerrarSol() : Resultado.Nao("Este sol já foi encerrado. Vejam o relatório e planejem o próximo.");

    public Resultado EncerrarSol()
    {
        if (Fase != FaseMissao.EmAndamento) return Resultado.Nao("A missão já terminou.");
        var fora = Tripulacao.Where(t => !t.NaBase).ToList();
        if (fora.Count > 0)
            return Resultado.Nao($"Ainda há gente fora da base ({string.Join(", ", fora.Select(t => t.Nome))}). Tragam todos de volta ou usem o retorno de emergência.");
        var rel = ProcessarSol();
        return Resultado.Sim($"Sol {rel.Sol + 1} encerrado.");
    }

    private RelatorioSol ProcessarSol()
    {
        double tau = Tau;
        var rel = new RelatorioSol { Sol = Sol, Tau = tau, AguaPotavelInicioKg = Base.AguaPotavelKg, BateriaMinWh = Base.BateriaWh };
        double aguaMarteAntes = Base.AguaExtraidaDeMarteKg;
        int analisadasAntes = AmostrasAnalisadas;
        var saudeAntes = Tripulacao.ToDictionary(t => t.Id, t => t.Saude);
        var causas = Tripulacao.ToDictionary(t => t.Id, _ => new SortedSet<string>());
        var eventos = new List<EventoRoteiro>();
        var notas = new List<ItemRelatorio>();
        int horasVigilia = 0, horasSemConforto = 0, horasSemComunicacao = 0;
        double limpoWh = 0;

        for (int i = 0; i < 24; i++)
        {
            int relogio = HoraDoDespertar + i;
            int hora = relogio % 24;
            int solFisico = Sol + relogio / 24;
            bool dormindo = hora >= 22 || hora < 6;

            // 1. os imprevistos do roteiro (não mudam a física: mudam a condição de um equipamento)
            foreach (var ev in Roteiro.Where(e => !e.Aconteceu && e.Sol == Sol && e.Hora == hora))
            {
                ev.Aconteceu = true;
                if (ev.Equip is { } tipo) { var eq = Base.Eq(tipo); eq.Saude = Math.Min(eq.Saude, ev.NovaSaude); }
                eventos.Add(ev);
                Anotar($"{ev.Titulo} às {hora} h. {ev.Texto}");
            }

            // 2. os serviços que terminam até esta hora passam a valer
            foreach (var s in Servicos.Where(s => s.Fim <= relogio + 1e-9).ToList())
            {
                Servicos.Remove(s);
                ConcluirServico(s, notas);
            }

            // 3. o que cada tripulante faz nesta hora, do ponto de vista da física
            var analisesDaHora = Servicos.Where(s => s.Tipo == "analise" && s.Inicio < relogio + 1 && s.Fim > relogio).ToList();
            var entrada = new EntradaHora
            {
                Sol = solFisico, Hora = hora, Tau = tau, Vigilia = !dormindo,
                RacionamentoHigiene = RacionamentoHigiene, LaboratorioEmUso = analisesDaHora.Count > 0,
            };
            var foraNestaHora = new HashSet<int>();
            foreach (var t in Tripulacao)
            {
                bool fora = t.Fora.Any(iv => iv.Cobre(relogio));
                if (fora) foraNestaHora.Add(t.Id);
                entrada.Tripulantes.Add(new TripulanteHora
                {
                    Id = t.Id, Fora = fora,
                    Fator = fora ? P.FatorAtividadeEva : dormindo ? P.FatorAtividadeSono : P.FatorAtividadeTrabalho,
                });
            }

            // 4. a física da hora
            var reg = Motor.PassoHora(Base, P, entrada);
            rel.Horas.Add(reg);
            rel.GeradoKwh += reg.GeracaoW * P.PassoH / 1000.0;
            rel.ConsumidoKwh += reg.ConsumoW * P.PassoH / 1000.0;
            rel.O2GeradoKg += reg.O2GeradoKg;
            rel.Co2RemovidoKg += reg.Co2RemovidoKg;
            rel.AguaRecuperadaKg += reg.AguaRecuperadaKg;
            rel.ArVazadoKg += reg.ArVazadoKg;
            rel.BateriaMinWh = Math.Min(rel.BateriaMinWh, reg.BateriaWh);
            limpoWh += SolMarte.IrradianciaSuperficieWM2(P, solFisico, hora + 0.5, P.TauCeuLimpo);
            if (analisesDaHora.Count > 0 && Base.Eq(TipoEquip.Laboratorio).PotenciaAtendidaW <= 0)
                foreach (var s in analisesDaHora) s.HorasSemEnergia++;
            if (!dormindo)
            {
                horasVigilia++;
                if (Base.Eq(TipoEquip.Iluminacao).PotenciaAtendidaW <= 0) horasSemConforto++;
            }
            if (Base.Eq(TipoEquip.Comunicacao).PotenciaAtendidaW <= 0) horasSemComunicacao++;

            // 5. a saúde de cada um (regra de jogo, com os limites do catálogo)
            foreach (var t in Tripulacao) SaudeDaHora(t, reg, foraNestaHora.Contains(t.Id), dormindo, causas[t.Id]);
        }
        foreach (var s in Servicos.ToList()) { Servicos.Remove(s); ConcluirServico(s, notas); }
        rel.GeracaoCeuLimpoKwh = limpoWh * P.AreaPaineisM2 * P.EficienciaPainel * (1.0 - Base.SujeiraPaineis) * P.PassoH / 1000.0;

        // desgaste das máquinas e poeira nos painéis (regras de cenário, sem sorteio)
        var desgastados = new List<string>();
        foreach (var eq in Base.Equipamentos.Where(e => e.UsaEnergia))
        {
            eq.Desgaste = Math.Min(1.0, eq.Desgaste + 0.12);
            if (eq.Desgaste > 0.5 && eq.Saude > 0.3) { eq.Saude = Math.Max(0.3, eq.Saude - 0.1); desgastados.Add(eq.Nome); }
        }
        Base.SujeiraPaineis = Math.Min(0.6, Base.SujeiraPaineis + P.DeposicaoPoeiraFracaoPorSol * Math.Max(1.0, tau / P.TauCeuLimpo));

        rel.AguaPotavelFimKg = Base.AguaPotavelKg;
        rel.BateriaFimWh = Base.BateriaWh;
        rel.AguaDeMarteKg = Base.AguaExtraidaDeMarteKg - aguaMarteAntes;
        rel.AmostrasAnalisadas = AmostrasAnalisadas - analisadasAntes;

        MontarRelatorio(rel, eventos, notas, causas, saudeAntes, desgastados, horasVigilia, horasSemConforto, horasSemComunicacao);
        rel.Moral = Moral;
        Relatorios.Add(rel);

        // vira o sol
        Sol++;
        foreach (var t in Tripulacao) { t.HorasUsadas = 0; t.RotinaFeita = false; t.Fora.Clear(); }
        PrevisaoRevelada = false; InspecaoFeita = false; Veiculo.Revisado = false; DescobertasNoSol = 0;
        VerificarFim();
        return rel;
    }

    private void ConcluirServico(ServicoAgendado s, List<ItemRelatorio> notas)
    {
        var quem = Trip(s.TripulanteId);
        switch (s.Tipo)
        {
            case "reparo":
            {
                var eq = Base.Eq(s.Equip!.Value);
                eq.Saude = 1.0; eq.Desgaste = 0;
                Anotar($"{quem.Nome} terminou o conserto: {eq.Nome}.");
                notas.Add(new ItemRelatorio
                {
                    Gravidade = Gravidade.Bom, Sistema = "equipe", Titulo = $"Conserto feito: {eq.Nome}",
                    Simples = $"{quem.Nome} consertou e a máquina voltou a funcionar bem.",
                    Porque = $"O conserto terminou às {s.Fim:0} h. Até essa hora a máquina trabalhou com defeito; depois, voltou ao normal.",
                    Numeros = $"Condição de {eq.Nome}: 100%. Tempo de conserto: {s.Fim - s.Inicio:0} h.",
                });
                break;
            }
            case "limpeza":
            {
                double antes = Base.SujeiraPaineis;
                Base.SujeiraPaineis = 0;
                Anotar($"{quem.Nome} terminou de limpar os painéis solares.");
                notas.Add(new ItemRelatorio
                {
                    Gravidade = Gravidade.Bom, Sistema = "energia", Titulo = "Painéis limpos",
                    Simples = $"{quem.Nome} tirou a poeira dos painéis. Eles voltaram a gerar tudo o que podem.",
                    Porque = "A poeira de Marte assenta nos painéis e tapa a luz. Limpos, eles aproveitam toda a luz que chega.",
                    Numeros = $"Sujeira antes da limpeza: {antes * 100:0.0}%. Depois: 0%.",
                });
                break;
            }
            case "amostras":
                AmostrasNaBase += (int)Math.Round(s.Quantidade);
                break;
            case "descarga":
                Motor.ReceberRegolito(Base, s.Quantidade);
                ArgilaEntregueKg += s.Quantidade;
                break;
            case "analise":
                if (s.HorasSemEnergia > 0)
                {
                    AmostrasNaBase++;
                    Anotar("A análise da amostra parou por falta de energia no laboratório.");
                    notas.Add(new ItemRelatorio
                    {
                        Gravidade = Gravidade.Atencao, Sistema = "ciencia", Titulo = "Análise interrompida",
                        Simples = "O laboratório ficou sem energia e a análise não terminou.",
                        Porque = "O laboratório precisa de energia durante toda a análise. A amostra não se perdeu: ela voltou para a base e pode ser analisada de novo.",
                        Numeros = $"Horas sem energia durante a análise: {s.HorasSemEnergia}. Potência do laboratório: {P.LaboratorioPotenciaW:0} W.",
                    });
                }
                else
                {
                    AmostrasAnalisadas++;
                    Moral = Math.Min(100, Moral + 3);
                    string achado = ((AmostrasAnalisadas - 1) % 4) switch
                    {
                        0 => "camadas finas de lama endurecida, do tipo que se forma no fundo de água parada",
                        1 => "minerais de argila, que só se formam onde houve água por muito tempo",
                        2 => "grãos arredondados, do tipo que um rio rola e alisa",
                        _ => "carbonatos, minerais que guardam pistas do clima antigo",
                    };
                    Anotar($"{quem.Nome} terminou a análise: a amostra tem {achado}.");
                    notas.Add(new ItemRelatorio
                    {
                        Gravidade = Gravidade.Bom, Sistema = "ciencia", Titulo = $"Amostra analisada ({AmostrasAnalisadas})",
                        Simples = $"{quem.Nome} analisou uma amostra. Ela tem {achado}.",
                        Porque = "Cada amostra conta um pedaço da história de Jezero, que já foi um lago. É para isso que a missão existe.",
                        Numeros = $"Amostras analisadas: {AmostrasAnalisadas} de 3. A análise usou 2 kg de água e {P.LaboratorioPotenciaW * (s.Fim - s.Inicio) / 1000:0.0} kWh.",
                    });
                }
                break;
        }
    }

    private void SaudeDaHora(Tripulante t, RegistroHora reg, bool fora, bool dormindo, SortedSet<string> causas)
    {
        double dano = 0;
        void Causa(string texto, double pontos) { dano += pontos; causas.Add(texto); }
        if (reg.SemO2.Contains(t.Id)) Causa("faltou oxigênio", 10);
        if (reg.SemAgua.Contains(t.Id)) Causa("faltou água para beber", 1.5);
        if (Base.AlimentoSecoKg <= 1e-9) Causa("faltou comida", 0.5);
        if (!fora)
        {
            if (reg.Ppco2Kpa >= P.Ppco2CriticoKpa) Causa("CO2 muito alto no ar", 5);
            else if (reg.Ppco2Kpa >= P.Ppco2PerigoKpa) Causa("CO2 alto no ar", 1.5);
            else if (reg.Ppco2Kpa >= P.Ppco2AlertaKpa) Causa("CO2 acima do limite", 0.2);
            if (reg.Ppo2Kpa < P.Ppo2CriticoKpa) Causa("oxigênio muito baixo", 6);
            else if (reg.Ppo2Kpa < P.Ppo2PerigoKpa) Causa("pouco oxigênio no ar", 2);
            else if (reg.Ppo2Kpa < P.Ppo2AlertaKpa - 0.1) Causa("oxigênio abaixo do ideal", 0.2);
            if (reg.PressaoKpa < P.PressaoCriticaKpa) Causa("pressão muito baixa", 8);
            if (reg.TemperaturaC < 5) Causa("muito frio na base", 2);
            else if (reg.TemperaturaC < 12) Causa("frio na base", 0.5);
            else if (reg.TemperaturaC > 35) Causa("muito calor na base", 2);
            else if (reg.TemperaturaC > 30) Causa("calor na base", 0.5);
        }
        if (dano > 0) t.Saude = Math.Max(0, t.Saude - dano);
        else if (dormindo && t.Saude > 0 && t.Saude < 100) t.Saude = Math.Min(100, t.Saude + 0.6);
        // radiação (sistema simplificado): dentro da base a dose cai pela metade (hipótese de blindagem)
        t.DoseMsv += P.RadiacaoSuperficieMsvDia * P.PassoDia * (fora ? 1.0 : 0.5);
    }

    private void VerificarFim()
    {
        if (Tripulacao.All(t => t.Incapacitado))
        {
            Fase = FaseMissao.Derrota;
            MotivoFim = "Toda a tripulação ficou sem condições de continuar. A missão foi interrompida e a equipe de resgate assumiu.";
        }
        else if (Sol >= SolsDaMissao)
        {
            bool todosBem = Tripulacao.All(t => !t.Incapacitado);
            bool cumpriu = Objetivos().Where(o => o.Obrigatorio).All(o => o.Cumprido);
            Fase = todosBem && cumpriu ? FaseMissao.Vitoria : FaseMissao.Incompleta;
            MotivoFim = Fase == FaseMissao.Vitoria
                ? "A tripulação chegou bem ao fim da missão e cumpriu os objetivos de ciência e de água."
                : !todosBem ? "A missão chegou ao fim, mas nem todos terminaram bem."
                : "A tripulação sobreviveu, mas a missão ficou incompleta: faltou cumprir algum objetivo.";
        }
        if (Fase != FaseMissao.EmAndamento) Diario.Add($"Fim da missão: {MotivoFim}");
    }

    // ───────────────────────── missões individuais e estrelas ─────────────────────────
    public (bool Cumprida, string Progresso) MissaoIndividual(Profissao p) => p switch
    {
        Profissao.Comandante => (Moral >= 60, $"Ânimo da equipe: {Moral:0} de 60"),
        Profissao.Engenheiro => (Base.Equipamentos.All(e => e.Saude >= 0.5), $"Máquinas com defeito grave: {Base.Equipamentos.Count(e => e.Saude < 0.5)}"),
        Profissao.Cientista => (AmostrasAnalisadas >= 3, $"Amostras analisadas: {AmostrasAnalisadas} de 3"),
        Profissao.Geologo => (FracaoExplorada >= 0.5, $"Mapa explorado: {FracaoExplorada * 100:0}% de 50%"),
        Profissao.Medico => (Tripulacao.All(t => t.Saude >= 50), $"Menor saúde da equipe: {Tripulacao.Min(t => t.Saude):0} de 50"),
        _ => (ArgilaEntregueKg >= 200, $"Solo argiloso entregue: {ArgilaEntregueKg:0} de 200 kg"),
    };

    public int Estrelas()
    {
        var obj = Objetivos();
        int n = 0;
        if (Sol >= SolsDaMissao && Tripulacao.All(t => !t.Incapacitado)) n++;
        n += obj.Count(o => o.Id != "sobreviver" && o.Cumprido);
        if (Tripulacao.Select(t => t.Profissao).Distinct().All(p => MissaoIndividual(p).Cumprida)) n++;
        return Math.Min(5, n);
    }

    // ───────────────────────── o relatório do sol: o que aconteceu e por quê ─────────────────────────
    private void MontarRelatorio(RelatorioSol rel, List<EventoRoteiro> eventos, List<ItemRelatorio> notas,
        Dictionary<int, SortedSet<string>> causas, Dictionary<int, double> saudeAntes, List<string> desgastados,
        int horasVigilia, int horasSemConforto, int horasSemComunicacao)
    {
        var itens = new List<ItemRelatorio>();
        void Item(Gravidade g, string sistema, string titulo, string simples, string porque, string numeros) =>
            itens.Add(new ItemRelatorio { Gravidade = g, Sistema = sistema, Titulo = titulo, Simples = simples, Porque = porque, Numeros = numeros });
        var horas = rel.Horas;
        var cortes = horas.SelectMany(h => h.SemEnergia).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        string NomeEq(TipoEquip t) => Base.Eq(t).Nome;
        double batPct = rel.BateriaFimWh / P.BateriaCapacidadeWh * 100;

        // ── imprevistos ──
        foreach (var ev in eventos)
        {
            string oQueFazer = ev.Equip is { } tp
                ? "Um(a) engenheiro(a) conserta em 3 horas; outra pessoa leva 6. " + (tp == TipoEquip.Vedacao
                    ? "Enquanto isso, o ar continua escapando."
                    : $"Enquanto isso, a máquina trabalha com {Base.Eq(tp).Saude * 100:0}% da capacidade.")
                : "";
            Item(Gravidade.Perigo, "evento", ev.Titulo, ev.Texto, $"{ev.Texto} {oQueFazer}".Trim(),
                $"Aconteceu às {ev.Hora} h. {(ev.Equip is { } t2 ? $"Condição de {NomeEq(t2)}: {Base.Eq(t2).Saude * 100:0}%." : "")}");
        }

        // ── energia ──
        string numerosEnergia = $"Gerado: {rel.GeradoKwh:0} kWh (céu limpo daria {rel.GeracaoCeuLimpoKwh:0}). Gasto: {rel.ConsumidoKwh:0} kWh. Bateria: mínima de {rel.BateriaMinWh / 1000:0} kWh; às 8 h, {rel.BateriaFimWh / 1000:0} kWh ({batPct:0}%).";
        if (cortes.Count > 0)
        {
            bool essencial = cortes.Keys.Any(t => t is TipoEquip.RemovedorCo2 or TipoEquip.GeradorO2 or TipoEquip.Aquecimento or TipoEquip.Desumidificador or TipoEquip.ProcessadorAgua);
            string lista = string.Join(", ", cortes.OrderByDescending(c => c.Value).Select(c => $"{NomeEq(c.Key)} ({c.Value} h)"));
            Item(essencial ? Gravidade.Perigo : Gravidade.Atencao, "energia", "Faltou energia",
                "A energia não deu para tudo e algumas máquinas ficaram paradas.",
                $"A base gastou mais do que os painéis geraram e a bateria não cobriu a diferença. Ficaram sem energia: {lista}. A energia vai primeiro para quem tem prioridade 1. No próximo sol, desliguem o que não é essencial{(rel.Tau >= 0.8 ? " enquanto a poeira não baixar" : "")}.",
                numerosEnergia);
        }
        else if (rel.Tau >= 0.8)
        {
            double pct = rel.GeracaoCeuLimpoKwh > 0 ? rel.GeradoKwh / rel.GeracaoCeuLimpoKwh * 100 : 0;
            Item(Gravidade.Atencao, "energia", "Poeira no céu",
                "A poeira escondeu parte do Sol e os painéis geraram menos.",
                $"Com a poeira no ar, chegou aos painéis só {pct:0}% da luz de um dia de céu limpo. A bateria cobriu a diferença e amanheceu com {batPct:0}% da carga.",
                $"Opacidade da poeira (tau): {rel.Tau:0.0}. {numerosEnergia}");
        }
        else
        {
            Item(Gravidade.Bom, "energia", "Energia em dia",
                "Os painéis solares deram conta do dia e a bateria sustentou a noite.",
                $"De dia os painéis alimentam a base e carregam a bateria; de noite a base vive da bateria. Ela amanheceu com {batPct:0}% da carga.",
                numerosEnergia);
        }
        if (Base.SujeiraPaineis >= 0.04)
            Item(Gravidade.Atencao, "energia", "Poeira nos painéis",
                "Os painéis estão sujos de poeira e geram menos.",
                $"A poeira que assenta nos painéis já tira {Base.SujeiraPaineis * 100:0}% da energia. Uma limpeza do lado de fora (2 horas para o engenheiro) devolve tudo.",
                $"Sujeira: {Base.SujeiraPaineis * 100:0.0}%. Perda num sol de céu limpo: cerca de {rel.GeracaoCeuLimpoKwh / Math.Max(0.4, 1 - Base.SujeiraPaineis) * Base.SujeiraPaineis:0} kWh.");

        // ── ar ──
        double co2Max = horas.Max(h => h.Ppco2Kpa), o2Min = horas.Min(h => h.Ppo2Kpa), pMin = horas.Min(h => h.PressaoKpa);
        double tMin = horas.Min(h => h.TemperaturaC), tMax = horas.Max(h => h.TemperaturaC), uMax = horas.Max(h => h.Umidade);
        var rem = Base.Eq(TipoEquip.RemovedorCo2); var ger = Base.Eq(TipoEquip.GeradorO2);
        if (co2Max >= P.Ppco2AlertaKpa)
        {
            string motivo = rem.Quebrado ? "O removedor de CO2 está quebrado"
                : !rem.Ligado ? "O removedor de CO2 ficou desligado"
                : cortes.TryGetValue(TipoEquip.RemovedorCo2, out int hc) ? $"O removedor de CO2 ficou {hc} h sem energia"
                : rem.Saude < 0.99 ? "O removedor de CO2 está com defeito e tira menos gás do ar"
                : horas.Count(h => h.Co2RemovidoKg <= 0) > 2 ? "O removedor de CO2 ficou parado parte do sol"
                : "A equipe soltou mais CO2 do que o removedor consegue tirar";
            Item(co2Max >= P.Ppco2PerigoKpa ? Gravidade.Perigo : Gravidade.Atencao, "ar", "Gás carbônico alto",
                "Tem gás carbônico demais no ar da base. Isso dá dor de cabeça e cansaço.",
                $"{motivo}. Cada pessoa solta cerca de 1 kg de CO2 por dia ao respirar; numa base fechada, sem remoção, ele vai se acumulando.",
                $"CO2 máximo: {co2Max:0.00} kPa (limite normal: {P.Ppco2AlertaKpa:0.00}; agir: {P.Ppco2PerigoKpa:0.00}; emergência: {P.Ppco2CriticoKpa:0.0}). Removido no sol: {rel.Co2RemovidoKg:0.0} kg.");
        }
        else
        {
            Item(Gravidade.Bom, "ar", "Ar bom para respirar",
                "O ar da base está limpo e com oxigênio.",
                "O removedor tirou do ar o gás carbônico que a equipe soltou, e o gerador repôs o oxigênio usado separando a água.",
                $"CO2 máximo: {co2Max:0.00} kPa. O2 mínimo: {o2Min:0.0} kPa. CO2 removido: {rel.Co2RemovidoKg:0.0} kg. O2 gerado: {rel.O2GeradoKg:0.0} kg, usando {rel.O2GeradoKg * Fisica.AguaPorKgO2:0.0} kg de água.");
        }
        bool valvula = horas.Any(h => h.Avisos.Contains("valvula_o2"));
        if (valvula || o2Min < P.Ppo2AlertaKpa - 0.1)
        {
            string motivo = ger.Quebrado ? "O gerador de oxigênio está quebrado"
                : !ger.Ligado ? "O gerador de oxigênio ficou desligado"
                : cortes.TryGetValue(TipoEquip.GeradorO2, out int hg) ? $"O gerador de oxigênio ficou {hg} h com pouca energia"
                : Base.AguaPotavelKg <= P.ReservaBebidaKg + 1 ? "O gerador parou para não gastar a água de beber"
                : rel.ArVazadoKg > 2 ? "O vazamento levou o oxigênio embora mais rápido do que o gerador repõe"
                : "O gerador não acompanhou o consumo";
            Item(o2Min < P.Ppo2PerigoKpa ? Gravidade.Perigo : Gravidade.Atencao, "ar", "Oxigênio baixo",
                "O oxigênio do ar ficou baixo.",
                $"{motivo}. {(valvula ? "A válvula de emergência soltou oxigênio do tanque de reserva para segurar o ar." : "O tanque de reserva está vazio.")}",
                $"O2 mínimo: {o2Min:0.0} kPa (ideal: {P.Ppo2AlvoKpa:0.0}; alerta: {P.Ppo2AlertaKpa:0.0}; perigo: {P.Ppo2PerigoKpa:0.0}). Tanque de reserva: {Base.TanqueO2Kg:0.0} kg.");
        }
        if (rel.ArVazadoKg > 1.0)
        {
            var ved = Base.Eq(TipoEquip.Vedacao);
            Item(rel.ArVazadoKg > 8 ? Gravidade.Perigo : Gravidade.Atencao, "ar", "Ar escapando",
                "O ar da base está escapando para fora.",
                "A vedação está danificada e o ar vaza para Marte. O nitrogênio de reserva repôs a pressão, mas ele acaba. Consertar a vedação resolve.",
                $"Ar perdido no sol: {rel.ArVazadoKg:0.0} kg. Nitrogênio de reserva: {Base.TanqueN2Kg:0} kg. Pressão mínima: {pMin:0.0} kPa. Condição da vedação: {ved.Saude * 100:0}%.");
        }
        if (horas.Any(h => h.Avisos.Contains("condensacao")))
            Item(Gravidade.Atencao, "ar", "Umidade no máximo",
                "O ar ficou tão úmido que a água escorreu pelas paredes.",
                "O desumidificador não recolheu o vapor da respiração e do suor (estava desligado, com defeito ou sem energia). Cada pessoa solta quase 3 kg de água no ar por dia.",
                $"Umidade máxima: {uMax * 100:0}%. A água que condensou foi para o tanque de água usada: nada se perdeu.");
        if (tMin < 18)
        {
            var aq = Base.Eq(TipoEquip.Aquecimento);
            string motivo = !aq.Ligado ? "O aquecimento ficou desligado" : cortes.ContainsKey(TipoEquip.Aquecimento) ? "O aquecimento ficou com pouca energia" : "O aquecimento não deu conta";
            Item(tMin < 12 ? Gravidade.Perigo : Gravidade.Atencao, "ar", "Frio dentro da base",
                "A base esfriou.",
                $"{motivo}. Lá fora a noite chega a {P.TempExternaMinC:0} °C, e a base perde calor pelas paredes o tempo todo.",
                $"Temperatura mínima na base: {tMin:0.0} °C (o confortável é de 18 a 27 °C).");
        }
        else if (tMax > 27)
            Item(tMax > 30 ? Gravidade.Perigo : Gravidade.Atencao, "ar", "Calor dentro da base", "A base esquentou demais.",
                "As máquinas e as pessoas soltam calor; com tudo ligado e a base bem isolada, a temperatura sobe.",
                $"Temperatura máxima na base: {tMax:0.0} °C (o confortável é de 18 a 27 °C).");

        // ── água ──
        var proc = Base.Eq(TipoEquip.ProcessadorAgua);
        string numerosAgua = $"Água potável: de {rel.AguaPotavelInicioKg:0} para {rel.AguaPotavelFimKg:0} kg. Tratada no sol: {rel.AguaRecuperadaKg:0.0} kg. Água usada esperando: {Base.AguaResidualKg + Base.AguaEmTratamentoKg:0} kg. Salmoura (não volta): {Base.SalmouraKg:0.0} kg.";
        if (horas.Any(h => h.SemAgua.Count > 0))
            Item(Gravidade.Perigo, "agua", "Faltou água para beber",
                "A água potável acabou.",
                "O tanque de água potável esvaziou. A água usada só volta a ser potável depois de passar pelo processador, que precisa estar ligado, com energia e em bom estado.",
                numerosAgua);
        else if (rel.AguaPotavelFimKg < rel.AguaPotavelInicioKg - 8)
        {
            string motivo = !proc.Ligado ? "O processador de água ficou desligado, e a água usada ficou parada no tanque"
                : cortes.TryGetValue(TipoEquip.ProcessadorAgua, out int hp) ? $"O processador de água ficou {hp} h sem energia"
                : proc.Saude < 0.95 ? "O filtro do processador está saturado: ele trata menos água por hora e perde mais na salmoura"
                : "A equipe e o gerador de oxigênio usaram mais água do que o tratamento devolveu";
            Item(Gravidade.Atencao, "agua", "A água potável diminuiu",
                "A base está com menos água potável do que ontem.",
                $"{motivo}. Na base, a água anda em círculo: tanque, uso, tratamento e tanque de novo. Se o tratamento para, o tanque esvazia.",
                numerosAgua);
        }
        else
            Item(Gravidade.Bom, "agua", "Água reciclada",
                "A água usada foi limpa e voltou para o tanque.",
                $"A água do banho, do suor e até do xixi é tratada e volta a ser potável. O tratamento leva {P.AguaTempoTratamentoH:0} horas e devolve {P.AguaRecuperacaoFracao * 100:0}% da água.",
                numerosAgua);
        if (rel.AguaDeMarteKg > 0.01)
            Item(Gravidade.Bom, "agua", "Água tirada de Marte",
                "O forno tirou água do solo de Marte!",
                "O solo argiloso do delta guarda água presa nos minerais. O forno aquece o solo, a água sai como vapor e vai para o tratamento.",
                $"Extraída no sol: {rel.AguaDeMarteKg:0.0} kg (total: {Base.AguaExtraidaDeMarteKg:0.0} kg). Solo esperando o forno: {Base.RegolitoKg:0} kg. O forno gasta {P.FornoKwhPorKgAgua:0.0} kWh por kg de água.");
        else if (Base.RegolitoKg > 1)
        {
            var forno = Base.Eq(TipoEquip.FornoRegolito);
            Item(Gravidade.Atencao, "agua", "O forno não trabalhou",
                "Há solo de Marte esperando, mas o forno ficou parado.",
                !forno.Ligado ? "O forno de extração está desligado." : "O forno ficou sem energia: ele tem prioridade baixa e é um dos primeiros a parar quando a energia aperta.",
                $"Solo esperando o forno: {Base.RegolitoKg:0} kg, com cerca de {Base.RegolitoKg * P.RegolitoFracaoAgua:0.0} kg de água.");
        }

        // ── máquinas ──
        if (desgastados.Count > 0)
            Item(Gravidade.Atencao, "equipe", "Máquinas desgastadas",
                "As máquinas estão cansadas e rendem menos.",
                "Faz vários sóis que ninguém inspeciona o suporte à vida. Sem inspeção, as máquinas se desgastam e perdem rendimento. A rotina do engenheiro evita isso.",
                $"Perderam rendimento: {string.Join(", ", desgastados)}.");

        // ── equipe ──
        foreach (var t in Tripulacao)
        {
            double antes = saudeAntes[t.Id];
            string motivos = causas[t.Id].Count > 0 ? string.Join(", ", causas[t.Id]) : "cansaço";
            if (t.Incapacitado)
                Item(Gravidade.Perigo, "equipe", $"{t.Nome} precisa de socorro",
                    $"{t.Nome} passou mal e não consegue trabalhar.",
                    $"Motivo: {motivos}. O(a) médico(a) trata em 1 hora; outra pessoa leva 2 horas e ajuda menos.",
                    $"Saúde: de {antes:0} para {t.Saude:0}.");
            else if (antes - t.Saude >= 3)
                Item(t.Saude < 50 ? Gravidade.Perigo : Gravidade.Atencao, "equipe", $"{t.Nome} perdeu saúde",
                    $"{t.Nome} não está se sentindo bem.",
                    $"Motivo: {motivos}.",
                    $"Saúde: de {antes:0} para {t.Saude:0}.");
        }

        // ── ânimo ──
        bool houvePerigo = itens.Any(i => i.Gravidade == Gravidade.Perigo && i.Sistema != "evento");
        bool houveAtencao = itens.Any(i => i.Gravidade == Gravidade.Atencao);
        var razoes = new List<string>();
        double m = Moral - 3; razoes.Add("mais um sol longe de casa (-3)");
        if (horasVigilia > 0 && horasSemConforto * 2 > horasVigilia) { m -= 4; razoes.Add("dia sem luz e conforto (-4)"); }
        if (horasSemComunicacao >= 20) { m -= 4; razoes.Add("sem notícias da Terra (-4)"); }
        if (RacionamentoHigiene) { m -= 3; razoes.Add("economia de água no banho (-3)"); }
        if (houvePerigo) { m -= 5; razoes.Add("um problema sério na base (-5)"); }
        else if (!houveAtencao) { m += 4; razoes.Add("tudo funcionando bem (+4)"); }
        Moral = Math.Clamp(m, 0, 100);
        Item(Moral < 40 ? Gravidade.Atencao : Gravidade.Bom, "equipe", $"Ânimo da equipe: {Moral:0}",
            Moral < 40 ? "A equipe está desanimada." : "A equipe segue animada.",
            $"O que pesou neste sol: {string.Join("; ", razoes)}. Reunião do comandante, descobertas e análises levantam o ânimo. Com ânimo abaixo de 30, cada um trabalha só 6 horas; com 85 ou mais, 9 horas.",
            $"Ânimo: {Moral:0} de 100. Horas de trabalho no próximo sol: {HorasPorSol}.");

        itens.AddRange(notas);
        if (DescobertasNoSol > 0)
            Item(Gravidade.Bom, "ciencia", $"Descobertas: {DescobertasNoSol}",
                "A equipe chegou a lugares novos e ganhou cartões de ciência.",
                "Cada lugar com nome guarda um fato real sobre Marte. Os cartões ficam na Central científica.",
                $"Mapa explorado: {FracaoExplorada * 100:0}%.");

        rel.Itens = itens.OrderByDescending(i => i.Sistema == "evento").ThenByDescending(i => (int)i.Gravidade).ToList();

        // um fato real por sol, escolhido pelo que aconteceu
        var pacote = PacoteNasa.Padrao;
        var dosLugares = Celulas.Where(c => c.DescobertaId != null).Select(c => c.DescobertaId!).ToHashSet();
        var jaVistos = Relatorios.Where(r => r.FatoId != null).Select(r => r.FatoId!).ToHashSet();
        string? preferido = rel.Tau >= 1.5 ? "energia-02" : Base.SujeiraPaineis >= 0.04 ? "energia-03" : rel.AguaDeMarteKg > 0.01 ? "agua-03" : null;
        if (preferido != null && !jaVistos.Contains(preferido) && pacote.Fato(preferido) != null) rel.FatoId = preferido;
        else rel.FatoId = pacote.Fatos.Where(f => !dosLugares.Contains(f.Id) && !jaVistos.Contains(f.Id)).Select(f => f.Id).FirstOrDefault()
                          ?? pacote.Fatos.Select(f => f.Id).ElementAtOrDefault(rel.Sol % Math.Max(1, pacote.Fatos.Count));
    }
}
