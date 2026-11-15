using System.Collections.Generic;
using System.Linq;
using MissaoMarte.Events;
using MissaoMarte.Game;
using MissaoMarte.Simulation;
using NUnit.Framework;

namespace MissaoMarte.Tests
{
    /// <summary>Testes das regras da partida em tempo real e das regras novas do motor (desgaste, vazamento, traje).</summary>
    public class HabitatMissionTests
    {
        const double Tolerance = 1e-9;

        /// <summary>
        /// Cenário pequeno por hora: 1 tripulante; consome 0,1 kg de O2, 0,2 kg de água e produz 0,1 kg de CO2
        /// por hora; geração de pico 10 kW; habitat de 1000 m³ a 300 K.
        /// </summary>
        static ScenarioParameters Scenario()
        {
            return new ScenarioParameters
            {
                SolarGenerationKWhPerCycle = 10,
                BatteryCapacityKWh = 20,
                InitialBatteryChargeKWh = 20,
                Equipment = new List<EquipmentDefinition>
                {
                    new EquipmentDefinition(EquipmentIds.Co2Scrubber, 1, 1),
                    new EquipmentDefinition(EquipmentIds.OxygenGenerator, 1, 2),
                    new EquipmentDefinition(EquipmentIds.WaterRecycler, 1, 3),
                    new EquipmentDefinition(EquipmentIds.IceProcessor, 1, 4, startsOn: false),
                },
                CrewCount = 1,
                OxygenConsumptionKgPerCrewPerCycle = 0.1,
                Co2ProductionKgPerCrewPerCycle = 0.1,
                OxygenTankCapacityKg = 10,
                InitialOxygenKg = 5,
                OxygenMinimumKg = 1,
                OxygenGeneratorOutputKgPerCycle = 0.1,
                Co2ScrubberCapacityKgPerCycle = 0.5,
                CabinLeakOxygenKgPerCycleAtBrokenSeal = 1.0,
                HabitatVolumeM3 = 1000,
                CabinTemperatureK = 300,
                Co2AlertMmHg = 4,
                Co2ActionMmHg = 7.6,
                Co2EvacuationMmHg = 10,
                SuitOxygenCapacityKg = 0.8,
                SuitOxygenUseKgPerCycle = 0.1,
                WaterConsumptionKgPerCrewPerCycle = 0.2,
                WaterRecoveryFraction = 1.0,
                WaterTreatmentCapacityKgPerCycle = 1,
                WaterTankCapacityKg = 50,
                InitialWaterKg = 20,
                WasteWaterTankCapacityKg = 10,
                IceProcessorCapacityKgPerCycle = 2,
                IceToWaterEfficiency = 0.5,
            };
        }

        static HabitatMission Mission(EventTimeline timeline = null, ScenarioParameters scenario = null, int sols = 2)
        {
            var config = new MissionConfig { MissionSols = sols, StartHour = 6, IceWaterObjectiveKg = 2 };
            var deposits = new[] { new IceDeposit("real", 20), new IceDeposit("falso", 0) };
            return new HabitatMission(scenario ?? Scenario(), timeline, config, deposits, 1);
        }

        // ---------- Motor: desgaste, vazamento e traje ----------

        [Test]
        public void EquipamentoQuebrado_NaoFuncionaNemConsomeEnergia()
        {
            var engine = new SimulationEngine(Scenario());
            engine.SetEquipmentCondition(EquipmentIds.Co2Scrubber, 0);

            CycleReport report = engine.AdvanceCycle(new CycleModifiers { SolarGenerationFactor = 0 });

            Assert.That(report.Power.IsPowered(EquipmentIds.Co2Scrubber), Is.False);
            Assert.That(report.Power.UnpoweredEquipmentIds, Has.No.Member(EquipmentIds.Co2Scrubber));
            Assert.That(report.Power.RequestedKWh, Is.EqualTo(2).Within(Tolerance));
            Assert.That(report.Air.Co2RemovedKg, Is.EqualTo(0).Within(Tolerance));
        }

        [Test]
        public void EquipamentoDesgastado_RendeProporcionalmenteMenos()
        {
            var engine = new SimulationEngine(Scenario());
            engine.SetEquipmentCondition(EquipmentIds.OxygenGenerator, 0.5);

            CycleReport report = engine.AdvanceCycle();

            Assert.That(report.Air.OxygenProducedKg, Is.EqualTo(0.05).Within(Tolerance));
        }

        [Test]
        public void VedacaoDanificada_VazaO2NaProporcaoDoDano()
        {
            var engine = new SimulationEngine(Scenario());
            engine.SetSealCondition(0.75);

            CycleReport report = engine.AdvanceCycle();

            // 5 + 0,1 produzido - 0,1 consumido - 0,25 vazado = 4,75
            Assert.That(report.Air.OxygenLeakedKg, Is.EqualTo(0.25).Within(Tolerance));
            Assert.That(engine.State.OxygenKg, Is.EqualTo(4.75).Within(Tolerance));
            Assert.That(report.Consequences.Any(c => c.Code == ConsequenceCode.OxygenLeak), Is.True);
        }

        [Test]
        public void Traje_GastaForaERecarregaComO2DaBase()
        {
            var engine = new SimulationEngine(Scenario());

            double missing = engine.UseSuitOxygen(3);
            Assert.That(missing, Is.EqualTo(0).Within(Tolerance));
            Assert.That(engine.State.SuitOxygenKg, Is.EqualTo(0.5).Within(Tolerance));

            double moved = engine.RefillSuit(1.0);
            Assert.That(moved, Is.EqualTo(0.3).Within(Tolerance));
            Assert.That(engine.State.SuitOxygenKg, Is.EqualTo(0.8).Within(Tolerance));
            Assert.That(engine.State.OxygenKg, Is.EqualTo(4.7).Within(Tolerance));

            Assert.That(engine.UseSuitOxygen(10), Is.EqualTo(0.2).Within(Tolerance));
            Assert.That(engine.State.SuitOxygenKg, Is.EqualTo(0).Within(Tolerance));
        }

        // ---------- Tempo ----------

        [Test]
        public void Tempo_AvancaUmaHoraDeSimulacaoPorHoraDeJogo()
        {
            HabitatMission mission = Mission();

            mission.Update(0.5, false);
            Assert.That(mission.HoursPlayed, Is.EqualTo(0));
            Assert.That(mission.HourOfDay, Is.EqualTo(6.5).Within(Tolerance));

            mission.Update(2.0, false);
            Assert.That(mission.HoursPlayed, Is.EqualTo(2));
            Assert.That(mission.HourOfDay, Is.EqualTo(8.5).Within(Tolerance));
            Assert.That(mission.Sol, Is.EqualTo(1));
        }

        [Test]
        public void Tempo_CicloDoMotorCorrespondeASolEHora()
        {
            Assert.That(HabitatMission.CycleAt(1, 6, 6), Is.EqualTo(1));
            Assert.That(HabitatMission.CycleAt(1, 23, 6), Is.EqualTo(18));
            Assert.That(HabitatMission.CycleAt(2, 0, 6), Is.EqualTo(19));
        }

        [Test]
        public void Luz_EhZeroANoiteEMaximaAoMeioDia()
        {
            HabitatMission mission = Mission();

            Assert.That(mission.Daylight(3), Is.EqualTo(0));
            Assert.That(mission.Daylight(20), Is.EqualTo(0));
            Assert.That(mission.Daylight(12), Is.EqualTo(1).Within(Tolerance));
            Assert.That(mission.Daylight(9), Is.EqualTo(System.Math.Sin(System.Math.PI / 4)).Within(Tolerance));
        }

        [Test]
        public void Tempestade_ReduzAGeracaoESujaOsPaineis()
        {
            var storm = new DustStorm(HabitatMission.CycleAt(1, 11, 6), 2, 0.25, 0.1);
            HabitatMission mission = Mission(new EventTimeline(new[] { storm }));

            mission.Update(5, false); // 06h a 11h, céu limpo
            Assert.That(mission.State.SolarPanelEfficiency, Is.EqualTo(1));
            mission.Update(1, false); // 11h: tempestade
            Assert.That(mission.LastReport.Power.SolarFactor, Is.EqualTo(mission.Daylight(11.5) * 0.25).Within(Tolerance));
            Assert.That(mission.State.SolarPanelEfficiency, Is.EqualTo(0.9).Within(Tolerance));

            mission.Update(2, false);
            Assert.That(mission.State.SolarPanelEfficiency, Is.EqualTo(0.8).Within(Tolerance));
            Assert.That(mission.CleanPanels().Success, Is.True);
            Assert.That(mission.State.SolarPanelEfficiency, Is.EqualTo(1));
        }

        // ---------- Imprevistos e consertos ----------

        [Test]
        public void Imprevisto_AconteceNaHoraMarcadaEOConsertoRestaura()
        {
            var failure = new EquipmentFailure(HabitatMission.CycleAt(1, 8, 6), EquipmentIds.WaterRecycler, 0.3, "Filtro saturado.");
            HabitatMission mission = Mission(new EventTimeline(null, new[] { failure }));

            mission.Update(2, false);
            Assert.That(mission.NeedsRepair(EquipmentIds.WaterRecycler), Is.False);
            mission.Update(1, false);
            Assert.That(mission.ConditionOf(EquipmentIds.WaterRecycler), Is.EqualTo(0.3).Within(Tolerance));
            Assert.That(mission.Log.Any(n => n.Kind == NoticeKind.Critical && n.Text.Contains("Filtro saturado.")), Is.True);

            Assert.That(mission.Repair(EquipmentIds.WaterRecycler).Success, Is.True);
            Assert.That(mission.ConditionOf(EquipmentIds.WaterRecycler), Is.EqualTo(1));
            Assert.That(mission.Repair(EquipmentIds.WaterRecycler).Success, Is.False);
        }

        [Test]
        public void Vedacao_PodeQuebrarEServirComoAlvoDeConserto()
        {
            var failure = new EquipmentFailure(1, EquipmentFailure.SealTarget, 0.5, "Vazamento.");
            HabitatMission mission = Mission(new EventTimeline(null, new[] { failure }));

            mission.Update(1, false);
            Assert.That(mission.State.SealCondition, Is.EqualTo(0.5).Within(Tolerance));
            Assert.That(mission.LastReport.Air.OxygenLeakedKg, Is.EqualTo(0.5).Within(Tolerance));

            mission.Repair(EquipmentFailure.SealTarget);
            Assert.That(mission.State.SealCondition, Is.EqualTo(1));
        }

        // ---------- Gelo ----------

        [Test]
        public void Gelo_DescobrirColetarEntregarEProcessar()
        {
            HabitatMission mission = Mission();

            Assert.That(mission.CanCollectIce("real", out _), Is.False, "antes de descobrir");
            mission.DiscoverDeposit("real");
            Assert.That(mission.IceFound, Is.True);

            Assert.That(mission.CollectIce("real").Success, Is.True);
            Assert.That(mission.CarriedIceKg, Is.EqualTo(10).Within(Tolerance));
            Assert.That(mission.FindDeposit("real").RemainingKg, Is.EqualTo(10).Within(Tolerance));
            Assert.That(mission.CollectIce("real").Success, Is.False, "carga cheia");

            mission.DeliverIce();
            Assert.That(mission.CarriedIceKg, Is.EqualTo(0));
            Assert.That(mission.State.IceKg, Is.EqualTo(10).Within(Tolerance));

            mission.ToggleEquipment(EquipmentIds.IceProcessor);
            mission.Update(1, false);
            // 2 kg de gelo processados × 50% = 1 kg de água
            Assert.That(mission.IceWaterProducedKg, Is.EqualTo(1).Within(Tolerance));
            Assert.That(mission.State.IceKg, Is.EqualTo(8).Within(Tolerance));
        }

        [Test]
        public void Gelo_AlarmeFalsoNaoContaComoDescoberta()
        {
            HabitatMission mission = Mission();

            mission.DiscoverDeposit("falso");

            Assert.That(mission.IceFound, Is.False);
            Assert.That(mission.CanCollectIce("falso", out _), Is.False);
        }

        // ---------- Amostras ----------

        [Test]
        public void Amostras_ColetarUmaVezEAnalisarNoLaboratorio()
        {
            HabitatMission mission = Mission();

            Assert.That(mission.CollectSample("a").Success, Is.True);
            Assert.That(mission.CollectSample("a").Success, Is.False);
            Assert.That(mission.SamplesCarried, Is.EqualTo(1));

            mission.AnalyzeSamples();
            Assert.That(mission.SamplesCarried, Is.EqualTo(0));
            Assert.That(mission.SamplesAnalyzed, Is.EqualTo(1));
            Assert.That(mission.Log.Count(n => n.Kind == NoticeKind.Discovery), Is.EqualTo(1));
        }

        [Test]
        public void Amostras_CadaAmostraAnalisadaRevelaUmaDescobertaDiferente()
        {
            HabitatMission mission = Mission();
            mission.CollectSample("a");
            mission.CollectSample("b");

            mission.AnalyzeSamples();

            var discoveries = mission.Log.Where(n => n.Kind == NoticeKind.Discovery).Select(n => n.Text).ToList();
            Assert.That(discoveries.Count, Is.EqualTo(2));
            Assert.That(discoveries[0], Is.Not.EqualTo(discoveries[1]));
            Assert.That(mission.Log.Any(n => n.Kind == NoticeKind.Lesson && n.Text.Contains("Marte é")), Is.False);
        }

        // ---------- Vitória e derrota ----------

        [Test]
        public void Derrota_TrajeSemOxigenioLaFora()
        {
            HabitatMission mission = Mission();

            mission.Update(7.9, true);
            Assert.That(mission.IsFinished, Is.False);
            mission.Update(0.2, true);

            Assert.That(mission.Status, Is.EqualTo(MissionStatus.Defeat));
            Assert.That(mission.DefeatReason, Is.EqualTo(DefeatReason.SuitOxygenDepleted));
        }

        [Test]
        public void Derrota_OxigenioAbaixoDoLimiteOperacional()
        {
            ScenarioParameters scenario = Scenario();
            scenario.InitialOxygenKg = 1.25;
            HabitatMission mission = Mission(scenario: scenario);
            mission.ToggleEquipment(EquipmentIds.OxygenGenerator);

            mission.Update(2, false);
            Assert.That(mission.IsFinished, Is.False);
            mission.Update(1, false);

            Assert.That(mission.DefeatReason, Is.EqualTo(DefeatReason.OxygenDepleted));
        }

        [Test]
        public void Derrota_CO2ToxicoSemSuporteAtmosferico()
        {
            ScenarioParameters scenario = Scenario();
            // 1000 m³ a 300 K: 1 mmHg ≈ 2,352 kg; 10 mmHg ≈ 23,52 kg. Começa em 23,35 kg; +0,1 kg por hora.
            scenario.InitialCo2Kg = 23.35;
            HabitatMission mission = Mission(scenario: scenario);
            mission.ToggleEquipment(EquipmentIds.Co2Scrubber);

            mission.Update(1, false);
            Assert.That(mission.IsFinished, Is.False);
            mission.Update(1, false);

            Assert.That(mission.DefeatReason, Is.EqualTo(DefeatReason.Co2Toxic));
        }

        [Test]
        public void Derrota_FalhaDeEnergiaNoSuporteAVidaPorTresHoras()
        {
            ScenarioParameters scenario = Scenario();
            scenario.InitialBatteryChargeKWh = 0;
            scenario.SolarGenerationKWhPerCycle = 0;
            HabitatMission mission = Mission(scenario: scenario);

            mission.Update(2, false);
            Assert.That(mission.IsFinished, Is.False);
            mission.Update(1, false);

            Assert.That(mission.DefeatReason, Is.EqualTo(DefeatReason.EnergyFailure));
        }

        [Test]
        public void Derrota_SobreviverSemCumprirOsObjetivos()
        {
            HabitatMission mission = Mission(sols: 1);

            while (!mission.IsFinished)
                mission.Update(1, false);

            Assert.That(mission.HoursPlayed, Is.EqualTo(18));
            Assert.That(mission.DefeatReason, Is.EqualTo(DefeatReason.ObjectivesNotMet));
        }

        [Test]
        public void Vitoria_SobreviverEProduzirAguaDoGelo()
        {
            HabitatMission mission = Mission(sols: 1);
            mission.DiscoverDeposit("real");
            mission.CollectIce("real");
            mission.DeliverIce();
            mission.ToggleEquipment(EquipmentIds.IceProcessor);

            while (!mission.IsFinished)
                mission.Update(1, false);

            Assert.That(mission.Status, Is.EqualTo(MissionStatus.Victory));
            Assert.That(mission.IceWaterProducedKg, Is.EqualTo(5).Within(Tolerance));
            Assert.That(mission.HourOfDay, Is.EqualTo(24));
        }

        [Test]
        public void DepoisDoFim_AcoesSaoRecusadas()
        {
            HabitatMission mission = Mission();
            mission.Update(8.5, true);
            Assert.That(mission.IsFinished, Is.True);

            Assert.That(mission.ToggleEquipment(EquipmentIds.Co2Scrubber).Success, Is.False);
            Assert.That(mission.CleanPanels().Success, Is.False);
            int hours = mission.HoursPlayed;
            mission.Update(5, false);
            Assert.That(mission.HoursPlayed, Is.EqualTo(hours));
        }

        // ---------- Missão do MVP ----------
        // Dependem dos valores didáticos de propósito: avisam se o balanceamento ficar impossível ou trivial.

        [Test]
        public void PrimeiraFonte_NaoFazerNadaLevaADerrota()
        {
            HabitatMission mission = PrimeiraFonte.Create();

            while (!mission.IsFinished)
                mission.Update(1, false);

            Assert.That(mission.Status, Is.EqualTo(MissionStatus.Defeat));
        }

        [Test]
        public void PrimeiraFonte_ConsertarGerenciarEnergiaEBuscarGeloLevaAVitoria()
        {
            HabitatMission mission = PrimeiraFonte.Create();
            string[] targets = { EquipmentIds.WaterRecycler, EquipmentIds.Co2Scrubber, EquipmentIds.OxygenGenerator, EquipmentFailure.SealTarget };
            int trips = 0;

            while (!mission.IsFinished)
            {
                foreach (string target in targets)
                {
                    if (mission.NeedsRepair(target))
                        mission.Repair(target);
                }

                if (mission.HoursPlayed == 3)
                    mission.DiscoverDeposit(PrimeiraFonte.DepositEast);
                if (mission.HoursPlayed >= 4 && trips < 3 && mission.HoursPlayed % 6 == 4 && mission.ActiveStorm == null)
                {
                    mission.CollectIce(PrimeiraFonte.DepositEast);
                    mission.DeliverIce();
                    trips++;
                    mission.Update(0.5, true);
                }

                bool save = mission.ActiveStorm != null || mission.State.BatteryChargeKWh < 8;
                bool o2Low = mission.State.OxygenKg < mission.Parameters.OxygenMinimumKg + 2;
                bool waterLow = mission.State.WaterKg < 8 || mission.State.WasteWaterKg > 14;
                mission.Engine.SetEquipmentOn(EquipmentIds.Infrastructure, !save);
                mission.Engine.SetEquipmentOn(EquipmentIds.OxygenGenerator, !save || o2Low);
                mission.Engine.SetEquipmentOn(EquipmentIds.WaterRecycler, !save || waterLow);
                bool sunny = mission.CurrentSunlight > 0.25 && mission.State.BatteryChargeKWh > 10;
                mission.Engine.SetEquipmentOn(EquipmentIds.IceProcessor, mission.State.IceKg > 0 && sunny && mission.ActiveStorm == null);
                if (mission.PanelsDirty && mission.ActiveStorm == null)
                    mission.CleanPanels();

                mission.Update(1, false);
            }

            Assert.That(mission.Status, Is.EqualTo(MissionStatus.Victory), mission.DefeatReason.ToString());
        }
    }
}
