using MissaoMarte.Nucleo.Simulacao;

namespace MissaoMarte.Nucleo.Jogo;

/// <summary>Um hexágono em coordenadas axiais (q, r). Cada hexágono do tabuleiro de Marte vale 1 km.</summary>
public readonly record struct Hex(int Q, int R)
{
    public int S => -Q - R;
    public static readonly Hex Origem = new(0, 0);
    public static readonly Hex[] Direcoes = { new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1) };
    public int Distancia(Hex o) => (Math.Abs(Q - o.Q) + Math.Abs(R - o.R) + Math.Abs(S - o.S)) / 2;
    public IEnumerable<Hex> Vizinhos() { var h = this; return Direcoes.Select(d => new Hex(h.Q + d.Q, h.R + d.R)); }
    public override string ToString() => $"{Q},{R}";
}

public enum Terreno { Base, Planicie, Rochoso, Dunas, Cratera, Delta, Escarpa }

public sealed class Celula
{
    public Hex Pos { get; set; }
    public Terreno Terreno { get; set; }
    public bool Explorado { get; set; }
    public string? Nome { get; set; }
    public int Amostras { get; set; }
    /// <summary>Solo argiloso com água presa nos minerais, ainda no chão (kg).</summary>
    public double ArgilaKg { get; set; }
    public string? DescobertaId { get; set; }
    public bool DescobertaVista { get; set; }
}

public enum Profissao { Comandante, Engenheiro, Cientista, Geologo, Medico, Piloto }

public static class Profissoes
{
    public static string Nome(this Profissao p) => p switch
    {
        Profissao.Comandante => "Comandante", Profissao.Engenheiro => "Engenheiro(a)", Profissao.Cientista => "Cientista",
        Profissao.Geologo => "Geólogo(a)", Profissao.Medico => "Médico(a)", _ => "Piloto",
    };
    public static string Papel(this Profissao p) => p switch
    {
        Profissao.Comandante => "Coordena a equipe e cuida do ânimo de todos.",
        Profissao.Engenheiro => "Cuida das máquinas: inspeciona, conserta e limpa os painéis.",
        Profissao.Cientista => "Analisa as amostras no laboratório e acompanha o tempo de Marte.",
        Profissao.Geologo => "Explora o terreno, enxerga mais longe e coleta as amostras.",
        Profissao.Medico => "Cuida da saúde da tripulação.",
        _ => "Dirige o veículo de exploração e leva a equipe e a carga.",
    };
    public static string MissaoIndividual(this Profissao p) => p switch
    {
        Profissao.Comandante => "Terminar a missão com o ânimo da equipe acima de 60.",
        Profissao.Engenheiro => "Nenhum equipamento quebrado no fim da missão.",
        Profissao.Cientista => "Analisar pelo menos 3 amostras.",
        Profissao.Geologo => "Explorar mais da metade do mapa.",
        Profissao.Medico => "Ninguém com a saúde abaixo de 50 no fim da missão.",
        _ => "Trazer para a base 200 kg de solo argiloso.",
    };
    public static string Rotina(this Profissao p) => p switch
    {
        Profissao.Comandante => "Reunião da equipe",
        Profissao.Engenheiro => "Inspeção do suporte à vida",
        Profissao.Cientista => "Previsão do tempo de Marte",
        Profissao.Geologo => "Estudar imagens de órbita",
        Profissao.Medico => "Check-up da tripulação",
        _ => "Revisão do veículo",
    };
    public static string Cor(this Profissao p) => p switch
    {
        Profissao.Comandante => "#f2c14e", Profissao.Engenheiro => "#f78154", Profissao.Cientista => "#4d9de0",
        Profissao.Geologo => "#b4654a", Profissao.Medico => "#5fad56", _ => "#9b72cf",
    };
}

/// <summary>Um intervalo de horas do sol em que o tripulante esteve fora da base (de traje).</summary>
public sealed class Intervalo
{
    public double Inicio { get; set; }
    public double Fim { get; set; }
    public bool Cobre(int hora) => Inicio < hora + 1 && Fim > hora;
}

public sealed class Tripulante
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public Profissao Profissao { get; set; }
    public double Saude { get; set; } = 100;
    public Hex Pos { get; set; } = Hex.Origem;
    public bool NoVeiculo { get; set; }
    public double HorasUsadas { get; set; }
    public bool RotinaFeita { get; set; }
    public int AmostrasNaMao { get; set; }
    public double DoseMsv { get; set; }
    public List<Intervalo> Fora { get; set; } = new();
    public List<string> Diario { get; set; } = new();
    public bool Incapacitado => Saude <= 0;
    public bool NaBase => Pos == Hex.Origem;
    public string Inicial => Nome.Length > 0 ? Nome[..1].ToUpperInvariant() : "?";
}

public sealed class Veiculo
{
    public Hex Pos { get; set; } = Hex.Origem;
    public double CargaArgilaKg { get; set; }
    public int Amostras { get; set; }
    public bool Revisado { get; set; }
    public const double CargaMaxKg = 300;
}

/// <summary>Um acontecimento do roteiro da missão. Não muda a física: muda um dado de entrada ou o estado de um equipamento.</summary>
public sealed class EventoRoteiro
{
    public int Sol { get; set; }
    public int Hora { get; set; }
    public TipoEquip? Equip { get; set; }
    public double NovaSaude { get; set; }
    public string Titulo { get; set; } = "";
    public string Texto { get; set; } = "";
    public bool Aconteceu { get; set; }
}

/// <summary>Um conserto ou serviço marcado para terminar numa hora do sol (o efeito físico entra nessa hora).</summary>
public sealed class ServicoAgendado
{
    public string Tipo { get; set; } = "";      // "reparo" | "limpeza" | "analise" | "amostras" | "descarga"
    public TipoEquip? Equip { get; set; }
    public int TripulanteId { get; set; }
    public double Inicio { get; set; }
    public double Fim { get; set; }
    /// <summary>Horas do serviço em que faltou energia (uma análise sem energia não termina).</summary>
    public int HorasSemEnergia { get; set; }
    /// <summary>Quanto chega (kg de solo ou número de amostras) nos serviços de entrega.</summary>
    public double Quantidade { get; set; }
}

public enum Gravidade { Bom, Atencao, Perigo }

public sealed class ItemRelatorio
{
    public Gravidade Gravidade { get; set; }
    public string Sistema { get; set; } = "";        // "ar" | "agua" | "energia" | "equipe" | "ciencia"
    public string Titulo { get; set; } = "";
    /// <summary>Frase curta, para 7 a 8 anos.</summary>
    public string Simples { get; set; } = "";
    /// <summary>Causa e consequência, para 9 a 10 anos.</summary>
    public string Porque { get; set; } = "";
    /// <summary>Os números, para 11 a 12 anos.</summary>
    public string Numeros { get; set; } = "";
}

public sealed class RelatorioSol
{
    public int Sol { get; set; }
    public List<ItemRelatorio> Itens { get; set; } = new();
    public List<RegistroHora> Horas { get; set; } = new();
    public string? FatoId { get; set; }
    public double GeradoKwh { get; set; }
    public double ConsumidoKwh { get; set; }
    public double GeracaoCeuLimpoKwh { get; set; }
    public double O2GeradoKg { get; set; }
    public double Co2RemovidoKg { get; set; }
    public double AguaRecuperadaKg { get; set; }
    public double ArVazadoKg { get; set; }
    public double AguaPotavelInicioKg { get; set; }
    public double AguaPotavelFimKg { get; set; }
    public double Tau { get; set; }
    public double BateriaMinWh { get; set; }
    public double BateriaFimWh { get; set; }
    public double Moral { get; set; }
    public double AguaDeMarteKg { get; set; }
    public int AmostrasAnalisadas { get; set; }
}

public sealed class Objetivo
{
    public string Id { get; set; } = "";
    public string Texto { get; set; } = "";
    public double Atual { get; set; }
    public double Meta { get; set; }
    public bool Obrigatorio { get; set; } = true;
    public bool Cumprido => Atual >= Meta - 1e-9;
}

public enum FaseMissao { EmAndamento, Vitoria, Derrota, Incompleta }

public sealed class Resultado
{
    public bool Ok { get; set; }
    public string Mensagem { get; set; } = "";
    public static Resultado Sim(string m = "") => new() { Ok = true, Mensagem = m };
    public static Resultado Nao(string m) => new() { Ok = false, Mensagem = m };
}
