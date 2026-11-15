using MissaoMarte.Nucleo.Jogo;
using MissaoMarte.Nucleo.Simulacao;
using Xunit;

namespace MissaoMarte.Testes;

/// <summary>
/// OS ACHADOS DA REVISÃO DE 14/11/2026, cada um preso por um teste: o sol encerrado duas vezes, as análises
/// simultâneas, a saída de traje sem oxigênio, a água de beber usada pelo gerador, a umidade acima de 100%,
/// os objetivos por arredondamento, o passageiro que não estava no veículo e o retorno de emergência repetido.
/// </summary>
public class TestesRevisao
{
    private static readonly Profissao[] Todas = Enum.GetValues<Profissao>();
    private static Partida Nova() => Partida.Nova("teste", Todas.Select(p => (p.Nome(), p)), 2);
    private static Tripulante De(Partida pt, Profissao p) => pt.Tripulacao.First(t => t.Profissao == p);

    [Fact]
    public void Dois_toques_em_encerrar_o_mesmo_sol_processam_um_sol_so()
    {
        var pt = Nova();
        int visto = pt.Sol;
        Assert.True(pt.EncerrarSol(visto).Ok);
        Assert.False(pt.EncerrarSol(visto).Ok);        // o segundo toque, ou o segundo aparelho, chega atrasado
        Assert.Equal(1, pt.Sol);
        Assert.Single(pt.Relatorios);
        Assert.True(pt.EncerrarSol(pt.Sol).Ok);        // o sol seguinte, visto na tela, encerra normalmente
        Assert.Equal(2, pt.Sol);
    }

    [Fact]
    public void O_laboratorio_faz_uma_analise_de_cada_vez()
    {
        var pt = Nova();
        pt.AmostrasNaBase = 3;
        var cien = De(pt, Profissao.Cientista); var com = De(pt, Profissao.Comandante);
        double agua = pt.Base.AguaPotavelKg;
        Assert.True(pt.Analisar(cien.Id).Ok);              // das 8 h às 10 h
        var recusa = pt.Analisar(com.Id);                  // das 8 h às 12 h: bate com a primeira
        Assert.False(recusa.Ok);
        Assert.Contains("uma análise de cada vez", recusa.Mensagem);
        Assert.Equal(2, pt.AmostrasNaBase);
        Assert.Equal(agua - 2, pt.Base.AguaPotavelKg, 9);  // a análise recusada não gastou água
        Assert.Equal(0, com.HorasUsadas);
        Assert.True(pt.Analisar(cien.Id).Ok);              // das 10 h às 12 h: em seguida, pode
        Assert.True(pt.EncerrarSol().Ok);
        Assert.Equal(2, pt.AmostrasAnalisadas);
    }

    [Fact]
    public void Sem_oxigenio_de_reserva_ninguem_sai_de_traje_mas_quem_esta_fora_volta_para_casa()
    {
        var pt = Nova();
        var geo = De(pt, Profissao.Geologo); var eng = De(pt, Profissao.Engenheiro); var pil = De(pt, Profissao.Piloto);
        Assert.True(pt.MoverAPe(geo.Id, new Hex(1, 0)).Ok);            // saiu com o tanque cheio
        pt.Base.TanqueO2Kg = 0.05;
        pt.Base.SujeiraPaineis = 0.10;
        var limpar = pt.LimparPaineis(eng.Id);
        Assert.False(limpar.Ok);
        Assert.Contains("oxigênio", limpar.Mensagem);
        Assert.False(pt.MoverAPe(eng.Id, new Hex(0, 1)).Ok);
        Assert.True(pt.Embarcar(pil.Id, true).Ok);
        Assert.False(pt.MoverVeiculo(new Hex(-1, 0)).Ok);
        Assert.Equal(0, eng.HorasUsadas);
        Assert.True(pt.MoverAPe(geo.Id, Hex.Origem).Ok);               // a volta para a base nunca é barrada
        // com o tanque cheio, as mesmas saídas são aceitas
        pt.Base.TanqueO2Kg = 25;
        Assert.True(pt.LimparPaineis(eng.Id).Ok);
        Assert.True(pt.MoverVeiculo(new Hex(-1, 0)).Ok);
    }

    [Fact]
    public void O_gerador_de_oxigenio_nao_usa_a_agua_de_beber_nem_depois_de_a_tripulacao_usar_agua_na_mesma_hora()
    {
        var p = ParametrosCenario.Padrao();
        var e = EstadoBase.Inicial(p);
        e.AguaPotavelKg = p.ReservaBebidaKg + 0.5;      // meio quilo acima da reserva no começo da hora
        e.O2Kg *= 0.90;                                 // falta oxigênio: o gerador quer trabalhar no máximo
        e.Eq(TipoEquip.ProcessadorAgua).Ligado = false; // nada volta para o tanque nesta hora
        var entrada = new EntradaHora
        {
            Sol = 0, Hora = 12, Tau = p.TauCeuLimpo,
            Tripulantes = Enumerable.Range(0, 6).Select(i => new TripulanteHora { Id = i, Fator = p.FatorAtividadeTrabalho }).ToList(),
        };
        var r = Motor.PassoHora(e, p, entrada);
        // seis pessoas bebendo e tomando banho já levaram o tanque abaixo da reserva: o gerador não pode tirar nada
        Assert.True(e.AguaPotavelKg < p.ReservaBebidaKg);
        Assert.Equal(0, r.O2GeradoKg);
        Assert.False(e.Livro.SaidasKg.ContainsKey("hidrogênio da eletrólise"));
    }

    [Fact]
    public void A_umidade_registrada_nunca_passa_de_100_por_cento_mesmo_com_a_base_esfriando_e_a_massa_fecha()
    {
        var p = ParametrosCenario.Padrao();
        var e = EstadoBase.Inicial(p);
        e.Eq(TipoEquip.Desumidificador).Ligado = false;
        e.Eq(TipoEquip.Aquecimento).Ligado = false;
        double m0 = e.MassaTotalKg();
        bool condensou = false;
        for (int i = 0; i < 36; i++)
        {
            int hora = i % 24;
            var r = Motor.PassoHora(e, p, new EntradaHora
            {
                Sol = i / 24, Hora = hora, Tau = p.TauCeuLimpo, Vigilia = hora is >= 6 and < 22,
                Tripulantes = Enumerable.Range(0, 6).Select(k => new TripulanteHora { Id = k, Fator = 1.0 }).ToList(),
            });
            Assert.True(r.Umidade <= 1.0 + 1e-9, $"hora {i}: umidade {r.Umidade:0.0000}");
            condensou |= r.Avisos.Contains("condensacao");
        }
        Assert.True(condensou, "sem desumidificador o ar tem de saturar e condensar");
        Assert.InRange(e.MassaTotalKg() + e.Livro.TotalSaidasKg - e.Livro.TotalEntradasKg - m0, -1e-6, 1e-6);
    }

    [Fact]
    public void Os_objetivos_nao_se_cumprem_por_arredondamento_nem_com_alguem_sem_condicoes()
    {
        var pt = Nova();
        pt.Base.AguaExtraidaDeMarteKg = 4.96;
        Assert.False(pt.Objetivos().First(o => o.Id == "agua").Cumprido);
        pt.Base.AguaExtraidaDeMarteKg = 5.0;
        Assert.True(pt.Objetivos().First(o => o.Id == "agua").Cumprido);
        pt.Sol = pt.SolsDaMissao;
        pt.Tripulacao[0].Saude = 0;
        Assert.False(pt.Objetivos().First(o => o.Id == "sobreviver").Cumprido);
        pt.Tripulacao[0].Saude = 40;
        Assert.True(pt.Objetivos().First(o => o.Id == "sobreviver").Cumprido);
    }

    [Fact]
    public void Quem_nao_esta_no_hexagono_do_veiculo_nao_viaja_com_ele()
    {
        var pt = Nova();
        var pil = De(pt, Profissao.Piloto); var med = De(pt, Profissao.Medico);
        Assert.True(pt.Embarcar(pil.Id, true).Ok);
        Assert.True(pt.Embarcar(med.Id, true).Ok);
        med.Saude = 0;                                         // passou mal a bordo, ainda na base
        Assert.True(pt.MoverVeiculo(new Hex(-1, 0)).Ok);
        Assert.Equal(Hex.Origem, med.Pos);
        med.Saude = 60;                                        // foi tratado na base, com o veículo longe
        Assert.True(pt.MoverVeiculo(new Hex(-2, 0)).Ok);
        Assert.Equal(Hex.Origem, med.Pos);                     // não é levado de onde não estava
        Assert.Equal(0, med.HorasUsadas);
        Assert.Equal(new Hex(-2, 0), pil.Pos);
    }

    [Fact]
    public void O_retorno_de_emergencia_so_vale_com_alguem_fora_e_um_segundo_toque_nao_tira_animo_de_novo()
    {
        var pt = Nova();
        double animo = pt.Moral;
        Assert.False(pt.RetornoDeEmergencia().Ok);             // ninguém está fora
        Assert.Equal(animo, pt.Moral);
        Assert.True(pt.MoverAPe(De(pt, Profissao.Geologo).Id, new Hex(1, 0)).Ok);
        Assert.True(pt.RetornoDeEmergencia().Ok);
        Assert.Equal(animo - 5, pt.Moral);
        Assert.False(pt.RetornoDeEmergencia().Ok);             // o segundo toque chega depois que todos voltaram
        Assert.Equal(animo - 5, pt.Moral);
    }
}
