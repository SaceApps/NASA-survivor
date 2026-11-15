using System;

namespace MissaoMarte.Simulation
{
    /// <summary>
    /// Descrição fixa de um equipamento do habitat (dado de cenário, não muda durante a partida).
    /// Os efeitos sobre ar e água serão acrescentados nas etapas seguintes.
    /// </summary>
    public sealed class EquipmentDefinition
    {
        public EquipmentDefinition(string id, double energyDemandKWhPerCycle, int priority, bool startsOn = true)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("O equipamento precisa de um id.", nameof(id));
            if (double.IsNaN(energyDemandKWhPerCycle) || energyDemandKWhPerCycle < 0)
                throw new ArgumentOutOfRangeException(nameof(energyDemandKWhPerCycle), "A demanda de energia não pode ser negativa.");

            Id = id;
            EnergyDemandKWhPerCycle = energyDemandKWhPerCycle;
            Priority = priority;
            StartsOn = startsOn;
        }

        /// <summary>Identificador estável, usado pela UI e pelos relatórios.</summary>
        public string Id { get; }

        /// <summary>Energia necessária para operar durante um ciclo inteiro (kWh).</summary>
        public double EnergyDemandKWhPerCycle { get; }

        /// <summary>Ordem de atendimento quando falta energia: número menor é atendido primeiro.</summary>
        public int Priority { get; }

        /// <summary>Se o equipamento começa a partida ligado.</summary>
        public bool StartsOn { get; }
    }
}
