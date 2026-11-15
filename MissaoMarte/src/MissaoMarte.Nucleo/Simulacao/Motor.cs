namespace MissaoMarte.Nucleo.Simulacao;

/// <summary>O que um tripulante está fazendo nesta hora, do ponto de vista da física.</summary>
public sealed class TripulanteHora
{
    public int Id { get; set; }
    /// <summary>Fora da base, de traje: respira do tanque de O2, e o CO2 e o vapor vão para fora.</summary>
    public bool Fora { get; set; }
    /// <summary>Metabolismo em relação à média do dia (dormindo, trabalhando, atividade externa).</summary>
    public double Fator { get; set; } = 1.0;
}

public sealed class EntradaHora
{
    public int Sol { get; set; }
    public int Hora { get; set; }
    /// <summary>Opacidade da poeira no céu (dado de entrada: vem do cenário ou de medidas reais).</summary>
    public double Tau { get; set; }
    public List<TripulanteHora> Tripulantes { get; set; } = new();
    public bool LaboratorioEmUso { get; set; }
    public bool Vigilia { get; set; } = true;
    public bool RacionamentoHigiene { get; set; }
}

/// <summary>O retrato de uma hora: o que o painel, os gráficos e o relatório mostram.</summary>
public sealed class RegistroHora
{
    public int Sol { get; set; }
    public int Hora { get; set; }
    public double Tau { get; set; }
    public double GeracaoW { get; set; }
    public double ConsumoW { get; set; }
    public double BateriaWh { get; set; }
    public double Ppo2Kpa { get; set; }
    public double Ppco2Kpa { get; set; }
    public double PressaoKpa { get; set; }
    public double Umidade { get; set; }
    public double TemperaturaC { get; set; }
    public double TempExternaC { get; set; }
    public double AguaPotavelKg { get; set; }
    public double AguaResidualKg { get; set; }
    public double AguaEmTratamentoKg { get; set; }
    public double O2GeradoKg { get; set; }
    public double Co2RemovidoKg { get; set; }
    public double AguaRecuperadaKg { get; set; }
    public double ArVazadoKg { get; set; }
    /// <summary>Equipamentos ligados que ficaram sem energia nesta hora.</summary>
    public List<TipoEquip> SemEnergia { get; set; } = new();
    /// <summary>Tripulantes que ficaram sem água para beber ou sem oxigênio nesta hora.</summary>
    public List<int> SemAgua { get; set; } = new();
    public List<int> SemO2 { get; set; } = new();
    public List<string> Avisos { get; set; } = new();
}

/// <summary>
/// O MOTOR DE SIMULAÇÃO. Um passo = uma hora marciana (1/24 de sol). É uma função determinística do estado e da
/// entrada: não sorteia nada e não "ajusta" recurso. Toda massa que muda de lugar sai de um reservatório e entra
/// em outro (ou atravessa a fronteira e fica anotada no livro); a energia gerada é igual à usada, perdida ou guardada.
/// Ordem do passo: energia (geração, pedidos, despacho por prioridade, bateria) → tripulação → vazamento →
/// remoção de CO2 → geração de O2 → válvulas dos tanques → umidade → temperatura → água → forno → veículo.
/// </summary>
public static class Motor
{
    public static RegistroHora PassoHora(EstadoBase e, ParametrosCenario p, EntradaHora en)
    {
        double dtS = p.PassoS, dtH = p.PassoH, dtDia = p.PassoDia;
        double v = p.VolumeCabineM3;
        var reg = new RegistroHora { Sol = en.Sol, Hora = en.Hora, Tau = en.Tau };
        var livro = e.Livro;
        double horaMeio = en.Hora + 0.5;
        double tExt = SolMarte.TemperaturaExternaC(p, horaMeio);
        reg.TempExternaC = tExt;
        int dentro = en.Tripulantes.Count(t => !t.Fora);

        // ───────── 1. ENERGIA: geração solar ─────────
        double g = SolMarte.IrradianciaSuperficieWM2(p, en.Sol, horaMeio, en.Tau);
        double pSolar = g * p.AreaPaineisM2 * p.EficienciaPainel * (1.0 - e.SujeiraPaineis);
        double eSolar = pSolar * dtH;

        // ───────── 2. PEDIDOS de potência de cada equipamento ─────────
        double ppO2 = Fisica.PressaoParcialKpa(e.O2Kg, Fisica.MO2, e.TemperaturaC, v);
        double o2Plano = 0, regolitoPlano = 0;
        foreach (var eq in e.Equipamentos) { eq.PotenciaPedidaW = 0; eq.PotenciaAtendidaW = 0; }
        foreach (var eq in e.Equipamentos.Where(x => x.UsaEnergia && x.Ligado && !x.Quebrado))
        {
            switch (eq.Tipo)
            {
                case TipoEquip.RemovedorCo2: eq.PotenciaPedidaW = p.Co2RemocaoPotenciaW; break;
                case TipoEquip.GeradorO2:
                {
                    // faz o que falta para o alvo, até o máximo, e nunca com a água reservada para beber
                    double alvoKg = Fisica.MassaParaPressaoKg(p.Ppo2AlvoKpa, Fisica.MO2, e.TemperaturaC, v);
                    double falta = Math.Max(0, alvoKg - e.O2Kg);
                    double max = p.O2GeracaoMaxKgDia * dtDia * eq.Saude;
                    double porAgua = Math.Max(0, e.AguaPotavelKg - p.ReservaBebidaKg) / Fisica.AguaPorKgO2;
                    o2Plano = Math.Min(falta, Math.Min(max, porAgua));
                    eq.PotenciaPedidaW = o2Plano * p.EletroliseKwhPorKgO2 * 1000.0 / dtH;
                    break;
                }
                case TipoEquip.Desumidificador: eq.PotenciaPedidaW = p.DesumidificadorPotenciaW; break;
                case TipoEquip.Aquecimento:
                {
                    // o que a base perde para fora, menos o calor que já existe dentro, mais uma recuperação em 3 horas
                    double pedido = p.UaCabineWK * (p.TemperaturaAlvoC - tExt) - e.CalorInternoW
                                    + p.CapacidadeTermicaJK * (p.TemperaturaAlvoC - e.TemperaturaC) / (3.0 * dtS);
                    eq.PotenciaPedidaW = Math.Clamp(pedido, 0, p.AquecimentoPotenciaMaxW) * eq.Saude;
                    break;
                }
                case TipoEquip.ProcessadorAgua:
                    if (e.AguaResidualKg > 0.01 || e.EmTratamento.Count > 0) eq.PotenciaPedidaW = p.AguaProcessadorPotenciaW;
                    break;
                case TipoEquip.Comunicacao: eq.PotenciaPedidaW = p.ComunicacaoPotenciaW; break;
                case TipoEquip.Laboratorio: if (en.LaboratorioEmUso) eq.PotenciaPedidaW = p.LaboratorioPotenciaW; break;
                case TipoEquip.FornoRegolito:
                    regolitoPlano = Math.Min(e.RegolitoKg, p.FornoRegolitoCapacidadeKgH * dtH * eq.Saude);
                    eq.PotenciaPedidaW = regolitoPlano * p.RegolitoFracaoAgua * p.FornoKwhPorKgAgua * 1000.0 / dtH;
                    break;
                case TipoEquip.CarregadorRover:
                    if (e.RoverNaBase && e.RoverBateriaWh < p.RoverBateriaWh - 1e-6)
                        eq.PotenciaPedidaW = Math.Min(p.RoverCarregadorPotenciaW, (p.RoverBateriaWh - e.RoverBateriaWh) / dtH);
                    break;
                case TipoEquip.Iluminacao: if (en.Vigilia) eq.PotenciaPedidaW = p.IluminacaoPotenciaW; break;
            }
        }

        // ───────── 3. DESPACHO por prioridade: quem não cabe fica sem energia nesta hora ─────────
        double batEntrega = Math.Min(e.BateriaWh * p.BateriaEficienciaDescarga, p.BateriaPotenciaMaxW * dtH);
        double disponivel = (eSolar + batEntrega) * (1.0 - p.PerdaDistribuicao);
        double eCargas = 0;
        foreach (var eq in e.Equipamentos.Where(x => x.PotenciaPedidaW > 0).OrderBy(x => x.Prioridade).ThenBy(x => (int)x.Tipo))
        {
            double pedidoWh = eq.PotenciaPedidaW * dtH;
            bool variavel = eq.Tipo is TipoEquip.GeradorO2 or TipoEquip.Aquecimento or TipoEquip.FornoRegolito or TipoEquip.CarregadorRover;
            double atendido = variavel ? Math.Min(pedidoWh, disponivel) : (pedidoWh <= disponivel + 1e-9 ? pedidoWh : 0);
            atendido = Math.Max(0, atendido);
            disponivel -= atendido;
            eCargas += atendido;
            eq.PotenciaAtendidaW = atendido / dtH;
            if (atendido < pedidoWh * 0.999) reg.SemEnergia.Add(eq.Tipo);
        }
        // balanço do barramento: geração + bateria = cargas + perdas (+ excedente que não coube na bateria)
        double eBarra = eCargas / (1.0 - p.PerdaDistribuicao);
        double perdas = eBarra - eCargas;
        double desperdicado = 0;
        if (eSolar >= eBarra)
        {
            double excedente = eSolar - eBarra;
            double carga = Math.Min(excedente, Math.Min(p.BateriaPotenciaMaxW * dtH, (p.BateriaCapacidadeWh - e.BateriaWh) / p.BateriaEficienciaCarga));
            carga = Math.Max(0, carga);
            e.BateriaWh += carga * p.BateriaEficienciaCarga;
            perdas += carga * (1.0 - p.BateriaEficienciaCarga);
            desperdicado = excedente - carga;
        }
        else
        {
            double falta = eBarra - eSolar;
            double retirado = Math.Min(e.BateriaWh, falta / p.BateriaEficienciaDescarga);
            e.BateriaWh -= retirado;
            perdas += retirado - falta;
        }
        livro.EnergiaGeradaWh += eSolar;
        livro.EnergiaCargasWh += eCargas;
        livro.EnergiaPerdasWh += perdas;
        livro.EnergiaDesperdicadaWh += desperdicado;
        reg.GeracaoW = pSolar;
        reg.ConsumoW = eCargas / dtH;
        bool atendeu(TipoEquip t) => e.Eq(t).PotenciaAtendidaW > 0;
        double fracao(TipoEquip t) { var q = e.Eq(t); return q.PotenciaPedidaW > 0 ? q.PotenciaAtendidaW / q.PotenciaPedidaW : 0; }

        // ───────── 4. TRIPULAÇÃO: respiração, água, comida (o corpo é passagem: o que entra, sai) ─────────
        double somaSaidas = p.UrinaKgCmDia + p.VaporRespiracaoTranspiracaoKgCmDia + p.AguaFecalKgCmDia;
        double fUrina = p.UrinaKgCmDia / somaSaidas, fVapor = p.VaporRespiracaoTranspiracaoKgCmDia / somaSaidas, fFecal = p.AguaFecalKgCmDia / somaSaidas;
        double calorCorpos = 0;
        foreach (var t in en.Tripulantes)
        {
            double o2 = p.O2ConsumoKgCmDia * t.Fator * dtDia;
            double s = 1.0;     // parte do metabolismo que o oxigênio disponível permite
            if (t.Fora)
            {
                double vaz = p.TrajeVazamentoKgH * dtH;
                double tirado = Math.Min(o2 + vaz, e.TanqueO2Kg);
                e.TanqueO2Kg -= tirado;
                double perdido = Math.Min(vaz, tirado);
                livro.Saiu("vazamento do traje", perdido);
                double respirado = tirado - perdido;
                s = o2 > 0 ? respirado / o2 : 1.0;
            }
            else
            {
                double respirado = Math.Min(o2, e.O2Kg);
                e.O2Kg -= respirado;
                s = o2 > 0 ? respirado / o2 : 1.0;
                calorCorpos += p.CalorMetabolicoW * t.Fator;
            }
            if (s < 0.999) reg.SemO2.Add(t.Id);

            double co2 = p.Co2ProducaoKgCmDia * t.Fator * dtDia * s;
            double aguaMet = p.AguaMetabolicaKgCmDia * t.Fator * dtDia * s;
            double residuo = p.ResiduoSolidoSecoKgCmDia * dtDia * s;
            // fechamento de massa do metabolismo: comida seca + O2 = CO2 + água metabólica + resíduo seco
            double comida = co2 + aguaMet + residuo - o2 * s;
            double daDespensa = Math.Min(comida, e.AlimentoSecoKg);
            e.AlimentoSecoKg -= daDespensa;
            livro.Entrou("reserva do corpo (sem comida)", comida - daDespensa);

            double beber = (p.AguaBebidaKgCmDia + p.AguaPreparoAlimentoKgCmDia) * dtDia;
            double bebido = Math.Min(beber, e.AguaPotavelKg);
            e.AguaPotavelKg -= bebido;
            if (bebido < beber * 0.999) reg.SemAgua.Add(t.Id);
            double umidadeComida = Math.Min(p.AguaNoAlimentoKgCmDia * dtDia, e.AlimentoUmidadeKg);
            e.AlimentoUmidadeKg -= umidadeComida;

            double aguaCorpo = bebido + umidadeComida + aguaMet;
            e.AguaResidualKg += aguaCorpo * fUrina;
            e.ResiduosSolidosKg += aguaCorpo * fFecal + residuo;
            if (t.Fora)
            {
                livro.Saiu("CO2 solto pelo traje", co2);
                livro.Saiu("vapor solto pelo traje", aguaCorpo * fVapor);
            }
            else
            {
                e.Co2Kg += co2;
                e.VaporKg += aguaCorpo * fVapor;
                // higiene e descarga: água potável que vira água residual
                double higiene = p.AguaHigieneKgCmDia * (en.RacionamentoHigiene ? 0.4 : 1.0) * dtDia + p.DescargaUrinaKgCmDia * dtDia;
                double usado = Math.Min(higiene, e.AguaPotavelKg);
                e.AguaPotavelKg -= usado;
                e.AguaResidualKg += usado;
            }
        }

        // ───────── 5. VAZAMENTO: o ar sai na mesma proporção de todos os gases ─────────
        double taxaVaz = p.VazamentoNominalFracaoDia + VazamentoPorVedacao(e.Eq(TipoEquip.Vedacao), p);
        double fVaz = 1.0 - Math.Exp(-taxaVaz * dtDia);
        double vazado = (e.O2Kg + e.Co2Kg + e.N2Kg + e.VaporKg) * fVaz;
        e.O2Kg *= 1 - fVaz; e.Co2Kg *= 1 - fVaz; e.N2Kg *= 1 - fVaz; e.VaporKg *= 1 - fVaz;
        livro.Saiu("ar vazado para Marte", vazado);
        reg.ArVazadoKg = vazado;

        // ───────── 6. REMOÇÃO DE CO2: tira mais quando há mais CO2 no ar (até o dobro do nominal) ─────────
        if (atendeu(TipoEquip.RemovedorCo2))
        {
            double pp = Fisica.PressaoParcialKpa(e.Co2Kg, Fisica.MCO2, e.TemperaturaC, v);
            double taxa = p.Co2RemocaoCapacidadeKgDia * e.Eq(TipoEquip.RemovedorCo2).Saude * Math.Min(pp / p.Co2RemocaoPpReferenciaKpa, 2.0);
            double m = Math.Min(e.Co2Kg, taxa * dtDia);
            e.Co2Kg -= m;
            livro.Saiu("CO2 removido e solto para fora", m);
            reg.Co2RemovidoKg = m;
        }

        // ───────── 7. GERAÇÃO DE O2 por eletrólise: 2 H2O → 2 H2 + O2 (o hidrogênio é solto) ─────────
        if (o2Plano > 0 && atendeu(TipoEquip.GeradorO2))
        {
            double m = o2Plano * fracao(TipoEquip.GeradorO2);
            double agua = Math.Min(m * Fisica.AguaPorKgO2, Math.Max(0, e.AguaPotavelKg - p.ReservaBebidaKg));
            m = agua / Fisica.AguaPorKgO2;
            e.AguaPotavelKg -= agua;
            e.O2Kg += m;
            livro.Saiu("hidrogênio da eletrólise", agua - m);
            reg.O2GeradoKg = m;
        }

        // ───────── 8. VÁLVULAS: O2 de reserva se o ar ficar pobre; nitrogênio para repor a pressão ─────────
        ppO2 = Fisica.PressaoParcialKpa(e.O2Kg, Fisica.MO2, e.TemperaturaC, v);
        if (ppO2 < p.Ppo2AlertaKpa && e.TanqueO2Kg > 0 && dentro > 0)
        {
            double m = Math.Min(Fisica.MassaParaPressaoKg(p.Ppo2AlertaKpa, Fisica.MO2, e.TemperaturaC, v) - e.O2Kg, e.TanqueO2Kg);
            m = Math.Max(0, m);
            e.TanqueO2Kg -= m; e.O2Kg += m;
            if (m > 0) reg.Avisos.Add("valvula_o2");
        }
        double pTotal = PressaoTotalKpa(e, p);
        if (pTotal < p.PressaoCabineKpa - 0.5 && e.TanqueN2Kg > 0)
        {
            double m = Math.Min(Fisica.MassaParaPressaoKg(p.PressaoCabineKpa - pTotal, Fisica.MN2, e.TemperaturaC, v), e.TanqueN2Kg);
            e.TanqueN2Kg -= m; e.N2Kg += m;
            if (m > 0.05) reg.Avisos.Add("reposicao_n2");
        }

        // ───────── 9. UMIDADE: o desumidificador recolhe o vapor; acima de 100% a água condensa ─────────
        double psat = Fisica.PressaoSaturacaoKpa(e.TemperaturaC);
        if (atendeu(TipoEquip.Desumidificador))
        {
            double alvo = Fisica.MassaParaPressaoKg(p.UmidadeAlvo * psat, Fisica.MH2O, e.TemperaturaC, v);
            double m = Math.Clamp(e.VaporKg - alvo, 0, p.DesumidificadorCapacidadeKgH * dtH * e.Eq(TipoEquip.Desumidificador).Saude);
            e.VaporKg -= m; e.AguaResidualKg += m;
        }
        double vaporMax = Fisica.MassaParaPressaoKg(psat, Fisica.MH2O, e.TemperaturaC, v);
        if (e.VaporKg > vaporMax)
        {
            double m = e.VaporKg - vaporMax;
            e.VaporKg = vaporMax; e.AguaResidualKg += m;
            reg.Avisos.Add("condensacao");
        }

        // ───────── 10. TEMPERATURA: balanço de calor da base ─────────
        double pAquec = e.Eq(TipoEquip.Aquecimento).PotenciaAtendidaW;
        double internos = e.Equipamentos.Where(x => x.Tipo is TipoEquip.RemovedorCo2 or TipoEquip.Desumidificador or TipoEquip.ProcessadorAgua
            or TipoEquip.Comunicacao or TipoEquip.Laboratorio or TipoEquip.Iluminacao).Sum(x => x.PotenciaAtendidaW);
        e.CalorInternoW = calorCorpos + internos * p.FracaoCalorEquipamentos;
        e.TemperaturaC += (pAquec + e.CalorInternoW - p.UaCabineWK * (e.TemperaturaC - tExt)) * dtS / p.CapacidadeTermicaJK;
        // o ar esfriou: se passou da saturação na temperatura nova, o excesso condensa (a umidade nunca passa de 100%)
        double vaporMaxFrio = Fisica.MassaParaPressaoKg(Fisica.PressaoSaturacaoKpa(e.TemperaturaC), Fisica.MH2O, e.TemperaturaC, v);
        if (e.VaporKg > vaporMaxFrio)
        {
            e.AguaResidualKg += e.VaporKg - vaporMaxFrio;
            e.VaporKg = vaporMaxFrio;
            if (!reg.Avisos.Contains("condensacao")) reg.Avisos.Add("condensacao");
        }

        // ───────── 11. ÁGUA: o tratamento leva tempo, tem capacidade e perde uma parte (salmoura) ─────────
        if (atendeu(TipoEquip.ProcessadorAgua))
        {
            var proc = e.Eq(TipoEquip.ProcessadorAgua);
            foreach (var l in e.EmTratamento) l.HorasRestantes -= dtH;
            double entra = Math.Min(e.AguaResidualKg, p.AguaProcessadorCapacidadeKgH * dtH * proc.Saude);
            if (entra > 1e-9)
            {
                e.AguaResidualKg -= entra;
                e.EmTratamento.Add(new LoteAgua { Kg = entra, HorasRestantes = p.AguaTempoTratamentoH });
            }
            double rec = p.AguaRecuperacaoFracao * (0.6 + 0.4 * proc.Saude);
            foreach (var l in e.EmTratamento.Where(x => x.HorasRestantes <= 0).ToList())
            {
                double potavel = l.Kg * rec;
                if (e.AguaPotavelKg + potavel > p.TanquePotavelCapacidadeKg) { reg.Avisos.Add("tanque_cheio"); break; }
                e.AguaPotavelKg += potavel;
                e.SalmouraKg += l.Kg - potavel;
                reg.AguaRecuperadaKg += potavel;
                e.EmTratamento.Remove(l);
            }
        }

        // ───────── 12. FORNO: aquece o solo e solta a água presa nos minerais (ainda precisa ser tratada) ─────────
        if (regolitoPlano > 0 && atendeu(TipoEquip.FornoRegolito))
        {
            double m = regolitoPlano * fracao(TipoEquip.FornoRegolito);
            double agua = m * p.RegolitoFracaoAgua;
            e.RegolitoKg -= m;
            e.AguaResidualKg += agua;
            e.AguaExtraidaDeMarteKg += agua;
            livro.Saiu("solo seco devolvido a Marte", m - agua);
        }

        // ───────── 13. VEÍCULO: a energia sai da base e entra na bateria dele ─────────
        if (atendeu(TipoEquip.CarregadorRover))
            e.RoverBateriaWh = Math.Min(p.RoverBateriaWh, e.RoverBateriaWh + e.Eq(TipoEquip.CarregadorRover).PotenciaAtendidaW * dtH);

        // ───────── retrato da hora ─────────
        reg.BateriaWh = e.BateriaWh;
        reg.Ppo2Kpa = Fisica.PressaoParcialKpa(e.O2Kg, Fisica.MO2, e.TemperaturaC, v);
        reg.Ppco2Kpa = Fisica.PressaoParcialKpa(e.Co2Kg, Fisica.MCO2, e.TemperaturaC, v);
        reg.PressaoKpa = PressaoTotalKpa(e, p);
        reg.Umidade = Fisica.PressaoParcialKpa(e.VaporKg, Fisica.MH2O, e.TemperaturaC, v) / Fisica.PressaoSaturacaoKpa(e.TemperaturaC);
        reg.TemperaturaC = e.TemperaturaC;
        reg.AguaPotavelKg = e.AguaPotavelKg;
        reg.AguaResidualKg = e.AguaResidualKg;
        reg.AguaEmTratamentoKg = e.AguaEmTratamentoKg;
        return reg;
    }

    /// <summary>Vedação danificada: quanto pior, mais ar vaza (fração do ar por dia).</summary>
    public static double VazamentoPorVedacao(Equipamento vedacao, ParametrosCenario p) => (1.0 - vedacao.Saude) * p.VedacaoVazamentoMaxFracaoDia;

    public static double PressaoTotalKpa(EstadoBase e, ParametrosCenario p) =>
        Fisica.PressaoParcialKpa(e.O2Kg, Fisica.MO2, e.TemperaturaC, p.VolumeCabineM3)
        + Fisica.PressaoParcialKpa(e.Co2Kg, Fisica.MCO2, e.TemperaturaC, p.VolumeCabineM3)
        + Fisica.PressaoParcialKpa(e.N2Kg, Fisica.MN2, e.TemperaturaC, p.VolumeCabineM3)
        + Fisica.PressaoParcialKpa(e.VaporKg, Fisica.MH2O, e.TemperaturaC, p.VolumeCabineM3);

    /// <summary>Solo de Marte trazido para a base: entra pela fronteira e fica anotado.</summary>
    public static void ReceberRegolito(EstadoBase e, double kg)
    {
        e.RegolitoKg += kg;
        e.Livro.Entrou("solo de Marte coletado", kg);
    }

    /// <summary>Água usada no laboratório: sai do tanque potável e vira água residual (nada se perde).</summary>
    public static bool UsarAguaNoLaboratorio(EstadoBase e, double kg)
    {
        if (e.AguaPotavelKg < kg) return false;
        e.AguaPotavelKg -= kg; e.AguaResidualKg += kg;
        return true;
    }
}
