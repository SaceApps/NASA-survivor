namespace MissaoMarte.Simulation
{
    /// <summary>
    /// Constantes físicas e químicas. Não são parâmetros de cenário: não mudam com o balanceamento do jogo.
    /// </summary>
    public static class PhysicalConstants
    {
        /// <summary>Constante universal dos gases, J/(mol·K). Valor exato do SI (2019).</summary>
        public const double GasConstant = 8.314462618;

        /// <summary>Pascal por milímetro de mercúrio (definição: 101325 Pa / 760).</summary>
        public const double PascalPerMmHg = 133.322368;

        /// <summary>Massa molar do CO2, kg/mol (12,011 + 2 × 15,999 g/mol).</summary>
        public const double Co2MolarMassKgPerMol = 0.044009;

        /// <summary>Massa molar da água, kg/mol.</summary>
        public const double WaterMolarMassKgPerMol = 0.018015;

        /// <summary>Massa molar do O2, kg/mol.</summary>
        public const double OxygenMolarMassKgPerMol = 0.031998;

        /// <summary>
        /// Água consumida por kg de O2 produzido na eletrólise (2 H2O -> 2 H2 + O2):
        /// 2 × 18,015 / 31,998 ≈ 1,126 kg de água por kg de O2.
        /// </summary>
        public const double WaterKgPerOxygenKg = 2.0 * WaterMolarMassKgPerMol / OxygenMolarMassKgPerMol;

        /// <summary>
        /// Duração de um sol (dia solar de Marte) em dias terrestres: 88 775,244 s / 86 400 s.
        /// Fonte: Allison &amp; McEwen (2000), valor usado pelo NASA GISS Mars24.
        /// </summary>
        public const double SolInEarthDays = 88775.244 / 86400.0;

        /// <summary>
        /// Raio equatorial de Marte, km. Fonte: NASA NSSDCA, Mars Fact Sheet
        /// (nssdc.gsfc.nasa.gov/planetary/factsheet/marsfact.html), 3396,2 km.
        /// </summary>
        public const double MarsEquatorialRadiusKm = 3396.2;
    }
}
