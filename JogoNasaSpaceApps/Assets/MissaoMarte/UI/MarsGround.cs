using UnityEngine;

namespace MissaoMarte.UI
{
    /// <summary>
    /// O solo de Marte do planeta inteiro, gerado por código: areia com ruído e crateras. A textura
    /// se repete sem emenda (o lado direito continua no esquerdo, e o de cima no de baixo), para
    /// o planeta poder se fechar nas bordas. Tudo fixo, sem sorteio entre partidas.
    /// </summary>
    static class MarsGround
    {
        /// <summary>Pixels por unidade do solo (a planta da base usa 100; o solo é mais suave).</summary>
        public const int PixelsPerUnit = 32;

        /// <summary>Área livre de crateras em volta da base (meia largura e meia altura, unidades).</summary>
        static readonly Vector2 BaseClearance = new Vector2(7.5f, 5.8f);

        static Sprite sprite;

        public static Sprite Sprite => sprite != null ? sprite : (sprite = Build());

        static Sprite Build()
        {
            int width = Mathf.RoundToInt(MarsWorld.Width * PixelsPerUnit);
            int height = Mathf.RoundToInt(MarsWorld.Height * PixelsPerUnit);
            var colors = new Color[width * height];

            var sandDark = new Color(0.50f, 0.22f, 0.12f);
            var sandLight = new Color(0.78f, 0.44f, 0.26f);
            var regionTint = new Color(0.62f, 0.30f, 0.20f);

            // Cada camada de ruído tem um número inteiro de células no planeta inteiro: assim ela se repete
            // sem emenda. Os tamanhos das células imitam as frequências do cenário antigo.
            var dunes = new PeriodicNoise(width / 32, height / 32, 32, 11);    // células de 1 unidade
            var hills = new PeriodicNoise(width / 128, height / 128, 128, 23); // 4 unidades
            var grain = new PeriodicNoise(width / 8, height / 8, 8, 37);       // 0,25 unidade
            var regions = new PeriodicNoise(4, 3, width / 4, 51);              // 12 unidades: muda o tom

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float n = hills.Sample(x, y) * 0.50f + dunes.Sample(x, y) * 0.32f + grain.Sample(x, y) * 0.18f;
                    Color c = Color.Lerp(sandDark, sandLight, n);
                    float region = regions.Sample(x, y);
                    c = Color.Lerp(c, c * regionTint * 1.6f, Mathf.Clamp01((region - 0.45f) * 1.4f));
                    colors[y * width + x] = c;
                }
            }

            StampCraters(colors, width, height);

            var pixels = new Color32[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                Color c = colors[i];
                c.a = 1f;
                pixels[i] = c;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "SoloDeMarte";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite result = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            result.name = "SoloDeMarte";
            return result;
        }

        /// <summary>
        /// Crateras em lugares fixos. A maior fica do outro lado do planeta (o ponto mais longe da base),
        /// para servir de referência a quem estiver dando a volta.
        /// </summary>
        static void StampCraters(Color[] colors, int width, int height)
        {
            var light = new Color(0.78f, 0.44f, 0.26f) * 1.1f;

            // Do outro lado do planeta (x = ±24, y = ±18).
            Stamp(colors, width, height, new Vector2(MarsWorld.Width / 2f, MarsWorld.Height / 2f), 3.2f, light);

            var random = new System.Random(2026);
            int placed = 0;
            for (int attempt = 0; attempt < 400 && placed < 46; attempt++)
            {
                var center = new Vector2(
                    (float)(random.NextDouble() - 0.5) * MarsWorld.Width,
                    (float)(random.NextDouble() - 0.5) * MarsWorld.Height);
                // Raios de 0,2 a 2,2 unidades, com mais crateras pequenas.
                float t = (float)random.NextDouble();
                float radius = 0.2f + 2.0f * t * t;
                if (Mathf.Abs(center.x) < BaseClearance.x + radius && Mathf.Abs(center.y) < BaseClearance.y + radius)
                    continue;
                Stamp(colors, width, height, center, radius, light);
                placed++;
            }
        }

        /// <summary>Escurece o fundo e clareia a borda, como no cenário antigo; atravessa as bordas.</summary>
        static void Stamp(Color[] colors, int width, int height, Vector2 center, float radius, Color rim)
        {
            float cx = center.x * PixelsPerUnit + width / 2f;
            float cy = center.y * PixelsPerUnit + height / 2f;
            float r = radius * PixelsPerUnit;
            float rimWidth = Mathf.Max(1.5f, r * 0.09f);
            int reach = Mathf.CeilToInt(r + rimWidth + 1);
            for (int oy = -reach; oy <= reach; oy++)
            {
                for (int ox = -reach; ox <= reach; ox++)
                {
                    int px = Mathf.FloorToInt(cx) + ox;
                    int py = Mathf.FloorToInt(cy) + oy;
                    float d = Mathf.Sqrt((px + 0.5f - cx) * (px + 0.5f - cx) + (py + 0.5f - cy) * (py + 0.5f - cy));
                    if (d >= r + rimWidth)
                        continue;
                    int wx = ((px % width) + width) % width;
                    int wy = ((py % height) + height) % height;
                    int i = wy * width + wx;
                    if (d < r)
                    {
                        // Sombra dentro da cratera, do lado de onde vem a luz (canto superior esquerdo).
                        float shade = 0.84f + 0.12f * (d / r);
                        float side = ((px + 0.5f - cx) - (py + 0.5f - cy)) / (r * 2.8f);
                        colors[i] *= Mathf.Clamp(shade + side * 0.12f, 0.6f, 1.05f);
                    }
                    else
                    {
                        colors[i] = Color.Lerp(colors[i], rim, 0.45f);
                    }
                }
            }
        }

        /// <summary>Ruído de valores numa grade que se repete a cada <c>cellsX</c> por <c>cellsY</c> células.</summary>
        sealed class PeriodicNoise
        {
            readonly float[] values;
            readonly int cellsX;
            readonly int cellsY;
            readonly float cellSize;

            public PeriodicNoise(int cellsX, int cellsY, float cellSize, int seed)
            {
                this.cellsX = Mathf.Max(1, cellsX);
                this.cellsY = Mathf.Max(1, cellsY);
                this.cellSize = cellSize;
                values = new float[this.cellsX * this.cellsY];
                var random = new System.Random(seed);
                for (int i = 0; i < values.Length; i++)
                    values[i] = (float)random.NextDouble();
            }

            float At(int x, int y)
            {
                x = ((x % cellsX) + cellsX) % cellsX;
                y = ((y % cellsY) + cellsY) % cellsY;
                return values[y * cellsX + x];
            }

            public float Sample(int px, int py)
            {
                float fx = px / cellSize;
                float fy = py / cellSize;
                int x0 = Mathf.FloorToInt(fx);
                int y0 = Mathf.FloorToInt(fy);
                float tx = Smooth(fx - x0);
                float ty = Smooth(fy - y0);
                float a = Mathf.Lerp(At(x0, y0), At(x0 + 1, y0), tx);
                float b = Mathf.Lerp(At(x0, y0 + 1), At(x0 + 1, y0 + 1), tx);
                return Mathf.Lerp(a, b, ty);
            }

            static float Smooth(float t) => t * t * (3f - 2f * t);
        }
    }
}
