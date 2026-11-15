using MissaoMarte.Nucleo.Simulacao;
using Xunit;

namespace MissaoMarte.Testes;

/// <summary>
/// O NÚCLEO CIENTÍFICO SOB PROVA. A regra do playbook: "os cálculos devem utilizar conservação de massa e balanço
/// energético; o software não pode simplesmente adicionar ou remover recursos". Aqui isso vira conta conferida.
/// </summary>
public class TestesMotor
{
    private static readonly ParametrosCenario P = ParametrosCenario.Padrao();

    private static EntradaHora Hora(int sol, int h, double tau = 0.5, int tripulantes = 4, int fora = 0, bool lab = false) => new()
    {
        Sol = sol, Hora = h, Tau = tau, LaboratorioEmUso = lab, Vigilia = h is >= 6 and < 22,
        Tripulantes = Enumerable.Range(0, tripulantes).Select(i => new TripulanteHora
        {
            Id = i, Fora = i < fora, Fator = i < fora ? P.FatorAtividadeEva : (h is >= 6 and < 22 ? P.FatorAtividadeTrabalho : P.FatorAtividadeSono),
        }).ToList(),
    };

    [Fact]
    public void O_catalogo_traz_todos_os_parametros_com_unidade_e_fonte()
    {
        var cat = CatalogoParametros.Carregar();
        Assert.True(cat.Parametros.Count >= 80);
        Assert.All(cat.Parametros, r => { Assert.False(string.IsNullOrWhiteSpace(r.Unidade)); Assert.False(string.IsNullOrWhiteSpace(r.Fonte)); });
        Assert.All(cat.Parametros.Where(r => r.Tipo == "ciencia"), r => Assert.StartsWith("http", r.Url == "" ? "http" : r.Url));
        Assert.Equal(cat.VersaoModelo, P.VersaoModelo);
    }

    [Fact]
    public void A_massa_se_conserva_a_cada_hora_numa_missao_inteira_com_falhas_e_exploracao()
    {
        var e = EstadoBase.Inicial(P);
        double m0 = e.MassaTotalKg();
        for (int sol = 0; sol < 8; sol++)
        {
            if (sol == 2) { e.Eq(TipoEquip.Vedacao).Saude = 0.5; Motor.ReceberRegolito(e, 120); }
            if (sol == 3) e.Eq(TipoEquip.ProcessadorAgua).Saude = 0.4;
            if (sol == 4) e.Eq(TipoEquip.RemovedorCo2).Saude = 0;
            if (sol == 6) e.Eq(TipoEquip.GeradorO2).Ligado = false;
            for (int h = 0; h < 24; h++)
            {
                double tau = sol is 4 or 5 ? 3.0 : 0.5;
                Motor.PassoHora(e, P, Hora(sol, h, tau, fora: h is >= 9 and < 13 ? 2 : 0, lab: h == 15));
                double erro = e.MassaTotalKg() + e.Livro.TotalSaidasKg - e.Livro.TotalEntradasKg - (m0 + 120 * (sol >= 2 ? 1 : 0) - 120 * (sol >= 2 ? 1 : 0));
                Assert.True(Math.Abs(erro) < 1e-6, $"massa não fechou no sol {sol} hora {h}: erro {erro:E3} kg");
                Assert.All(new[] { e.O2Kg, e.Co2Kg, e.N2Kg, e.VaporKg, e.AguaPotavelKg, e.AguaResidualKg, e.TanqueO2Kg, e.TanqueN2Kg, e.AlimentoSecoKg, e.BateriaWh },
                    x => Assert.True(x >= -1e-9, "reservatório negativo"));
            }
        }
        // a missão mexeu de verdade nos reservatórios (não é um teste vazio)
        Assert.True(e.Livro.TotalSaidasKg > 20);
        Assert.True(e.Livro.EntradasKg["solo de Marte coletado"] == 120);
    }

    [Fact]
    public void A_energia_fecha_geracao_mais_bateria_igual_cargas_mais_perdas()
    {
        var e = EstadoBase.Inicial(P);
        double bat0 = e.BateriaWh;
        for (int sol = 0; sol < 4; sol++)
            for (int h = 0; h < 24; h++)
            {
                Motor.PassoHora(e, P, Hora(sol, h, sol == 2 ? 4.0 : 0.5));
                var l = e.Livro;
                double erro = l.EnergiaGeradaWh + (bat0 - e.BateriaWh) - l.EnergiaCargasWh - l.EnergiaPerdasWh - l.EnergiaDesperdicadaWh;
                Assert.True(Math.Abs(erro) < 1e-6, $"energia não fechou no sol {sol} hora {h}: {erro:E3} Wh");
                Assert.InRange(e.BateriaWh, -1e-9, P.BateriaCapacidadeWh + 1e-9);
            }
        Assert.True(e.Livro.EnergiaGeradaWh > 100_000);
    }

    [Fact]
    public void Sem_o_removedor_o_CO2_sobe_o_que_a_conta_dos_gases_manda()
    {
        var e = EstadoBase.Inicial(P);
        e.Eq(TipoEquip.RemovedorCo2).Ligado = false;
        double antes = Fisica.PressaoParcialKpa(e.Co2Kg, Fisica.MCO2, e.TemperaturaC, P.VolumeCabineM3);
        for (int h = 0; h < 24; h++) Motor.PassoHora(e, P, Hora(0, h));
        double depois = Fisica.PressaoParcialKpa(e.Co2Kg, Fisica.MCO2, e.TemperaturaC, P.VolumeCabineM3);
        // conta independente: 4 pessoas, 16 h acordadas e 8 h dormindo, n = m/M, p = nRT/V
        double fatorMedio = (16 * P.FatorAtividadeTrabalho + 8 * P.FatorAtividadeSono) / 24.0;
        double kg = 4 * P.Co2ProducaoKgCmDia * fatorMedio * (P.SolSegundos / 86400.0);
        double esperado = kg / Fisica.MCO2 * Fisica.R * (P.TemperaturaAlvoC + 273.15) / P.VolumeCabineM3 / 1000.0;
        Assert.InRange(depois - antes, esperado * 0.97, esperado * 1.03);
        Assert.True(depois > P.Ppco2AlertaKpa, "um sol sem o removedor tem de passar do limite de atenção");
    }

    [Fact]
    public void A_eletrolise_respeita_a_estequiometria_e_a_agua_reservada_para_beber()
    {
        var e = EstadoBase.Inicial(P);
        e.O2Kg *= 0.97;                                  // falta oxigênio: o gerador trabalha
        double agua0 = e.AguaPotavelKg, o20 = e.O2Kg;
        var en = Hora(0, 12, tripulantes: 0);            // sem ninguém: só o gerador mexe em O2 e água
        e.Eq(TipoEquip.ProcessadorAgua).Ligado = false;
        var r = Motor.PassoHora(e, P, en);
        Assert.True(r.O2GeradoKg > 0.1);
        double fVaz = 1 - Math.Exp(-P.VazamentoNominalFracaoDia * P.PassoDia);
        Assert.Equal(agua0 - e.AguaPotavelKg, r.O2GeradoKg * 2 * 18.015 / 31.998, 9);
        Assert.Equal(e.Livro.SaidasKg["hidrogênio da eletrólise"], (agua0 - e.AguaPotavelKg) - r.O2GeradoKg, 9);
        Assert.Equal(o20 * (1 - fVaz) + r.O2GeradoKg, e.O2Kg, 6);
        // com a água na reserva de beber, o gerador para (não tira a água da tripulação)
        e.AguaPotavelKg = P.ReservaBebidaKg - 1;
        Assert.Equal(0, Motor.PassoHora(e, P, en).O2GeradoKg);
    }

    [Fact]
    public void Faltando_energia_o_opcional_cai_antes_do_essencial()
    {
        var e = EstadoBase.Inicial(P);
        // noite, quase sem bateria: só dá para o removedor de CO2 e uma sobra
        double pouco = (P.Co2RemocaoPotenciaW * P.PassoH + 400) / (P.BateriaEficienciaDescarga * (1 - P.PerdaDistribuicao));
        e.BateriaWh = pouco;
        var r = Motor.PassoHora(e, P, Hora(0, 2));
        Assert.Contains(TipoEquip.Aquecimento, r.SemEnergia);
        Assert.True(e.Eq(TipoEquip.RemovedorCo2).PotenciaAtendidaW > 0, "o removedor de CO2 (prioridade 1) tem de ser atendido primeiro");
        Assert.Equal(0, e.Eq(TipoEquip.Comunicacao).PotenciaAtendidaW);
        // o jogador muda a prioridade: a comunicação passa na frente e é atendida
        var e2 = EstadoBase.Inicial(P);
        e2.BateriaWh = pouco;
        e2.Eq(TipoEquip.Comunicacao).Prioridade = 0;
        Motor.PassoHora(e2, P, Hora(0, 2));
        Assert.True(e2.Eq(TipoEquip.Comunicacao).PotenciaAtendidaW > 0);
    }

    [Fact]
    public void A_agua_tratada_nao_volta_na_hora_e_uma_parte_vira_salmoura()
    {
        var e = EstadoBase.Inicial(P);
        e.AguaResidualKg = 3; double potavel0 = e.AguaPotavelKg;
        var vazio = Hora(0, 12, tripulantes: 0);
        e.Eq(TipoEquip.GeradorO2).Ligado = false;
        e.Eq(TipoEquip.Desumidificador).Ligado = false;
        var r1 = Motor.PassoHora(e, P, vazio);
        Assert.Equal(0, r1.AguaRecuperadaKg);
        Assert.Equal(3, e.AguaEmTratamentoKg, 9);
        Assert.Equal(potavel0, e.AguaPotavelKg, 9);
        double recuperada = 0;
        for (int i = 0; i < 5; i++) recuperada += Motor.PassoHora(e, P, vazio).AguaRecuperadaKg;
        Assert.Equal(3 * P.AguaRecuperacaoFracao, recuperada, 9);
        Assert.Equal(3 * (1 - P.AguaRecuperacaoFracao), e.SalmouraKg, 9);
    }

    [Fact]
    public void A_tempestade_de_poeira_corta_a_geracao_pela_formula_declarada()
    {
        double Dia(double tau)
        {
            var e = EstadoBase.Inicial(P);
            for (int h = 0; h < 24; h++) Motor.PassoHora(e, P, Hora(0, h, tau));
            return e.Livro.EnergiaGeradaWh;
        }
        double limpo = Dia(P.TauCeuLimpo), tempestade = Dia(3.0);
        Assert.Equal(Math.Exp(-P.KAtenuacaoPoeira * (3.0 - P.TauCeuLimpo)), tempestade / limpo, 9);
        Assert.True(limpo > 120_000, $"um sol limpo tem de gerar mais de 120 kWh (gerou {limpo / 1000:F1})");
    }

    [Fact]
    public void O_Sol_ao_meio_dia_bate_com_a_conta_feita_a_mao()
    {
        double ls = P.LsInicialGraus + 0.5 * 360.0 / P.AnoMarcianoSols;
        double r = P.SemiEixoUa * (1 - P.Excentricidade * P.Excentricidade) / (1 + P.Excentricidade * Math.Cos((ls - P.LsPerielioGraus) * Math.PI / 180));
        double decl = Math.Asin(Math.Sin(P.ObliquidadeGraus * Math.PI / 180) * Math.Sin(ls * Math.PI / 180));
        double cz = Math.Cos((P.LatitudeGraus * Math.PI / 180) - decl);     // ao meio-dia o zênite é latitude − declinação
        double esperado = P.ConstanteSolarTerraWM2 / (r * r) * cz * Math.Exp(-P.KAtenuacaoPoeira * 0.5);
        Assert.Equal(esperado, SolMarte.IrradianciaSuperficieWM2(P, 0, 12.0, 0.5), 6);
        Assert.InRange(SolMarte.IrradianciaTopoWM2(P, ls), 480, 720);       // Marte recebe de ~490 a ~715 W/m2
        Assert.Equal(0, SolMarte.IrradianciaSuperficieWM2(P, 0, 0.5, 0.5));  // noite
    }

    [Fact]
    public void O_motor_e_deterministico()
    {
        double Rodar()
        {
            var e = EstadoBase.Inicial(P);
            for (int sol = 0; sol < 3; sol++) for (int h = 0; h < 24; h++) Motor.PassoHora(e, P, Hora(sol, h, 0.5 + sol, fora: h == 10 ? 1 : 0));
            return e.MassaTotalKg() * 1e3 + e.BateriaWh + e.TemperaturaC * 7 + e.Co2Kg * 13;
        }
        Assert.Equal(Rodar(), Rodar());
    }

    [Fact]
    public void Uma_base_saudavel_se_sustenta_por_oito_sois_dentro_dos_limites()
    {
        var e = EstadoBase.Inicial(P);
        for (int sol = 0; sol < 8; sol++)
            for (int h = 0; h < 24; h++)
            {
                var r = Motor.PassoHora(e, P, Hora(sol, h));
                Assert.InRange(r.Ppo2Kpa, P.Ppo2AlertaKpa, 23.5);
                Assert.True(r.Ppco2Kpa < P.Ppco2AlertaKpa, $"CO2 em {r.Ppco2Kpa:F2} kPa no sol {sol} hora {h}");
                Assert.InRange(r.TemperaturaC, 18, 27);
                Assert.InRange(r.Umidade, 0.25, 0.75);
                Assert.InRange(r.PressaoKpa, 99, 103);
                Assert.Empty(r.SemEnergia);
                Assert.Empty(r.SemAgua);
            }
        Assert.True(e.AguaPotavelKg > 60, $"água potável no fim: {e.AguaPotavelKg:F1} kg");
        Assert.True(e.BateriaWh > P.BateriaCapacidadeWh * 0.15);
    }
}
