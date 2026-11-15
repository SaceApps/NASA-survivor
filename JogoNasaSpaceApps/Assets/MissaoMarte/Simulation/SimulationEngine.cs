using System;
using System.Collections.Generic;

namespace MissaoMarte.Simulation
{
    /// <summary>
    /// Ponto de entrada do motor de simulação. Não depende de UnityEngine nem de interface.
    /// É determinístico: mesmo estado + mesmas entradas = mesmo resultado.
    ///
    /// Ordem de cálculo de um ciclo (é ela que cria a interdependência entre os sistemas):
    ///   1. Energia: decide quais equipamentos ligados realmente funcionam.
    ///   2. Água: a tripulação consome e o reciclador (se funcionou) recupera.
    ///   3. Ar: o gerador de O2 (se funcionou) usa a água que sobrou; a tripulação consome O2
    ///      e produz CO2; o removedor (se funcionou) retira CO2.
    /// </summary>
    public sealed class SimulationEngine
    {
        public SimulationEngine(ScenarioParameters parameters)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            parameters.Validate();
            State = new HabitatState(parameters);
        }

        public ScenarioParameters Parameters { get; }

        public HabitatState State { get; }

        /// <summary>Pressão parcial atual de CO2 na cabine (mmHg).</summary>
        public double Co2PressureMmHg => Parameters.Co2PressureMmHg(State.Co2Kg);

        /// <summary>Liga ou desliga um equipamento. O efeito aparece no próximo ciclo simulado.</summary>
        public void SetEquipmentOn(string equipmentId, bool isOn)
        {
            EquipmentState item = State.FindEquipment(equipmentId);
            if (item == null)
                throw new ArgumentException("Equipamento desconhecido: " + equipmentId, nameof(equipmentId));
            item.IsOn = isOn;
        }

        // ---------- Ações fora do ciclo (exploração, reparos) ----------
        // São as únicas outras formas de alterar o estado; cada uma é explícita.

        /// <summary>Gasta energia da bateria imediatamente (ex.: enviar o rover). Devolve false se não houver carga.</summary>
        public bool TrySpendEnergy(double kWh)
        {
            if (double.IsNaN(kWh) || kWh < 0) throw new ArgumentOutOfRangeException(nameof(kWh));
            if (kWh > State.BatteryChargeKWh + PowerSystem.Epsilon)
                return false;
            State.BatteryChargeKWh = Math.Max(0.0, State.BatteryChargeKWh - kWh);
            return true;
        }

        /// <summary>Acrescenta gelo bruto ao estoque da base (coletado na exploração).</summary>
        public void AddIce(double kg)
        {
            if (double.IsNaN(kg) || kg < 0) throw new ArgumentOutOfRangeException(nameof(kg));
            State.IceKg += kg;
        }

        /// <summary>Define a eficiência dos painéis solares (poeira acumulada ou limpeza). Limitada a [0, 1].</summary>
        public void SetSolarPanelEfficiency(double efficiency)
        {
            if (double.IsNaN(efficiency)) throw new ArgumentOutOfRangeException(nameof(efficiency));
            State.SolarPanelEfficiency = Math.Min(1.0, Math.Max(0.0, efficiency));
        }

        /// <summary>Define a condição de um equipamento (desgaste, quebra ou conserto). Limitada a [0, 1].</summary>
        public void SetEquipmentCondition(string equipmentId, double condition)
        {
            EquipmentState item = State.FindEquipment(equipmentId);
            if (item == null)
                throw new ArgumentException("Equipamento desconhecido: " + equipmentId, nameof(equipmentId));
            if (double.IsNaN(condition)) throw new ArgumentOutOfRangeException(nameof(condition));
            item.Condition = Math.Min(1.0, Math.Max(0.0, condition));
        }

        /// <summary>Define a condição da vedação da base. Limitada a [0, 1].</summary>
        public void SetSealCondition(double condition)
        {
            if (double.IsNaN(condition)) throw new ArgumentOutOfRangeException(nameof(condition));
            State.SealCondition = Math.Min(1.0, Math.Max(0.0, condition));
        }

        /// <summary>O2 que a vedação deixa escapar em um ciclo com a condição atual (kg).</summary>
        public double CabinLeakKgPerCycle => (1.0 - State.SealCondition) * Parameters.CabinLeakOxygenKgPerCycleAtBrokenSeal;

        /// <summary>
        /// Gasta O2 do traje por uma fração de ciclo passada fora da base. Devolve o O2 que faltou
        /// (zero se o tanque deu conta).
        /// </summary>
        public double UseSuitOxygen(double cycles)
        {
            if (double.IsNaN(cycles) || cycles < 0) throw new ArgumentOutOfRangeException(nameof(cycles));
            double needed = cycles * Parameters.SuitOxygenUseKgPerCycle;
            double used = Math.Min(needed, State.SuitOxygenKg);
            State.SuitOxygenKg -= used;
            return needed - used;
        }

        /// <summary>
        /// Recarrega o traje com O2 da reserva da base (transferência explícita: a reserva diminui
        /// exatamente o que o traje recebe). Devolve quanto foi transferido.
        /// </summary>
        public double RefillSuit(double maxKg)
        {
            if (double.IsNaN(maxKg) || maxKg < 0) throw new ArgumentOutOfRangeException(nameof(maxKg));
            double room = Math.Max(0.0, Parameters.SuitOxygenCapacityKg - State.SuitOxygenKg);
            double moved = Math.Min(maxKg, Math.Min(room, State.OxygenKg));
            State.OxygenKg -= moved;
            State.SuitOxygenKg += moved;
            return moved;
        }

        static double Condition(EquipmentState item)
        {
            return item != null ? item.Condition : 1.0;
        }

        static double WeatherFactor(CycleModifiers modifiers)
        {
            double factor = modifiers != null ? modifiers.SolarGenerationFactor : 1.0;
            return Math.Min(1.0, Math.Max(0.0, factor));
        }

        /// <summary>Calcula só a energia do próximo ciclo, sem alterar o estado.</summary>
        public PowerReport PreviewPower(CycleModifiers modifiers = null)
        {
            // Geração efetiva = condição do tempo x estado dos painéis (poeira acumulada).
            return PowerSystem.Resolve(
                State.BatteryChargeKWh, State.Equipment, Parameters, WeatherFactor(modifiers) * State.SolarPanelEfficiency);
        }

        /// <summary>
        /// Calcula o próximo ciclo inteiro sem alterar o estado.
        /// Permite mostrar ao jogador o efeito de uma decisão antes de avançar.
        /// </summary>
        public CycleReport PreviewCycle(CycleModifiers modifiers = null)
        {
            PowerReport power = PreviewPower(modifiers);

            bool generatorRunning = power.IsPowered(EquipmentIds.OxygenGenerator);
            bool scrubberRunning = power.IsPowered(EquipmentIds.Co2Scrubber);
            bool treatmentRunning = power.IsPowered(EquipmentIds.WaterRecycler);
            bool iceProcessorRunning = power.IsPowered(EquipmentIds.IceProcessor);

            WaterReport water = WaterSystem.ResolveCrewAndProcessing(
                State.WaterKg, State.WasteWaterKg, State.IceKg, treatmentRunning, iceProcessorRunning, Parameters,
                Condition(State.FindEquipment(EquipmentIds.WaterRecycler)),
                Condition(State.FindEquipment(EquipmentIds.IceProcessor)));
            AirReport air = AirSystem.Resolve(
                State.OxygenKg, State.Co2Kg, water.AvailableForEquipmentKg, generatorRunning, scrubberRunning, Parameters,
                Condition(State.FindEquipment(EquipmentIds.OxygenGenerator)),
                Condition(State.FindEquipment(EquipmentIds.Co2Scrubber)),
                CabinLeakKgPerCycle);
            WaterSystem.ApplyEquipmentUse(water, air.WaterUsedKg);

            var consequences = new List<Consequence>();
            double weather = WeatherFactor(modifiers);
            if (weather < 1.0)
                consequences.Add(new Consequence(ConsequenceCode.SolarGenerationReduced, null, weather));
            if (State.SolarPanelEfficiency < 1.0)
                consequences.Add(new Consequence(ConsequenceCode.SolarPanelsDusty, null, State.SolarPanelEfficiency));
            PowerSystem.AppendConsequences(power, Parameters, consequences);
            AirSystem.AppendConsequences(air, Parameters, consequences);
            WaterSystem.AppendConsequences(water, consequences);

            return new CycleReport(State.Cycle, power, air, water, consequences);
        }

        /// <summary>Simula um ciclo operacional, atualiza o estado e devolve o que aconteceu.</summary>
        /// <param name="modifiers">Condições externas do ciclo; null significa condições normais.</param>
        public CycleReport AdvanceCycle(CycleModifiers modifiers = null)
        {
            CycleReport report = PreviewCycle(modifiers);

            State.BatteryChargeKWh = report.Power.BatteryAfterKWh;
            State.OxygenKg = report.Air.OxygenAfterKg;
            State.Co2Kg = report.Air.Co2AfterKg;
            State.WaterKg = report.Water.AfterKg;
            State.WasteWaterKg = report.Water.WasteAfterKg;
            State.IceKg = report.Water.IceAfterKg;
            State.Cycle = report.Cycle + 1;

            return report;
        }
    }
}
