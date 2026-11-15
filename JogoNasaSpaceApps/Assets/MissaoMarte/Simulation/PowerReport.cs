using System.Collections.Generic;

namespace MissaoMarte.Simulation
{
    /// <summary>
    /// Resultado do cálculo de energia de um ciclo. Todas as energias em kWh.
    /// Vale sempre: BatteryBefore + Generated = BatteryAfter + Consumed + Wasted.
    /// </summary>
    public sealed class PowerReport
    {
        internal PowerReport() { }

        /// <summary>Fator solar realmente aplicado, já limitado ao intervalo [0, 1].</summary>
        public double SolarFactor { get; internal set; }

        /// <summary>Energia gerada no ciclo, já com o fator solar aplicado.</summary>
        public double GeneratedKWh { get; internal set; }

        /// <summary>Soma da demanda de todos os equipamentos que o jogador deixou ligados.</summary>
        public double RequestedKWh { get; internal set; }

        /// <summary>Energia efetivamente consumida pelos equipamentos que funcionaram.</summary>
        public double ConsumedKWh { get; internal set; }

        public double BatteryBeforeKWh { get; internal set; }

        public double BatteryAfterKWh { get; internal set; }

        /// <summary>Geração excedente que não coube na bateria e foi perdida.</summary>
        public double WastedKWh { get; internal set; }

        /// <summary>Equipamentos ligados que receberam energia e funcionaram neste ciclo.</summary>
        public IReadOnlyList<string> PoweredEquipmentIds { get; internal set; }

        /// <summary>Equipamentos ligados que ficaram parados por falta de energia.</summary>
        public IReadOnlyList<string> UnpoweredEquipmentIds { get; internal set; }

        /// <summary>Indica se o equipamento funcionou neste ciclo (estava ligado e recebeu energia).</summary>
        public bool IsPowered(string equipmentId)
        {
            foreach (string id in PoweredEquipmentIds)
            {
                if (id == equipmentId)
                    return true;
            }
            return false;
        }
    }
}
