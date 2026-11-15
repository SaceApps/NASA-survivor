using System.Globalization;
using MissaoMarte.Nucleo.Jogo;

namespace MissaoMarte.Web.Servicos;

/// <summary>Números para SVG e CSS (sempre com ponto, seja qual for o idioma) e a geometria dos hexágonos.</summary>
public static class Fmt
{
    public static string N(double v, string formato = "0.##") => v.ToString(formato, CultureInfo.InvariantCulture);

    public const double Lado = 33;

    /// <summary>Centro de um hexágono "de ponta para cima" em coordenadas axiais.</summary>
    public static (double X, double Y) Centro(Hex h, double lado = Lado) => (lado * Math.Sqrt(3) * (h.Q + h.R / 2.0), lado * 1.5 * h.R);

    public static string HexPontos(double cx, double cy, double lado)
    {
        var p = new string[6];
        for (int i = 0; i < 6; i++)
        {
            double a = Math.PI / 180 * (60 * i - 30);
            p[i] = N(cx + lado * Math.Cos(a)) + "," + N(cy + lado * Math.Sin(a));
        }
        return string.Join(" ", p);
    }
}
