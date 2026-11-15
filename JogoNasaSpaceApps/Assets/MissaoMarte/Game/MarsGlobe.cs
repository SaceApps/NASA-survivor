using System;
using System.Globalization;
using MissaoMarte.Simulation;

namespace MissaoMarte.Game
{
    /// <summary>
    /// A superfície de Marte no jogo: um retângulo que se fecha nas bordas. Quem sai pela direita
    /// volta pela esquerda, e quem sai por cima volta por baixo; assim não há paredes e dá para dar a
    /// volta no planeta. A base fica na origem. As coordenadas do astronauta podem crescer sem limite;
    /// a "cópia" de cada coisa mais próxima dele é obtida com <see cref="Delta"/>.
    /// </summary>
    public sealed class MarsGlobe
    {
        public MarsGlobe(double width, double height)
        {
            if (!(width > 0) || !(height > 0))
                throw new ArgumentOutOfRangeException(nameof(width), "O tamanho do planeta precisa ser positivo.");
            Width = width;
            Height = height;
        }

        /// <summary>Uma volta no sentido leste-oeste (unidades do mundo).</summary>
        public double Width { get; }

        /// <summary>Uma volta no sentido norte-sul (unidades do mundo).</summary>
        public double Height { get; }

        /// <summary>Leva o valor para o intervalo [-período/2, período/2).</summary>
        public static double Wrap(double value, double period)
        {
            double half = period / 2.0;
            double r = (value + half) % period;
            if (r < 0)
                r += period;
            return r - half;
        }

        public double WrapX(double x) => Wrap(x, Width);

        public double WrapY(double y) => Wrap(y, Height);

        /// <summary>Menor deslocamento de um ponto a outro, podendo atravessar a borda do mapa.</summary>
        public void Delta(double fromX, double fromY, double toX, double toY, out double dx, out double dy)
        {
            dx = WrapX(toX - fromX);
            dy = WrapY(toY - fromY);
        }

        /// <summary>Menor distância entre dois pontos, contando que o mapa se fecha nas bordas.</summary>
        public double Distance(double ax, double ay, double bx, double by)
        {
            Delta(ax, ay, bx, by, out double dx, out double dy);
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Circunferência de Marte no equador (km), a partir do raio equatorial
        /// (<see cref="PhysicalConstants.MarsEquatorialRadiusKm"/>).
        /// </summary>
        public static double RealEquatorKm => 2.0 * Math.PI * PhysicalConstants.MarsEquatorialRadiusKm;

        /// <summary>Aviso mostrado quando o astronauta completa uma volta.</summary>
        public static string LapMessage =>
            "Você deu a volta em Marte! O de verdade tem " +
            RealEquatorKm.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.') +
            " km de volta no equador.";
    }

    /// <summary>
    /// Conta as voltas completas ao redor do planeta. Soma o caminho andado em cada direção (atravessar a
    /// borda conta como andar normalmente) e marca uma volta quando a soma chega ao tamanho do planeta.
    /// Ir e voltar pelo mesmo caminho não conta.
    /// </summary>
    public sealed class LapCounter
    {
        readonly MarsGlobe globe;
        double lastX;
        double lastY;
        double travelledX;
        double travelledY;
        bool hasLast;

        public LapCounter(MarsGlobe globe)
        {
            this.globe = globe ?? throw new ArgumentNullException(nameof(globe));
        }

        public int Laps { get; private set; }

        /// <summary>Recomeça a contagem a partir deste ponto.</summary>
        public void Reset(double x, double y)
        {
            lastX = x;
            lastY = y;
            travelledX = 0;
            travelledY = 0;
            hasLast = true;
            Laps = 0;
        }

        /// <summary>Registra a nova posição. Devolve true se ela completou uma volta.</summary>
        public bool Move(double x, double y)
        {
            if (!hasLast)
            {
                Reset(x, y);
                return false;
            }

            globe.Delta(lastX, lastY, x, y, out double dx, out double dy);
            lastX = x;
            lastY = y;
            travelledX += dx;
            travelledY += dy;

            bool lap = false;
            if (Math.Abs(travelledX) >= globe.Width)
            {
                travelledX -= Math.Sign(travelledX) * globe.Width;
                lap = true;
            }
            if (Math.Abs(travelledY) >= globe.Height)
            {
                travelledY -= Math.Sign(travelledY) * globe.Height;
                lap = true;
            }
            if (lap)
                Laps++;
            return lap;
        }
    }
}
