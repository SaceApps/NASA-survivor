using System;
using System.Collections.Generic;
using System.Linq;

namespace MissaoMarte.Simulation
{
    /// <summary>
    /// Cálculo de energia de um ciclo: geração, distribuição aos equipamentos e bateria.
    /// É uma função pura (não altera nada), o que permite testar e também simular "e se" pela UI.
    ///
    /// Simplificações do modelo (decisões de projeto, não parâmetros científicos):
    /// 1. A geração do ciclo e a carga da bateria formam um único saldo disponível no ciclo;
    ///    não há distinção entre dia e noite dentro do ciclo.
    /// 2. Cada equipamento funciona o ciclo inteiro ou não funciona (sem operação parcial).
    /// 3. Na falta de energia, os equipamentos são atendidos por prioridade (menor número primeiro;
    ///    empate segue a ordem da lista). Um equipamento que não cabe no saldo fica parado, mas
    ///    outro de prioridade mais baixa e demanda menor ainda pode funcionar.
    /// 4. A bateria carrega e descarrega sem perdas.
    /// </summary>
    public static class PowerSystem
    {
        /// <summary>Tolerância para erros de arredondamento de ponto flutuante (kWh).</summary>
        public const double Epsilon = 1e-9;

        /// <param name="batteryChargeKWh">Carga da bateria no início do ciclo.</param>
        /// <param name="equipment">Equipamentos do habitat com a decisão ligado/desligado.</param>
        /// <param name="parameters">Valores do cenário.</param>
        /// <param name="solarFactor">Multiplicador da geração solar: 1 = céu limpo, 0 = sem geração.
        /// Valores fora de [0, 1] são limitados a esse intervalo.</param>
        public static PowerReport Resolve(
            double batteryChargeKWh,
            IReadOnlyList<EquipmentState> equipment,
            ScenarioParameters parameters,
            double solarFactor)
        {
            if (equipment == null) throw new ArgumentNullException(nameof(equipment));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            if (double.IsNaN(solarFactor)) throw new ArgumentException("O fator solar não pode ser NaN.", nameof(solarFactor));
            if (double.IsNaN(batteryChargeKWh) || batteryChargeKWh < 0)
                throw new ArgumentOutOfRangeException(nameof(batteryChargeKWh), "A carga da bateria não pode ser negativa.");

            double factor = Math.Min(1.0, Math.Max(0.0, solarFactor));
            double generated = parameters.SolarGenerationKWhPerCycle * factor;
            double remaining = generated + batteryChargeKWh;

            var powered = new List<string>();
            var unpowered = new List<string>();
            double requested = 0;
            double consumed = 0;

            // OrderBy é estável: em caso de empate de prioridade vale a ordem da lista.
            foreach (EquipmentState item in equipment.Where(e => e.IsOn && !e.IsBroken).OrderBy(e => e.Definition.Priority))
            {
                double demand = item.Definition.EnergyDemandKWhPerCycle;
                requested += demand;

                if (demand <= remaining + Epsilon)
                {
                    powered.Add(item.Definition.Id);
                    consumed += demand;
                    remaining = Math.Max(0.0, remaining - demand);
                }
                else
                {
                    unpowered.Add(item.Definition.Id);
                }
            }

            double capacity = parameters.BatteryCapacityKWh;
            double batteryAfter = Math.Min(capacity, remaining);
            double wasted = Math.Max(0.0, remaining - capacity);

            return new PowerReport
            {
                SolarFactor = factor,
                GeneratedKWh = generated,
                RequestedKWh = requested,
                ConsumedKWh = consumed,
                BatteryBeforeKWh = batteryChargeKWh,
                BatteryAfterKWh = batteryAfter,
                WastedKWh = wasted,
                PoweredEquipmentIds = powered,
                UnpoweredEquipmentIds = unpowered,
            };
        }

        /// <summary>Traduz o resultado de energia em consequências estruturadas para a explicação ao jogador.</summary>
        public static void AppendConsequences(PowerReport report, ScenarioParameters parameters, List<Consequence> output)
        {
            foreach (string id in report.UnpoweredEquipmentIds)
                output.Add(new Consequence(ConsequenceCode.EquipmentWithoutPower, id, 0));

            if (report.WastedKWh > Epsilon)
                output.Add(new Consequence(ConsequenceCode.SolarEnergyWasted, null, report.WastedKWh));

            if (parameters.BatteryCapacityKWh > 0 && report.BatteryAfterKWh <= Epsilon)
                output.Add(new Consequence(ConsequenceCode.BatteryDepleted, null, 0));
        }
    }
}
