using UnityEngine;

namespace MissaoMarte.UI
{
    /// <summary>Sprites simples desenhados por código (círculo, quadrado arredondado, losango).</summary>
    static class ShapeSprites
    {
        const int Size = 64;

        static Sprite circle;
        static Sprite ring;
        static Sprite rounded;
        static Sprite diamond;
        static Sprite square;

        public static Sprite Circle => circle != null ? circle : (circle = Make("Circulo", (u, v) => Edge(1f - Len(u, v))));

        public static Sprite Ring => ring != null ? ring : (ring = Make("Anel", (u, v) =>
        {
            float d = Len(u, v);
            return Mathf.Min(Edge(1f - d), Edge(d - 0.72f));
        }));

        public static Sprite Rounded => rounded != null ? rounded : (rounded = Make("Arredondado", (u, v) =>
        {
            const float r = 0.35f;
            float qx = Mathf.Max(Mathf.Abs(u) - (1f - r), 0f);
            float qy = Mathf.Max(Mathf.Abs(v) - (1f - r), 0f);
            return Edge(r - Mathf.Sqrt(qx * qx + qy * qy));
        }));

        public static Sprite Diamond => diamond != null ? diamond : (diamond = Make("Losango", (u, v) => Edge(1f - (Mathf.Abs(u) + Mathf.Abs(v)))));

        public static Sprite Square => square != null ? square : (square = Make("Quadrado", (u, v) => 1f));

        /// <summary>Seta em forma de dardo apontando para a direita (+x); gire o Transform para apontar.</summary>
        public static Sprite Arrow => arrow != null ? arrow : (arrow = Make("Seta", (u, v) =>
        {
            // Ponta em (1, 0), cantos de trás em (-0,75, ±0,5) e entalhe até (-0,3, 0).
            // Distância até o lado mais perto (positiva = dentro).
            const float sideLength = 1.8200f; // |(0,5, 1,75)|
            const float notchLength = 1.3454f; // |(1, 0,9)|
            float top = (-0.5f * (u + 0.75f) - 1.75f * (v - 0.5f)) / sideLength;
            float bottom = (-0.5f * (u + 0.75f) + 1.75f * (v + 0.5f)) / sideLength;
            float notch = (u + 0.3f + 0.9f * Mathf.Abs(v)) / notchLength;
            return Edge(Mathf.Min(notch, Mathf.Min(top, bottom)));
        }));

        static Sprite arrow;

        static float Len(float u, float v) => Mathf.Sqrt(u * u + v * v);

        /// <summary>Borda suave: distância positiva = dentro.</summary>
        static float Edge(float distance) => Mathf.Clamp01(distance * Size * 0.5f);

        static Sprite Make(string name, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size * 2f - 1f;
                    float v = (y + 0.5f) / Size * 2f - 1f;
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(u, v)) * 255f);
                    pixels[y * Size + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            // 64 pixels por unidade: o sprite mede 1 x 1 unidade e é escalado pelo Transform.
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = name;
            return sprite;
        }
    }
}
