using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MissaoMarte.UI
{
    /// <summary>Uma animação da spritesheet: quadros em ordem, velocidade e se repete.</summary>
    public sealed class SpriteAnimation
    {
        public string Name;
        public Sprite[] Frames;
        public float Fps;
        public bool Loop;
    }

    /// <summary>
    /// Spritesheet no formato do Space Explorers Pixel Pack (CC0): um PNG e um JSON com "frames"
    /// (retângulo de cada quadro, origem no canto superior esquerdo) e, opcionalmente, "animations"
    /// (lista de quadros, fps, loop). Os dois ficam em Resources com o mesmo nome.
    /// Quadros sem animação própria viram uma animação de um quadro só, com o mesmo nome.
    /// </summary>
    public sealed class SpriteSheet
    {
        /// <summary>
        /// Pixels da imagem por unidade do mundo. Personagem e objetos usam a mesma escala (o pack
        /// foi desenhado assim: astronauta de ~84 px ao lado de objetos de 128 px).
        /// </summary>
        public const float PixelsPerUnit = 160f;

        readonly Dictionary<string, SpriteAnimation> animations = new Dictionary<string, SpriteAnimation>();

        static readonly Regex FramePattern = new Regex(
            "\"(?<name>[^\"]+)\"\\s*:\\s*\\{\\s*\"frame\"\\s*:\\s*\\{\\s*\"x\"\\s*:\\s*(?<x>\\d+)\\s*,\\s*\"y\"\\s*:\\s*(?<y>\\d+)\\s*,\\s*\"w\"\\s*:\\s*(?<w>\\d+)\\s*,\\s*\"h\"\\s*:\\s*(?<h>\\d+)");

        static readonly Regex AnimationPattern = new Regex(
            "\"(?<name>[^\"]+)\"\\s*:\\s*\\{\\s*\"frames\"\\s*:\\s*\\[(?<frames>[^\\]]*)\\]\\s*,\\s*\"fps\"\\s*:\\s*(?<fps>[\\d.]+)\\s*,\\s*\"loop\"\\s*:\\s*(?<loop>true|false)");

        static readonly Regex QuotedPattern = new Regex("\"(?<v>[^\"]+)\"");

        /// <param name="pivot">Pivô de cada quadro (0..1, a partir do canto inferior esquerdo).</param>
        SpriteSheet(Texture2D texture, string json, Vector2 pivot)
        {
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            var frames = new Dictionary<string, Sprite>();
            foreach (Match m in FramePattern.Matches(json))
            {
                int x = int.Parse(m.Groups["x"].Value, CultureInfo.InvariantCulture);
                int y = int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture);
                int w = int.Parse(m.Groups["w"].Value, CultureInfo.InvariantCulture);
                int h = int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
                // O JSON conta y de cima para baixo; a textura do Unity, de baixo para cima.
                var rect = new Rect(x, texture.height - y - h, w, h);
                if (rect.xMin < 0 || rect.yMin < 0 || rect.xMax > texture.width || rect.yMax > texture.height)
                {
                    // Textura importada menor que a original (ex.: limite de tamanho no import).
                    Debug.LogWarning("[Missão Marte] Quadro " + m.Groups["name"].Value + " fora da textura " +
                        texture.name + " (" + texture.width + "x" + texture.height + "); confira o import.");
                    continue;
                }
                Sprite sprite = Sprite.Create(texture, rect, pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = m.Groups["name"].Value;
                frames[sprite.name] = sprite;
            }

            foreach (Match m in AnimationPattern.Matches(json))
            {
                var list = new List<Sprite>();
                foreach (Match q in QuotedPattern.Matches(m.Groups["frames"].Value))
                    if (frames.TryGetValue(q.Groups["v"].Value, out Sprite sprite))
                        list.Add(sprite);
                if (list.Count == 0)
                    continue;
                var animation = new SpriteAnimation
                {
                    Name = m.Groups["name"].Value,
                    Frames = list.ToArray(),
                    Fps = Mathf.Max(1f, float.Parse(m.Groups["fps"].Value, CultureInfo.InvariantCulture)),
                    Loop = m.Groups["loop"].Value == "true",
                };
                animations[animation.Name] = animation;
            }

            foreach (KeyValuePair<string, Sprite> frame in frames)
            {
                if (!animations.ContainsKey(frame.Key))
                    animations[frame.Key] = new SpriteAnimation { Name = frame.Key, Frames = new[] { frame.Value }, Fps = 1f, Loop = true };
            }
        }

        /// <summary>Carrega Resources/{path}.png + .json. Devolve null (com aviso) se faltar algo.</summary>
        public static SpriteSheet Load(string path, Vector2 pivot)
        {
            var texture = Resources.Load<Texture2D>(path);
            var json = Resources.Load<TextAsset>(path);
            if (texture == null || json == null)
            {
                Debug.LogWarning("[Missão Marte] Spritesheet não encontrada em Resources/" + path + " (.png e .json).");
                return null;
            }
            var sheet = new SpriteSheet(texture, json.text, pivot);
            if (sheet.animations.Count == 0)
            {
                Debug.LogWarning("[Missão Marte] Nenhum quadro lido de Resources/" + path + ".json.");
                return null;
            }
            return sheet;
        }

        public SpriteAnimation Get(string name)
        {
            return animations.TryGetValue(name, out SpriteAnimation animation) ? animation : null;
        }
    }

    /// <summary>Toca as animações de uma spritesheet num SpriteRenderer (avançado pelo GameView a cada quadro).</summary>
    public sealed class SpriteAnimator
    {
        readonly SpriteSheet sheet;
        readonly string fallback;
        SpriteAnimation current;
        float time;

        public SpriteAnimator(SpriteSheet sheet, SpriteRenderer renderer, string initial)
        {
            this.sheet = sheet;
            Renderer = renderer;
            fallback = initial;
            Play(initial);
            Advance(0f);
        }

        public SpriteRenderer Renderer { get; }

        public string Current => current != null ? current.Name : null;

        /// <summary>Troca de animação (recomeça do primeiro quadro só se for outra).</summary>
        public void Play(string name)
        {
            SpriteAnimation next = sheet.Get(name) ?? sheet.Get(fallback);
            if (next == null || next == current)
                return;
            current = next;
            time = 0f;
        }

        public void Advance(float deltaTime)
        {
            if (current == null)
                return;
            time += Mathf.Max(0f, deltaTime);
            int index = Mathf.FloorToInt(time * current.Fps);
            index = current.Loop ? index % current.Frames.Length : Mathf.Min(index, current.Frames.Length - 1);
            Renderer.sprite = current.Frames[index];
        }

        /// <summary>Os desenhos olham para a direita; espelha quando anda para a esquerda.</summary>
        public void Face(float horizontal)
        {
            if (horizontal > 0.01f)
                Renderer.flipX = false;
            else if (horizontal < -0.01f)
                Renderer.flipX = true;
        }
    }
}
