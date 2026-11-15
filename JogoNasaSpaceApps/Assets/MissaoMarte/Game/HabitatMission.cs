using System;
using System.Collections.Generic;
using MissaoMarte.Events;
using MissaoMarte.Simulation;

namespace MissaoMarte.Game
{
    public enum MissionStatus
    {
        InProgress,
        Victory,
        Defeat,
    }

    public enum DefeatReason
    {
        None,
        OxygenDepleted,
        Co2Toxic,
        WaterDepleted,
        EnergyFailure,
        SuitOxygenDepleted,
        ObjectivesNotMet,
    }

    public enum NoticeKind
    {
        Info,
        Good,
        Warning,
        Critical,

        /// <summary>Explicação educativa curta, mostrada uma vez.</summary>
        Lesson,

        /// <summary>Fato sobre Marte revelado por uma pesquisa (amostra analisada). A tela mostra numa
        /// janela no centro e congela o tempo até o jogador fechar.</summary>
        Discovery,
    }

    /// <summary>Um aviso para o jogador (frase curta).</summary>
    public readonly struct Notice
    {
        public Notice(NoticeKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public NoticeKind Kind { get; }

        public string Text { get; }
    }

    /// <summary>Resultado de uma ação do jogador.</summary>
    public readonly struct ActionResult
    {
        public ActionResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public bool Success { get; }

        public string Message { get; }
    }

    /// <summary>Um ponto do mapa onde os sensores indicaram gelo. Pode ser alarme falso.</summary>
    public sealed class IceDeposit
    {
        public IceDeposit(string id, double iceKg)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("O depósito precisa de um id.", nameof(id));
            if (double.IsNaN(iceKg) || iceKg < 0) throw new ArgumentOutOfRangeException(nameof(iceKg));
            Id = id;
            InitialKg = iceKg;
            RemainingKg = iceKg;
        }

        public string Id { get; }

        public double InitialKg { get; }

        public double RemainingKg { get; internal set; }

        /// <summary>Tem gelo de verdade (falso = alarme falso dos sensores).</summary>
        public bool IsReal => InitialKg > 0;

        /// <summary>Alguém já foi até lá e conferiu.</summary>
        public bool Discovered { get; internal set; }
    }

    /// <summary>Parâmetros da missão que não são física. Todos [DIDÁTICO].</summary>
    public sealed class MissionConfig
    {
        /// <summary>Hora do dia em que a missão começa (sol 1).</summary>
        public int StartHour { get; set; } = 6;

        /// <summary>Quantos sols a tripulação precisa completar.</summary>
        public int MissionSols { get; set; } = 5;

        /// <summary>Água potável que precisa ser obtida do gelo (kg ≈ litros).</summary>
        public double IceWaterObjectiveKg { get; set; } = 20;

        /// <summary>Quanto gelo o astronauta consegue carregar de uma vez (kg).</summary>
        public double IceCarryCapacityKg { get; set; } = 10;

        /// <summary>O2 que a base transfere para o traje por hora, com o astronauta dentro (kg).</summary>
        public double SuitRefillKgPerHour { get; set; } = 0.6;

        /// <summary>
        /// Energia de bateria gasta em um conserto (kWh). Zero no MVP: o custo do conserto é o tempo do
        /// astronauta (o dobro sem iluminação). Com custo em energia, uma bateria vazia impedia consertar
        /// o suporte atmosférico e a partida ficava sem saída.
        /// </summary>
        public double RepairEnergyKWh { get; set; } = 0;

        /// <summary>Horas seguidas com equipamento de suporte à vida ligado e sem energia que encerram a missão.</summary>
        public int EnergyFailureHours { get; set; } = 3;

        /// <summary>Nascer e pôr do sol (hora do dia). Fora desse intervalo os painéis não geram.</summary>
        public double SunriseHour { get; set; } = 6;

        public double SunsetHour { get; set; } = 18;
    }

    /// <summary>
    /// Uma partida em tempo real: o tempo corre em horas marcianas, o motor de simulação roda uma vez
    /// por hora e o astronauta age pelo mapa (consertos, gelo, painéis, amostras).
    /// Não depende de UnityEngine: a tela só chama <see cref="Update"/> e as ações.
    /// </summary>
    public sealed class HabitatMission
    {
        static readonly string[] LifeSupport = { EquipmentIds.Co2Scrubber, EquipmentIds.OxygenGenerator };

        readonly List<IceDeposit> deposits = new List<IceDeposit>();
        readonly HashSet<string> samplesCollected = new HashSet<string>();
        readonly HashSet<string> activeWarnings = new HashSet<string>();
        readonly HashSet<string> lessonsShown = new HashSet<string>();
        readonly List<Notice> log = new List<Notice>();
        double hourFraction;
        int hoursWithoutLifeSupportPower;

        public HabitatMission(
            ScenarioParameters parameters,
            EventTimeline timeline,
            MissionConfig config,
            IEnumerable<IceDeposit> iceDeposits = null,
            int sampleSiteCount = 0)
        {
            Config = config ?? new MissionConfig();
            if (Config.MissionSols < 1) throw new ArgumentOutOfRangeException(nameof(config));
            if (Config.StartHour < 0 || Config.StartHour > 23) throw new ArgumentOutOfRangeException(nameof(config));

            Engine = new SimulationEngine(parameters);
            Timeline = timeline ?? new EventTimeline();
            SampleSiteCount = Math.Max(0, sampleSiteCount);

            if (iceDeposits != null)
            {
                foreach (IceDeposit deposit in iceDeposits)
                {
                    if (deposit == null) throw new ArgumentException("Lista de depósitos com item nulo.");
                    deposits.Add(deposit);
                }
            }
        }

        public SimulationEngine Engine { get; }

        public EventTimeline Timeline { get; }

        public MissionConfig Config { get; }

        public HabitatState State => Engine.State;

        public ScenarioParameters Parameters => Engine.Parameters;

        public IReadOnlyList<IceDeposit> IceDeposits => deposits;

        public int SampleSiteCount { get; }

        public MissionStatus Status { get; private set; } = MissionStatus.InProgress;

        public DefeatReason DefeatReason { get; private set; } = DefeatReason.None;

        public bool IsFinished => Status != MissionStatus.InProgress;

        /// <summary>Último ciclo (hora) simulado, ou null antes da primeira hora completa.</summary>
        public CycleReport LastReport { get; private set; }

        /// <summary>Todos os avisos desde o começo da missão.</summary>
        public IReadOnlyList<Notice> Log => log;

        /// <summary>Disparado a cada novo aviso.</summary>
        public event Action<Notice> Noticed;

        // ---------- Tempo ----------

        /// <summary>Horas completas já simuladas.</summary>
        public int HoursPlayed => State.Cycle - 1;

        /// <summary>Horas da missão inteira: do início até o fim do último sol.</summary>
        public int TotalHours => Config.MissionSols * 24 - Config.StartHour;

        /// <summary>Hora absoluta desde a meia-noite do sol 1, com a fração da hora em andamento.</summary>
        double AbsoluteHour => Config.StartHour + HoursPlayed + hourFraction;

        public int Sol => Math.Min(Config.MissionSols, 1 + (int)(AbsoluteHour / 24.0));

        /// <summary>Hora do dia (0 a 24), contínua.</summary>
        public double HourOfDay => HoursPlayed >= TotalHours ? 24.0 : AbsoluteHour % 24.0;

        /// <summary>Ciclo do motor que corresponde a um sol e uma hora do dia.</summary>
        public static int CycleAt(int sol, int hourOfDay, int startHour)
        {
            return (sol - 1) * 24 + hourOfDay - startHour + 1;
        }

        public int CycleAt(int sol, int hourOfDay)
        {
            return CycleAt(sol, hourOfDay, Config.StartHour);
        }

        /// <summary>Luz do sol de 0 a 1 ao longo do dia (meia senoide entre o nascer e o pôr do sol).</summary>
        public double Daylight(double hourOfDay)
        {
            double day = Config.SunsetHour - Config.SunriseHour;
            if (day <= 0 || hourOfDay <= Config.SunriseHour || hourOfDay >= Config.SunsetHour)
                return 0;
            return Math.Sin(Math.PI * (hourOfDay - Config.SunriseHour) / day);
        }

        /// <summary>Luz que chega aos painéis agora (dia/noite × tempestade), de 0 a 1.</summary>
        public double CurrentSunlight
        {
            get
            {
                DustStorm storm = ActiveStorm;
                return Daylight(HourOfDay) * (storm != null ? storm.SolarFactor : 1.0);
            }
        }

        public DustStorm ActiveStorm => Timeline.GetActiveStorm(State.Cycle);

        public DustStorm NextStorm => Timeline.GetNextStorm(State.Cycle);

        /// <summary>A iluminação da base funcionou na última hora (sem luz, consertar demora mais).</summary>
        public bool LightsWorking =>
            LastReport == null || State.FindEquipment(EquipmentIds.Infrastructure) == null
            || LastReport.Power.IsPowered(EquipmentIds.Infrastructure);

        // ---------- Objetivos ----------

        public bool IceFound
        {
            get
            {
                foreach (IceDeposit deposit in deposits)
                {
                    if (deposit.Discovered && deposit.IsReal)
                        return true;
                }
                return false;
            }
        }

        /// <summary>Água potável obtida do gelo até agora (kg ≈ litros).</summary>
        public double IceWaterProducedKg { get; private set; }

        public bool IceWaterObjectiveMet => IceWaterProducedKg + PowerSystem.Epsilon >= Config.IceWaterObjectiveKg;

        public bool ObjectivesMet => (deposits.Count == 0 || IceFound) && (deposits.Count == 0 || IceWaterObjectiveMet);

        /// <summary>Gelo que o astronauta está carregando (kg).</summary>
        public double CarriedIceKg { get; private set; }

        public int SamplesCarried { get; private set; }

        public int SamplesAnalyzed { get; private set; }

        public bool IsSampleCollected(string siteId) => samplesCollected.Contains(siteId);

        public IceDeposit FindDeposit(string id)
        {
            foreach (IceDeposit deposit in deposits)
            {
                if (deposit.Id == id)
                    return deposit;
            }
            return null;
        }

        // ---------- Tempo correndo ----------

        /// <summary>
        /// Avança o relógio. Chamado pela tela a cada quadro com o tempo de jogo passado (em horas marcianas)
        /// e com a posição do astronauta (dentro ou fora dos módulos pressurizados).
        /// </summary>
        public void Update(double hours, bool astronautOutside)
        {
            if (IsFinished || double.IsNaN(hours) || hours <= 0)
                return;

            // O traje: fora, gasta O2; dentro, recarrega com O2 da reserva da base.
            if (astronautOutside)
            {
                double missing = Engine.UseSuitOxygen(hours);
                double fraction = Parameters.SuitOxygenCapacityKg > 0 ? State.SuitOxygenKg / Parameters.SuitOxygenCapacityKg : 1;
                Warn("suit", fraction < 0.25, NoticeKind.Critical, "Traje com pouco oxigênio: volte para a base!");
                if (missing > 0)
                {
                    Finish(DefeatReason.SuitOxygenDepleted);
                    return;
                }
            }
            else
            {
                Engine.RefillSuit(Config.SuitRefillKgPerHour * hours);
                Warn("suit", false, NoticeKind.Critical, null);
            }

            hourFraction += hours;
            while (hourFraction >= 1.0 && !IsFinished)
            {
                hourFraction -= 1.0;
                RunHour();
            }
        }

        void RunHour()
        {
            int cycle = State.Cycle;

            foreach (EquipmentFailure failure in Timeline.GetFailures(cycle))
                ApplyFailure(failure);

            DustStorm storm = Timeline.GetActiveStorm(cycle);
            DustStorm next = Timeline.GetNextStorm(cycle);
            if (next != null && next.StartCycle - cycle == 12)
                Notify(NoticeKind.Warning, "Previsão: tempestade de poeira em 12 horas.");
            if (storm != null && storm.StartCycle == cycle)
            {
                Notify(NoticeKind.Critical, "Tempestade de poeira! Os painéis vão gerar bem menos.");
                Lesson("storm", "Na tempestade a poeira bloqueia o Sol. Desligue o que não é essencial para a bateria durar.");
            }
            if (storm != null && storm.EndCycle == cycle - 1)
                Notify(NoticeKind.Good, "A tempestade passou.");

            double hourOfDay = (Config.StartHour + cycle - 1) % 24 + 0.5;
            var modifiers = new CycleModifiers
            {
                SolarGenerationFactor = Daylight(hourOfDay) * (storm != null ? storm.SolarFactor : 1.0),
            };

            CycleReport report = Engine.AdvanceCycle(modifiers);
            LastReport = report;
            IceWaterProducedKg += report.Water.IceWaterKg;

            if (storm != null && storm.PanelEfficiencyLossPerCycle > 0)
                Engine.SetSolarPanelEfficiency(State.SolarPanelEfficiency - storm.PanelEfficiencyLossPerCycle);

            ReportWarnings(report);

            DefeatReason reason = EvaluateDefeat(report);
            if (reason != DefeatReason.None)
            {
                Finish(reason);
                return;
            }

            if (HoursPlayed >= TotalHours)
                Finish(ObjectivesMet ? DefeatReason.None : DefeatReason.ObjectivesNotMet);
        }

        void ApplyFailure(EquipmentFailure failure)
        {
            if (failure.Target == EquipmentFailure.SealTarget)
            {
                Engine.SetSealCondition(Math.Min(State.SealCondition, failure.NewCondition));
            }
            else
            {
                EquipmentState item = State.FindEquipment(failure.Target);
                if (item == null)
                    return;
                Engine.SetEquipmentCondition(failure.Target, Math.Min(item.Condition, failure.NewCondition));
            }
            Notify(NoticeKind.Critical, failure.Title + " Vá até lá e conserte.");
        }

        /// <summary>Regras de derrota de uma hora simulada.</summary>
        DefeatReason EvaluateDefeat(CycleReport report)
        {
            ScenarioParameters p = Parameters;

            if (report.Air.OxygenShortageKg > 0
                || (p.CrewCount > 0 && p.OxygenMinimumKg > 0 && report.Air.OxygenAfterKg < p.OxygenMinimumKg))
                return DefeatReason.OxygenDepleted;

            if (p.Co2EvacuationMmHg > 0 && p.Co2KgPerMmHg > 0 && report.Air.Co2PressureAfterMmHg >= p.Co2EvacuationMmHg)
                return DefeatReason.Co2Toxic;

            if (report.Water.CrewShortageKg > 0)
                return DefeatReason.WaterDepleted;

            bool lifeSupportDark = false;
            foreach (string id in LifeSupport)
            {
                foreach (string unpowered in report.Power.UnpoweredEquipmentIds)
                {
                    if (unpowered == id)
                        lifeSupportDark = true;
                }
            }
            hoursWithoutLifeSupportPower = lifeSupportDark ? hoursWithoutLifeSupportPower + 1 : 0;
            if (Config.EnergyFailureHours > 0 && hoursWithoutLifeSupportPower >= Config.EnergyFailureHours)
                return DefeatReason.EnergyFailure;

            return DefeatReason.None;
        }

        void ReportWarnings(CycleReport report)
        {
            ScenarioParameters p = Parameters;

            if (report.Power.ConsumedKWh > report.Power.GeneratedKWh + PowerSystem.Epsilon && report.Power.GeneratedKWh > 0)
                Lesson("battery", "Os painéis não geram o suficiente: a base está usando a bateria.");

            foreach (string id in report.Power.UnpoweredEquipmentIds)
                Warn("power:" + id, true, NoticeKind.Critical, "Sem energia: " + Explanations.EquipmentName(id) + " parou.");
            foreach (EquipmentState item in State.Equipment)
            {
                if (!Contains(report.Power.UnpoweredEquipmentIds, item.Definition.Id))
                    Warn("power:" + item.Definition.Id, false, NoticeKind.Critical, null);
            }

            Warn("battery-empty", report.Power.BatteryAfterKWh <= PowerSystem.Epsilon, NoticeKind.Critical, "Bateria vazia!");

            double o2 = report.Air.OxygenAfterKg;
            Warn("o2-low", o2 < p.OxygenMinimumKg + 3 * p.CrewOxygenDemandKgPerCycle + PowerSystem.Epsilon,
                NoticeKind.Critical, "Oxigênio baixo na base!");

            double co2 = report.Air.Co2PressureAfterMmHg;
            bool co2High = p.Co2AlertMmHg > 0 && co2 >= p.Co2AlertMmHg;
            Warn("co2-high", co2High, NoticeKind.Warning, "CO2 acumulando no ar!");
            if (co2High)
                Lesson("co2", "A tripulação expira CO2. O suporte atmosférico precisa removê-lo para o ar continuar respirável.");

            double water = report.Water.AfterKg;
            Warn("water-low", water < 12 * p.CrewWaterDemandKgPerCycle, NoticeKind.Critical, "Água potável acabando!");

            Warn("waste-overflow", report.Water.WasteOverflowKg > PowerSystem.Epsilon, NoticeKind.Warning,
                "Tanque de água usada cheio: água sendo descartada.");

            if (report.Water.IceWaterKg > PowerSystem.Epsilon)
                Lesson("first-ice-water", "Primeira água tirada do gelo de Marte!");
            if (IceWaterObjectiveMet)
                Lesson("ice-objective", "Objetivo cumprido: " + Explanations.Number(Config.IceWaterObjectiveKg, 0) + " L de água do gelo.");

            Warn("leak", report.Air.OxygenLeakedKg > PowerSystem.Epsilon, NoticeKind.Warning, "O ar está vazando pela vedação.");
        }

        static bool Contains(IReadOnlyList<string> list, string value)
        {
            foreach (string item in list)
            {
                if (item == value)
                    return true;
            }
            return false;
        }

        void Finish(DefeatReason reason)
        {
            DefeatReason = reason;
            Status = reason == DefeatReason.None ? MissionStatus.Victory : MissionStatus.Defeat;
        }

        // ---------- Ações do astronauta ----------

        public bool IsEquipmentOn(string equipmentId)
        {
            EquipmentState item = State.FindEquipment(equipmentId);
            if (item == null)
                throw new ArgumentException("Equipamento desconhecido: " + equipmentId, nameof(equipmentId));
            return item.IsOn;
        }

        public ActionResult ToggleEquipment(string equipmentId)
        {
            if (IsFinished)
                return new ActionResult(false, "A missão terminou.");
            bool on = !IsEquipmentOn(equipmentId);
            Engine.SetEquipmentOn(equipmentId, on);
            return new ActionResult(true, Explanations.EquipmentName(equipmentId) + (on ? " ligado." : " desligado."));
        }

        /// <summary>Condição atual de um alvo de conserto (equipamento ou vedação).</summary>
        public double ConditionOf(string target)
        {
            if (target == EquipmentFailure.SealTarget)
                return State.SealCondition;
            EquipmentState item = State.FindEquipment(target);
            return item != null ? item.Condition : 1.0;
        }

        public bool NeedsRepair(string target) => ConditionOf(target) < 1.0;

        public bool CanRepair(string target, out string reason)
        {
            reason = null;
            if (IsFinished) reason = "A missão terminou.";
            else if (!NeedsRepair(target)) reason = "Está funcionando bem.";
            else if (State.BatteryChargeKWh + PowerSystem.Epsilon < Config.RepairEnergyKWh) reason = "Sem bateria para as ferramentas.";
            return reason == null;
        }

        /// <summary>Conserta um equipamento ou a vedação: gasta energia da bateria e devolve a condição a 100%.</summary>
        public ActionResult Repair(string target)
        {
            if (!CanRepair(target, out string reason))
                return new ActionResult(false, reason);

            Engine.TrySpendEnergy(Config.RepairEnergyKWh);
            if (target == EquipmentFailure.SealTarget)
                Engine.SetSealCondition(1.0);
            else
                Engine.SetEquipmentCondition(target, 1.0);

            string message = Explanations.RepairTargetName(target) + " consertado.";
            Notify(NoticeKind.Good, message);
            return new ActionResult(true, message);
        }

        public bool PanelsDirty => State.SolarPanelEfficiency < 1.0 - 1e-6;

        public ActionResult CleanPanels()
        {
            if (IsFinished)
                return new ActionResult(false, "A missão terminou.");
            if (!PanelsDirty)
                return new ActionResult(false, "Os painéis já estão limpos.");

            Engine.SetSolarPanelEfficiency(1.0);
            const string message = "Painéis limpos: geração solar de volta a 100%.";
            Notify(NoticeKind.Good, message);
            return new ActionResult(true, message);
        }

        /// <summary>O astronauta chegou perto de um ponto marcado pelos sensores e conferiu o que há lá.</summary>
        public ActionResult DiscoverDeposit(string depositId)
        {
            IceDeposit deposit = FindDeposit(depositId);
            if (deposit == null || deposit.Discovered || IsFinished)
                return new ActionResult(false, null);

            deposit.Discovered = true;
            if (deposit.IsReal)
            {
                Notify(NoticeKind.Good, "Gelo encontrado!");
                Lesson("ice", Explanations.IceNote);
                return new ActionResult(true, "Gelo encontrado!");
            }

            Notify(NoticeKind.Info, "Só rocha: alarme falso dos sensores.");
            return new ActionResult(true, "Só rocha.");
        }

        public bool CanCollectIce(string depositId, out string reason)
        {
            IceDeposit deposit = FindDeposit(depositId);
            reason = null;
            if (IsFinished) reason = "A missão terminou.";
            else if (deposit == null || !deposit.Discovered || !deposit.IsReal) reason = "Não há gelo aqui.";
            else if (deposit.RemainingKg <= PowerSystem.Epsilon) reason = "Este depósito acabou.";
            else if (CarriedIceKg >= Config.IceCarryCapacityKg - PowerSystem.Epsilon) reason = "Carga cheia: leve o gelo para a estação.";
            return reason == null;
        }

        public ActionResult CollectIce(string depositId)
        {
            if (!CanCollectIce(depositId, out string reason))
                return new ActionResult(false, reason);

            IceDeposit deposit = FindDeposit(depositId);
            double amount = Math.Min(deposit.RemainingKg, Config.IceCarryCapacityKg - CarriedIceKg);
            deposit.RemainingKg -= amount;
            CarriedIceKg += amount;
            return new ActionResult(true, "+" + Explanations.Number(amount, 0) + " kg de gelo");
        }

        /// <summary>Entrega o gelo carregado na estação de processamento (vai para o estoque da base).</summary>
        public ActionResult DeliverIce()
        {
            if (IsFinished)
                return new ActionResult(false, "A missão terminou.");
            if (CarriedIceKg <= PowerSystem.Epsilon)
                return new ActionResult(false, "Você não está carregando gelo.");

            double amount = CarriedIceKg;
            CarriedIceKg = 0;
            Engine.AddIce(amount);

            bool stationOn = State.FindEquipment(EquipmentIds.IceProcessor) != null && IsEquipmentOn(EquipmentIds.IceProcessor);
            string message = Explanations.Number(amount, 0) + " kg de gelo entregues." + (stationOn ? "" : " Ligue a estação!");
            Notify(NoticeKind.Good, message);
            return new ActionResult(true, message);
        }

        public ActionResult CollectSample(string siteId)
        {
            if (IsFinished || string.IsNullOrEmpty(siteId) || samplesCollected.Contains(siteId))
                return new ActionResult(false, null);

            samplesCollected.Add(siteId);
            SamplesCarried++;
            return new ActionResult(true, "Amostra coletada. Leve ao laboratório.");
        }

        public ActionResult AnalyzeSamples()
        {
            if (IsFinished)
                return new ActionResult(false, "A missão terminou.");
            if (SamplesCarried == 0)
                return new ActionResult(false, "Você não tem amostras.");

            int before = SamplesAnalyzed;
            SamplesAnalyzed += SamplesCarried;
            SamplesCarried = 0;
            for (int i = before + 1; i <= SamplesAnalyzed; i++)
                Notify(NoticeKind.Discovery, Explanations.ScienceFact(i));
            return new ActionResult(true, "Amostras analisadas.");
        }

        // ---------- Avisos ----------

        void Notify(NoticeKind kind, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var notice = new Notice(kind, text);
            log.Add(notice);
            Noticed?.Invoke(notice);
        }

        /// <summary>Avisa uma vez quando a situação começa; avisa de novo só depois que ela acabar e voltar.</summary>
        void Warn(string key, bool active, NoticeKind kind, string text)
        {
            if (active)
            {
                if (activeWarnings.Add(key))
                    Notify(kind, text);
            }
            else
            {
                activeWarnings.Remove(key);
            }
        }

        void Lesson(string key, string text)
        {
            if (lessonsShown.Add(key))
                Notify(NoticeKind.Lesson, text);
        }
    }
}
