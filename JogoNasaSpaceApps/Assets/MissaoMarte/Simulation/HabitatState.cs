using System.Collections.Generic;

namespace MissaoMarte.Simulation
{
    /// <summary>
    /// Estado do habitat que persiste de um ciclo para o outro. Só o motor altera estes valores.
    /// </summary>
    public sealed class HabitatState
    {
        readonly List<EquipmentState> equipment = new List<EquipmentState>();

        internal HabitatState(ScenarioParameters parameters)
        {
            BatteryChargeKWh = parameters.InitialBatteryChargeKWh;
            OxygenKg = parameters.InitialOxygenKg;
            Co2Kg = parameters.InitialCo2Kg;
            WaterKg = parameters.InitialWaterKg;
            WasteWaterKg = parameters.InitialWasteWaterKg;
            IceKg = parameters.InitialIceKg;
            SuitOxygenKg = parameters.SuitOxygenCapacityKg;
            foreach (EquipmentDefinition definition in parameters.Equipment)
                equipment.Add(new EquipmentState(definition, definition.StartsOn));
        }

        /// <summary>Número do ciclo que será simulado a seguir. Começa em 1.</summary>
        public int Cycle { get; internal set; } = 1;

        /// <summary>Carga atual da bateria (kWh).</summary>
        public double BatteryChargeKWh { get; internal set; }

        /// <summary>O2 disponível no reservatório (kg).</summary>
        public double OxygenKg { get; internal set; }

        /// <summary>CO2 acumulado no ar da cabine (kg).</summary>
        public double Co2Kg { get; internal set; }

        /// <summary>Água potável no reservatório (kg; 1 kg de água é cerca de 1 litro).</summary>
        public double WaterKg { get; internal set; }

        /// <summary>Água residual aguardando tratamento (kg).</summary>
        public double WasteWaterKg { get; internal set; }

        /// <summary>Gelo bruto estocado na base, ainda não processado (kg).</summary>
        public double IceKg { get; internal set; }

        /// <summary>Eficiência dos painéis solares (1 = limpos; menor que 1 = cobertos de poeira).</summary>
        public double SolarPanelEfficiency { get; internal set; } = 1.0;

        /// <summary>Condição da vedação da base (1 = perfeita; menor que 1 = o ar vaza).</summary>
        public double SealCondition { get; internal set; } = 1.0;

        /// <summary>O2 no tanque do traje de quem está fazendo atividade externa (kg).</summary>
        public double SuitOxygenKg { get; internal set; }

        public IReadOnlyList<EquipmentState> Equipment => equipment;

        /// <summary>Devolve o equipamento com o id informado, ou null se não existir.</summary>
        public EquipmentState FindEquipment(string id)
        {
            foreach (EquipmentState item in equipment)
            {
                if (item.Definition.Id == id)
                    return item;
            }
            return null;
        }
    }
}
