using System;
using System.Collections.Generic;
using System.Globalization;
using MissaoMarte.Simulation;

namespace MissaoMarte.Game
{
    /// <summary>
    /// Texto da tela de abertura "Marte de verdade × Marte do jogo". Mostrada antes da missão para separar
    /// o que é fato (com fonte) do que foi ajustado para caber numa partida de poucos minutos.
    ///
    /// Os números não são escritos à mão: saem das mesmas constantes que a simulação usa
    /// (<see cref="PhysicalConstants"/>, <see cref="ProvisionalScenario"/>, <see cref="PrimeiraFonte"/>,
    /// <see cref="MissionConfig"/>). Se o balanceamento mudar, a tela acompanha.
    /// Validação dos valores em 14/11/2026 (ver docs da conversa e comentários de cada constante).
    /// </summary>
    public sealed class MarsBriefing
    {
        public const string Label = "ANTES DE SAIR DA NAVE";
        public const string Title = "MARTE DE VERDADE × MARTE DO JOGO";
        public const string RealHeading = "É ASSIM EM MARTE";
        public const string Footnote = "Os fatos de Marte vêm da NASA. Os números do jogo foram ajustados para a partida caber em poucos minutos.";

        /// <summary>Linhas no máximo em cada coluna (a tela tem espaço para isso).</summary>
        public const int MaxLines = 6;

        MarsBriefing(string gameHeading, IReadOnlyList<string> realFacts, IReadOnlyList<string> gameRules)
        {
            GameHeading = gameHeading;
            RealFacts = realFacts;
            GameRules = gameRules;
        }

        public string GameHeading { get; }

        /// <summary>Fatos reais (coluna da esquerda).</summary>
        public IReadOnlyList<string> RealFacts { get; }

        /// <summary>Simplificações do jogo (coluna da direita).</summary>
        public IReadOnlyList<string> GameRules { get; }

        /// <summary>Monta o texto para a missão e para a velocidade do relógio da tela.</summary>
        public static MarsBriefing Create(HabitatMission mission, double secondsPerMarsHour)
        {
            if (mission == null) throw new ArgumentNullException(nameof(mission));
            if (double.IsNaN(secondsPerMarsHour) || secondsPerMarsHour <= 0)
                throw new ArgumentOutOfRangeException(nameof(secondsPerMarsHour));

            ScenarioParameters p = mission.Parameters;
            MissionConfig config = mission.Config;

            var real = new List<string>
            {
                "Um dia em Marte se chama sol e dura " + SolDuration() + ".",
                "O ar é quase todo gás carbônico (CO2) e tem menos de 1% da pressão da Terra: lá fora, só de traje.",
                "Cada pessoa respira cerca de " + Short(ProvisionalScenario.OxygenKgPerCrewPerEarthDay) + " kg de oxigênio e solta "
                    + Short(ProvisionalScenario.Co2KgPerCrewPerEarthDay) + " kg de CO2 por dia.",
                "E bebe cerca de " + Short(ProvisionalScenario.WaterKgPerCrewPerEarthDay) + " litros de água por dia.",
                "Na Estação Espacial, " + Explanations.Number(p.WaterRecoveryFraction * 100, 0)
                    + " de cada 100 litros de água usada voltam limpos para beber.",
                "Um traje espacial aguenta até " + Explanations.Number(ProvisionalScenario.EvaMaxDurationEarthHours, 0) + " horas lá fora.",
            };

            double solSeconds = 24 * secondsPerMarsHour;
            double speedUp = MarsHourSeconds / secondsPerMarsHour;
            double dustSpeedUp = PrimeiraFonte.StormPanelLossPerHour * 24 / PrimeiraFonte.RealPanelDustLossPerSol;
            double missionMinutes = mission.TotalHours * secondsPerMarsHour / 60.0;

            var game = new List<string>
            {
                "1 sol passa em " + RealDuration(solSeconds) + ": o tempo corre umas " + Thousands(RoundNice(speedUp)) + " vezes mais rápido.",
                "Cada hora do relógio é uma hora de Marte: " + Explanations.Number(Math.Floor(MarsHourSeconds / 60), 0) + " minutos da Terra.",
                "O Sol nasce às " + Explanations.Number(config.SunriseHour, 0) + " h e se põe às " + Explanations.Number(config.SunsetHour, 0)
                    + " h todo dia. Em Marte de verdade, isso muda com as estações do ano.",
                "A tempestade de poeira dura " + Sols(PrimeiraFonte.StormHours / 24.0)
                    + ". As de verdade podem durar semanas e escurecer muito mais.",
                "A poeira suja os painéis umas " + Thousands(RoundNice(dustSpeedUp)) + " vezes mais rápido que na vida real.",
                "Os tanques são pequenos: se as máquinas pararem, o oxigênio e a água acabam em " + Sols(ReserveSols(p), approximate: true) + ".",
            };

            string heading = "NO JOGO, PARA CABER EM " + Explanations.Number(Math.Ceiling(missionMinutes), 0) + " MINUTOS";
            return new MarsBriefing(heading, real, game);
        }

        /// <summary>Segundos da Terra em uma hora marciana (1/24 de sol), cerca de 3 699 s.</summary>
        public static double MarsHourSeconds => PhysicalConstants.SolInEarthDays * 86400.0 / 24.0;

        /// <summary>Duração do sol em horas e minutos da Terra ("24 h 39 min").</summary>
        public static string SolDuration()
        {
            double totalMinutes = PhysicalConstants.SolInEarthDays * 24 * 60;
            int hours = (int)(totalMinutes / 60);
            int minutes = (int)(totalMinutes - hours * 60);
            return hours + " h " + minutes + " min";
        }

        /// <summary>
        /// Sóis até o primeiro recurso acabar se nenhuma máquina funcionar: O2 até o limite mínimo
        /// ou água potável até zero, o que vier antes.
        /// </summary>
        public static double ReserveSols(ScenarioParameters p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            double hours = double.MaxValue;
            if (p.CrewOxygenDemandKgPerCycle > 0)
                hours = Math.Min(hours, Math.Max(0, p.InitialOxygenKg - p.OxygenMinimumKg) / p.CrewOxygenDemandKgPerCycle);
            if (p.CrewWaterDemandKgPerCycle > 0)
                hours = Math.Min(hours, p.InitialWaterKg / p.CrewWaterDemandKgPerCycle);
            return hours / 24.0;
        }

        /// <summary>Arredonda para 2 algarismos significativos (1 479 → 1 500; 103 → 100).</summary>
        public static double RoundNice(double value)
        {
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
                return value;
            double scale = Math.Pow(10, Math.Floor(Math.Log10(value)) - 1);
            return Math.Round(value / scale) * scale;
        }

        static string Thousands(double value)
        {
            return Math.Round(value).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');
        }

        /// <summary>Número com até 1 casa, sem ",0" no fim.</summary>
        static string Short(double value)
        {
            string text = Explanations.Number(value, 1);
            return text.EndsWith(",0", StringComparison.Ordinal) ? text.Substring(0, text.Length - 2) : text;
        }

        static string Sols(double sols, bool approximate = false)
        {
            double shown = approximate ? Math.Round(sols * 2) / 2 : sols;
            // Em português, de 1 até menos de 2 fica no singular ("1,5 sol").
            string word = shown < 2 ? " sol" : " sóis";
            return (approximate ? "cerca de " : "") + Short(shown) + word;
        }

        static string RealDuration(double seconds)
        {
            if (seconds < 60)
                return Explanations.Number(seconds, 0) + " segundos";
            double minutes = seconds / 60;
            return Short(minutes) + (Math.Abs(minutes - 1) < 1e-9 ? " minuto" : " minutos");
        }
    }
}
