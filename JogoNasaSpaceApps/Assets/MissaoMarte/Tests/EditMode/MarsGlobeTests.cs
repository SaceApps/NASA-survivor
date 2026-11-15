using MissaoMarte.Game;
using NUnit.Framework;

namespace MissaoMarte.Tests
{
    public class MarsGlobeTests
    {
        static MarsGlobe Globe() => new MarsGlobe(48, 36);

        [TestCase(0, 0)]
        [TestCase(10, 10)]
        [TestCase(23.9, 23.9)]
        [TestCase(24, -24)]
        [TestCase(25, -23)]
        [TestCase(-25, 23)]
        [TestCase(48 * 3 + 1, 1)]
        [TestCase(-48 * 5 - 1, -1)]
        public void Wrap_LevaParaDentroDoPlaneta(double value, double expected)
        {
            Assert.That(MarsGlobe.Wrap(value, 48), Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void Delta_AtravessaABordaPeloCaminhoMaisCurto()
        {
            MarsGlobe globe = Globe();
            globe.Delta(23, 17, -23, -17, out double dx, out double dy);
            Assert.That(dx, Is.EqualTo(2).Within(1e-9));
            Assert.That(dy, Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void Distance_UsaACopiaMaisProxima()
        {
            MarsGlobe globe = Globe();
            // O astronauta já deu duas voltas para o leste: a base (origem) continua a 1 unidade.
            Assert.That(globe.Distance(48 * 2 + 1, 0, 0, 0), Is.EqualTo(1).Within(1e-9));
            // O ponto mais longe da base é o "outro lado" do planeta.
            Assert.That(globe.Distance(0, 0, 24, 18), Is.EqualTo(30).Within(1e-9));
        }

        [Test]
        public void LapCounter_VoltaInteiraParaOLeste()
        {
            var laps = new LapCounter(Globe());
            laps.Reset(0, 0);
            int completed = 0;
            for (double x = 0.5; x <= 48.0; x += 0.5)
            {
                if (laps.Move(x, 0))
                    completed++;
            }
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(laps.Laps, Is.EqualTo(1));
        }

        [Test]
        public void LapCounter_ContaComPosicoesJaDobradasNaBorda()
        {
            // As posições chegam "dobradas" (de -24 a 24): atravessar a borda não pode zerar o caminho.
            MarsGlobe globe = Globe();
            var laps = new LapCounter(globe);
            laps.Reset(0, 0);
            int completed = 0;
            for (double y = -0.5; y >= -36.0; y -= 0.5)
            {
                if (laps.Move(0, globe.WrapY(y)))
                    completed++;
            }
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void LapCounter_IrEVoltarNaoContaVolta()
        {
            var laps = new LapCounter(Globe());
            laps.Reset(0, 0);
            for (int trip = 0; trip < 5; trip++)
            {
                for (double x = 0; x <= 30; x += 1)
                    Assert.That(laps.Move(x, 0), Is.False);
                for (double x = 30; x >= 0; x -= 1)
                    Assert.That(laps.Move(x, 0), Is.False);
            }
            Assert.That(laps.Laps, Is.EqualTo(0));
        }

        [Test]
        public void LapMessage_TrazACircunferenciaDeVerdade()
        {
            // 2 × π × 3396,2 km ≈ 21 339 km (NASA, Mars Fact Sheet).
            Assert.That(MarsGlobe.RealEquatorKm, Is.EqualTo(21339).Within(1));
            StringAssert.Contains("21.339 km", MarsGlobe.LapMessage);
        }
    }
}
