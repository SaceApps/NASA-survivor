using MissaoMarte.Nucleo.Dados;
using MissaoMarte.Nucleo.Jogo;
using MissaoMarte.Nucleo.Simulacao;
using Xunit;

namespace MissaoMarte.Testes;

/// <summary>
/// AS REGRAS DO JOGO SOB PROVA: o que os jogadores podem e não podem fazer, o ciclo do sol, o relatório,
/// salvar e carregar, e a missão inteira (quem cuida da base vence; quem não cuida, perde).
/// </summary>
public class TestesJogo
{
    private static readonly Profissao[] Todas = Enum.GetValues<Profissao>();
    private static Partida Nova(int n = 6, int nivel = 2) => Partida.Nova("teste", Todas.Take(n).Select(p => (p.Nome(), p)), nivel);
    private static Tripulante De(Partida pt, Profissao p) => pt.Tripulacao.First(t => t.Profissao == p);

    /// <summary>Uma equipe que faz o básico: rotinas, consertos e economia de energia na tempestade.</summary>
    private static void JogarOBasico(Partida pt)
    {
        bool aperto = pt.AmbienteDoSol().Tau >= 1.5 || pt.BateriaFracao < 0.25;
        foreach (var tipo in new[] { TipoEquip.FornoRegolito, TipoEquip.CarregadorRover, TipoEquip.Iluminacao })
            if (pt.Base.Eq(tipo).Ligado == aperto) pt.Alternar(tipo);
        var eng = De(pt, Profissao.Engenheiro);
        foreach (var eq in pt.Base.Equipamentos.Where(e => e.Saude < 0.99).OrderBy(e => e.Saude).ToList())
            if (!pt.Reparar(eng.Id, eq.Tipo).Ok)
                pt.Reparar(pt.Tripulacao.Where(t => t.Id != eng.Id).OrderByDescending(pt.HorasLivres).First().Id, eq.Tipo);
        foreach (var t in pt.Tripulacao) pt.FazerRotina(t.Id);
    }

    [Fact]
    public void O_mapa_tem_61_hexagonos_e_todo_lugar_com_nome_guarda_um_fato_real_com_fonte_e_data()
    {
        var mapa = MapaJezero.Criar();
        Assert.Equal(61, mapa.Count);
        Assert.Equal(7, mapa.Count(c => c.Explorado));
        var pacote = PacoteNasa.Padrao;
        var lugares = mapa.Where(c => c.DescobertaId != null).ToList();
        Assert.True(lugares.Count >= 10);
        Assert.Equal(lugares.Count, lugares.Select(c => c.DescobertaId).Distinct().Count());
        Assert.All(lugares, c => Assert.NotNull(pacote.Fato(c.DescobertaId)));
        Assert.All(pacote.Fatos, f =>
        {
            Assert.StartsWith("http", f.Url);
            Assert.False(string.IsNullOrWhiteSpace(f.Fonte));
            Assert.False(string.IsNullOrWhiteSpace(f.DataReferencia));
        });
        Assert.All(pacote.Imagens, i => { Assert.False(string.IsNullOrWhiteSpace(i.Credito)); Assert.StartsWith("https://images.nasa.gov/", i.UrlPagina); });
    }

    [Fact]
    public void O_veiculo_nao_sobe_a_escarpa_mas_chega_ao_pe_do_delta_e_a_pe_se_chega_ao_topo()
    {
        var pt = Nova();
        Assert.NotNull(pt.CustoVeiculo(new Hex(-2, 0)));
        Assert.Null(pt.CustoVeiculo(new Hex(-4, 0)));
        Assert.NotNull(pt.CustoAPe(new Hex(-2, 0), new Hex(-4, 0)));
        // sem ninguém a bordo o veículo não anda; a pé, longe da base e do veículo, a regra de segurança barra
        Assert.False(pt.MoverVeiculo(new Hex(1, 0)).Ok);
        Assert.False(pt.MoverAPe(De(pt, Profissao.Geologo).Id, new Hex(4, -2)).Ok);
    }

    [Fact]
    public void Uma_jogada_recusada_nao_muda_nada_na_partida()
    {
        var pt = Nova();
        string antes = pt.ParaJson();
        Assert.False(pt.Analisar(De(pt, Profissao.Cientista).Id).Ok);                              // não há amostra
        Assert.False(pt.MoverVeiculo(new Hex(1, 0)).Ok);                                            // ninguém a bordo
        Assert.False(pt.Reparar(De(pt, Profissao.Engenheiro).Id, TipoEquip.RemovedorCo2).Ok);       // está em ordem
        Assert.False(pt.ColetarAmostra(De(pt, Profissao.Geologo).Id).Ok);                           // na base não há rocha
        Assert.False(pt.ColetarArgila(De(pt, Profissao.Piloto).Id).Ok);
        Assert.False(pt.Alternar(TipoEquip.Vedacao).Ok);                                            // vedação não tem chave
        Assert.Equal(antes, pt.ParaJson());
    }

    [Fact]
    public void As_horas_de_trabalho_tem_limite_e_a_rotina_e_uma_por_sol()
    {
        var pt = Nova();
        var eng = De(pt, Profissao.Engenheiro);
        pt.Base.Eq(TipoEquip.Vedacao).Saude = 0.5; pt.Base.Eq(TipoEquip.ProcessadorAgua).Saude = 0.5; pt.Base.Eq(TipoEquip.RemovedorCo2).Saude = 0.5;
        Assert.True(pt.Reparar(eng.Id, TipoEquip.Vedacao).Ok);             // 3 h
        Assert.False(pt.Reparar(eng.Id, TipoEquip.Vedacao).Ok);            // já está sendo consertada
        Assert.True(pt.Reparar(eng.Id, TipoEquip.ProcessadorAgua).Ok);     // 6 h
        Assert.False(pt.Reparar(eng.Id, TipoEquip.RemovedorCo2).Ok);       // 9 h não cabem em 8
        Assert.True(pt.FazerRotina(eng.Id).Ok);                            // 7 h
        Assert.False(pt.FazerRotina(eng.Id).Ok);
        Assert.Equal(1, pt.HorasLivres(eng), 9);
        // quem não é engenheiro leva o dobro
        var med = De(pt, Profissao.Medico);
        Assert.True(pt.Reparar(med.Id, TipoEquip.RemovedorCo2).Ok);
        Assert.Equal(2, pt.HorasLivres(med), 9);
    }

    [Fact]
    public void O_conserto_so_vale_a_partir_da_hora_em_que_termina()
    {
        var pt = Nova();
        pt.Roteiro.Clear();
        pt.Base.Eq(TipoEquip.RemovedorCo2).Saude = 0;
        Assert.True(pt.Reparar(De(pt, Profissao.Engenheiro).Id, TipoEquip.RemovedorCo2).Ok);   // das 8 h às 11 h
        Assert.True(pt.EncerrarSol().Ok);
        var horas = pt.Relatorios[^1].Horas;
        Assert.All(horas.Take(3), h => Assert.Equal(0, h.Co2RemovidoKg));          // 8 h, 9 h e 10 h: ainda quebrado
        Assert.All(horas.Skip(3), h => Assert.True(h.Co2RemovidoKg > 0));          // das 11 h em diante: funcionando
        Assert.Equal(1.0, pt.Base.Eq(TipoEquip.RemovedorCo2).Saude);
    }

    [Fact]
    public void Nao_da_para_encerrar_o_sol_com_gente_fora_e_o_retorno_de_emergencia_custa_saude()
    {
        var pt = Nova();
        var geo = De(pt, Profissao.Geologo);
        Assert.True(pt.MoverAPe(geo.Id, new Hex(2, -1)).Ok);
        Assert.False(pt.EncerrarSol().Ok);
        Assert.Equal(0, pt.Sol);
        Assert.True(pt.RetornoDeEmergencia().Ok);
        Assert.True(geo.NaBase);
        Assert.Equal(75, geo.Saude, 9);
        Assert.True(pt.EncerrarSol().Ok);
        Assert.Equal(1, pt.Sol);
        Assert.All(pt.Tripulacao, t => Assert.Equal(0, t.HorasUsadas));
    }

    [Fact]
    public void Quem_esta_de_traje_respira_do_tanque_de_reserva_e_nao_do_ar_da_base()
    {
        var pt = Nova();
        pt.Roteiro.Clear();
        var geo = De(pt, Profissao.Geologo);
        double tanque0 = pt.Base.TanqueO2Kg;
        Assert.True(pt.MoverAPe(geo.Id, new Hex(2, -1)).Ok);
        Assert.True(pt.MoverAPe(geo.Id, Hex.Origem).Ok);
        Assert.True(pt.EncerrarSol().Ok);
        Assert.True(pt.Base.TanqueO2Kg < tanque0 - 0.1, "a atividade externa tem de gastar o oxigênio de reserva");
        Assert.True(pt.Base.Livro.SaidasKg.ContainsKey("CO2 solto pelo traje"));
        Assert.InRange(pt.DiferencaDeMassaKg, -1e-6, 1e-6);
    }

    [Fact]
    public void A_amostra_coletada_so_pode_ser_analisada_no_sol_seguinte_e_a_analise_conta_para_o_objetivo()
    {
        var pt = Nova();
        var geo = De(pt, Profissao.Geologo); var cien = De(pt, Profissao.Cientista);
        var afloramento = new Hex(2, -1);
        Assert.True(pt.MoverAPe(geo.Id, afloramento).Ok);
        Assert.True(pt.Cel(afloramento)!.DescobertaVista);                 // chegar revela o cartão de ciência
        Assert.True(pt.ColetarAmostra(geo.Id).Ok);
        Assert.True(pt.MoverAPe(geo.Id, Hex.Origem).Ok);
        Assert.Equal(0, pt.AmostrasNaBase);
        Assert.Equal(1, pt.AmostrasChegando);
        Assert.False(pt.Analisar(cien.Id).Ok);                             // a amostra ainda não chegou ao laboratório
        Assert.True(pt.EncerrarSol().Ok);
        Assert.Equal(1, pt.AmostrasNaBase);
        double agua = pt.Base.AguaPotavelKg;
        Assert.True(pt.Analisar(cien.Id).Ok);
        Assert.Equal(agua - 2, pt.Base.AguaPotavelKg, 9);                  // a análise usa 2 kg de água, que vira água usada
        Assert.True(pt.EncerrarSol().Ok);
        Assert.Equal(1, pt.AmostrasAnalisadas);
        Assert.Equal(1, pt.Objetivos().First(o => o.Id == "ciencia").Atual);
        Assert.Contains(pt.CartoesConquistados(), id => id == pt.Cel(afloramento)!.DescobertaId);
    }

    [Fact]
    public void Analise_sem_energia_no_laboratorio_nao_termina_e_a_amostra_volta()
    {
        var pt = Nova();
        pt.AmostrasNaBase = 1;
        Assert.True(pt.Analisar(De(pt, Profissao.Cientista).Id).Ok);
        Assert.True(pt.Alternar(TipoEquip.Laboratorio).Ok);                // alguém desliga o laboratório no meio
        Assert.True(pt.EncerrarSol().Ok);
        Assert.Equal(0, pt.AmostrasAnalisadas);
        Assert.Equal(1, pt.AmostrasNaBase);
        Assert.Contains(pt.Relatorios[^1].Itens, i => i.Titulo == "Análise interrompida");
    }

    [Fact]
    public void O_solo_argiloso_do_delta_vira_agua_de_marte_e_a_massa_continua_fechando()
    {
        var pt = Nova();
        pt.Roteiro.Clear();
        var pil = De(pt, Profissao.Piloto); var geo = De(pt, Profissao.Geologo);
        Assert.True(pt.Embarcar(pil.Id, true).Ok);
        Assert.True(pt.Embarcar(geo.Id, true).Ok);
        double bateria = pt.Base.RoverBateriaWh;
        Assert.True(pt.MoverVeiculo(new Hex(-2, 0)).Ok);
        Assert.True(pt.Base.RoverBateriaWh < bateria);
        Assert.False(pt.Base.RoverNaBase);
        Assert.True(pt.ColetarArgila(pil.Id).Ok);                          // duas pessoas e o veículo
        Assert.True(pt.MoverVeiculo(Hex.Origem).Ok);
        Assert.Equal(150, pt.ArgilaChegandoKg, 6);
        Assert.Equal(0, pt.Base.RegolitoKg);                               // o solo só entra no forno na hora da chegada
        Assert.True(pt.EncerrarSol().Ok);
        Assert.Equal(150, pt.ArgilaEntregueKg, 6);
        Assert.Equal(150 * pt.P.RegolitoFracaoAgua, pt.Base.AguaExtraidaDeMarteKg, 6);
        Assert.True(pt.Objetivos().First(o => o.Id == "agua").Cumprido);
        Assert.InRange(pt.DiferencaDeMassaKg, -1e-6, 1e-6);
        Assert.Contains(pt.Relatorios[^1].Itens, i => i.Titulo == "Água tirada de Marte");
    }

    [Fact]
    public void Salvar_e_carregar_devolve_a_mesma_partida_e_o_resto_da_missao_sai_igual()
    {
        var a = Nova();
        JogarOBasico(a);
        Assert.True(a.MoverAPe(De(a, Profissao.Geologo).Id, new Hex(0, 1)).Ok);
        Assert.True(a.MoverAPe(De(a, Profissao.Geologo).Id, Hex.Origem).Ok);
        Assert.True(a.EncerrarSol().Ok);
        var b = Partida.DeJson(a.ParaJson());
        Assert.Equal(a.ParaJson(), b.ParaJson());
        while (a.Fase == FaseMissao.EmAndamento)
        {
            JogarOBasico(a); JogarOBasico(b);
            Assert.True(a.EncerrarSol().Ok);
            Assert.True(b.EncerrarSol().Ok);
        }
        Assert.Equal(a.ParaJson(), b.ParaJson());
    }

    [Fact]
    public void A_previsao_nao_mexe_na_partida_e_acerta_o_sol_quando_nao_ha_imprevisto()
    {
        var pt = Nova();
        pt.Roteiro.Clear();
        Assert.True(pt.Alternar(TipoEquip.GeradorO2).Ok);
        string antes = pt.ParaJson();
        var previsto = pt.Prever();
        Assert.Equal(antes, pt.ParaJson());
        Assert.True(pt.EncerrarSol().Ok);
        var real = pt.Relatorios[^1];
        Assert.Equal(previsto.GeradoKwh, real.GeradoKwh, 9);
        Assert.Equal(previsto.BateriaFimWh, real.BateriaFimWh, 6);
        Assert.Equal(previsto.Horas.Max(h => h.Ppco2Kpa), real.Horas.Max(h => h.Ppco2Kpa), 9);
        Assert.Equal(previsto.Horas.Min(h => h.Ppo2Kpa), real.Horas.Min(h => h.Ppo2Kpa), 9);
    }

    [Fact]
    public void A_tempestade_de_poeira_derruba_a_geracao_na_proporcao_da_formula()
    {
        var pt = Nova();
        double limpo = pt.GeracaoPrevistaKwh(pt.P.TauCeuLimpo);
        double tempestade = pt.GeracaoPrevistaKwh(3.2);
        Assert.Equal(Math.Exp(-pt.P.KAtenuacaoPoeira * (3.2 - pt.P.TauCeuLimpo)), tempestade / limpo, 9);
        Assert.InRange(tempestade / limpo, 0.35, 0.45);
    }

    [Fact]
    public void Sem_fazer_nada_a_missao_de_9_a_10_anos_termina_em_derrota_e_o_relatorio_diz_por_que()
    {
        var pt = Nova(nivel: 2);
        while (pt.Fase == FaseMissao.EmAndamento) Assert.True(pt.EncerrarSol().Ok);
        Assert.Equal(FaseMissao.Derrota, pt.Fase);
        Assert.Equal(0, pt.Estrelas());
        var titulos = pt.Relatorios.SelectMany(r => r.Itens).Where(i => i.Gravidade == Gravidade.Perigo).Select(i => i.Titulo).ToHashSet();
        Assert.Contains("Gás carbônico alto", titulos);
        Assert.Contains("Faltou energia", titulos);
        Assert.Contains("Vazamento na vedação", titulos);
        Assert.InRange(pt.DiferencaDeMassaKg, -1e-6, 1e-6);
    }

    [Fact]
    public void Fazendo_o_basico_todos_sobrevivem_bem_nos_tres_niveis_e_as_contas_fecham()
    {
        foreach (int nivel in new[] { 1, 2, 3 })
        {
            var pt = Nova(nivel: nivel);
            while (pt.Fase == FaseMissao.EmAndamento)
            {
                JogarOBasico(pt);
                Assert.True(pt.EncerrarSol().Ok);
            }
            Assert.Equal(FaseMissao.Incompleta, pt.Fase);                  // sobreviveu, mas não fez a ciência nem a água
            Assert.All(pt.Tripulacao, t => Assert.True(t.Saude >= 80, $"nível {nivel}: {t.Nome} terminou com saúde {t.Saude:0}"));
            Assert.Equal(8, pt.Relatorios.Count);
            Assert.All(pt.Relatorios, r => { Assert.Equal(24, r.Horas.Count); Assert.NotNull(PacoteNasa.Padrao.Fato(r.FatoId)); });
            Assert.Equal(8, pt.Relatorios.Select(r => r.FatoId).Distinct().Count());
            // massa: guardada + saídas − entradas = começo; energia: gerada = usada + perdida + sobra + variação da bateria
            Assert.InRange(pt.DiferencaDeMassaKg, -1e-6, 1e-6);
            var livro = pt.Base.Livro;
            double bateria0 = pt.P.BateriaCapacidadeWh * pt.P.BateriaInicialFracao;
            double fechamento = livro.EnergiaGeradaWh - livro.EnergiaCargasWh - livro.EnergiaPerdasWh - livro.EnergiaDesperdicadaWh - (pt.Base.BateriaWh - bateria0);
            Assert.InRange(fechamento, -1e-4, 1e-4);
        }
    }

    [Fact]
    public void O_nivel_de_7_a_8_anos_perdoa_mais_que_o_de_9_a_10()
    {
        var facil = Nova(nivel: 1);
        while (facil.Fase == FaseMissao.EmAndamento) Assert.True(facil.EncerrarSol().Ok);
        Assert.NotEqual(FaseMissao.Derrota, facil.Fase);
        Assert.True(facil.Roteiro.Count < Nova(nivel: 2).Roteiro.Count);
        Assert.True(facil.TauPorSol.Max() < Nova(nivel: 2).TauPorSol.Max());
    }

    [Fact]
    public void A_missao_funciona_com_2_a_6_tripulantes_e_com_qualquer_combinacao_de_profissoes()
    {
        for (int n = 2; n <= 6; n++)
        {
            var pt = Partida.Nova("teste", Todas.Reverse().Take(n).Select(p => ("", p)), 2);
            Assert.Equal(n, pt.Tripulacao.Count);
            Assert.All(pt.Tripulacao, t => Assert.False(string.IsNullOrWhiteSpace(t.Nome)));
            foreach (var t in pt.Tripulacao) Assert.True(pt.FazerRotina(t.Id).Ok || t.Profissao == Profissao.Cientista);
            Assert.True(pt.EncerrarSol().Ok);
            Assert.Single(pt.Relatorios);
        }
        Assert.Throws<ArgumentException>(() => Partida.Nova("só um", new[] { ("A", Profissao.Medico) }, 2));
    }
}
