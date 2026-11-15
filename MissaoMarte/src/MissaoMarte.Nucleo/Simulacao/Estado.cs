namespace MissaoMarte.Nucleo.Simulacao;

public enum TipoEquip
{
    RemovedorCo2, GeradorO2, Desumidificador, Aquecimento, ProcessadorAgua,
    Comunicacao, Laboratorio, FornoRegolito, CarregadorRover, Iluminacao,
    /// <summary>A vedação da base: não gasta energia; danificada, deixa o ar vazar.</summary>
    Vedacao,
}

public sealed class Equipamento
{
    public TipoEquip Tipo { get; set; }
    public string Nome { get; set; } = "";
    public string Explica { get; set; } = "";
    /// <summary>Chave ligada pelos jogadores.</summary>
    public bool Ligado { get; set; } = true;
    /// <summary>1 = essencial (o último a ser desligado quando falta energia) … 4 = opcional.</summary>
    public int Prioridade { get; set; } = 2;
    /// <summary>Condição de 0 a 1: abaixo de 1 trabalha menos; 0 = quebrado.</summary>
    public double Saude { get; set; } = 1.0;
    /// <summary>Desgaste de 0 a 1 (regra de jogo): sem inspeção cresce a cada sol.</summary>
    public double Desgaste { get; set; }
    public double PotenciaPedidaW { get; set; }
    public double PotenciaAtendidaW { get; set; }
    public bool UsaEnergia => Tipo != TipoEquip.Vedacao;
    public bool Quebrado => Saude <= 1e-6;
}

/// <summary>Um lote de água dentro do tratamento: só vira potável quando o tempo acaba.</summary>
public sealed class LoteAgua
{
    public double Kg { get; set; }
    public double HorasRestantes { get; set; }
}

/// <summary>
/// O ESTADO FÍSICO DA BASE. Massas em kg, energia em Wh, temperatura em °C. Tudo o que o motor muda está aqui
/// ou no livro de fronteira (o que entrou e saiu do sistema), para a conservação de massa poder ser conferida.
/// </summary>
public sealed class EstadoBase
{
    // Ar da cabine
    public double O2Kg { get; set; }
    public double Co2Kg { get; set; }
    public double N2Kg { get; set; }
    public double VaporKg { get; set; }
    public double TemperaturaC { get; set; }
    public double TanqueO2Kg { get; set; }
    public double TanqueN2Kg { get; set; }

    // Água
    public double AguaPotavelKg { get; set; }
    public double AguaResidualKg { get; set; }
    public List<LoteAgua> EmTratamento { get; set; } = new();
    public double SalmouraKg { get; set; }

    // Comida e resíduos
    public double AlimentoSecoKg { get; set; }
    public double AlimentoUmidadeKg { get; set; }
    public double ResiduosSolidosKg { get; set; }

    // Solo de Marte esperando o forno
    public double RegolitoKg { get; set; }
    public double AguaExtraidaDeMarteKg { get; set; }

    // Energia
    public double BateriaWh { get; set; }
    public double SujeiraPaineis { get; set; }
    public double RoverBateriaWh { get; set; }
    public bool RoverNaBase { get; set; } = true;
    public double CalorInternoW { get; set; }

    public List<Equipamento> Equipamentos { get; set; } = new();
    public LivroFronteira Livro { get; set; } = new();

    public Equipamento Eq(TipoEquip t) => Equipamentos.First(e => e.Tipo == t);
    public double AguaEmTratamentoKg => EmTratamento.Sum(l => l.Kg);

    /// <summary>Toda a massa guardada dentro da base (kg). Com o livro de fronteira, fecha a conservação de massa.</summary>
    public double MassaTotalKg() =>
        O2Kg + Co2Kg + N2Kg + VaporKg + TanqueO2Kg + TanqueN2Kg
        + AguaPotavelKg + AguaResidualKg + AguaEmTratamentoKg + SalmouraKg
        + AlimentoSecoKg + AlimentoUmidadeKg + ResiduosSolidosKg + RegolitoKg;

    public static EstadoBase Inicial(ParametrosCenario p)
    {
        double t = p.TemperaturaAlvoC, v = p.VolumeCabineM3;
        double psat = Fisica.PressaoSaturacaoKpa(t);
        double pVapor = p.UmidadeInicial * psat;
        double pO2 = p.PressaoCabineKpa * p.FracaoO2Cabine;
        double pN2 = p.PressaoCabineKpa - pO2 - p.Ppco2InicialKpa - pVapor;
        var e = new EstadoBase
        {
            O2Kg = Fisica.MassaParaPressaoKg(pO2, Fisica.MO2, t, v),
            Co2Kg = Fisica.MassaParaPressaoKg(p.Ppco2InicialKpa, Fisica.MCO2, t, v),
            N2Kg = Fisica.MassaParaPressaoKg(pN2, Fisica.MN2, t, v),
            VaporKg = Fisica.MassaParaPressaoKg(pVapor, Fisica.MH2O, t, v),
            TemperaturaC = t,
            TanqueO2Kg = p.TanqueO2InicialKg,
            TanqueN2Kg = p.TanqueN2InicialKg,
            AguaPotavelKg = p.AguaPotavelInicialKg,
            AguaResidualKg = p.AguaResidualInicialKg,
            AlimentoSecoKg = p.AlimentoSecoInicialKg,
            BateriaWh = p.BateriaCapacidadeWh * p.BateriaInicialFracao,
            RoverBateriaWh = p.RoverBateriaWh,
        };
        // a comida embalada traz a própria umidade, na proporção do consumo
        double secoPorDia = p.Co2ProducaoKgCmDia + p.AguaMetabolicaKgCmDia - p.O2ConsumoKgCmDia + p.ResiduoSolidoSecoKgCmDia;
        e.AlimentoUmidadeKg = p.AlimentoSecoInicialKg * p.AguaNoAlimentoKgCmDia / secoPorDia;
        e.Equipamentos = new List<Equipamento>
        {
            new() { Tipo = TipoEquip.RemovedorCo2, Nome = "Removedor de CO2", Prioridade = 1, Explica = "Tira do ar o gás carbônico que a gente solta ao respirar." },
            new() { Tipo = TipoEquip.GeradorO2, Nome = "Gerador de oxigênio", Prioridade = 1, Explica = "Separa a água em oxigênio e hidrogênio usando eletricidade." },
            new() { Tipo = TipoEquip.Desumidificador, Nome = "Circulação e desumidificador", Prioridade = 2, Explica = "Mexe o ar e recolhe a água do suor e da respiração." },
            new() { Tipo = TipoEquip.Aquecimento, Nome = "Aquecimento", Prioridade = 2, Explica = "Mantém a base quente. Lá fora faz muito frio." },
            new() { Tipo = TipoEquip.ProcessadorAgua, Nome = "Processador de água", Prioridade = 2, Explica = "Limpa a água usada para ela voltar a ser potável." },
            new() { Tipo = TipoEquip.Comunicacao, Nome = "Comunicação com a Terra", Prioridade = 3, Explica = "Traz a previsão do tempo de Marte e as mensagens da Terra." },
            new() { Tipo = TipoEquip.Laboratorio, Nome = "Laboratório", Prioridade = 3, Explica = "Onde as amostras são analisadas." },
            new() { Tipo = TipoEquip.FornoRegolito, Nome = "Forno de extração de água", Prioridade = 3, Explica = "Aquece o solo de Marte para soltar a água presa nos minerais." },
            new() { Tipo = TipoEquip.CarregadorRover, Nome = "Carregador do veículo", Prioridade = 3, Explica = "Carrega a bateria do veículo de exploração." },
            new() { Tipo = TipoEquip.Iluminacao, Nome = "Iluminação e conforto", Prioridade = 4, Explica = "Luzes, tela e conforto da tripulação." },
            new() { Tipo = TipoEquip.Vedacao, Nome = "Vedação da base", Prioridade = 0, Explica = "Segura o ar dentro da base. Se estragar, o ar vaza." },
        };
        return e;
    }
}

/// <summary>
/// O LIVRO DE FRONTEIRA: tudo o que atravessou a fronteira da base, por motivo. A regra do projeto é que nenhum
/// recurso aparece ou some: massa guardada + saídas − entradas é constante, e a energia gerada é igual à
/// usada + perdida + guardada. Os testes conferem isso a cada hora.
/// </summary>
public sealed class LivroFronteira
{
    public Dictionary<string, double> SaidasKg { get; set; } = new();
    public Dictionary<string, double> EntradasKg { get; set; } = new();
    public double EnergiaGeradaWh { get; set; }
    public double EnergiaCargasWh { get; set; }
    public double EnergiaPerdasWh { get; set; }
    public double EnergiaDesperdicadaWh { get; set; }

    public void Saiu(string motivo, double kg) { if (kg != 0) SaidasKg[motivo] = SaidasKg.GetValueOrDefault(motivo) + kg; }
    public void Entrou(string motivo, double kg) { if (kg != 0) EntradasKg[motivo] = EntradasKg.GetValueOrDefault(motivo) + kg; }
    public double TotalSaidasKg => SaidasKg.Values.Sum();
    public double TotalEntradasKg => EntradasKg.Values.Sum();
}
