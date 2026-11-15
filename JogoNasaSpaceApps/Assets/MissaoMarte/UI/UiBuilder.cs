using UnityEngine;
using UnityEngine.UI;

namespace MissaoMarte.UI
{
    /// <summary>
    /// Funções auxiliares para montar a interface por código (uGUI), sem prefabs nem assets.
    /// Todo elemento é posicionado por âncoras em fração do pai, então a tela se adapta a qualquer resolução.
    /// </summary>
    static class UiBuilder
    {
        static Font cachedFont;
        static Sprite roundedSprite;

        /// <summary>Raio usado nos painéis do HUD (unidades da tela de referência 1280 x 720).</summary>
        public const float DefaultRadius = 16f;

        /// <summary>Raio "infinito": <see cref="RoundedCorners"/> limita à metade do lado menor, e a peça vira uma pílula.</summary>
        public const float Pill = 1000f;

        /// <summary>Raio, em pixels, do canto desenhado no sprite fatiado.</summary>
        public const int SpriteCorner = 48;

        public static Font DefaultFont
        {
            get
            {
                if (cachedFont == null)
                    cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return cachedFont;
            }
        }

        /// <summary>Cria um retângulo filho ocupando a região [min, max] do pai (frações de 0 a 1).</summary>
        public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>Painel com cantos arredondados. <paramref name="radius"/> 0 deixa o retângulo reto.</summary>
        public static Image Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color, float radius = DefaultRadius)
        {
            RectTransform rect = Rect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (radius > 0f)
            {
                image.sprite = RoundedSprite;
                image.type = Image.Type.Sliced;
                image.fillCenter = true;
                // O componente ajusta o raio ao tamanho real da peça (no máximo metade do lado menor).
                var corners = rect.gameObject.AddComponent<RoundedCorners>();
                corners.Radius = radius;
                corners.Apply();
            }
            return image;
        }

        /// <summary>Camada reta que cobre o pai inteiro (escurecer a tela atrás das janelas).</summary>
        public static Image Overlay(string name, Transform parent, Color color)
        {
            return Panel(name, parent, Vector2.zero, Vector2.one, color, 0f);
        }

        /// <summary>Retângulo branco com cantos arredondados e borda suave, fatiado em 9 partes.</summary>
        static Sprite RoundedSprite
        {
            get
            {
                if (roundedSprite != null)
                    return roundedSprite;
                const int size = SpriteCorner * 2 + 4;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "CantoArredondado";
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                var pixels = new Color32[size * size];
                float half = size / 2f;
                float inner = half - SpriteCorner;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float qx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inner, 0f);
                        float qy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inner, 0f);
                        float inside = SpriteCorner - Mathf.Sqrt(qx * qx + qy * qy);
                        byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(inside + 0.5f) * 255f);
                        pixels[y * size + x] = new Color32(255, 255, 255, a);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                var border = new Vector4(SpriteCorner, SpriteCorner, SpriteCorner, SpriteCorner);
                roundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                roundedSprite.name = "CantoArredondado";
                return roundedSprite;
            }
        }

        public static Text Label(
            string name, Transform parent, Vector2 min, Vector2 max,
            string content, int fontSize, TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = Rect(name, parent, min, max);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            // Se o texto não couber, a fonte diminui em vez de cortar.
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 9;
            text.resizeTextMaxSize = fontSize;
            return text;
        }

        public static Button Button(
            string name, Transform parent, Vector2 min, Vector2 max,
            string caption, int fontSize, Color background, Color textColor, out Text captionText)
        {
            Image image = Panel(name, parent, min, max, background, Pill);
            image.raycastTarget = true;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.selectedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            captionText = Label("Texto", image.transform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f),
                caption, fontSize, TextAnchor.MiddleCenter, textColor, FontStyle.Bold);
            return button;
        }
    }
}
