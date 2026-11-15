using MissaoMarte.Game;
using UnityEngine;

namespace MissaoMarte.UI
{
    /// <summary>
    /// Tamanho do Marte do jogo e contas com Vector2. O planeta se fecha nas bordas
    /// (ver <see cref="MarsGlobe"/>); a base fica no centro, na origem.
    /// [DIDÁTICO] O tamanho é de jogo, não real: uma volta leste-oeste leva uns 16 s andando, o que
    /// cabe num traje cheio (~19 s fora da base). O planeta de verdade tem ~21 339 km de volta.
    /// </summary>
    public static class MarsWorld
    {
        /// <summary>Uma volta leste-oeste, em unidades (a planta da base mede ~12,7).</summary>
        public const float Width = 48f;

        /// <summary>Uma volta norte-sul, em unidades (a planta da base mede ~9,4).</summary>
        public const float Height = 36f;

        public static readonly MarsGlobe Globe = new MarsGlobe(Width, Height);

        public static Vector2 Size => new Vector2(Width, Height);

        /// <summary>Leva a posição para a cópia do planeta centrada na base.</summary>
        public static Vector2 Wrap(Vector2 position)
        {
            return new Vector2((float)Globe.WrapX(position.x), (float)Globe.WrapY(position.y));
        }

        /// <summary>Menor deslocamento de <paramref name="from"/> até <paramref name="to"/>.</summary>
        public static Vector2 Delta(Vector2 from, Vector2 to)
        {
            Globe.Delta(from.x, from.y, to.x, to.y, out double dx, out double dy);
            return new Vector2((float)dx, (float)dy);
        }

        /// <summary>A cópia de <paramref name="home"/> mais perto de <paramref name="reference"/>.</summary>
        public static Vector2 NearestImage(Vector2 home, Vector2 reference)
        {
            return reference + Delta(reference, home);
        }

        public static float Distance(Vector2 a, Vector2 b)
        {
            return Delta(a, b).magnitude;
        }
    }
}
