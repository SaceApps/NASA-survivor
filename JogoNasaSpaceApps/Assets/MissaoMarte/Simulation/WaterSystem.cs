using System;
using System.Collections.Generic;

namespace MissaoMarte.Simulation
{
    /// <summary>
    /// Resultado do cálculo de água de um ciclo. Massas em kg (1 kg de água ≈ 1 litro).
    /// Vale sempre para a água potável:
    ///   After = Before - CrewConsumed + Recovered + IceWater - UsedByOxygenGenerator.
    /// </summary>
    public sealed class WaterReport
    {
        internal WaterReport() { }

        // ---------- Água potável e tripulação ----------

        public double BeforeKg { get; internal set; }

        /// <summary>Água que a tripulação precisava neste ciclo.</summary>
        public double CrewDemandKg { get; internal set; }

        /// <summary>Água efetivamente consumida pela tripulação (vira água residual).</summary>
        public double CrewConsumedKg { get; internal set; }

        /// <summary>Água que faltou para a tripulação (demanda não atendida).</summary>
        public double CrewShortageKg { get; internal set; }

        // ---------- Água residual e tratamento ----------

        public double WasteBeforeKg { get; internal set; }

        /// <summary>Água residual que não coube no tanque e foi descartada.</summary>
        public double WasteOverflowKg { get; internal set; }

        public bool TreatmentRunning { get; internal set; }

        /// <summary>Água residual que passou pelo tratamento.</summary>
        public double TreatedKg { get; internal set; }

        /// <summary>Água potável devolvida pelo tratamento (TreatedKg × eficiência).</summary>
        public double RecoveredKg { get; internal set; }

        public double WasteAfterKg { get; internal set; }

        // ---------- Gelo ----------

        public bool IceProcessorRunning { get; internal set; }

        public double IceBeforeKg { get; internal set; }

        /// <summary>Gelo bruto que passou pela estação de processamento.</summary>
        public double IceProcessedKg { get; internal set; }

        /// <summary>Água potável obtida do gelo (IceProcessedKg × eficiência).</summary>
        public double IceWaterKg { get; internal set; }

        public double IceAfterKg { get; internal set; }

        // ---------- Fechamento ----------

        /// <summary>Água potável que sobra para os equipamentos depois da tripulação, do tratamento e do gelo.</summary>
        public double AvailableForEquipmentKg { get; internal set; }

        /// <summary>Água consumida pelo gerador de O2 (eletrólise).</summary>
        public double UsedByOxygenGeneratorKg { get; internal set; }

        public double AfterKg { get; internal set; }
    }

    /// <summary>
    /// Cálculo de água de um ciclo. Função pura. Toda entrada e saída de água é explícita:
    /// nada é criado para "fechar a conta".
    ///
    /// Ordem dentro do ciclo:
    ///   1. A tripulação consome água potável; a água usada vira água residual
    ///      (o que não couber no tanque de água residual é descartado).
    ///   2. O tratamento (se funcionou) processa água residual até a sua capacidade e devolve
    ///      uma fração como água potável; o restante é perdido como salmoura.
    ///   3. A estação de gelo (se funcionou) processa gelo bruto até a sua capacidade e converte
    ///      uma fração em água potável; o restante são impurezas.
    ///   4. O gerador de O2 usa a água potável que sobrou (segunda etapa, depois do cálculo de ar).
    ///
    /// Simplificações (decisões de projeto): toda a água consumida vira água residual; água metabólica
    /// e umidade do ar não são simuladas; tratamento e estação de gelo não produzem além do que cabe
    /// no reservatório de água potável.
    /// </summary>
    public static class WaterSystem
    {
        const double Epsilon = PowerSystem.Epsilon;

        /// <summary>Primeira etapa: tripulação, tratamento de água residual e processamento de gelo.</summary>
        public static WaterReport ResolveCrewAndProcessing(
            double potableKg,
            double wasteKg,
            double iceKg,
            bool treatmentRunning,
            bool iceProcessorRunning,
            ScenarioParameters parameters,
            double treatmentCondition = 1.0,
            double iceProcessorCondition = 1.0)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            treatmentCondition = double.IsNaN(treatmentCondition) ? 0.0 : Math.Min(1.0, Math.Max(0.0, treatmentCondition));
            iceProcessorCondition = double.IsNaN(iceProcessorCondition) ? 0.0 : Math.Min(1.0, Math.Max(0.0, iceProcessorCondition));

            double potable = Math.Max(0.0, potableKg);
            double waste = Math.Max(0.0, wasteKg);
            double ice = Math.Max(0.0, iceKg);

            // 1. Tripulação
            double demand = parameters.CrewWaterDemandKgPerCycle;
            double consumed = Math.Min(demand, potable);
            double shortage = demand - consumed;
            if (shortage < Epsilon) shortage = 0;
            potable -= consumed;

            waste += consumed;
            double overflow = Math.Max(0.0, waste - parameters.WasteWaterTankCapacityKg);
            waste -= overflow;

            // 2. Tratamento de água residual
            double treated = 0;
            double recovered = 0;
            if (treatmentRunning)
            {
                double efficiency = parameters.WaterRecoveryFraction;
                treated = Math.Min(parameters.WaterTreatmentCapacityKgPerCycle * treatmentCondition, waste);
                treated = Math.Min(treated, LimitByRoom(parameters.WaterTankCapacityKg - potable, efficiency));
                recovered = treated * efficiency;
                waste -= treated;
                potable += recovered;
            }

            // 3. Processamento de gelo
            double iceProcessed = 0;
            double iceWater = 0;
            if (iceProcessorRunning)
            {
                double efficiency = parameters.IceToWaterEfficiency;
                iceProcessed = Math.Min(parameters.IceProcessorCapacityKgPerCycle * iceProcessorCondition, ice);
                iceProcessed = Math.Min(iceProcessed, LimitByRoom(parameters.WaterTankCapacityKg - potable, efficiency));
                iceWater = iceProcessed * efficiency;
                ice -= iceProcessed;
                potable += iceWater;
            }

            return new WaterReport
            {
                BeforeKg = potableKg,
                CrewDemandKg = demand,
                CrewConsumedKg = consumed,
                CrewShortageKg = shortage,
                WasteBeforeKg = wasteKg,
                WasteOverflowKg = overflow,
                TreatmentRunning = treatmentRunning,
                TreatedKg = treated,
                RecoveredKg = recovered,
                WasteAfterKg = Math.Max(0.0, waste),
                IceProcessorRunning = iceProcessorRunning,
                IceBeforeKg = iceKg,
                IceProcessedKg = iceProcessed,
                IceWaterKg = iceWater,
                IceAfterKg = Math.Max(0.0, ice),
                AvailableForEquipmentKg = potable,
                UsedByOxygenGeneratorKg = 0,
                AfterKg = potable,
            };
        }

        /// <summary>Quanto material bruto pode ser processado sem a água produzida passar do espaço livre.</summary>
        static double LimitByRoom(double roomKg, double efficiency)
        {
            if (efficiency <= 0)
                return double.MaxValue;
            return Math.Max(0.0, roomKg) / efficiency;
        }

        /// <summary>Segunda etapa: desconta a água usada pelo gerador de O2.</summary>
        public static void ApplyEquipmentUse(WaterReport report, double usedByOxygenGeneratorKg)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (usedByOxygenGeneratorKg > report.AvailableForEquipmentKg + Epsilon)
                throw new ArgumentException("O gerador de O2 não pode usar mais água do que a disponível.");

            report.UsedByOxygenGeneratorKg = usedByOxygenGeneratorKg;
            report.AfterKg = Math.Max(0.0, report.AvailableForEquipmentKg - usedByOxygenGeneratorKg);
        }

        /// <summary>Traduz o resultado de água em consequências estruturadas para a explicação ao jogador.</summary>
        public static void AppendConsequences(WaterReport report, List<Consequence> output)
        {
            if (!report.TreatmentRunning && report.CrewConsumedKg > Epsilon)
                output.Add(new Consequence(ConsequenceCode.WaterNotRecycled, EquipmentIds.WaterRecycler, report.WasteAfterKg));

            if (report.WasteOverflowKg > Epsilon)
                output.Add(new Consequence(ConsequenceCode.WasteWaterOverflow, null, report.WasteOverflowKg));

            if (report.IceWaterKg > Epsilon)
                output.Add(new Consequence(ConsequenceCode.IceProcessed, EquipmentIds.IceProcessor, report.IceWaterKg));
            else if (report.IceProcessorRunning)
                output.Add(new Consequence(ConsequenceCode.IceProcessorIdle, EquipmentIds.IceProcessor, 0));

            if (report.CrewShortageKg > 0)
                output.Add(new Consequence(ConsequenceCode.WaterShortage, null, report.CrewShortageKg));
        }
    }
}
