using System;
using System.Collections.Generic;

namespace MissaoMarte.Simulation
{
    /// <summary>Resultado do cálculo de ar de um ciclo. Massas em kg, pressões em mmHg.</summary>
    public sealed class AirReport
    {
        internal AirReport() { }

        public bool GeneratorRunning { get; internal set; }
        public bool ScrubberRunning { get; internal set; }

        public double OxygenBeforeKg { get; internal set; }

        /// <summary>O2 produzido pelo gerador neste ciclo.</summary>
        public double OxygenProducedKg { get; internal set; }

        /// <summary>O2 que a tripulação precisava neste ciclo.</summary>
        public double OxygenDemandKg { get; internal set; }

        /// <summary>O2 efetivamente consumido pela tripulação.</summary>
        public double OxygenConsumedKg { get; internal set; }

        /// <summary>O2 que faltou para a tripulação (demanda não atendida).</summary>
        public double OxygenShortageKg { get; internal set; }

        /// <summary>O2 perdido para fora da base por vazamento.</summary>
        public double OxygenLeakedKg { get; internal set; }

        public double OxygenAfterKg { get; internal set; }

        /// <summary>Água consumida pelo gerador de O2 (eletrólise).</summary>
        public double WaterUsedKg { get; internal set; }

        /// <summary>O gerador funcionou, mas produziu menos porque faltou água.</summary>
        public bool GeneratorLimitedByWater { get; internal set; }

        public double Co2BeforeKg { get; internal set; }
        public double Co2ProducedKg { get; internal set; }
        public double Co2RemovedKg { get; internal set; }
        public double Co2AfterKg { get; internal set; }

        /// <summary>Pressão parcial de CO2 na cabine ao fim do ciclo.</summary>
        public double Co2PressureAfterMmHg { get; internal set; }
    }

    /// <summary>
    /// Cálculo de ar de um ciclo: O2 do reservatório e CO2 acumulado na cabine. Função pura.
    ///
    /// Simplificações do modelo (decisões de projeto):
    /// 1. O O2 é tratado como uma reserva em kg; a pressão parcial de O2 na cabine não é simulada.
    /// 2. O gerador de O2 faz eletrólise da água e só produz o que cabe no reservatório
    ///    (contando o que a tripulação consome no próprio ciclo) e o que a água disponível permite.
    /// 3. O CO2 se mistura uniformemente no ar da cabine; a pressão parcial vem da lei dos gases ideais.
    /// 4. O removedor de CO2 retira até a sua capacidade por ciclo e não reduz abaixo do nível nominal.
    /// 5. A tripulação produz CO2 à taxa nominal em todos os ciclos.
    /// </summary>
    public static class AirSystem
    {
        const double Epsilon = PowerSystem.Epsilon;

        /// <param name="oxygenKg">O2 no reservatório no início do ciclo.</param>
        /// <param name="co2Kg">CO2 no ar da cabine no início do ciclo.</param>
        /// <param name="waterAvailableKg">Água que o gerador de O2 pode usar neste ciclo.</param>
        /// <param name="generatorRunning">O gerador de O2 está ligado e recebeu energia.</param>
        /// <param name="scrubberRunning">O removedor de CO2 está ligado e recebeu energia.</param>
        public static AirReport Resolve(
            double oxygenKg,
            double co2Kg,
            double waterAvailableKg,
            bool generatorRunning,
            bool scrubberRunning,
            ScenarioParameters parameters,
            double generatorCondition = 1.0,
            double scrubberCondition = 1.0,
            double leakKg = 0.0)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            generatorCondition = Clamp01(generatorCondition);
            scrubberCondition = Clamp01(scrubberCondition);
            leakKg = Math.Max(0.0, leakKg);

            double demand = parameters.CrewOxygenDemandKgPerCycle;

            double produced = 0;
            double waterUsed = 0;
            bool limitedByWater = false;
            if (generatorRunning)
            {
                double room = Math.Max(0.0, parameters.OxygenTankCapacityKg - oxygenKg + demand);
                double wanted = Math.Min(parameters.OxygenGeneratorOutputKgPerCycle * generatorCondition, room);
                double allowedByWater = Math.Max(0.0, waterAvailableKg) / PhysicalConstants.WaterKgPerOxygenKg;

                produced = Math.Min(wanted, allowedByWater);
                limitedByWater = allowedByWater + Epsilon < wanted;
                waterUsed = produced * PhysicalConstants.WaterKgPerOxygenKg;
            }

            double available = oxygenKg + produced;
            double consumed = Math.Min(demand, available);
            double shortage = demand - consumed;
            if (shortage < Epsilon) shortage = 0;
            double remaining = Math.Max(0.0, available - consumed);
            double leaked = Math.Min(leakKg, remaining);
            double oxygenAfter = remaining - leaked;

            double co2Produced = parameters.CrewCo2ProductionKgPerCycle;
            double co2Total = co2Kg + co2Produced;
            double co2Removed = 0;
            if (scrubberRunning)
            {
                double nominalKg = parameters.Co2NominalMmHg * parameters.Co2KgPerMmHg;
                co2Removed = Math.Min(parameters.Co2ScrubberCapacityKgPerCycle * scrubberCondition, Math.Max(0.0, co2Total - nominalKg));
            }
            double co2After = co2Total - co2Removed;

            return new AirReport
            {
                GeneratorRunning = generatorRunning,
                ScrubberRunning = scrubberRunning,
                OxygenBeforeKg = oxygenKg,
                OxygenProducedKg = produced,
                OxygenDemandKg = demand,
                OxygenConsumedKg = consumed,
                OxygenShortageKg = shortage,
                OxygenLeakedKg = leaked,
                OxygenAfterKg = oxygenAfter,
                WaterUsedKg = waterUsed,
                GeneratorLimitedByWater = limitedByWater,
                Co2BeforeKg = co2Kg,
                Co2ProducedKg = co2Produced,
                Co2RemovedKg = co2Removed,
                Co2AfterKg = co2After,
                Co2PressureAfterMmHg = parameters.Co2PressureMmHg(co2After),
            };
        }

        static double Clamp01(double value)
        {
            return double.IsNaN(value) ? 0.0 : Math.Min(1.0, Math.Max(0.0, value));
        }

        /// <summary>Traduz o resultado de ar em consequências estruturadas para a explicação ao jogador.</summary>
        public static void AppendConsequences(AirReport report, ScenarioParameters parameters, List<Consequence> output)
        {
            if (report.OxygenDemandKg > 0)
            {
                if (!report.GeneratorRunning)
                    output.Add(new Consequence(ConsequenceCode.OxygenGeneratorNotRunning, EquipmentIds.OxygenGenerator, report.OxygenConsumedKg));
                else if (report.GeneratorLimitedByWater)
                    output.Add(new Consequence(ConsequenceCode.OxygenGeneratorLimitedByWater, EquipmentIds.OxygenGenerator, report.OxygenProducedKg));
            }

            if (report.WaterUsedKg > Epsilon)
                output.Add(new Consequence(ConsequenceCode.OxygenProducedFromWater, EquipmentIds.OxygenGenerator, report.WaterUsedKg));

            if (report.OxygenLeakedKg > Epsilon)
                output.Add(new Consequence(ConsequenceCode.OxygenLeak, null, report.OxygenLeakedKg));

            if (report.OxygenShortageKg > 0)
                output.Add(new Consequence(ConsequenceCode.OxygenShortage, null, report.OxygenShortageKg));

            if (!report.ScrubberRunning && report.Co2ProducedKg > 0)
                output.Add(new Consequence(ConsequenceCode.Co2NotRemoved, EquipmentIds.Co2Scrubber, report.Co2PressureAfterMmHg));

            if (parameters.Co2KgPerMmHg > 0)
            {
                double pressure = report.Co2PressureAfterMmHg;
                if (parameters.Co2ActionMmHg > 0 && pressure >= parameters.Co2ActionMmHg)
                    output.Add(new Consequence(ConsequenceCode.Co2AboveActionLevel, null, pressure));
                else if (parameters.Co2AlertMmHg > 0 && pressure >= parameters.Co2AlertMmHg)
                    output.Add(new Consequence(ConsequenceCode.Co2AboveAlertLevel, null, pressure));
            }
        }
    }
}
