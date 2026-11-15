using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MissaoMarte.Simulation.Tests
{
    /// <summary>
    /// Testes do cálculo de energia. Usam números próprios, escolhidos para a conta ser
    /// conferível de cabeça; não dependem dos valores provisórios do cenário.
    /// </summary>
    public class PowerSystemTests
    {
        const double Tolerance = 1e-9;

        static ScenarioParameters Scenario(double generation, double capacity)
        {
            return new ScenarioParameters
            {
                SolarGenerationKWhPerCycle = generation,
                BatteryCapacityKWh = capacity,
            };
        }

        static EquipmentState Eq(string id, double demand, int priority, bool on = true)
        {
            return new EquipmentState(new EquipmentDefinition(id, demand, priority), on);
        }

        static List<EquipmentState> List(params EquipmentState[] items)
        {
            return new List<EquipmentState>(items);
        }

        // ---------- Geração ----------

        [Test]
        public void Geracao_EscalaComOFatorSolar()
        {
            PowerReport report = PowerSystem.Resolve(0, List(), Scenario(100, 200), 0.25);

            Assert.That(report.GeneratedKWh, Is.EqualTo(25).Within(Tolerance));
            Assert.That(report.SolarFactor, Is.EqualTo(0.25).Within(Tolerance));
        }

        [TestCase(-0.5, 0.0)]
        [TestCase(1.7, 100.0)]
        public void Geracao_FatorSolarForaDoIntervaloEhLimitado(double factor, double expectedGeneration)
        {
            PowerReport report = PowerSystem.Resolve(0, List(), Scenario(100, 200), factor);

            Assert.That(report.GeneratedKWh, Is.EqualTo(expectedGeneration).Within(Tolerance));
        }

        [Test]
        public void Geracao_FatorSolarNaNEhRejeitado()
        {
            Assert.Throws<ArgumentException>(() => PowerSystem.Resolve(0, List(), Scenario(100, 200), double.NaN));
        }

        // ---------- Consumo e bateria ----------

        [Test]
        public void GeracaoSuficiente_TodosFuncionamEOExcedenteCarregaABateria()
        {
            var equipment = List(Eq("a", 40, 1), Eq("b", 20, 2));

            PowerReport report = PowerSystem.Resolve(50, equipment, Scenario(100, 200), 1.0);

            // 50 + 100 - 60 = 90
            Assert.That(report.PoweredEquipmentIds, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(report.UnpoweredEquipmentIds, Is.Empty);
            Assert.That(report.RequestedKWh, Is.EqualTo(60).Within(Tolerance));
            Assert.That(report.ConsumedKWh, Is.EqualTo(60).Within(Tolerance));
            Assert.That(report.BatteryAfterKWh, Is.EqualTo(90).Within(Tolerance));
            Assert.That(report.WastedKWh, Is.EqualTo(0).Within(Tolerance));
        }

        [Test]
        public void ExcedenteAlemDaCapacidade_EhDesperdicado()
        {
            var equipment = List(Eq("a", 40, 1));

            PowerReport report = PowerSystem.Resolve(190, equipment, Scenario(100, 200), 1.0);

            // 190 + 100 - 40 = 250; cabem 200; 50 perdidos
            Assert.That(report.BatteryAfterKWh, Is.EqualTo(200).Within(Tolerance));
            Assert.That(report.WastedKWh, Is.EqualTo(50).Within(Tolerance));
        }

        [Test]
        public void GeracaoInsuficiente_ABateriaCobreODeficit()
        {
            var equipment = List(Eq("a", 40, 1), Eq("b", 20, 2));

            PowerReport report = PowerSystem.Resolve(100, equipment, Scenario(30, 200), 1.0);

            // 100 + 30 - 60 = 70
            Assert.That(report.UnpoweredEquipmentIds, Is.Empty);
            Assert.That(report.BatteryAfterKWh, Is.EqualTo(70).Within(Tolerance));
        }

        [Test]
        public void SemBateria_ExcedenteEhTodoDesperdicado()
        {
            var equipment = List(Eq("a", 20, 1));

            PowerReport report = PowerSystem.Resolve(0, equipment, Scenario(50, 0), 1.0);

            Assert.That(report.PoweredEquipmentIds, Is.EqualTo(new[] { "a" }));
            Assert.That(report.BatteryAfterKWh, Is.EqualTo(0).Within(Tolerance));
            Assert.That(report.WastedKWh, Is.EqualTo(30).Within(Tolerance));
        }

        // ---------- Disponibilidade de energia para os equipamentos ----------

        [Test]
        public void FaltaDeEnergia_EquipamentoDeMenorPrioridadeFicaParado()
        {
            var equipment = List(Eq("a", 40, 1), Eq("b", 20, 2));

            PowerReport report = PowerSystem.Resolve(20, equipment, Scenario(30, 200), 1.0);

            // disponível 50: "a" usa 40, sobram 10, "b" (20) não cabe
            Assert.That(report.PoweredEquipmentIds, Is.EqualTo(new[] { "a" }));
            Assert.That(report.UnpoweredEquipmentIds, Is.EqualTo(new[] { "b" }));
            Assert.That(report.RequestedKWh, Is.EqualTo(60).Within(Tolerance));
            Assert.That(report.ConsumedKWh, Is.EqualTo(40).Within(Tolerance));
            Assert.That(report.BatteryAfterKWh, Is.EqualTo(10).Within(Tolerance));
            Assert.That(report.IsPowered("a"), Is.True);
            Assert.That(report.IsPowered("b"), Is.False);
        }

        [Test]
        public void FaltaDeEnergia_PrioridadeValeMaisQueAOrdemDaLista()
        {
            // "b" aparece primeiro na lista, mas "a" tem prioridade maior (número menor)
            var equipment = List(Eq("b", 30, 2), Eq("a", 30, 1));

            PowerReport report = PowerSystem.Resolve(0, equipment, Scenario(30, 200), 1.0);

            Assert.That(report.PoweredEquipmentIds, Is.EqualTo(new[] { "a" }));
            Assert.That(report.UnpoweredEquipmentIds, Is.EqualTo(new[] { "b" }));
        }

        [Test]
        public void FaltaDeEnergia_EmpateDePrioridadeSegueAOrdemDaLista()
        {
            var equipment = List(Eq("a", 30, 1), Eq("b", 30, 1));

            PowerReport report = PowerSystem.Resolve(0, equipment, Scenario(30, 200), 1.0);

            Assert.That(report.PoweredEquipmentIds, Is.EqualTo(new[] { "a" }));
            Assert.That(report.UnpoweredEquipmentIds, Is.EqualTo(new[] { "b" }));
        }

        [Test]
        public void FaltaDeEnergia_EquipamentoMenorDePrioridadeBaixaAindaFuncionaSeCouber()
        {
            var equipment = List(Eq("a", 40, 1), Eq("b", 20, 2));

            PowerReport report = PowerSystem.Resolve(0, equipment, Scenario(30, 200), 1.0);

            // disponível 30: "a" (40) não cabe; "b" (20) cabe; sobram 10
            Assert.That(report.PoweredEquipmentIds, Is.EqualTo(new[] { "b" }));
            Assert.That(report.UnpoweredEquipmentIds, Is.EqualTo(new[] { "a" }));
            Assert.That(report.BatteryAfterKWh, Is.EqualTo(10).Within(Tolerance));
        }

        [Test]
        public void EquipamentoDesligado_NaoConsomeNemApareceComoSemEnergia()
        {
            var equipment = List(Eq("a", 40, 1), Eq("b", 20, 2, on: false));

            PowerReport report = PowerSystem.Resolve(0, equipment, Scenario(100, 200), 1.0);

            Assert.That(report.RequestedKWh, Is.EqualTo(40).Within(Tolerance));
            Assert.That(report.ConsumedKWh, Is.EqualTo(40).Within(Tolerance));
            Assert.That(report.PoweredEquipmentIds, Is.EqualTo(new[] { "a" }));
            Assert.That(report.UnpoweredEquipmentIds, Is.Empty);
        }

        [Test]
        public void ErroDeArredondamento_NaoDesligaEquipamentoQueCabeExatamente()
        {
            // 3.3 + 3.3 + 3.4 em ponto flutuante passa um pouco de 10
            var equipment = List(Eq("a", 3.3, 1), Eq("b", 3.3, 2), Eq("c", 3.4, 3));

            PowerReport report = PowerSystem.Resolve(0, equipment, Scenario(10, 200), 1.0);

            Assert.That(report.UnpoweredEquipmentIds, Is.Empty);
            Assert.That(report.BatteryAfterKWh, Is.GreaterThanOrEqualTo(0));
            Assert.That(report.BatteryAfterKWh, Is.EqualTo(0).Within(Tolerance));
        }

        // ---------- Invariantes ----------

        [TestCase(0, 100, 200, 1.0)]
        [TestCase(50, 100, 200, 1.0)]
        [TestCase(190, 100, 200, 1.0)]
        [TestCase(20, 30, 200, 1.0)]
        [TestCase(0, 30, 200, 1.0)]
        [TestCase(100, 100, 200, 0.0)]
        [TestCase(100, 100, 200, 0.3)]
        [TestCase(0, 50, 0, 1.0)]
        public void Invariante_EnergiaSeConservaEBateriaFicaDentroDosLimites(
            double battery, double generation, double capacity, double factor)
        {
            var equipment = List(Eq("a", 40, 1), Eq("b", 20, 2), Eq("c", 25, 3));

            PowerReport report = PowerSystem.Resolve(battery, equipment, Scenario(generation, capacity), factor);

            double entrada = report.BatteryBeforeKWh + report.GeneratedKWh;
            double saida = report.BatteryAfterKWh + report.ConsumedKWh + report.WastedKWh;
            Assert.That(saida, Is.EqualTo(entrada).Within(Tolerance));
            Assert.That(report.BatteryAfterKWh, Is.InRange(0.0, capacity));
            Assert.That(report.ConsumedKWh, Is.LessThanOrEqualTo(report.RequestedKWh + Tolerance));
            Assert.That(report.PoweredEquipmentIds.Count + report.UnpoweredEquipmentIds.Count, Is.EqualTo(3));
        }
    }
}
