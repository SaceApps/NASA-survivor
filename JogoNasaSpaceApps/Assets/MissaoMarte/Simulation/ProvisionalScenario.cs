using System.Collections.Generic;

namespace MissaoMarte.Simulation
{
    /// <summary>Ids dos equipamentos do cenário.</summary>
    public static class EquipmentIds
    {
        public const string OxygenGenerator = "oxygen_generator";
        public const string Co2Scrubber = "co2_scrubber";
        public const string WaterRecycler = "water_recycler";
        public const string IceProcessor = "ice_processor";
        public const string Infrastructure = "infrastructure";
    }

    /// <summary>
    /// Cenário da missão "Operação: Primeira Fonte". Um ciclo da simulação = 1 hora marciana
    /// (1/24 de sol, cerca de 1,03 hora da Terra).
    ///
    /// IMPORTANTE: são parâmetros didáticos de demonstração, não valores de engenharia validados
    /// para uma missão real. Cada valor está marcado como:
    ///   [FONTE]    valor tirado de documento citado (convertido para hora marciana);
    ///   [DIDÁTICO] número escolhido para a jogabilidade, sem fonte científica.
    ///
    /// Fontes:
    ///   BVAD   = Anderson, Ewert, Keener &amp; Wagner (2018). Life Support Baseline Values and Assumptions
    ///            Document. NASA/TP-2015-218570/REV1. https://ntrs.nasa.gov/citations/20180001338
    ///   BVAD2  = NASA/TP-2015-218570/REV2 (2022), Tabela 4-70 "Typical EVA Metabolic Loads" e seção 4.6.2
    ///            (valores levantados pela equipe na versão web do jogo, docs/pesquisa).
    ///   NASA-98 = NASA (2023), 98% de recuperação de água no ECLSS da ISS com o Brine Processor Assembly
    ///            (fonte secundária: space.com).
    /// </summary>
    public static class ProvisionalScenario
    {
        /// <summary>[FONTE] BVAD, Tabela 3-33: O2 consumido, 0,816 kg por tripulante por dia terrestre.</summary>
        public const double OxygenKgPerCrewPerEarthDay = 0.816;

        /// <summary>[FONTE] BVAD, Tabela 3-33: carga de CO2, 1,04 kg por tripulante por dia terrestre.</summary>
        public const double Co2KgPerCrewPerEarthDay = 1.04;

        /// <summary>[FONTE] BVAD, Tabela 3-33: água potável consumida, 2,5 kg por tripulante por dia terrestre.</summary>
        public const double WaterKgPerCrewPerEarthDay = 2.5;

        /// <summary>[FONTE] BVAD2, Tabela 4-70: O2 metabólico em atividade externa, 0,086 kg por tripulante por hora terrestre.</summary>
        public const double EvaOxygenKgPerEarthHour = 0.086;

        /// <summary>[FONTE] BVAD2, seção 4.6.2: duração máxima de uma atividade externa, 8 horas.</summary>
        public const double EvaMaxDurationEarthHours = 8;

        /// <summary>Horas da Terra em uma hora marciana (1 sol / 24).</summary>
        public const double EarthHoursPerMarsHour = PhysicalConstants.SolInEarthDays;

        /// <summary>Converte uma taxa diária terrestre para taxa por hora marciana.</summary>
        public static double PerMarsHour(double perEarthDay)
        {
            return perEarthDay * PhysicalConstants.SolInEarthDays / 24.0;
        }

        public static ScenarioParameters Create()
        {
            return new ScenarioParameters
            {
                // ---------- Energia (kWh por hora = kW): tudo [DIDÁTICO] ----------
                // Pico de geração ao meio-dia; a curva do dia e a tempestade entram como fator externo.
                SolarGenerationKWhPerCycle = 9,
                BatteryCapacityKWh = 36,
                InitialBatteryChargeKWh = 26,
                Equipment = new List<EquipmentDefinition>
                {
                    // [DIDÁTICO] potência e prioridade (menor número = atendido primeiro).
                    new EquipmentDefinition(EquipmentIds.Co2Scrubber, energyDemandKWhPerCycle: 0.5, priority: 1),
                    new EquipmentDefinition(EquipmentIds.OxygenGenerator, energyDemandKWhPerCycle: 1.0, priority: 2),
                    new EquipmentDefinition(EquipmentIds.WaterRecycler, energyDemandKWhPerCycle: 0.6, priority: 3),
                    new EquipmentDefinition(EquipmentIds.Infrastructure, energyDemandKWhPerCycle: 0.4, priority: 4),
                    new EquipmentDefinition(EquipmentIds.IceProcessor, energyDemandKWhPerCycle: 1.2, priority: 5, startsOn: false),
                },

                // [DIDÁTICO] tamanho da tripulação.
                CrewCount = 4,

                // ---------- Ar ----------
                // [FONTE] taxas metabólicas do BVAD, convertidas para hora marciana.
                OxygenConsumptionKgPerCrewPerCycle = PerMarsHour(OxygenKgPerCrewPerEarthDay),
                Co2ProductionKgPerCrewPerCycle = PerMarsHour(Co2KgPerCrewPerEarthDay),

                // [DIDÁTICO] reservatório de O2, limite operacional, capacidade dos equipamentos e vazamento.
                OxygenTankCapacityKg = 15,
                InitialOxygenKg = 10,
                OxygenMinimumKg = 3,
                OxygenGeneratorOutputKgPerCycle = 0.2,
                Co2ScrubberCapacityKgPerCycle = 0.4,
                CabinLeakOxygenKgPerCycleAtBrokenSeal = 0.4,

                // [DIDÁTICO] volume pressurizado, temperatura da cabine e nível normal de CO2.
                HabitatVolumeM3 = 300,
                CabinTemperatureK = 295,
                Co2NominalMmHg = 2.0,
                InitialCo2Kg = 0, // ajustado abaixo para o nível nominal

                // [FONTE] BVAD, seção 4.1 (regras de voo da ISS, Law 2010):
                //   4 mmHg   = máximo operacional adotado pela ISS;
                //   7,6 mmHg = obrigatório tomar medidas para reduzir o CO2;
                //   10 a 15 mmHg = ação imediata, com exposição limitada a 8 horas.
                // No jogo, chegar a 10 mmHg encerra a missão.
                Co2AlertMmHg = 4.0,
                Co2ActionMmHg = 7.6,
                Co2EvacuationMmHg = 10.0,

                // ---------- Traje ----------
                // [FONTE] BVAD2: consumo em atividade externa e duração máxima de 8 h, convertidos para hora marciana.
                SuitOxygenUseKgPerCycle = EvaOxygenKgPerEarthHour * EarthHoursPerMarsHour,
                SuitOxygenCapacityKg = EvaOxygenKgPerEarthHour * EvaMaxDurationEarthHours,

                // ---------- Água (1 kg ≈ 1 litro) ----------
                // [FONTE] consumo do BVAD, convertido para hora marciana.
                WaterConsumptionKgPerCrewPerCycle = PerMarsHour(WaterKgPerCrewPerEarthDay),

                // [FONTE] NASA-98: 98% de recuperação de água demonstrada na ISS.
                WaterRecoveryFraction = 0.98,

                // [DIDÁTICO] reservatórios e capacidade do tratamento.
                WaterTreatmentCapacityKgPerCycle = 0.6,
                WaterTankCapacityKg = 60,
                InitialWaterKg = 20,
                WasteWaterTankCapacityKg = 20,
                InitialWasteWaterKg = 0,

                // ---------- Gelo: tudo [DIDÁTICO] ----------
                IceProcessorCapacityKgPerCycle = 2.0,
                IceToWaterEfficiency = 0.7,
                InitialIceKg = 0,
            }.WithInitialCo2AtNominalLevel();
        }

        static ScenarioParameters WithInitialCo2AtNominalLevel(this ScenarioParameters parameters)
        {
            parameters.InitialCo2Kg = parameters.Co2NominalMmHg * parameters.Co2KgPerMmHg;
            return parameters;
        }
    }
}
