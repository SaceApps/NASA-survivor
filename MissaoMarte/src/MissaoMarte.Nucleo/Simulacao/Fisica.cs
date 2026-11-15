namespace MissaoMarte.Nucleo.Simulacao;

/// <summary>Constantes e leis físicas usadas pelo motor (gás ideal, saturação do vapor, geometria do Sol em Marte).</summary>
public static class Fisica
{
    public const double R = 8.314462618;        // J/(mol·K)
    public const double MO2 = 0.031998;         // kg/mol
    public const double MCO2 = 0.044009;
    public const double MN2 = 0.028014;
    public const double MH2O = 0.018015;
    /// <summary>Água consumida por kg de O2 na eletrólise: 2 H2O → 2 H2 + O2.</summary>
    public const double AguaPorKgO2 = 2.0 * MH2O / MO2;

    /// <summary>Lei dos gases ideais: pressão parcial (kPa) de uma massa de gás num volume, a uma temperatura.</summary>
    public static double PressaoParcialKpa(double kg, double massaMolar, double tempC, double volumeM3) =>
        kg / massaMolar * R * (tempC + 273.15) / volumeM3 / 1000.0;

    /// <summary>O inverso: massa (kg) que dá uma pressão parcial.</summary>
    public static double MassaParaPressaoKg(double kpa, double massaMolar, double tempC, double volumeM3) =>
        kpa * 1000.0 * volumeM3 / (R * (tempC + 273.15)) * massaMolar;

    /// <summary>Pressão de saturação do vapor de água (kPa), fórmula de Magnus (Alduchov e Eskridge, 1996).</summary>
    public static double PressaoSaturacaoKpa(double tempC) => 0.61094 * Math.Exp(17.625 * tempC / (tempC + 243.04));
}

/// <summary>O Sol visto de Marte: distância, estação e altura no céu ao longo do sol (dia marciano).</summary>
public static class SolMarte
{
    private static double Rad(double graus) => graus * Math.PI / 180.0;

    /// <summary>Longitude solar (estação) no instante dado; avanço uniforme ao longo do ano (aproximação).</summary>
    public static double LsGraus(ParametrosCenario p, int sol, double fracaoDoSol) =>
        (p.LsInicialGraus + (sol + fracaoDoSol) * 360.0 / p.AnoMarcianoSols) % 360.0;

    /// <summary>Irradiância no topo da atmosfera (W/m2): lei do inverso do quadrado da distância Sol–Marte.</summary>
    public static double IrradianciaTopoWM2(ParametrosCenario p, double lsGraus)
    {
        double r = p.SemiEixoUa * (1 - p.Excentricidade * p.Excentricidade)
                   / (1 + p.Excentricidade * Math.Cos(Rad(lsGraus - p.LsPerielioGraus)));
        return p.ConstanteSolarTerraWM2 / (r * r);
    }

    /// <summary>Cosseno do ângulo zenital do Sol (1 = Sol a pino; menor ou igual a 0 = noite).</summary>
    public static double CosZenital(ParametrosCenario p, double lsGraus, double horaLocal)
    {
        double decl = Math.Asin(Math.Sin(Rad(p.ObliquidadeGraus)) * Math.Sin(Rad(lsGraus)));
        double h = (horaLocal - 12.0) / 24.0 * 2.0 * Math.PI;
        double lat = Rad(p.LatitudeGraus);
        return Math.Sin(lat) * Math.Sin(decl) + Math.Cos(lat) * Math.Cos(decl) * Math.Cos(h);
    }

    /// <summary>
    /// Irradiância num painel horizontal na superfície (W/m2). A poeira entra pela opacidade tau, com a aproximação
    /// exp(-k·tau) calibrada com dados públicos do rover Opportunity (ver o catálogo: k_atenuacao_poeira).
    /// </summary>
    public static double IrradianciaSuperficieWM2(ParametrosCenario p, int sol, double horaLocal, double tau)
    {
        double ls = LsGraus(p, sol, horaLocal / 24.0);
        double cz = CosZenital(p, ls, horaLocal);
        if (cz <= 0) return 0;
        return IrradianciaTopoWM2(p, ls) * cz * Math.Exp(-p.KAtenuacaoPoeira * tau);
    }

    /// <summary>Temperatura do ar do lado de fora (°C): senoide entre a mínima e a máxima medidas, pico às 14 h locais.</summary>
    public static double TemperaturaExternaC(ParametrosCenario p, double horaLocal)
    {
        double media = (p.TempExternaMaxC + p.TempExternaMinC) / 2.0;
        double amp = (p.TempExternaMaxC - p.TempExternaMinC) / 2.0;
        return media + amp * Math.Cos((horaLocal - 14.0) / 24.0 * 2.0 * Math.PI);
    }
}
