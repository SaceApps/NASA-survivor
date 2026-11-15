using UnityEngine;

namespace MissaoMarte.UI
{
    /// <summary>
    /// O cenário a partir da planta da base (Resources/Cenario): as áreas claras da imagem são os
    /// módulos pressurizados; as escuras, o solo de Marte. A partir dela são gerados a máscara
    /// "dentro/fora" e uma textura estilizada dos módulos (piso metálico). Trocando a imagem, a base
    /// do jogo muda junto. A base fica no centro do planeta (ver <see cref="MarsWorld"/>); as posições
    /// aqui são da cópia da base na origem (use <see cref="MarsWorld.Wrap"/> antes de consultar).
    /// </summary>
    public sealed class MapData
    {
        public const float PixelsPerUnit = 100f;
        public const string ResourceName = "Cenario";

        readonly bool[] inside;

        public int Width { get; }

        public int Height { get; }

        public Texture2D Texture { get; }

        public Sprite Sprite { get; }

        public Vector2 WorldSize => new Vector2(Width / PixelsPerUnit, Height / PixelsPerUnit);

        public Rect WorldRect => new Rect(-WorldSize.x / 2f, -WorldSize.y / 2f, WorldSize.x, WorldSize.y);

        /// <summary>Carrega a imagem do cenário. Devolve null (com erro no Console) se ela não existir.</summary>
        public static MapData Load()
        {
            var source = Resources.Load<Texture2D>(ResourceName);
            if (source == null)
            {
                Debug.LogError("[Missão Marte] Imagem do cenário não encontrada em Resources/" + ResourceName + ".");
                return null;
            }
            return new MapData(source);
        }

        MapData(Texture2D source)
        {
            Width = source.width;
            Height = source.height;

            Color32[] pixels = ReadPixels(source);
            inside = new bool[Width * Height];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                int luminance = (c.r * 299 + c.g * 587 + c.b * 114) / 1000;
                inside[i] = luminance > 100;
            }

            Texture = BuildStyledTexture();
            Sprite = Sprite.Create(Texture, new Rect(0, 0, Width, Height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            Sprite.name = "CenarioEstilizado";
        }

        /// <summary>Lê os pixels mesmo de texturas importadas sem leitura habilitada (via RenderTexture).</summary>
        static Color32[] ReadPixels(Texture2D source)
        {
            if (source.isReadable)
                return source.GetPixels32();

            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            Color32[] data = copy.GetPixels32();
            DestroySafe(copy);
            return data;
        }

        static void DestroySafe(Object target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        /// <summary>Pixel (x da esquerda, y de baixo) está dentro de um módulo?</summary>
        bool In(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return false;
            return inside[y * Width + x];
        }

        /// <summary>Posição no mundo está dentro de um módulo pressurizado?</summary>
        public bool IsInside(Vector2 world)
        {
            int x = Mathf.RoundToInt(world.x * PixelsPerUnit + Width / 2f);
            int y = Mathf.RoundToInt(world.y * PixelsPerUnit + Height / 2f);
            return In(x, y);
        }

        /// <summary>Converte coordenadas da imagem original (x da esquerda, y de cima) para o mundo.</summary>
        public Vector2 PixelToWorld(float x, float yFromTop)
        {
            return new Vector2((x - Width / 2f) / PixelsPerUnit, (Height / 2f - yFromTop) / PixelsPerUnit);
        }

        // ---------- Textura estilizada ----------

        /// <summary>
        /// Só os módulos e a sombra deles: o lado de fora fica transparente, e por baixo aparece o solo do
        /// planeta inteiro (<see cref="MarsGround"/>). Assim a base não tem um "recorte" retangular.
        /// </summary>
        Texture2D BuildStyledTexture()
        {
            var colors = new Color32[Width * Height];
            var floor = new Color32(198, 202, 208, 255);
            var floorLine = new Color32(180, 185, 192, 255);
            var wall = new Color32(86, 94, 106, 255);
            var wallLight = new Color32(120, 130, 144, 255);
            var shadow = new Color32(0, 0, 0, 72);
            var clear = new Color32(0, 0, 0, 0);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    if (inside[i])
                    {
                        bool edge = !In(x - 4, y) || !In(x + 4, y) || !In(x, y - 4) || !In(x, y + 4);
                        bool innerEdge = !edge && (!In(x - 7, y) || !In(x + 7, y) || !In(x, y - 7) || !In(x, y + 7));
                        if (edge)
                            colors[i] = wall;
                        else if (innerEdge)
                            colors[i] = wallLight;
                        else
                            colors[i] = (x % 34 == 0 || y % 34 == 0) ? floorLine : floor;
                    }
                    else
                    {
                        // Sombra dos módulos (luz vindo do canto superior esquerdo).
                        colors[i] = In(x - 7, y + 7) || In(x - 4, y + 4) ? shadow : clear;
                    }
                }
            }

            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.name = "CenarioEstilizado";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(colors);
            texture.Apply(false, true);
            return texture;
        }
    }
}
