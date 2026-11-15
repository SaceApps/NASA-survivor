using System;
using MissaoMarte.Game;
using MissaoMarte.Simulation;
using NUnit.Framework;

namespace MissaoMarte.Tests
{
    public class MarsBriefingTests
    {
        static MarsBriefing Create(double secondsPerMarsHour = 2.5)
        {
            return MarsBriefing.Create(PrimeiraFonte.Create(), secondsPerMarsHour);
        }

        static string All(MarsBriefing briefing)
        {
            return string.Join("\n", briefing.RealFacts) + "\n" + string.Join("\n", briefing.GameRules) + "\n" + briefing.GameHeading;
        }

        [Test]
        public void SolDuraVinteEQuatroHorasETrintaENoveMinutos()
        {
            // 88 775,244 s = 24 h 39 min 35 s (Allison & McEwen 2000).
            Assert.That(MarsBriefing.SolDuration(), Is.EqualTo("24 h 39 min"));
            Assert.That(MarsBriefing.MarsHourSeconds, Is.EqualTo(3698.97).Within(0.01));
        }

        [Test]
        public void TextoUsaOsNumerosDaSimulacao()
        {
            string text = All(Create());
            Assert.That(text.Contains("24 h 39 min"), Is.True);
            Assert.That(text.Contains("0,8 kg de oxigênio"), Is.True);
            Assert.That(text.Contains("1 kg de CO2"), Is.True);
            Assert.That(text.Contains("2,5 litros"), Is.True);
            Assert.That(text.Contains("98 de cada 100"), Is.True);
            Assert.That(text.Contains("8 horas"), Is.True);
            Assert.That(text.Contains("1 sol passa em 1 minuto"), Is.True);
            Assert.That(text.Contains("1.500 vezes"), Is.True);
            Assert.That(text.Contains("61 minutos"), Is.True);
            Assert.That(text.Contains("às 6 h") && text.Contains("às 18 h"), Is.True);
            Assert.That(text.Contains("dura 1,5 sol"), Is.True);
            Assert.That(text.Contains("100 vezes mais rápido"), Is.True);
            Assert.That(text.Contains("cerca de 2 sóis"), Is.True);
            Assert.That(text.Contains("CABER EM 5 MINUTOS"), Is.True);
        }

        [Test]
        public void RelogioMaisLentoMudaOTexto()
        {
            // 5 s por hora marciana: 1 sol = 2 minutos, ~740 vezes mais rápido, partida de 9,5 min.
            string text = All(Create(5));
            Assert.That(text.Contains("1 sol passa em 2 minutos"), Is.True);
            Assert.That(text.Contains("740 vezes"), Is.True);
            Assert.That(text.Contains("CABER EM 10 MINUTOS"), Is.True);
        }

        [Test]
        public void ReservaSemMaquinasAcabaEmCercaDeDoisSois()
        {
            // O2: (10 - 3) kg / 0,1397 kg/h = 50,1 h; água: 20 kg / 0,4281 kg/h = 46,7 h (a água acaba antes).
            ScenarioParameters p = ProvisionalScenario.Create();
            Assert.That(MarsBriefing.ReserveSols(p) * 24, Is.EqualTo(46.7).Within(0.1));
        }

        [Test]
        public void ArredondaParaDoisAlgarismos()
        {
            Assert.That(MarsBriefing.RoundNice(1479.6), Is.EqualTo(1500).Within(1e-9));
            Assert.That(MarsBriefing.RoundNice(102.86), Is.EqualTo(100).Within(1e-9));
            Assert.That(MarsBriefing.RoundNice(739.8), Is.EqualTo(740).Within(1e-9));
        }

        [Test]
        public void CabeNaTela()
        {
            MarsBriefing briefing = Create();
            Assert.That(briefing.RealFacts.Count, Is.LessThanOrEqualTo(MarsBriefing.MaxLines));
            Assert.That(briefing.GameRules.Count, Is.LessThanOrEqualTo(MarsBriefing.MaxLines));
            foreach (string line in briefing.RealFacts)
                Assert.That(line.Length, Is.LessThanOrEqualTo(110), line);
            foreach (string line in briefing.GameRules)
                Assert.That(line.Length, Is.LessThanOrEqualTo(110), line);
        }

        [Test]
        public void RecusaRelogioInvalido()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(0));
            Assert.Throws<ArgumentNullException>(() => MarsBriefing.Create(null, 2.5));
        }
    }
}
