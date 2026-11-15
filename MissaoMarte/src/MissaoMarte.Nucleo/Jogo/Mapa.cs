namespace MissaoMarte.Nucleo.Jogo;

/// <summary>
/// O TABULEIRO DE MARTE: um pedaço da cratera Jezero em hexágonos de 1 km, com a base no centro.
/// A geografia é uma simplificação inspirada no local real: o delta fica a oeste, a cerca de 2 km do pouso do
/// Perseverance, atrás de uma escarpa; o fundo da cratera é plano, com campos de pedras e de dunas.
/// O relevo é conhecido desde o começo (há imagens de órbita); o que cada lugar guarda só aparece ao explorar.
/// </summary>
public static class MapaJezero
{
    public const int Raio = 4;

    /// <summary>Os lugares com nome: o que há para coletar e o cartão de ciência (fato real da NASA) que cada um revela.</summary>
    private static readonly (int Q, int R, string Nome, int Amostras, double ArgilaKg, string FatoId, Terreno? Terreno)[] Lugares =
    {
        (0, 1, "Pouso do Perseverance", 0, 0, "perseverance-01", null),
        (1, -1, "Campo de voo do Ingenuity", 0, 0, "perseverance-03", null),
        (2, -1, "Afloramento rochoso", 2, 0, "marte-05", Terreno.Rochoso),
        (-2, 0, "Frente do delta", 1, 450, "jezero-02", null),
        (-2, 1, "Depósito Three Forks", 0, 0, "perseverance-02", null),
        (-3, 3, "Morro Kodiak", 1, 300, "jezero-01", null),
        (-4, 0, "Topo do delta", 3, 0, "jezero-03", null),
        (1, -4, "Campo de dunas", 1, 0, "marte-03", null),
        (0, 3, "Cratera pequena", 2, 0, "agua-01", null),
        (4, -2, "Mirante do leste", 1, 0, "marte-06", null),
        (3, 1, "Planície de pedras", 1, 0, "perseverance-04", null),
    };

    public static Terreno TerrenoDe(Hex h)
    {
        if (h == Hex.Origem) return Terreno.Base;
        double leste = h.Q + h.R / 2.0;            // posição leste-oeste (negativo = oeste, onde fica o delta)
        if (leste <= -3.5) return Terreno.Delta;    // o topo do delta
        if (leste <= -2.5) return Terreno.Escarpa;  // a frente íngreme: o veículo não sobe
        if (leste <= -1.5 && h.R >= -2) return Terreno.Delta;   // o pé do delta, com as argilas
        if (h.R <= -3) return Terreno.Dunas;
        if (h.R >= 3) return h.Q is -1 or 0 ? Terreno.Cratera : Terreno.Rochoso;
        if (leste >= 2) return Terreno.Rochoso;
        return Terreno.Planicie;
    }

    public static List<Celula> Criar()
    {
        var celulas = new List<Celula>();
        for (int q = -Raio; q <= Raio; q++)
            for (int r = Math.Max(-Raio, -q - Raio); r <= Math.Min(Raio, -q + Raio); r++)
            {
                var h = new Hex(q, r);
                celulas.Add(new Celula { Pos = h, Terreno = TerrenoDe(h), Explorado = h.Distancia(Hex.Origem) <= 1 });
            }
        foreach (var l in Lugares)
        {
            var c = celulas.First(x => x.Pos == new Hex(l.Q, l.R));
            c.Nome = l.Nome; c.Amostras = l.Amostras; c.ArgilaKg = l.ArgilaKg; c.DescobertaId = l.FatoId;
            if (l.Terreno is { } t) c.Terreno = t;
        }
        celulas.First(c => c.Pos == Hex.Origem).Nome = "Base Jezero";
        return celulas;
    }

    public static string NomeDoTerreno(Terreno t) => t switch
    {
        Terreno.Base => "Base", Terreno.Planicie => "Planície", Terreno.Rochoso => "Campo de pedras", Terreno.Dunas => "Dunas",
        Terreno.Cratera => "Cratera", Terreno.Delta => "Delta", _ => "Escarpa",
    };

    public static string DicaDoTerreno(Terreno t) => t switch
    {
        Terreno.Planicie => "Chão firme e plano: o caminho mais fácil.",
        Terreno.Rochoso => "Muitas pedras: anda-se mais devagar.",
        Terreno.Dunas => "Areia fofa: gasta o dobro de tempo e muita bateria do veículo.",
        Terreno.Cratera => "Borda inclinada: gasta o dobro.",
        Terreno.Delta => "Camadas de lama e areia de um rio antigo. É onde ficam as argilas com água.",
        Terreno.Escarpa => "Parede íngreme: o veículo não sobe. A pé, leva muito tempo.",
        _ => "A casa da tripulação.",
    };
}
