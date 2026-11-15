using MissaoMarte.Nucleo.Simulacao;

namespace MissaoMarte.Nucleo.Jogo;

/// <summary>Consultas para a tela: quanto custa uma viagem e como está cada sistema (semáforo com o motivo).</summary>
public sealed partial class Partida
{
    public (double Horas, int Km)? CustoAPe(Hex de, Hex para)
    {
        var r = Rota(de, para, FatorAPe);
        return r is null ? null : (r.Value.Custo * 0.5, r.Value.Caminho.Count);
    }

    public (double Horas, double Kwh, int Km)? CustoVeiculo(Hex para)
    {
        var r = Rota(Veiculo.Pos, para, FatorVeiculo);
        if (r is null) return null;
        bool piloto = NoVeiculo.Any(t => t.Profissao == Profissao.Piloto);
        double economia = Veiculo.Revisado ? 0.85 : 1.0;
        return (Math.Max(0.25, r.Value.Custo * 0.15 * (piloto ? 1.0 : 2.0)),
                r.Value.Custo * P.RoverWhPorKm * economia * (piloto ? 1.0 : 1.5) / 1000.0,
                r.Value.Caminho.Count);
    }

    public (Gravidade G, string Frase) EstadoDoAr()
    {
        var rem = Base.Eq(TipoEquip.RemovedorCo2); var ger = Base.Eq(TipoEquip.GeradorO2); var ved = Base.Eq(TipoEquip.Vedacao);
        if (Ppco2Kpa >= P.Ppco2PerigoKpa) return (Gravidade.Perigo, "Gás carbônico alto demais no ar.");
        if (Ppo2Kpa < P.Ppo2PerigoKpa) return (Gravidade.Perigo, "Pouco oxigênio no ar.");
        if (PressaoKpa < 70) return (Gravidade.Perigo, "A pressão da base caiu muito.");
        if (rem.Quebrado) return (Gravidade.Perigo, "O removedor de CO2 está quebrado: o gás carbônico vai subir.");
        if (ved.Saude < 0.999) return (Gravidade.Atencao, "A vedação está vazando ar.");
        if (!rem.Ligado) return (Gravidade.Atencao, "O removedor de CO2 está desligado.");
        if (!ger.Ligado) return (Gravidade.Atencao, "O gerador de oxigênio está desligado: a base está vivendo do ar guardado.");
        if (rem.Saude < 0.999 || ger.Saude < 0.999) return (Gravidade.Atencao, "Uma máquina do ar está com defeito.");
        if (Ppco2Kpa >= P.Ppco2AlertaKpa) return (Gravidade.Atencao, "Gás carbônico acima do limite normal.");
        if (Ppo2Kpa < P.Ppo2AlertaKpa - 0.1) return (Gravidade.Atencao, "Oxigênio abaixo do ideal.");
        if (Base.TanqueN2Kg < 20) return (Gravidade.Atencao, "Pouco nitrogênio de reserva.");
        return (Gravidade.Bom, "Ar bom para respirar.");
    }

    public (Gravidade G, string Frase) EstadoDaAgua()
    {
        var proc = Base.Eq(TipoEquip.ProcessadorAgua);
        if (Base.AguaPotavelKg <= P.ReservaBebidaKg) return (Gravidade.Perigo, "Só resta a reserva de água para beber.");
        if (proc.Quebrado) return (Gravidade.Perigo, "O processador de água está quebrado.");
        if (!proc.Ligado) return (Gravidade.Atencao, "O processador de água está desligado: a água usada não volta.");
        if (proc.Saude < 0.999) return (Gravidade.Atencao, "O filtro do processador está saturado: perde-se mais água.");
        if (Base.AguaPotavelKg < 80) return (Gravidade.Atencao, "O tanque de água potável está baixo.");
        return (Gravidade.Bom, "A água está sendo reciclada.");
    }

    public (Gravidade G, string Frase) EstadoDaEnergia()
    {
        if (BateriaFracao < 0.10) return (Gravidade.Perigo, "A bateria está quase vazia.");
        if (Tau >= 1.5) return (Gravidade.Atencao, "Tempestade de poeira: os painéis geram bem menos.");
        if (BateriaFracao < 0.30) return (Gravidade.Atencao, "A bateria amanheceu baixa.");
        if (Base.SujeiraPaineis >= 0.04) return (Gravidade.Atencao, "Os painéis estão sujos de poeira.");
        if (Tau >= 0.8) return (Gravidade.Atencao, "Poeira no ar: os painéis geram menos.");
        return (Gravidade.Bom, "Energia em dia.");
    }

    /// <summary>Os cartões de ciência já conquistados: os lugares visitados e os fatos dos relatórios.</summary>
    public List<string> CartoesConquistados() =>
        Celulas.Where(c => c.DescobertaVista && c.DescobertaId != null).Select(c => c.DescobertaId!)
            .Concat(Relatorios.Where(r => r.FatoId != null).Select(r => r.FatoId!)).Distinct().ToList();
}
