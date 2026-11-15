namespace MissaoMarte.Simulation
{
    /// <summary>
    /// Condições externas que valem para um ciclo. É por aqui que os eventos ambientais
    /// (ex.: tempestade de poeira) influenciam a simulação, sem o motor conhecer o módulo de eventos.
    /// </summary>
    public sealed class CycleModifiers
    {
        /// <summary>Multiplicador da geração solar: 1 = céu limpo, 0 = sem geração.</summary>
        public double SolarGenerationFactor { get; set; } = 1.0;
    }
}
