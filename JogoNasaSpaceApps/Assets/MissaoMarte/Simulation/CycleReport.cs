using System.Collections.Generic;

namespace MissaoMarte.Simulation
{
    /// <summary>Tipos de consequência que o motor sabe explicar. A camada de jogo converte o código em texto.</summary>
    public enum ConsequenceCode
    {
        // ---------- Energia ----------

        /// <summary>A geração solar ficou abaixo do normal. Value = fator aplicado (0 a 1).</summary>
        SolarGenerationReduced,

        /// <summary>Um equipamento ligado ficou parado por falta de energia. EquipmentId = qual.</summary>
        EquipmentWithoutPower,

        /// <summary>Sobrou geração que não coube na bateria. Value = kWh perdidos.</summary>
        SolarEnergyWasted,

        /// <summary>A bateria terminou o ciclo vazia.</summary>
        BatteryDepleted,

        /// <summary>Os painéis solares estão cobertos de poeira. Value = eficiência atual (0 a 1).</summary>
        SolarPanelsDusty,

        // ---------- Ar ----------

        /// <summary>O gerador de O2 não funcionou; a tripulação usou a reserva. Value = kg retirados da reserva.</summary>
        OxygenGeneratorNotRunning,

        /// <summary>O gerador de O2 produziu menos por falta de água. Value = kg de O2 produzidos.</summary>
        OxygenGeneratorLimitedByWater,

        /// <summary>O gerador de O2 consumiu água para produzir O2. Value = kg de água usados.</summary>
        OxygenProducedFromWater,

        /// <summary>Faltou O2 para a tripulação. Value = kg que faltaram.</summary>
        OxygenShortage,

        /// <summary>A vedação danificada deixou O2 escapar. Value = kg perdidos.</summary>
        OxygenLeak,

        /// <summary>O removedor de CO2 não funcionou e o CO2 acumulou. Value = pressão parcial final (mmHg).</summary>
        Co2NotRemoved,

        /// <summary>O CO2 passou do nível de alerta. Value = pressão parcial (mmHg).</summary>
        Co2AboveAlertLevel,

        /// <summary>O CO2 passou do nível em que é obrigatório agir. Value = pressão parcial (mmHg).</summary>
        Co2AboveActionLevel,

        // ---------- Água ----------

        /// <summary>O tratamento não funcionou; a água usada ficou acumulada como água residual. Value = kg no tanque de água residual.</summary>
        WaterNotRecycled,

        /// <summary>O tanque de água residual encheu e parte foi descartada. Value = kg descartados.</summary>
        WasteWaterOverflow,

        /// <summary>A estação de gelo produziu água potável. Value = kg de água obtidos.</summary>
        IceProcessed,

        /// <summary>A estação de gelo funcionou sem gelo para processar (gastou energia sem produzir).</summary>
        IceProcessorIdle,

        /// <summary>Faltou água para a tripulação. Value = kg que faltaram.</summary>
        WaterShortage,
    }

    /// <summary>Uma consequência de um ciclo, em formato de dados (sem texto).</summary>
    public readonly struct Consequence
    {
        public Consequence(ConsequenceCode code, string equipmentId, double value)
        {
            Code = code;
            EquipmentId = equipmentId;
            Value = value;
        }

        public ConsequenceCode Code { get; }

        /// <summary>Equipamento envolvido, ou null quando não se aplica.</summary>
        public string EquipmentId { get; }

        /// <summary>Valor associado; o significado depende do código.</summary>
        public double Value { get; }
    }

    /// <summary>Tudo o que aconteceu em um ciclo simulado.</summary>
    public sealed class CycleReport
    {
        internal CycleReport(int cycle, PowerReport power, AirReport air, WaterReport water, IReadOnlyList<Consequence> consequences)
        {
            Cycle = cycle;
            Power = power;
            Air = air;
            Water = water;
            Consequences = consequences;
        }

        /// <summary>Número do ciclo que foi simulado.</summary>
        public int Cycle { get; }

        public PowerReport Power { get; }

        public AirReport Air { get; }

        public WaterReport Water { get; }

        public IReadOnlyList<Consequence> Consequences { get; }
    }
}
