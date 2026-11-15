using MissaoMarte.Events;
using MissaoMarte.Simulation;

namespace MissaoMarte.Game
{
    /// <summary>
    /// Monta a missão "Operação: Primeira Fonte": cenário, roteiro de imprevistos (inspirado no roteiro
    /// da versão web do jogo), tempestade e pontos de gelo. Tudo fixo, sem sorteio.
    /// </summary>
    public static class PrimeiraFonte
    {
        public const string MissionName = "OPERAÇÃO: PRIMEIRA FONTE";

        public const string DepositEast = "gelo_leste";
        public const string DepositWest = "gelo_oeste";
        public const string FalseSignal = "sinal_norte";

        /// <summary>[DIDÁTICO] Gelo em cada depósito verdadeiro (kg).</summary>
        public const double IceDepositKg = 30;

        /// <summary>
        /// [DIDÁTICO] Fração da luz que passa durante a tempestade. Referência real, bem mais severa: na
        /// tempestade global de 2018 a produção do rover Opportunity caiu de 645 Wh para 22 Wh por sol
        /// (≈3%), com opacidade (tau) de 10,8 (The Planetary Society, junho de 2018).
        /// </summary>
        public const double StormSolarFactor = 0.25;

        /// <summary>
        /// [DIDÁTICO] Eficiência que os painéis perdem por hora de tempestade. Muito acima do real
        /// (cerca de 0,28% por sol em tempo normal, Jenkins, Landis et al., NTRS 20020031145),
        /// para a limpeza dos painéis caber numa partida de poucos minutos.
        /// </summary>
        public const double StormPanelLossPerHour = 0.012;

        /// <summary>
        /// [FONTE] Perda real de eficiência dos painéis pela poeira, por sol, em tempo normal (0,28%):
        /// Jenkins, Landis et al., NTRS 20020031145. Só serve de comparação na tela de abertura.
        /// </summary>
        public const double RealPanelDustLossPerSol = 0.0028;

        /// <summary>[DIDÁTICO] Horas marcianas de tempestade. Tempestades reais duram de dias a meses.</summary>
        public const int StormHours = 36;

        public const int SampleSites = 3;

        public static HabitatMission Create()
        {
            var config = new MissionConfig();
            int At(int sol, int hour) => HabitatMission.CycleAt(sol, hour, config.StartHour);

            var timeline = new EventTimeline(
                new[]
                {
                    // Sol 3, 06 h, por 36 horas.
                    new DustStorm(At(3, 6), StormHours, StormSolarFactor, StormPanelLossPerHour),
                },
                new[]
                {
                    new EquipmentFailure(At(1, 13), EquipmentIds.WaterRecycler, 0.3,
                        "Filtro de água saturado: o tratamento rende bem menos."),
                    new EquipmentFailure(At(2, 2), EquipmentFailure.SealTarget, 0.4,
                        "Vazamento na vedação: o frio da noite encolheu a borracha da escotilha."),
                    new EquipmentFailure(At(4, 1), EquipmentIds.Co2Scrubber, 0.0,
                        "A poeira travou o suporte atmosférico!"),
                    new EquipmentFailure(At(5, 10), EquipmentIds.OxygenGenerator, 0.4,
                        "Gerador de oxigênio fraco: uma célula desgastou."),
                });

            var deposits = new[]
            {
                new IceDeposit(DepositEast, IceDepositKg),
                new IceDeposit(DepositWest, IceDepositKg),
                new IceDeposit(FalseSignal, 0),
            };

            return new HabitatMission(ProvisionalScenario.Create(), timeline, config, deposits, SampleSites);
        }
    }
}
