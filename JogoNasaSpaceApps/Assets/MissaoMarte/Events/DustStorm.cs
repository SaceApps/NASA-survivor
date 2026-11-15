using System;

namespace MissaoMarte.Events
{
    /// <summary>
    /// Tempestade de poeira: durante alguns ciclos a poeira na atmosfera bloqueia a luz e a geração
    /// solar cai para uma fração do normal. A poeira também se deposita sobre os painéis, que perdem
    /// eficiência até serem limpos.
    /// </summary>
    public sealed class DustStorm
    {
        /// <param name="startCycle">Primeiro ciclo afetado.</param>
        /// <param name="durationCycles">Quantos ciclos a tempestade dura.</param>
        /// <param name="solarFactor">Fração da geração solar que resta durante a tempestade (0 a 1).</param>
        /// <param name="panelEfficiencyLossPerCycle">Quanto de eficiência os painéis perdem a cada ciclo
        /// de tempestade, por poeira depositada (0 a 1).</param>
        public DustStorm(int startCycle, int durationCycles, double solarFactor, double panelEfficiencyLossPerCycle = 0)
        {
            if (startCycle < 1) throw new ArgumentOutOfRangeException(nameof(startCycle));
            if (durationCycles < 1) throw new ArgumentOutOfRangeException(nameof(durationCycles));
            if (double.IsNaN(solarFactor) || solarFactor < 0 || solarFactor > 1)
                throw new ArgumentOutOfRangeException(nameof(solarFactor));
            if (double.IsNaN(panelEfficiencyLossPerCycle) || panelEfficiencyLossPerCycle < 0 || panelEfficiencyLossPerCycle > 1)
                throw new ArgumentOutOfRangeException(nameof(panelEfficiencyLossPerCycle));

            StartCycle = startCycle;
            DurationCycles = durationCycles;
            SolarFactor = solarFactor;
            PanelEfficiencyLossPerCycle = panelEfficiencyLossPerCycle;
        }

        public int StartCycle { get; }

        public int DurationCycles { get; }

        public double SolarFactor { get; }

        public double PanelEfficiencyLossPerCycle { get; }

        /// <summary>Último ciclo afetado.</summary>
        public int EndCycle => StartCycle + DurationCycles - 1;

        public bool IsActive(int cycle)
        {
            return cycle >= StartCycle && cycle <= EndCycle;
        }
    }
}
