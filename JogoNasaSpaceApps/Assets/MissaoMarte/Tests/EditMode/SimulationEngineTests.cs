using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MissaoMarte.Simulation.Tests
{
    /// <summary>Testes do motor: estado entre ciclos, decisões do jogador e consequências.</summary>
    public class SimulationEngineTests
    {
        const double Tolerance = 1e-9;

        /// <summary>Geração 100, bateria 100/100, "a" consome 60 (prioridade 1), "b" consome 30 (prioridade 2).</summary>
        static ScenarioParameters TestScenario()
        {
            return new ScenarioParameters
            {
                SolarGenerationKWhPerCycle = 100,
                BatteryCapacityKWh = 100,
                InitialBatteryChargeKWh = 100,
                Equipment = new List<EquipmentDefinition>
                {
                    new EquipmentDefinition("a", 60, 1),
                    new EquipmentDefinition("b", 30, 2),
                },
            };
        }

        static bool Has(CycleReport report, ConsequenceCode code, string equipmentId = null)
        {
            return report.Consequences.Any(c => c.Code == code && c.EquipmentId == equipmentId);
        }

        [Test]
        public void EstadoInicial_VemDoCenario()
        {
            ScenarioParameters scenario = TestScenario();
            scenario.Equipment = new List<EquipmentDefinition>
            {
                new EquipmentDefinition("a", 60, 1, startsOn: true),
                new EquipmentDefinition("b", 30, 2, startsOn: false),
            };

            var engine = new SimulationEngine(scenario);

            Assert.That(engine.State.Cycle, Is.EqualTo(1));
            Assert.That(engine.State.BatteryChargeKWh, Is.EqualTo(100).Within(Tolerance));
            Assert.That(engine.State.FindEquipment("a").IsOn, Is.True);
            Assert.That(engine.State.FindEquipment("b").IsOn, Is.False);
        }

        [Test]
        public void CenarioInvalido_EhRejeitado()
        {
            ScenarioParameters cargaMaiorQueCapacidade = TestScenario();
            cargaMaiorQueCapacidade.InitialBatteryChargeKWh = 150;

            ScenarioParameters idDuplicado = TestScenario();
            idDuplicado.Equipment = new List<EquipmentDefinition>
            {
                new EquipmentDefinition("a", 10, 1),
                new EquipmentDefinition("a", 20, 2),
            };

            Assert.Throws<ArgumentException>(() => new SimulationEngine(cargaMaiorQueCapacidade));
            Assert.Throws<ArgumentException>(() => new SimulationEngine(idDuplicado));
        }

        [Test]
        public void AvancarCiclo_AtualizaCicloEBateria()
        {
            ScenarioParameters scenario = TestScenario();
            scenario.InitialBatteryChargeKWh = 40;
            var engine = new SimulationEngine(scenario);

            CycleReport report = engine.AdvanceCycle();

            // 40 + 100 - 90 = 50
            Assert.That(report.Cycle, Is.EqualTo(1));
            Assert.That(engine.State.Cycle, Is.EqualTo(2));
            Assert.That(report.Power.BatteryAfterKWh, Is.EqualTo(50).Within(Tolerance));
            Assert.That(engine.State.BatteryChargeKWh, Is.EqualTo(50).Within(Tolerance));
        }

        [Test]
        public void SemModificadores_ValeCeuLimpo()
        {
            var engine = new SimulationEngine(TestScenario());

            CycleReport report = engine.AdvanceCycle(null);

            Assert.That(report.Power.SolarFactor, Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(report.Power.GeneratedKWh, Is.EqualTo(100).Within(Tolerance));
            Assert.That(Has(report, ConsequenceCode.SolarGenerationReduced), Is.False);
        }

        [Test]
        public void DesligarEquipamento_ReduzOConsumoDoProximoCiclo()
        {
            var engine = new SimulationEngine(TestScenario());

            engine.SetEquipmentOn("a", false);
            CycleReport report = engine.AdvanceCycle();

            Assert.That(report.Power.ConsumedKWh, Is.EqualTo(30).Within(Tolerance));
            Assert.That(report.Power.PoweredEquipmentIds, Is.EqualTo(new[] { "b" }));

            engine.SetEquipmentOn("a", true);
            CycleReport seguinte = engine.AdvanceCycle();

            Assert.That(seguinte.Power.ConsumedKWh, Is.EqualTo(90).Within(Tolerance));
        }

        [Test]
        public void EquipamentoDesconhecido_LancaExcecao()
        {
            var engine = new SimulationEngine(TestScenario());

            Assert.Throws<ArgumentException>(() => engine.SetEquipmentOn("nao_existe", true));
        }

        [Test]
        public void Previsao_NaoAlteraOEstado()
        {
            var engine = new SimulationEngine(TestScenario());

            PowerReport previsao = engine.PreviewPower(new CycleModifiers { SolarGenerationFactor = 0.2 });

            // 100 + 20 - 90 = 30
            Assert.That(previsao.BatteryAfterKWh, Is.EqualTo(30).Within(Tolerance));
            Assert.That(engine.State.BatteryChargeKWh, Is.EqualTo(100).Within(Tolerance));
            Assert.That(engine.State.Cycle, Is.EqualTo(1));
        }

        [Test]
        public void GeracaoReduzidaPorVariosCiclos_EsgotaABateriaEDepoisRecupera()
        {
            var engine = new SimulationEngine(TestScenario());
            var reduzida = new CycleModifiers { SolarGenerationFactor = 0.2 }; // gera 20 por ciclo

            // Ciclo 1: 100 + 20 = 120 -> "a" 60 e "b" 30 funcionam -> bateria 30
            CycleReport c1 = engine.AdvanceCycle(reduzida);
            Assert.That(c1.Power.UnpoweredEquipmentIds, Is.Empty);
            Assert.That(engine.State.BatteryChargeKWh, Is.EqualTo(30).Within(Tolerance));
            Assert.That(Has(c1, ConsequenceCode.SolarGenerationReduced), Is.True);

            // Ciclo 2: 30 + 20 = 50 -> "a" (60) para; "b" funciona -> bateria 20
            CycleReport c2 = engine.AdvanceCycle(reduzida);
            Assert.That(c2.Power.UnpoweredEquipmentIds, Is.EqualTo(new[] { "a" }));
            Assert.That(Has(c2, ConsequenceCode.EquipmentWithoutPower, "a"), Is.True);
            Assert.That(Has(c2, ConsequenceCode.BatteryDepleted), Is.False);
            Assert.That(engine.State.BatteryChargeKWh, Is.EqualTo(20).Within(Tolerance));

            // Ciclo 3: 20 + 20 = 40 -> só "b" -> bateria 10
            engine.AdvanceCycle(reduzida);
            Assert.That(engine.State.BatteryChargeKWh, Is.EqualTo(10).Within(Tolerance));

            // Ciclo 4: 10 + 20 = 30 -> só "b" -> bateria 0
            CycleReport c4 = engine.AdvanceCycle(reduzida);
            Assert.That(engine.State.BatteryChargeKWh, Is.EqualTo(0).Within(Tolerance));
            Assert.That(Has(c4, ConsequenceCode.BatteryDepleted), Is.True);

            // Ciclo 5, céu limpo: 0 + 100 = 100 -> "a" e "b" voltam -> bateria 10
            CycleReport c5 = engine.AdvanceCycle();
            Assert.That(c5.Cycle, Is.EqualTo(5));
            Assert.That(c5.Power.UnpoweredEquipmentIds, Is.Empty);
            Assert.That(engine.State.BatteryChargeKWh, Is.EqualTo(10).Within(Tolerance));
        }

        [Test]
        public void BateriaCheia_ExcedenteViraConsequenciaDeDesperdicio()
        {
            var engine = new SimulationEngine(TestScenario());
            engine.SetEquipmentOn("a", false);

            CycleReport report = engine.AdvanceCycle();

            // 100 + 100 - 30 = 170; cabem 100; 70 perdidos
            Consequence desperdicio = report.Consequences.Single(c => c.Code == ConsequenceCode.SolarEnergyWasted);
            Assert.That(desperdicio.Value, Is.EqualTo(70).Within(Tolerance));
        }

        [Test]
        public void CenarioProvisorio_EhValidoERodaUmCiclo()
        {
            // Não confere valores (são provisórios); só garante que o cenário é consistente.
            var engine = new SimulationEngine(ProvisionalScenario.Create());

            CycleReport report = engine.AdvanceCycle();

            Assert.That(report.Power.BatteryAfterKWh, Is.GreaterThanOrEqualTo(0));
            Assert.That(engine.State.Cycle, Is.EqualTo(2));
        }
    }
}
