using System;

namespace MissaoMarte.Simulation
{
    /// <summary>
    /// Estado de um equipamento durante a partida: a definição fixa mais a decisão do jogador (ligado/desligado).
    /// "Ligado" não garante funcionamento: isso depende da energia disponível no ciclo (ver <see cref="PowerReport"/>).
    /// </summary>
    public sealed class EquipmentState
    {
        public EquipmentState(EquipmentDefinition definition, bool isOn)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            IsOn = isOn;
        }

        public EquipmentDefinition Definition { get; }

        /// <summary>Decisão do jogador. Alterada por <see cref="SimulationEngine.SetEquipmentOn"/>.</summary>
        public bool IsOn { get; internal set; }

        /// <summary>
        /// Condição do equipamento: 1 = perfeito; entre 0 e 1 = desgastado (rende proporcionalmente menos);
        /// 0 = quebrado (não funciona nem consome energia, mesmo ligado).
        /// </summary>
        public double Condition { get; internal set; } = 1.0;

        public bool IsBroken => Condition <= 0;
    }
}
