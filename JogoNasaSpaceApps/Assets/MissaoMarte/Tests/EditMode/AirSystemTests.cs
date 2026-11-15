using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MissaoMarte.Simulation.Tests
{
    /// <summary>
    /// Testes do cálculo de ar. Números próprios, fáceis de conferir:
    /// 2 tripulantes; cada um consome 1 kg de O2 e produz 1,5 kg de CO2 por ciclo.
    /// </summary>
    public class AirSystemTests
    {
        const double Tolerance = 1e-9;

        static ScenarioParameters Scenario()
        {
            return new ScenarioParameters
            {
                CrewCount = 2,
                OxygenConsumptionKgPerCrewPerCycle = 1.0,   // demanda total: 2 kg
                Co2ProductionKgPerCrewPerCycle = 1.5,       // produção total: 3 kg
                OxygenTankCapacityKg = 20,
                OxygenGeneratorOutputKgPerCycle = 3,
                Co2ScrubberCapacityKgPerCycle = 4,
            };
        }

        static ScenarioParameters ScenarioWithCabin()
        {
            ScenarioParameters scenario = Scenario();
            scenario.HabitatVolumeM3 = 1000;
            scenario.CabinTemperatureK = 300;
            scenario.Co2NominalMmHg = 2;
            scenario.Co2AlertMmHg = 4;
            scenario.Co2ActionMmHg = 7.6;
            scenario.Co2EvacuationMmHg = 15;
            return scenario;
        }

        static List<Consequence> Consequences(AirReport report, ScenarioParameters scenario)
        {
            var list = new List<Consequence>();
            AirSystem.AppendConsequences(report, scenario, list);
            return list;
        }

        // ---------- Oxigênio ----------

        [Test]
        public void SemGerador_TripulacaoConsomeDaReserva()
        {
            AirReport report = AirSystem.Resolve(10, 0, 100, false, false, Scenario());

            Assert.That(report.OxygenProducedKg, Is.EqualTo(0).Within(Tolerance));
            Assert.That(report.OxygenConsumedKg, Is.EqualTo(2).Within(Tolerance));
            Assert.That(report.OxygenAfterKg, Is.EqualTo(8).Within(Tolerance));
            Assert.That(report.WaterUsedKg, Is.EqualTo(0).Within(Tolerance));
        }

        [Test]
        public void ComGerador_ProduzO2EConsomeAguaNaProporcaoDaEletrolise()
        {
            AirReport report = AirSystem.Resolve(10, 0, 100, true, false, Scenario());

            // 10 + 3 - 2 = 11; água: 3 kg de O2 × (2 × 18,015 / 31,998) ≈ 3,378 kg
            Assert.That(report.OxygenProducedKg, Is.EqualTo(3).Within(Tolerance));
            Assert.That(report.OxygenAfterKg, Is.EqualTo(11).Within(Tolerance));
            Assert.That(report.WaterUsedKg, Is.EqualTo(3.378).Within(0.001));
            Assert.That(report.GeneratorLimitedByWater, Is.False);
        }

        [Test]
        public void GeradorComPoucaAgua_ProduzSoOQueAAguaPermite()
        {
            AirReport report = AirSystem.Resolve(10, 0, 2.0, true, false, Scenario());

            // 2 kg de água rendem 2 / 1,126 ≈ 1,776 kg de O2
            Assert.That(report.WaterUsedKg, Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(report.OxygenProducedKg, Is.EqualTo(1.776).Within(0.001));
            Assert.That(report.GeneratorLimitedByWater, Is.True);
            Assert.That(Consequences(report, Scenario()).Any(c => c.Code == ConsequenceCode.OxygenGeneratorLimitedByWater), Is.True);
        }

        [Test]
        public void GeradorSemAgua_NaoProduzNada()
        {
            AirReport report = AirSystem.Resolve(10, 0, 0, true, false, Scenario());

            Assert.That(report.OxygenProducedKg, Is.EqualTo(0).Within(Tolerance));
            Assert.That(report.WaterUsedKg, Is.EqualTo(0).Within(Tolerance));
            Assert.That(report.OxygenAfterKg, Is.EqualTo(8).Within(Tolerance));
            Assert.That(report.GeneratorLimitedByWater, Is.True);
        }

        [Test]
        public void ReservatorioCheio_GeradorSoRepoeOConsumoDoCiclo()
        {
            AirReport report = AirSystem.Resolve(20, 0, 100, true, false, Scenario());

            // cabe apenas o que a tripulação consome no ciclo: 2 kg
            Assert.That(report.OxygenProducedKg, Is.EqualTo(2).Within(Tolerance));
            Assert.That(report.OxygenAfterKg, Is.EqualTo(20).Within(Tolerance));
            Assert.That(report.GeneratorLimitedByWater, Is.False);
        }

        [Test]
        public void ReservaInsuficiente_RegistraFaltaDeO2()
        {
            AirReport report = AirSystem.Resolve(0.5, 0, 100, false, false, Scenario());

            Assert.That(report.OxygenConsumedKg, Is.EqualTo(0.5).Within(Tolerance));
            Assert.That(report.OxygenShortageKg, Is.EqualTo(1.5).Within(Tolerance));
            Assert.That(report.OxygenAfterKg, Is.EqualTo(0).Within(Tolerance));

            List<Consequence> consequences = Consequences(report, Scenario());
            Assert.That(consequences.Any(c => c.Code == ConsequenceCode.OxygenShortage), Is.True);
            Assert.That(consequences.Any(c => c.Code == ConsequenceCode.OxygenGeneratorNotRunning), Is.True);
        }

        [TestCase(10, 100, true)]
        [TestCase(10, 2, true)]
        [TestCase(20, 100, true)]
        [TestCase(0.5, 100, false)]
        [TestCase(0, 0, true)]
        public void Invariante_MassaDeO2SeConserva(double oxygen, double water, bool generator)
        {
            AirReport report = AirSystem.Resolve(oxygen, 0, water, generator, false, Scenario());

            Assert.That(report.OxygenAfterKg, Is.EqualTo(report.OxygenBeforeKg + report.OxygenProducedKg - report.OxygenConsumedKg).Within(Tolerance));
            Assert.That(report.OxygenConsumedKg + report.OxygenShortageKg, Is.EqualTo(report.OxygenDemandKg).Within(Tolerance));
            Assert.That(report.OxygenAfterKg, Is.InRange(0.0, 20.0));
            Assert.That(report.WaterUsedKg, Is.LessThanOrEqualTo(water + Tolerance));
        }

        // ---------- CO2 ----------

        [Test]
        public void SemRemovedor_CO2Acumula()
        {
            AirReport report = AirSystem.Resolve(10, 1, 0, false, false, Scenario());

            // 1 + 3 = 4
            Assert.That(report.Co2ProducedKg, Is.EqualTo(3).Within(Tolerance));
            Assert.That(report.Co2RemovedKg, Is.EqualTo(0).Within(Tolerance));
            Assert.That(report.Co2AfterKg, Is.EqualTo(4).Within(Tolerance));
            Assert.That(Consequences(report, Scenario()).Any(c => c.Code == ConsequenceCode.Co2NotRemoved), Is.True);
        }

        [Test]
        public void ComRemovedor_RemoveAteACapacidade()
        {
            AirReport report = AirSystem.Resolve(10, 5, 0, false, true, Scenario());

            // 5 + 3 = 8; capacidade 4 -> sobram 4
            Assert.That(report.Co2RemovedKg, Is.EqualTo(4).Within(Tolerance));
            Assert.That(report.Co2AfterKg, Is.EqualTo(4).Within(Tolerance));
        }

        [Test]
        public void Removedor_NaoReduzAbaixoDoNivelNominal()
        {
            ScenarioParameters scenario = ScenarioWithCabin();
            double nominalKg = 2 * scenario.Co2KgPerMmHg;

            AirReport report = AirSystem.Resolve(10, nominalKg, 0, false, true, scenario);

            // produz 3 kg, capacidade 4: remove só os 3 kg que estão acima do nominal
            Assert.That(report.Co2RemovedKg, Is.EqualTo(3).Within(Tolerance));
            Assert.That(report.Co2AfterKg, Is.EqualTo(nominalKg).Within(Tolerance));
            Assert.That(report.Co2PressureAfterMmHg, Is.EqualTo(2).Within(1e-6));
        }

        [Test]
        public void ConversaoParaPressaoParcial_SegueALeiDosGasesIdeais()
        {
            // Checagem independente: a 0 °C e 760 mmHg, 1 mol de gás ideal ocupa 22,414 L.
            // Logo 1 m³ de CO2 puro tem 1000 / 22,414 mol × 44,009 g/mol ≈ 1,9635 kg.
            var scenario = new ScenarioParameters { HabitatVolumeM3 = 1, CabinTemperatureK = 273.15 };

            Assert.That(760 * scenario.Co2KgPerMmHg, Is.EqualTo(1.9635).Within(0.001));
            Assert.That(scenario.Co2PressureMmHg(1.9635), Is.EqualTo(760).Within(0.5));
        }

        [Test]
        public void SemVolumeDefinido_PressaoParcialEhZero()
        {
            Assert.That(Scenario().Co2PressureMmHg(5), Is.EqualTo(0));
        }

        [Test]
        public void Consequencias_AlertaEAcaoSeguemOsLimitesDePressao()
        {
            ScenarioParameters scenario = ScenarioWithCabin();
            double kgPerMmHg = scenario.Co2KgPerMmHg;

            // termina em 5 mmHg: acima do alerta (4), abaixo da ação (7,6)
            AirReport alerta = AirSystem.Resolve(10, 5 * kgPerMmHg - 3, 0, false, false, scenario);
            // termina em 9 mmHg: acima da ação
            AirReport acao = AirSystem.Resolve(10, 9 * kgPerMmHg - 3, 0, false, false, scenario);
            // termina em 3 mmHg: abaixo do alerta
            AirReport normal = AirSystem.Resolve(10, 3 * kgPerMmHg - 3, 0, false, false, scenario);

            Assert.That(alerta.Co2PressureAfterMmHg, Is.EqualTo(5).Within(1e-6));
            Assert.That(Consequences(alerta, scenario).Select(c => c.Code), Has.Member(ConsequenceCode.Co2AboveAlertLevel));
            Assert.That(Consequences(alerta, scenario).Select(c => c.Code), Has.No.Member(ConsequenceCode.Co2AboveActionLevel));

            Assert.That(Consequences(acao, scenario).Select(c => c.Code), Has.Member(ConsequenceCode.Co2AboveActionLevel));
            Assert.That(Consequences(acao, scenario).Select(c => c.Code), Has.No.Member(ConsequenceCode.Co2AboveAlertLevel));

            Assert.That(Consequences(normal, scenario).Select(c => c.Code), Has.No.Member(ConsequenceCode.Co2AboveAlertLevel));
            Assert.That(Consequences(normal, scenario).Select(c => c.Code), Has.No.Member(ConsequenceCode.Co2AboveActionLevel));
        }
    }
}
