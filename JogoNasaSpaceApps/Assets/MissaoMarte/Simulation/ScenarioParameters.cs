using System;
using System.Collections.Generic;

namespace MissaoMarte.Simulation
{
    /// <summary>
    /// Todos os valores numéricos de um cenário ficam aqui, separados da lógica.
    /// O motor não tem números de cenário embutidos: quem cria o cenário é responsável por documentar
    /// a fonte de cada valor ou marcá-lo como provisório (ver <see cref="ProvisionalScenario"/>).
    /// Um "ciclo" é a unidade de tempo do jogo; todas as taxas são por ciclo.
    /// </summary>
    public sealed class ScenarioParameters
    {
        // ---------- Energia ----------

        /// <summary>Energia gerada pelos painéis solares em um ciclo com céu limpo (kWh).</summary>
        public double SolarGenerationKWhPerCycle { get; set; }

        /// <summary>Capacidade máxima da bateria (kWh).</summary>
        public double BatteryCapacityKWh { get; set; }

        /// <summary>Carga da bateria no início da partida (kWh).</summary>
        public double InitialBatteryChargeKWh { get; set; }

        /// <summary>Equipamentos existentes no habitat.</summary>
        public IReadOnlyList<EquipmentDefinition> Equipment { get; set; } = new List<EquipmentDefinition>();

        // ---------- Tripulação ----------

        /// <summary>Número de tripulantes.</summary>
        public int CrewCount { get; set; }

        // ---------- Ar ----------

        /// <summary>O2 consumido por tripulante em um ciclo (kg).</summary>
        public double OxygenConsumptionKgPerCrewPerCycle { get; set; }

        /// <summary>CO2 produzido por tripulante em um ciclo (kg).</summary>
        public double Co2ProductionKgPerCrewPerCycle { get; set; }

        /// <summary>Capacidade do reservatório de O2 (kg).</summary>
        public double OxygenTankCapacityKg { get; set; }

        /// <summary>O2 no reservatório no início da partida (kg).</summary>
        public double InitialOxygenKg { get; set; }

        /// <summary>Produção máxima do gerador de O2 em um ciclo (kg).</summary>
        public double OxygenGeneratorOutputKgPerCycle { get; set; }

        /// <summary>Remoção máxima de CO2 pelo removedor em um ciclo (kg).</summary>
        public double Co2ScrubberCapacityKgPerCycle { get; set; }

        /// <summary>CO2 no ar da cabine no início da partida (kg).</summary>
        public double InitialCo2Kg { get; set; }

        /// <summary>Volume de ar pressurizado do habitat (m³). Converte massa de CO2 em pressão parcial.</summary>
        public double HabitatVolumeM3 { get; set; }

        /// <summary>Temperatura do ar da cabine (K). Usada na conversão de massa de CO2 em pressão parcial.</summary>
        public double CabinTemperatureK { get; set; }

        /// <summary>Pressão parcial de CO2 que o removedor mantém em operação normal (mmHg); ele não reduz abaixo disso.</summary>
        public double Co2NominalMmHg { get; set; }

        /// <summary>Pressão parcial de CO2 a partir da qual há alerta (mmHg).</summary>
        public double Co2AlertMmHg { get; set; }

        /// <summary>Pressão parcial de CO2 a partir da qual é obrigatório agir (mmHg).</summary>
        public double Co2ActionMmHg { get; set; }

        /// <summary>Pressão parcial de CO2 a partir da qual o habitat se torna inabitável (mmHg).</summary>
        public double Co2EvacuationMmHg { get; set; }

        /// <summary>Limite operacional da reserva de O2 (kg): abaixo disso a missão é abortada.</summary>
        public double OxygenMinimumKg { get; set; }

        /// <summary>O2 perdido por ciclo com a vedação totalmente danificada (kg). A perda é proporcional ao dano.</summary>
        public double CabinLeakOxygenKgPerCycleAtBrokenSeal { get; set; }

        // ---------- Traje (atividade externa) ----------

        /// <summary>Capacidade do tanque de O2 do traje (kg).</summary>
        public double SuitOxygenCapacityKg { get; set; }

        /// <summary>O2 que o traje gasta por ciclo fora da base (kg).</summary>
        public double SuitOxygenUseKgPerCycle { get; set; }

        // ---------- Água ----------

        /// <summary>Água potável consumida por tripulante em um ciclo (kg).</summary>
        public double WaterConsumptionKgPerCrewPerCycle { get; set; }

        /// <summary>Eficiência do tratamento: fração da água residual tratada que volta como água potável (0 a 1).</summary>
        public double WaterRecoveryFraction { get; set; }

        /// <summary>Quanta água residual o tratamento consegue processar em um ciclo (kg).</summary>
        public double WaterTreatmentCapacityKgPerCycle { get; set; }

        /// <summary>Capacidade do reservatório de água potável (kg).</summary>
        public double WaterTankCapacityKg { get; set; }

        /// <summary>Água potável no início da partida (kg).</summary>
        public double InitialWaterKg { get; set; }

        /// <summary>Capacidade do tanque de água residual (kg).</summary>
        public double WasteWaterTankCapacityKg { get; set; }

        /// <summary>Água residual no início da partida (kg).</summary>
        public double InitialWasteWaterKg { get; set; }

        // ---------- Gelo ----------

        /// <summary>Quanto gelo bruto a estação de processamento converte em um ciclo (kg).</summary>
        public double IceProcessorCapacityKgPerCycle { get; set; }

        /// <summary>Fração do gelo bruto que vira água potável (o resto são impurezas) (0 a 1).</summary>
        public double IceToWaterEfficiency { get; set; }

        /// <summary>Gelo bruto estocado na base no início da partida (kg).</summary>
        public double InitialIceKg { get; set; }

        // ---------- Valores derivados ----------

        /// <summary>
        /// Massa de CO2 (kg) que corresponde a 1 mmHg de pressão parcial no volume do habitat,
        /// pela lei dos gases ideais: m = p·V·M / (R·T). Zero se o volume ou a temperatura não foram definidos.
        /// </summary>
        public double Co2KgPerMmHg
        {
            get
            {
                if (HabitatVolumeM3 <= 0 || CabinTemperatureK <= 0)
                    return 0;
                return PhysicalConstants.PascalPerMmHg * HabitatVolumeM3 * PhysicalConstants.Co2MolarMassKgPerMol
                    / (PhysicalConstants.GasConstant * CabinTemperatureK);
            }
        }

        /// <summary>Pressão parcial (mmHg) correspondente a uma massa de CO2 no ar da cabine.</summary>
        public double Co2PressureMmHg(double co2Kg)
        {
            double kgPerMmHg = Co2KgPerMmHg;
            return kgPerMmHg > 0 ? co2Kg / kgPerMmHg : 0;
        }

        /// <summary>O2 que a tripulação inteira consome em um ciclo (kg).</summary>
        public double CrewOxygenDemandKgPerCycle => CrewCount * OxygenConsumptionKgPerCrewPerCycle;

        /// <summary>CO2 que a tripulação inteira produz em um ciclo (kg).</summary>
        public double CrewCo2ProductionKgPerCycle => CrewCount * Co2ProductionKgPerCrewPerCycle;

        /// <summary>Água que a tripulação inteira consome em um ciclo (kg).</summary>
        public double CrewWaterDemandKgPerCycle => CrewCount * WaterConsumptionKgPerCrewPerCycle;

        /// <summary>Lança exceção se o cenário for inconsistente. Chamado pelo motor ao iniciar.</summary>
        public void Validate()
        {
            RequireNonNegative(SolarGenerationKWhPerCycle, nameof(SolarGenerationKWhPerCycle));
            RequireNonNegative(BatteryCapacityKWh, nameof(BatteryCapacityKWh));
            RequireNonNegative(InitialBatteryChargeKWh, nameof(InitialBatteryChargeKWh));
            RequireNonNegative(OxygenConsumptionKgPerCrewPerCycle, nameof(OxygenConsumptionKgPerCrewPerCycle));
            RequireNonNegative(Co2ProductionKgPerCrewPerCycle, nameof(Co2ProductionKgPerCrewPerCycle));
            RequireNonNegative(OxygenTankCapacityKg, nameof(OxygenTankCapacityKg));
            RequireNonNegative(InitialOxygenKg, nameof(InitialOxygenKg));
            RequireNonNegative(OxygenGeneratorOutputKgPerCycle, nameof(OxygenGeneratorOutputKgPerCycle));
            RequireNonNegative(Co2ScrubberCapacityKgPerCycle, nameof(Co2ScrubberCapacityKgPerCycle));
            RequireNonNegative(InitialCo2Kg, nameof(InitialCo2Kg));
            RequireNonNegative(HabitatVolumeM3, nameof(HabitatVolumeM3));
            RequireNonNegative(CabinTemperatureK, nameof(CabinTemperatureK));
            RequireNonNegative(Co2NominalMmHg, nameof(Co2NominalMmHg));
            RequireNonNegative(Co2AlertMmHg, nameof(Co2AlertMmHg));
            RequireNonNegative(Co2ActionMmHg, nameof(Co2ActionMmHg));
            RequireNonNegative(Co2EvacuationMmHg, nameof(Co2EvacuationMmHg));
            RequireNonNegative(WaterConsumptionKgPerCrewPerCycle, nameof(WaterConsumptionKgPerCrewPerCycle));
            RequireNonNegative(WaterRecoveryFraction, nameof(WaterRecoveryFraction));
            RequireNonNegative(WaterTankCapacityKg, nameof(WaterTankCapacityKg));
            RequireNonNegative(InitialWaterKg, nameof(InitialWaterKg));
            RequireNonNegative(OxygenMinimumKg, nameof(OxygenMinimumKg));
            RequireNonNegative(CabinLeakOxygenKgPerCycleAtBrokenSeal, nameof(CabinLeakOxygenKgPerCycleAtBrokenSeal));
            RequireNonNegative(SuitOxygenCapacityKg, nameof(SuitOxygenCapacityKg));
            RequireNonNegative(SuitOxygenUseKgPerCycle, nameof(SuitOxygenUseKgPerCycle));
            RequireNonNegative(WaterTreatmentCapacityKgPerCycle, nameof(WaterTreatmentCapacityKgPerCycle));
            RequireNonNegative(WasteWaterTankCapacityKg, nameof(WasteWaterTankCapacityKg));
            RequireNonNegative(InitialWasteWaterKg, nameof(InitialWasteWaterKg));
            RequireNonNegative(IceProcessorCapacityKgPerCycle, nameof(IceProcessorCapacityKgPerCycle));
            RequireNonNegative(IceToWaterEfficiency, nameof(IceToWaterEfficiency));
            RequireNonNegative(InitialIceKg, nameof(InitialIceKg));

            if (CrewCount < 0)
                throw new ArgumentException("O número de tripulantes não pode ser negativo.");
            if (InitialBatteryChargeKWh > BatteryCapacityKWh)
                throw new ArgumentException("A carga inicial da bateria não pode ser maior que a capacidade.");
            if (InitialOxygenKg > OxygenTankCapacityKg)
                throw new ArgumentException("O O2 inicial não pode ser maior que a capacidade do reservatório.");
            if (InitialWaterKg > WaterTankCapacityKg)
                throw new ArgumentException("A água inicial não pode ser maior que a capacidade do reservatório.");
            if (InitialWasteWaterKg > WasteWaterTankCapacityKg)
                throw new ArgumentException("A água residual inicial não pode ser maior que a capacidade do tanque.");
            if (WaterRecoveryFraction > 1)
                throw new ArgumentException("A eficiência do tratamento de água precisa estar entre 0 e 1.");
            if (IceToWaterEfficiency > 1)
                throw new ArgumentException("A eficiência do processamento de gelo precisa estar entre 0 e 1.");
            if (Equipment == null)
                throw new ArgumentException("A lista de equipamentos não pode ser nula.");

            var ids = new HashSet<string>();
            foreach (EquipmentDefinition definition in Equipment)
            {
                if (definition == null)
                    throw new ArgumentException("A lista de equipamentos contém um item nulo.");
                if (!ids.Add(definition.Id))
                    throw new ArgumentException("Id de equipamento duplicado: " + definition.Id);
            }
        }

        static void RequireNonNegative(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentException(name + " precisa ser um número finito maior ou igual a zero.");
        }
    }
}
