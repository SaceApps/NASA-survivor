using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MissaoMarte.Simulation.Tests
{
    /// <summary>
    /// Testes do cálculo de água (potável, residual e gelo) e da interdependência entre energia, ar e água.
    /// Números próprios: 2 tripulantes; cada um consome 5 kg de água, 1 kg de O2 e produz 1,5 kg de CO2
    /// por ciclo.
    /// </summary>
    public class WaterAndInterdependencyTests
    {
        const double Tolerance = 1e-9;

        /// <summary>Água usada para produzir 3 kg de O2 por eletrólise.</summary>
        const double WaterFor3KgOxygen = 3 * PhysicalConstants.WaterKgPerOxygenKg;

        /// <summary>
        /// Cenário só de água: demanda de 10 kg por ciclo; tratamento processa até 8 kg com 90% de
        /// eficiência; tanque de água residual de 15 kg; estação de gelo processa até 6 kg com 50%.
        /// </summary>
        static ScenarioParameters WaterScenario()
        {
            return new ScenarioParameters
            {
                CrewCount = 2,
                WaterConsumptionKgPerCrewPerCycle = 5.0,
                WaterRecoveryFraction = 0.9,
                WaterTreatmentCapacityKgPerCycle = 8,
                WaterTankCapacityKg = 100,
                WasteWaterTankCapacityKg = 15,
                IceProcessorCapacityKgPerCycle = 6,
                IceToWaterEfficiency = 0.5,
            };
        }

        /// <summary>Cenário completo, com tratamento folgado (processa tudo) para as contas ficarem simples.</summary>
        static ScenarioParameters Scenario()
        {
            return new ScenarioParameters
            {
                SolarGenerationKWhPerCycle = 100,
                BatteryCapacityKWh = 100,
                InitialBatteryChargeKWh = 50,
                Equipment = new List<EquipmentDefinition>
                {
                    new EquipmentDefinition(EquipmentIds.OxygenGenerator, 40, 1),
                    new EquipmentDefinition(EquipmentIds.Co2Scrubber, 20, 2),
                    new EquipmentDefinition(EquipmentIds.WaterRecycler, 25, 3),
                    new EquipmentDefinition(EquipmentIds.IceProcessor, 30, 4, startsOn: false),
                },
                CrewCount = 2,
                OxygenConsumptionKgPerCrewPerCycle = 1.0,
                Co2ProductionKgPerCrewPerCycle = 1.5,
                OxygenTankCapacityKg = 20,
                InitialOxygenKg = 10,
                OxygenGeneratorOutputKgPerCycle = 3,
                Co2ScrubberCapacityKgPerCycle = 4,
                WaterConsumptionKgPerCrewPerCycle = 5.0,   // demanda total: 10 kg
                WaterRecoveryFraction = 0.9,
                WaterTreatmentCapacityKgPerCycle = 50,
                WaterTankCapacityKg = 100,
                InitialWaterKg = 50,
                WasteWaterTankCapacityKg = 100,
                IceProcessorCapacityKgPerCycle = 6,
                IceToWaterEfficiency = 0.5,
            };
        }

        static bool HasConsequence(CycleReport report, ConsequenceCode code, string equipmentId = null)
        {
            return report.Consequences.Any(c => c.Code == code && c.EquipmentId == equipmentId);
        }

        static List<ConsequenceCode> Codes(WaterReport report)
        {
            var list = new List<Consequence>();
            WaterSystem.AppendConsequences(report, list);
            return list.Select(c => c.Code).ToList();
        }

        // ---------- Água potável e residual ----------

        [Test]
        public void SemTratamento_AAguaUsadaViraAguaResidual()
        {
            WaterReport report = WaterSystem.ResolveCrewAndProcessing(50, 0, 0, false, false, WaterScenario());

            Assert.That(report.CrewConsumedKg, Is.EqualTo(10).Within(Tolerance));
            Assert.That(report.RecoveredKg, Is.EqualTo(0).Within(Tolerance));
            Assert.That(report.WasteAfterKg, Is.EqualTo(10).Within(Tolerance));
            Assert.That(report.AfterKg, Is.EqualTo(40).Within(Tolerance));
            Assert.That(Codes(report), Has.Member(ConsequenceCode.WaterNotRecycled));
        }

        [Test]
        public void ComTratamento_RecuperaAteACapacidadeComAEficienciaConfigurada()
        {
            WaterReport report = WaterSystem.ResolveCrewAndProcessing(50, 0, 0, true, false, WaterScenario());

            // residual 10; trata 8 (capacidade); recupera 8 × 0,9 = 7,2; sobram 2 de residual
            Assert.That(report.TreatedKg, Is.EqualTo(8).Within(Tolerance));
            Assert.That(report.RecoveredKg, Is.EqualTo(7.2).Within(Tolerance));
            Assert.That(report.WasteAfterKg, Is.EqualTo(2).Within(Tolerance));
            Assert.That(report.AfterKg, Is.EqualTo(47.2).Within(Tolerance));
        }

        [Test]
        public void AguaResidualAcumulada_PodeSerRecuperadaDepois()
        {
            WaterReport report = WaterSystem.ResolveCrewAndProcessing(50, 5, 0, true, false, WaterScenario());

            // residual 5 + 10 = 15; trata 8; sobram 7
            Assert.That(report.WasteAfterKg, Is.EqualTo(7).Within(Tolerance));
            Assert.That(report.WasteOverflowKg, Is.EqualTo(0).Within(Tolerance));
        }

        [Test]
        public void TanqueDeAguaResidualCheio_OExcedenteEhDescartado()
        {
            WaterReport report = WaterSystem.ResolveCrewAndProcessing(50, 12, 0, false, false, WaterScenario());

            // residual 12 + 10 = 22; cabem 15; 7 descartados
            Assert.That(report.WasteOverflowKg, Is.EqualTo(7).Within(Tolerance));
            Assert.That(report.WasteAfterKg, Is.EqualTo(15).Within(Tolerance));
            Assert.That(Codes(report), Has.Member(ConsequenceCode.WasteWaterOverflow));
        }

        [Test]
        public void ReservaInsuficiente_RegistraFaltaDeAgua()
        {
            WaterReport report = WaterSystem.ResolveCrewAndProcessing(4, 0, 0, true, false, WaterScenario());

            // consome os 4 kg que existem; faltam 6; trata os 4 e recupera 3,6
            Assert.That(report.CrewConsumedKg, Is.EqualTo(4).Within(Tolerance));
            Assert.That(report.CrewShortageKg, Is.EqualTo(6).Within(Tolerance));
            Assert.That(report.AfterKg, Is.EqualTo(3.6).Within(Tolerance));
            Assert.That(Codes(report), Has.Member(ConsequenceCode.WaterShortage));
        }

        // ---------- Gelo ----------

        [Test]
        public void EstacaoDeGelo_ConverteGeloEmAguaComAEficienciaConfigurada()
        {
            WaterReport report = WaterSystem.ResolveCrewAndProcessing(50, 0, 10, false, true, WaterScenario());

            // processa 6 kg (capacidade); 50% vira água: 3 kg; sobram 4 kg de gelo
            Assert.That(report.IceProcessedKg, Is.EqualTo(6).Within(Tolerance));
            Assert.That(report.IceWaterKg, Is.EqualTo(3).Within(Tolerance));
            Assert.That(report.IceAfterKg, Is.EqualTo(4).Within(Tolerance));
            Assert.That(report.AfterKg, Is.EqualTo(43).Within(Tolerance));
            Assert.That(Codes(report), Has.Member(ConsequenceCode.IceProcessed));
        }

        [Test]
        public void GeloEstocado_NaoViraAguaSemAEstacaoFuncionando()
        {
            WaterReport report = WaterSystem.ResolveCrewAndProcessing(50, 0, 10, false, false, WaterScenario());

            Assert.That(report.IceWaterKg, Is.EqualTo(0).Within(Tolerance));
            Assert.That(report.IceAfterKg, Is.EqualTo(10).Within(Tolerance));
            Assert.That(report.AfterKg, Is.EqualTo(40).Within(Tolerance));
        }

        [Test]
        public void EstacaoDeGeloSemEstoque_NaoProduzNadaEAvisa()
        {
            WaterReport report = WaterSystem.ResolveCrewAndProcessing(50, 0, 0, false, true, WaterScenario());

            Assert.That(report.IceWaterKg, Is.EqualTo(0).Within(Tolerance));
            Assert.That(Codes(report), Has.Member(ConsequenceCode.IceProcessorIdle));
        }

        [Test]
        public void ReservatorioCheio_LimitaTratamentoEGelo()
        {
            WaterReport report = WaterSystem.ResolveCrewAndProcessing(100, 0, 10, true, true, WaterScenario());

            // 100 - 10 = 90; tratamento devolve 7,2 -> 97,2; cabem só 2,8 kg de água de gelo -> 5,6 kg de gelo
            Assert.That(report.IceWaterKg, Is.EqualTo(2.8).Within(Tolerance));
            Assert.That(report.IceProcessedKg, Is.EqualTo(5.6).Within(Tolerance));
            Assert.That(report.IceAfterKg, Is.EqualTo(4.4).Within(Tolerance));
            Assert.That(report.AfterKg, Is.EqualTo(100).Within(Tolerance));
        }

        // ---------- Invariantes ----------

        [TestCase(50, 0, 0, false, false)]
        [TestCase(50, 0, 0, true, false)]
        [TestCase(50, 12, 0, false, false)]
        [TestCase(4, 3, 10, true, true)]
        [TestCase(100, 15, 30, true, true)]
        [TestCase(0, 0, 5, true, true)]
        public void Invariante_NenhumaAguaApareceOuSomeSemExplicacao(
            double potable, double waste, double ice, bool treatment, bool iceProcessor)
        {
            ScenarioParameters scenario = WaterScenario();

            WaterReport report = WaterSystem.ResolveCrewAndProcessing(potable, waste, ice, treatment, iceProcessor, scenario);

            Assert.That(report.AfterKg,
                Is.EqualTo(report.BeforeKg - report.CrewConsumedKg + report.RecoveredKg + report.IceWaterKg).Within(Tolerance));
            Assert.That(report.WasteAfterKg,
                Is.EqualTo(report.WasteBeforeKg + report.CrewConsumedKg - report.WasteOverflowKg - report.TreatedKg).Within(Tolerance));
            Assert.That(report.IceAfterKg, Is.EqualTo(report.IceBeforeKg - report.IceProcessedKg).Within(Tolerance));
            Assert.That(report.RecoveredKg, Is.LessThanOrEqualTo(report.TreatedKg + Tolerance));
            Assert.That(report.IceWaterKg, Is.LessThanOrEqualTo(report.IceProcessedKg + Tolerance));
            Assert.That(report.AfterKg, Is.InRange(0.0, scenario.WaterTankCapacityKg + Tolerance));
            Assert.That(report.WasteAfterKg, Is.InRange(0.0, scenario.WasteWaterTankCapacityKg + Tolerance));
        }

        [Test]
        public void UsoPeloGerador_EhDescontadoDoQueSobrou()
        {
            WaterReport report = WaterSystem.ResolveCrewAndProcessing(50, 0, 0, false, false, WaterScenario());

            WaterSystem.ApplyEquipmentUse(report, 3);

            Assert.That(report.UsedByOxygenGeneratorKg, Is.EqualTo(3).Within(Tolerance));
            Assert.That(report.AfterKg, Is.EqualTo(37).Within(Tolerance));
            Assert.Throws<ArgumentException>(() => WaterSystem.ApplyEquipmentUse(report, 1000));
        }

        // ---------- Interdependência (motor completo) ----------

        [Test]
        public void CicloNormal_OsTresSistemasFechamAConta()
        {
            var engine = new SimulationEngine(Scenario());

            CycleReport report = engine.AdvanceCycle();

            // Energia: 50 + 100 - 85 = 65
            Assert.That(report.Power.UnpoweredEquipmentIds, Is.Empty);
            Assert.That(engine.State.BatteryChargeKWh, Is.EqualTo(65).Within(Tolerance));
            // Água: 50 - 10 + 9 = 49, menos a eletrólise de 3 kg de O2; resta 1 kg de salmoura perdida
            Assert.That(report.Water.UsedByOxygenGeneratorKg, Is.EqualTo(WaterFor3KgOxygen).Within(Tolerance));
            Assert.That(engine.State.WaterKg, Is.EqualTo(49 - WaterFor3KgOxygen).Within(Tolerance));
            Assert.That(engine.State.WasteWaterKg, Is.EqualTo(0).Within(Tolerance));
            // O2: 10 + 3 - 2 = 11
            Assert.That(engine.State.OxygenKg, Is.EqualTo(11).Within(Tolerance));
            // CO2: 0 + 3 - 3 = 0
            Assert.That(engine.State.Co2Kg, Is.EqualTo(0).Within(Tolerance));
            Assert.That(HasConsequence(report, ConsequenceCode.OxygenProducedFromWater, EquipmentIds.OxygenGenerator), Is.True);
        }

        [Test]
        public void FaltaDeEnergia_ParaRemovedorETratamentoEAfetaArEAgua()
        {
            var engine = new SimulationEngine(Scenario());
            var semSol = new CycleModifiers { SolarGenerationFactor = 0 };

            CycleReport report = engine.AdvanceCycle(semSol);

            // disponível 50: gerador (40) funciona; removedor (20) e tratamento (25) não cabem
            Assert.That(report.Power.PoweredEquipmentIds, Is.EqualTo(new[] { EquipmentIds.OxygenGenerator }));
            Assert.That(HasConsequence(report, ConsequenceCode.EquipmentWithoutPower, EquipmentIds.Co2Scrubber), Is.True);
            Assert.That(HasConsequence(report, ConsequenceCode.EquipmentWithoutPower, EquipmentIds.WaterRecycler), Is.True);

            // Ar: CO2 acumula os 3 kg produzidos
            Assert.That(engine.State.Co2Kg, Is.EqualTo(3).Within(Tolerance));
            Assert.That(HasConsequence(report, ConsequenceCode.Co2NotRemoved, EquipmentIds.Co2Scrubber), Is.True);

            // Água: 50 - 10 (sem recuperação) - eletrólise; os 10 kg ficam como água residual
            Assert.That(report.Water.RecoveredKg, Is.EqualTo(0).Within(Tolerance));
            Assert.That(engine.State.WaterKg, Is.EqualTo(40 - WaterFor3KgOxygen).Within(Tolerance));
            Assert.That(engine.State.WasteWaterKg, Is.EqualTo(10).Within(Tolerance));
            Assert.That(HasConsequence(report, ConsequenceCode.WaterNotRecycled, EquipmentIds.WaterRecycler), Is.True);
        }

        [Test]
        public void GeradorDesligadoPeloJogador_PoupaAguaMasGastaAReservaDeO2()
        {
            var engine = new SimulationEngine(Scenario());
            engine.SetEquipmentOn(EquipmentIds.OxygenGenerator, false);

            CycleReport report = engine.AdvanceCycle();

            Assert.That(report.Water.UsedByOxygenGeneratorKg, Is.EqualTo(0).Within(Tolerance));
            Assert.That(engine.State.WaterKg, Is.EqualTo(49).Within(Tolerance));
            Assert.That(engine.State.OxygenKg, Is.EqualTo(8).Within(Tolerance));
            Assert.That(HasConsequence(report, ConsequenceCode.OxygenGeneratorNotRunning, EquipmentIds.OxygenGenerator), Is.True);
        }

        [Test]
        public void SemAgua_GeradorComEnergiaNaoProduzO2()
        {
            ScenarioParameters scenario = Scenario();
            scenario.InitialWaterKg = 0;
            var engine = new SimulationEngine(scenario);

            CycleReport report = engine.AdvanceCycle();

            Assert.That(report.Power.IsPowered(EquipmentIds.OxygenGenerator), Is.True);
            Assert.That(report.Air.OxygenProducedKg, Is.EqualTo(0).Within(Tolerance));
            Assert.That(engine.State.OxygenKg, Is.EqualTo(8).Within(Tolerance));
            Assert.That(HasConsequence(report, ConsequenceCode.OxygenGeneratorLimitedByWater, EquipmentIds.OxygenGenerator), Is.True);
            Assert.That(HasConsequence(report, ConsequenceCode.WaterShortage), Is.True);
        }

        [Test]
        public void GeloColetado_SoViraAguaComAEstacaoLigadaEComEnergia()
        {
            var engine = new SimulationEngine(Scenario());
            engine.AddIce(10);

            // Estação desligada: o gelo fica no estoque.
            engine.AdvanceCycle();
            Assert.That(engine.State.IceKg, Is.EqualTo(10).Within(Tolerance));

            // Estação ligada, com energia (65 + 100 >= 115): processa 6 kg e produz 3 kg de água.
            engine.SetEquipmentOn(EquipmentIds.IceProcessor, true);
            CycleReport comEnergia = engine.AdvanceCycle();
            Assert.That(comEnergia.Water.IceWaterKg, Is.EqualTo(3).Within(Tolerance));
            Assert.That(engine.State.IceKg, Is.EqualTo(4).Within(Tolerance));

            // Estação ligada, mas sem energia para ela: nada é produzido.
            CycleReport semEnergia = engine.AdvanceCycle(new CycleModifiers { SolarGenerationFactor = 0 });
            Assert.That(semEnergia.Power.IsPowered(EquipmentIds.IceProcessor), Is.False);
            Assert.That(semEnergia.Water.IceWaterKg, Is.EqualTo(0).Within(Tolerance));
            Assert.That(engine.State.IceKg, Is.EqualTo(4).Within(Tolerance));
        }

        [Test]
        public void PoeiraNosPaineis_ReduzAGeracaoESomaComATempestade()
        {
            var engine = new SimulationEngine(Scenario());
            engine.SetSolarPanelEfficiency(0.5);

            CycleReport soPoeira = engine.PreviewCycle();
            CycleReport poeiraETempestade = engine.PreviewCycle(new CycleModifiers { SolarGenerationFactor = 0.5 });

            Assert.That(soPoeira.Power.GeneratedKWh, Is.EqualTo(50).Within(Tolerance));
            Assert.That(HasConsequence(soPoeira, ConsequenceCode.SolarPanelsDusty), Is.True);
            Assert.That(HasConsequence(soPoeira, ConsequenceCode.SolarGenerationReduced), Is.False);
            Assert.That(poeiraETempestade.Power.GeneratedKWh, Is.EqualTo(25).Within(Tolerance));
            Assert.That(HasConsequence(poeiraETempestade, ConsequenceCode.SolarGenerationReduced), Is.True);
        }

        [Test]
        public void GastoImediatoDeEnergia_SoAconteceSeHouverCarga()
        {
            var engine = new SimulationEngine(Scenario());

            Assert.That(engine.TrySpendEnergy(20), Is.True);
            Assert.That(engine.State.BatteryChargeKWh, Is.EqualTo(30).Within(Tolerance));
            Assert.That(engine.TrySpendEnergy(100), Is.False);
            Assert.That(engine.State.BatteryChargeKWh, Is.EqualTo(30).Within(Tolerance));
        }

        [Test]
        public void PrevisaoDoCiclo_NaoAlteraOEstado()
        {
            var engine = new SimulationEngine(Scenario());

            CycleReport previsao = engine.PreviewCycle();

            Assert.That(previsao.Cycle, Is.EqualTo(1));
            Assert.That(previsao.Air.OxygenAfterKg, Is.EqualTo(11).Within(Tolerance));
            Assert.That(engine.State.Cycle, Is.EqualTo(1));
            Assert.That(engine.State.OxygenKg, Is.EqualTo(10).Within(Tolerance));
            Assert.That(engine.State.WaterKg, Is.EqualTo(50).Within(Tolerance));
            Assert.That(engine.State.WasteWaterKg, Is.EqualTo(0).Within(Tolerance));
            Assert.That(engine.State.BatteryChargeKWh, Is.EqualTo(50).Within(Tolerance));
        }

        [Test]
        public void CenarioDaMissao_UsaAsTaxasDocumentadasConvertidasParaHoraMarciana()
        {
            ScenarioParameters mvp = ProvisionalScenario.Create();
            // 1 hora marciana = 1/24 de sol = (88 775,244 s / 86 400 s) / 24 dias terrestres
            const double marsHourInEarthDays = 88775.244 / 86400.0 / 24.0;
            const double marsHourInEarthHours = 88775.244 / 86400.0;

            // BVAD (NASA/TP-2015-218570/REV1), Tabela 3-33, por tripulante por dia terrestre
            Assert.That(mvp.OxygenConsumptionKgPerCrewPerCycle, Is.EqualTo(0.816 * marsHourInEarthDays).Within(1e-12));
            Assert.That(mvp.Co2ProductionKgPerCrewPerCycle, Is.EqualTo(1.04 * marsHourInEarthDays).Within(1e-12));
            Assert.That(mvp.WaterConsumptionKgPerCrewPerCycle, Is.EqualTo(2.5 * marsHourInEarthDays).Within(1e-12));
            // BVAD Rev 2, Tabela 4-70 e seção 4.6.2: EVA gasta 0,086 kg/h de O2 e dura no máximo 8 h
            Assert.That(mvp.SuitOxygenUseKgPerCycle, Is.EqualTo(0.086 * marsHourInEarthHours).Within(1e-12));
            Assert.That(mvp.SuitOxygenCapacityKg, Is.EqualTo(0.086 * 8).Within(1e-12));
            // Regras de voo da ISS citadas no BVAD, seção 4.1
            Assert.That(mvp.Co2AlertMmHg, Is.EqualTo(4.0));
            Assert.That(mvp.Co2ActionMmHg, Is.EqualTo(7.6));
            Assert.That(mvp.Co2EvacuationMmHg, Is.EqualTo(10.0));
            // A partida começa com o CO2 no nível nominal
            Assert.That(mvp.Co2PressureMmHg(mvp.InitialCo2Kg), Is.EqualTo(mvp.Co2NominalMmHg).Within(1e-9));
        }
    }
}
