using System;
using System.Collections.Generic;
using MissaoMarte.Events;
using MissaoMarte.Game;
using MissaoMarte.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace MissaoMarte.UI
{
    /// <summary>O que o astronauta pode fazer no ponto mais próximo (montado pelo controlador).</summary>
    public struct InteractionPrompt
    {
        public string Text;

        /// <summary>Progresso de uma ação de segurar a tecla (0 a 1), ou negativo se não houver.</summary>
        public float Progress;

        /// <summary>Falso quando a ação não está disponível (o texto explica o motivo).</summary>
        public bool Enabled;

        public static InteractionPrompt None => new InteractionPrompt { Text = null, Progress = -1f, Enabled = false };
    }

    /// <summary>
    /// Tudo o que aparece na tela: o mundo (cenário, estações, astronauta) e o HUD.
    /// Só desenha o estado da <see cref="HabitatMission"/>; regras e cálculos ficam fora daqui.
    /// </summary>
    public sealed class GameView
    {
        // ---------- Cores ----------
        static readonly Color Panel = new Color(0.05f, 0.06f, 0.09f, 0.82f);
        static readonly Color TextMain = new Color(0.95f, 0.95f, 0.93f);
        static readonly Color TextDim = new Color(0.70f, 0.73f, 0.78f);
        static readonly Color Good = new Color(0.35f, 0.82f, 0.45f);
        static readonly Color Warn = new Color(0.98f, 0.76f, 0.25f);
        static readonly Color Bad = new Color(0.95f, 0.32f, 0.27f);
        static readonly Color Off = new Color(0.50f, 0.52f, 0.56f);
        static readonly Color Energy = new Color(0.98f, 0.82f, 0.30f);
        static readonly Color WaterBlue = new Color(0.32f, 0.62f, 0.98f);
        static readonly Color LessonBlue = new Color(0.12f, 0.22f, 0.40f, 0.92f);
        static readonly Color IceCyan = new Color(0.62f, 0.92f, 1f);
        static readonly Color PropShadow = new Color(0, 0, 0, 0.30f);
        /// <summary>Tinta de um objeto desligado ou sem energia.</summary>
        static readonly Color PropOff = new Color(0.45f, 0.45f, 0.50f);
        /// <summary>Tinta da poeira sobre os painéis.</summary>
        static readonly Color Dust = new Color(0.85f, 0.58f, 0.42f);
        static readonly Color Accent = new Color(0.82f, 0.36f, 0.20f);

        sealed class SpotView
        {
            public SpotDefinition Definition;
            public Vector2 Position;
            public GameObject Root;
            public SpriteRenderer Body;
            public SpriteRenderer Dot;
            public SpriteRenderer Highlight;
            public TextMesh Code;
            public TextMesh Caption;
            public readonly List<SpriteRenderer> Parts = new List<SpriteRenderer>();

            /// <summary>Objetos da spritesheet (vazio quando o ponto usa as formas simples).</summary>
            public readonly List<SpriteAnimator> Props = new List<SpriteAnimator>();
        }

        /// <summary>Algo do mundo que tem uma posição "de casa" e é desenhado na cópia mais perto da câmera.</summary>
        sealed class Wrapped
        {
            public Transform Transform;
            public Vector2 Home;
        }

        /// <summary>Seta na borda da tela apontando para algo fora de vista.</summary>
        sealed class Arrow
        {
            public GameObject Root;
            public Vector2 Home;
            public SpotView Spot;
            public bool Wanted = true;
        }

        sealed class Gauge
        {
            public Text Value;
            public Text Note;
            public Image Fill;
        }

        sealed class Toast
        {
            public GameObject Root;
            public Image Background;
            public Text Text;
            public float TimeLeft;
        }

        readonly MapData map;
        readonly List<SpotView> spots = new List<SpotView>();
        readonly Transform groundRoot;
        readonly List<Wrapped> wrapped = new List<Wrapped>();
        readonly List<Arrow> arrows = new List<Arrow>();

        /// <summary>Pedras soltas pelo planeta (só enfeite, para dar noção de distância e movimento).</summary>
        const int RockCount = 40;

        /// <summary>Zoom da câmera do jogo; as setas crescem junto quando a câmera se afasta.</summary>
        const float ArrowReferenceHalfHeight = 3.3f;
        readonly SpriteSheet props;
        readonly SpriteSheet items;

        // Astronauta
        readonly Transform astronaut;
        readonly SpriteAnimator astronautAnimator;
        Vector2 astronautVelocity;
        bool astronautWorking;
        readonly SpriteRenderer carryIce;
        readonly SpriteRenderer carrySample;
        readonly GameObject suitBar;
        readonly Transform suitFill;
        readonly SpriteRenderer suitFillRenderer;

        // HUD
        readonly Image skyOverlay;
        readonly Gauge energy;
        readonly Gauge oxygen;
        readonly Gauge co2;
        readonly Gauge water;
        readonly Text solText;
        readonly Text clockText;
        readonly Image[] objectiveBoxes = new Image[3];
        readonly Text[] objectiveTexts = new Text[3];
        readonly List<Toast> toasts = new List<Toast>();
        readonly GameObject promptRoot;
        readonly Text promptText;
        readonly GameObject promptBar;
        readonly Image promptFill;
        readonly GameObject suitHud;
        readonly Image suitHudFill;
        readonly Text suitHudText;
        readonly Text carryText;
        readonly Text[] equipmentStates;
        readonly Image[] equipmentDots;
        readonly GameObject introRoot;
        readonly GameObject briefingRoot;
        readonly Text briefingGameHeading;
        readonly Text[] briefingReal = new Text[MarsBriefing.MaxLines];
        readonly Text[] briefingGame = new Text[MarsBriefing.MaxLines];
        readonly Button briefingButton;
        readonly Text briefingButtonText;
        readonly GameObject endRoot;
        readonly Text endTitle;
        readonly Text endText;
        readonly Text endStats;
        readonly GameObject discoveryRoot;
        readonly Text discoveryText;
        readonly Button discoveryButton;
        readonly Text discoveryButtonText;
        readonly GameObject guideRoot;
        readonly Text guideTitle;
        readonly Image guidePicture;
        readonly Text guideWhat;
        readonly Text guideWhy;
        readonly Text guideFact;
        readonly Text guideSource;
        readonly Button guideButton;
        readonly Text guideButtonText;
        readonly GameObject menuRoot;

        static readonly string[] EquipmentOrder =
        {
            EquipmentIds.Co2Scrubber, EquipmentIds.OxygenGenerator, EquipmentIds.WaterRecycler,
            EquipmentIds.IceProcessor, EquipmentIds.Infrastructure,
        };

        public event Action StartRequested;
        public event Action RestartRequested;

        /// <summary>O jogador clicou em "Continuar" na tela "Marte de verdade × Marte do jogo".</summary>
        public event Action BriefingClosed;

        public bool IsBriefingVisible => briefingRoot.activeSelf;

        /// <summary>O jogador fechou a janela de descoberta (botão ou clique).</summary>
        public event Action DiscoveryClosed;

        public bool IsDiscoveryVisible => discoveryRoot.activeSelf;

        /// <summary>O jogador fechou o tutorial de uma máquina.</summary>
        public event Action GuideClosed;

        public bool IsGuideVisible => guideRoot.activeSelf;

        /// <summary>O jogador escolheu "Sair do jogo" no menu.</summary>
        public event Action QuitRequested;

        public bool IsMenuVisible => menuRoot.activeSelf;

        public Transform Astronaut => astronaut;

        /// <summary>Spritesheet do astronauta em Resources (PNG + JSON com o mesmo nome).</summary>
        public const string AstronautSheet = "Personagens/kai_spritesheet";

        /// <summary>Linhas transparentes abaixo dos pés no quadro de 128 px (o pivô fica nos pés).</summary>
        const float AstronautFeetMarginPixels = 3f;

        /// <summary>Objetos da base (128 px, animados) e itens pequenos (64 px), em Resources.</summary>
        public const string PropsSheet = "Props/props_128_spritesheet";
        public const string ItemsSheet = "Props/items_64_spritesheet";

        /// <summary>Altura dos pés do desenho em relação ao centro do astronauta.</summary>
        const float AstronautFeetY = -0.12f;

        /// <summary>Movimento atual (para escolher a animação e o lado para onde ele olha).</summary>
        public void SetAstronautMotion(Vector2 velocity, bool working)
        {
            astronautVelocity = velocity;
            astronautWorking = working;
        }

        public MapData Map => map;

        /// <param name="root">Pai de tudo que for criado.</param>
        /// <param name="map">Cenário.</param>
        /// <param name="hudCamera">Null para desenhar o HUD direto na tela (jogo); uma câmera para desenhar
        /// na imagem dela (capturas de tela no Editor).</param>
        public GameView(Transform root, MapData map, Camera hudCamera = null)
        {
            this.map = map;

            // ---------- Mundo ----------
            Transform world = new GameObject("Mundo").transform;
            world.SetParent(root, false);

            // Solo do planeta inteiro: 3 x 3 cópias em volta da câmera, para nunca aparecer borda.
            groundRoot = new GameObject("SoloDeMarte").transform;
            groundRoot.SetParent(world, false);
            Sprite ground = MarsGround.Sprite;
            for (int gy = -1; gy <= 1; gy++)
            {
                for (int gx = -1; gx <= 1; gx++)
                {
                    var tile = new GameObject("Solo " + gx + "," + gy).AddComponent<SpriteRenderer>();
                    tile.transform.SetParent(groundRoot, false);
                    tile.transform.localPosition = new Vector3(gx * MarsWorld.Width, gy * MarsWorld.Height, 0f);
                    tile.sprite = ground;
                    tile.sortingOrder = -10;
                }
            }

            var background = new GameObject("Cenario").AddComponent<SpriteRenderer>();
            background.transform.SetParent(world, false);
            background.sprite = map.Sprite;
            background.sortingOrder = 0;
            AddWrapped(background.transform, Vector2.zero);

            // Objetos com o pivô no meio da parte desenhada (que fica na metade de baixo do quadro).
            props = SpriteSheet.Load(PropsSheet, new Vector2(0.5f, 0.45f));
            items = SpriteSheet.Load(ItemsSheet, new Vector2(0.5f, 0.5f));
            BuildRocks(world);
            foreach (SpotDefinition definition in BaseLayout.Spots)
                spots.Add(BuildSpot(world, definition));

            // Setas na borda da tela: a base, os sinais de gelo e as amostras.
            BuildArrow(world, "Seta base", map.PixelToWorld(BaseLayout.SpawnPixel.x, BaseLayout.SpawnPixel.y), null, TextMain);
            foreach (SpotView spot in spots)
            {
                if (spot.Definition.Kind == SpotKind.IceDeposit)
                    BuildArrow(world, "Seta " + spot.Definition.Id, spot.Position, spot, IceCyan);
                else if (spot.Definition.Kind == SpotKind.Sample)
                    BuildArrow(world, "Seta " + spot.Definition.Id, spot.Position, spot, new Color(0.80f, 0.62f, 1f));
            }

            astronaut = new GameObject("Astronauta").transform;
            astronaut.SetParent(world, false);
            astronaut.position = map.PixelToWorld(BaseLayout.SpawnPixel.x, BaseLayout.SpawnPixel.y);
            float suitBarHeight = 0.32f;
            SpriteSheet sheet = SpriteSheet.Load(AstronautSheet, new Vector2(0.5f, AstronautFeetMarginPixels / 128f));
            if (sheet != null)
            {
                // Personagem Kai (Space Explorers Pixel Pack, CC0). Os pés ficam um pouco abaixo do
                // centro do colisor, para o desenho "pisar" onde o astronauta está.
                MakeSprite(astronaut, "Sombra", ShapeSprites.Circle, new Vector2(0f, AstronautFeetY), new Vector2(0.36f, 0.11f), new Color(0, 0, 0, 0.35f), 19);
                var bodyRenderer = new GameObject("Corpo").AddComponent<SpriteRenderer>();
                bodyRenderer.transform.SetParent(astronaut, false);
                bodyRenderer.transform.localPosition = new Vector3(0f, AstronautFeetY, 0f);
                bodyRenderer.sortingOrder = 20;
                astronautAnimator = new SpriteAnimator(sheet, bodyRenderer, "idle");
                carryIce = MakeSprite(astronaut, "Gelo", ShapeSprites.Diamond, new Vector2(0.2f, 0.08f), 0.16f, IceCyan, 23);
                carrySample = MakeSprite(astronaut, "Amostra", ShapeSprites.Diamond, new Vector2(-0.2f, 0.08f), 0.13f, new Color(0.75f, 0.55f, 1f), 23);
                suitBarHeight = 0.56f;
            }
            else
            {
                MakeSprite(astronaut, "Sombra", ShapeSprites.Circle, new Vector2(0.03f, -0.04f), 0.36f, new Color(0, 0, 0, 0.35f), 19);
                MakeSprite(astronaut, "Corpo", ShapeSprites.Circle, Vector2.zero, 0.32f, Color.white, 20);
                MakeSprite(astronaut, "Mochila", ShapeSprites.Rounded, new Vector2(0f, -0.1f), 0.16f, new Color(0.72f, 0.74f, 0.78f), 21);
                MakeSprite(astronaut, "Visor", ShapeSprites.Circle, new Vector2(0f, 0.05f), 0.17f, new Color(0.95f, 0.55f, 0.15f), 22);
                carryIce = MakeSprite(astronaut, "Gelo", ShapeSprites.Diamond, new Vector2(0.17f, 0.12f), 0.16f, IceCyan, 23);
                carrySample = MakeSprite(astronaut, "Amostra", ShapeSprites.Diamond, new Vector2(-0.17f, 0.12f), 0.13f, new Color(0.75f, 0.55f, 1f), 23);
            }

            suitBar = new GameObject("BarraTraje");
            suitBar.transform.SetParent(astronaut, false);
            suitBar.transform.localPosition = new Vector3(0f, suitBarHeight, 0f);
            MakeSprite(suitBar.transform, "Fundo", ShapeSprites.Square, Vector2.zero, new Vector2(0.56f, 0.09f), new Color(0, 0, 0, 0.7f), 24);
            suitFillRenderer = MakeSprite(suitBar.transform, "Nivel", ShapeSprites.Square, Vector2.zero, new Vector2(0.52f, 0.05f), IceCyan, 25);
            suitFill = suitFillRenderer.transform;

            // ---------- HUD ----------
            var canvasObject = new GameObject("HUD", typeof(RectTransform));
            canvasObject.transform.SetParent(root, false);
            canvasObject.layer = 5;
            var canvas = canvasObject.AddComponent<Canvas>();
            if (hudCamera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = hudCamera;
                canvas.planeDistance = 1f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            canvas.sortingOrder = 10;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            Transform hud = canvasObject.transform;

            skyOverlay = UiBuilder.Overlay("Ceu", hud, new Color(0, 0, 0, 0));

            // Recursos (canto superior esquerdo)
            Transform resources = UiBuilder.Panel("Recursos", hud, new Vector2(0.012f, 0.66f), new Vector2(0.235f, 0.985f), Panel).transform;
            energy = BuildGauge(resources, 3, "ENERGIA", Energy);
            oxygen = BuildGauge(resources, 2, "OXIGÊNIO", Good);
            co2 = BuildGauge(resources, 1, "CO2", Good);
            water = BuildGauge(resources, 0, "ÁGUA", WaterBlue);

            // Sol e relógio (centro do topo)
            Transform clock = UiBuilder.Panel("Relogio", hud, new Vector2(0.40f, 0.885f), new Vector2(0.60f, 0.985f), Panel).transform;
            solText = UiBuilder.Label("Sol", clock, new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.98f), "", 26, TextAnchor.MiddleCenter, TextMain, FontStyle.Bold);
            clockText = UiBuilder.Label("Hora", clock, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.48f), "", 15, TextAnchor.MiddleCenter, TextDim, FontStyle.Bold);

            // Objetivos (canto superior direito)
            Transform objectives = UiBuilder.Panel("Objetivos", hud, new Vector2(0.765f, 0.80f), new Vector2(0.988f, 0.985f), Panel).transform;
            UiBuilder.Label("Titulo", objectives, new Vector2(0.06f, 0.76f), new Vector2(0.95f, 0.97f), "PRIMEIRA FONTE", 13, TextAnchor.MiddleLeft, Accent, FontStyle.Bold);
            for (int i = 0; i < 3; i++)
            {
                float top = 0.74f - i * 0.245f;
                objectiveBoxes[i] = UiBuilder.Panel("Marca" + i, objectives, new Vector2(0.06f, top - 0.15f), new Vector2(0.115f, top - 0.04f), Off, 4f);
                objectiveTexts[i] = UiBuilder.Label("Texto" + i, objectives, new Vector2(0.15f, top - 0.21f), new Vector2(0.97f, top), "", 14, TextAnchor.MiddleLeft, TextMain);
            }

            // Avisos (centro, abaixo do relógio)
            for (int i = 0; i < 3; i++)
            {
                float top = 0.865f - i * 0.075f;
                Image background2 = UiBuilder.Panel("Aviso" + i, hud, new Vector2(0.30f, top - 0.065f), new Vector2(0.70f, top), Panel, UiBuilder.Pill);
                Text text = UiBuilder.Label("Texto", background2.transform, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f), "", 15, TextAnchor.MiddleCenter, TextMain, FontStyle.Bold);
                background2.gameObject.SetActive(false);
                toasts.Add(new Toast { Root = background2.gameObject, Background = background2, Text = text });
            }

            // Ação disponível (centro da base)
            Image prompt = UiBuilder.Panel("Acao", hud, new Vector2(0.32f, 0.035f), new Vector2(0.68f, 0.12f), Panel, 24f);
            promptRoot = prompt.gameObject;
            promptText = UiBuilder.Label("Texto", prompt.transform, new Vector2(0.03f, 0.30f), new Vector2(0.97f, 0.95f), "", 18, TextAnchor.MiddleCenter, TextMain, FontStyle.Bold);
            Image bar = UiBuilder.Panel("Barra", prompt.transform, new Vector2(0.10f, 0.12f), new Vector2(0.90f, 0.24f), new Color(0, 0, 0, 0.6f), UiBuilder.Pill);
            promptBar = bar.gameObject;
            promptFill = UiBuilder.Panel("Nivel", bar.transform, Vector2.zero, Vector2.one, Good, UiBuilder.Pill);

            // Traje e carga (canto inferior esquerdo)
            Image suitPanel = UiBuilder.Panel("Traje", hud, new Vector2(0.012f, 0.035f), new Vector2(0.235f, 0.12f), Panel);
            suitHud = suitPanel.gameObject;
            suitHudText = UiBuilder.Label("Texto", suitPanel.transform, new Vector2(0.05f, 0.52f), new Vector2(0.95f, 0.95f), "TRAJE", 13, TextAnchor.MiddleLeft, TextMain, FontStyle.Bold);
            Image suitBack = UiBuilder.Panel("Barra", suitPanel.transform, new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.42f), new Color(0, 0, 0, 0.6f), UiBuilder.Pill);
            suitHudFill = UiBuilder.Panel("Nivel", suitBack.transform, Vector2.zero, Vector2.one, IceCyan, UiBuilder.Pill);
            carryText = UiBuilder.Label("Carga", hud, new Vector2(0.012f, 0.125f), new Vector2(0.30f, 0.17f), "", 14, TextAnchor.MiddleLeft, TextMain, FontStyle.Bold);

            // Equipamentos (canto inferior direito)
            Transform equipment = UiBuilder.Panel("Equipamentos", hud, new Vector2(0.765f, 0.035f), new Vector2(0.988f, 0.235f), Panel).transform;
            equipmentStates = new Text[EquipmentOrder.Length];
            equipmentDots = new Image[EquipmentOrder.Length];
            for (int i = 0; i < EquipmentOrder.Length; i++)
            {
                float top = 0.95f - i * 0.185f;
                equipmentDots[i] = UiBuilder.Panel("Ponto" + i, equipment, new Vector2(0.04f, top - 0.12f), new Vector2(0.085f, top - 0.05f), Off, UiBuilder.Pill);
                UiBuilder.Label("Nome" + i, equipment, new Vector2(0.11f, top - 0.17f), new Vector2(0.62f, top), Explanations.EquipmentName(EquipmentOrder[i]), 13, TextAnchor.MiddleLeft, TextMain);
                equipmentStates[i] = UiBuilder.Label("Estado" + i, equipment, new Vector2(0.60f, top - 0.17f), new Vector2(0.97f, top), "", 12, TextAnchor.MiddleRight, TextDim, FontStyle.Bold);
            }

            // Abertura
            Image intro = UiBuilder.Overlay("Abertura", hud, new Color(0.02f, 0.02f, 0.04f, 0.78f));
            intro.raycastTarget = true;
            introRoot = intro.gameObject;
            Transform introBox = UiBuilder.Panel("Caixa", intro.transform, new Vector2(0.25f, 0.24f), new Vector2(0.75f, 0.78f), Panel, 28f).transform;
            UiBuilder.Label("Missao", introBox, new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.95f), "MISSÃO MARTE", 18, TextAnchor.MiddleCenter, Accent, FontStyle.Bold);
            UiBuilder.Label("Titulo", introBox, new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.82f), PrimeiraFonte.MissionName, 32, TextAnchor.MiddleCenter, TextMain, FontStyle.Bold);
            UiBuilder.Label("Texto", introBox, new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.62f),
                "Mantenha a base viva por 5 sóis.\nAche gelo lá fora e tire dele 20 L de água.\n\nWASD anda   -   E interage\nFora dos módulos, o traje gasta oxigênio.",
                18, TextAnchor.MiddleCenter, TextMain);
            Button start = UiBuilder.Button("Comecar", introBox, new Vector2(0.30f, 0.07f), new Vector2(0.70f, 0.22f), "COMEÇAR  (E)", 20, Accent, TextMain, out _);
            start.onClick.AddListener(() => StartRequested?.Invoke());

            // Marte de verdade × Marte do jogo: aparece por cima da abertura, uma vez por sessão
            Image briefing = UiBuilder.Overlay("Briefing", hud, new Color(0.02f, 0.02f, 0.04f, 0.9f));
            briefing.raycastTarget = true;
            briefingRoot = briefing.gameObject;
            Image briefingBox = UiBuilder.Panel("Caixa", briefing.transform, new Vector2(0.07f, 0.05f), new Vector2(0.93f, 0.95f), Panel, 28f);
            briefingBox.color = new Color(Panel.r, Panel.g, Panel.b, 0.98f);
            Transform b = briefingBox.transform;
            UiBuilder.Panel("Faixa", b, new Vector2(0.42f, 0.95f), new Vector2(0.58f, 0.965f), Accent, UiBuilder.Pill);
            UiBuilder.Label("Rotulo", b, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.94f), MarsBriefing.Label, 15, TextAnchor.MiddleCenter, Accent, FontStyle.Bold);
            UiBuilder.Label("Titulo", b, new Vector2(0.05f, 0.79f), new Vector2(0.95f, 0.885f), MarsBriefing.Title, 30, TextAnchor.MiddleCenter, TextMain, FontStyle.Bold);
            BriefingColumn(b, "Verdade", new Vector2(0.03f, 0.2f), new Vector2(0.49f, 0.77f), new Color(0.62f, 0.92f, 1f, 0.08f),
                IceCyan, briefingReal).text = MarsBriefing.RealHeading;
            briefingGameHeading = BriefingColumn(b, "Jogo", new Vector2(0.51f, 0.2f), new Vector2(0.97f, 0.77f),
                new Color(Dust.r, Dust.g, Dust.b, 0.12f), Warn, briefingGame);
            UiBuilder.Label("Nota", b, new Vector2(0.05f, 0.135f), new Vector2(0.95f, 0.185f), MarsBriefing.Footnote, 13, TextAnchor.MiddleCenter, TextDim, FontStyle.Italic);
            briefingButton = UiBuilder.Button("Continuar", b, new Vector2(0.36f, 0.025f), new Vector2(0.64f, 0.115f), "CONTINUAR  (E)", 20, Accent, TextMain, out briefingButtonText);
            briefingButton.onClick.AddListener(() => BriefingClosed?.Invoke());
            briefingRoot.SetActive(false);

            // Fim de missão
            Image end = UiBuilder.Overlay("Fim", hud, new Color(0.02f, 0.02f, 0.04f, 0.8f));
            end.raycastTarget = true;
            endRoot = end.gameObject;
            Transform endBox = UiBuilder.Panel("Caixa", end.transform, new Vector2(0.24f, 0.24f), new Vector2(0.76f, 0.78f), Panel, 28f).transform;
            endTitle = UiBuilder.Label("Titulo", endBox, new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.94f), "", 38, TextAnchor.MiddleCenter, TextMain, FontStyle.Bold);
            endText = UiBuilder.Label("Texto", endBox, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.72f), "", 19, TextAnchor.MiddleCenter, TextMain);
            endStats = UiBuilder.Label("Numeros", endBox, new Vector2(0.08f, 0.26f), new Vector2(0.92f, 0.42f), "", 15, TextAnchor.MiddleCenter, TextDim);
            Button again = UiBuilder.Button("DeNovo", endBox, new Vector2(0.30f, 0.07f), new Vector2(0.70f, 0.22f), "JOGAR DE NOVO  (R)", 20, Accent, TextMain, out _);
            again.onClick.AddListener(() => RestartRequested?.Invoke());
            endRoot.SetActive(false);

            // Descoberta da pesquisa: janela no centro, fundo escurecido, tempo parado
            Image discovery = UiBuilder.Overlay("Descoberta", hud, new Color(0.02f, 0.02f, 0.04f, 0.72f));
            discovery.raycastTarget = true;
            discoveryRoot = discovery.gameObject;
            Image discoveryBox = UiBuilder.Panel("Caixa", discovery.transform, new Vector2(0.27f, 0.25f), new Vector2(0.73f, 0.77f), LessonBlue, 28f);
            discoveryBox.color = new Color(LessonBlue.r, LessonBlue.g, LessonBlue.b, 0.98f);
            Transform box = discoveryBox.transform;
            UiBuilder.Panel("Faixa", box, new Vector2(0.40f, 0.925f), new Vector2(0.60f, 0.95f), Accent, UiBuilder.Pill);
            UiBuilder.Label("Rotulo", box, new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.93f), "PESQUISA CONCLUÍDA", 16, TextAnchor.MiddleCenter, IceCyan, FontStyle.Bold);
            UiBuilder.Label("Titulo", box, new Vector2(0.05f, 0.66f), new Vector2(0.95f, 0.81f), "DESCOBERTA SOBRE MARTE", 28, TextAnchor.MiddleCenter, TextMain, FontStyle.Bold);
            discoveryText = UiBuilder.Label("Fato", box, new Vector2(0.08f, 0.30f), new Vector2(0.92f, 0.64f), "", 26, TextAnchor.MiddleCenter, TextMain, FontStyle.Bold);
            discoveryButton = UiBuilder.Button("Entendi", box, new Vector2(0.30f, 0.07f), new Vector2(0.70f, 0.22f), "ENTENDI  (E)", 20, Accent, TextMain, out discoveryButtonText);
            discoveryButton.onClick.AddListener(() => DiscoveryClosed?.Invoke());
            discoveryRoot.SetActive(false);

            // Tutorial da máquina: aparece antes do primeiro uso, com o tempo parado
            Image guide = UiBuilder.Overlay("Tutorial", hud, new Color(0.02f, 0.02f, 0.04f, 0.75f));
            guide.raycastTarget = true;
            guideRoot = guide.gameObject;
            Image guideBox = UiBuilder.Panel("Caixa", guide.transform, new Vector2(0.16f, 0.08f), new Vector2(0.84f, 0.92f), LessonBlue, 28f);
            guideBox.color = new Color(LessonBlue.r, LessonBlue.g, LessonBlue.b, 0.98f);
            Transform g = guideBox.transform;
            UiBuilder.Panel("Faixa", g, new Vector2(0.42f, 0.945f), new Vector2(0.58f, 0.965f), Accent, UiBuilder.Pill);
            UiBuilder.Label("Rotulo", g, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.93f), "CONHEÇA A MÁQUINA", 15, TextAnchor.MiddleCenter, IceCyan, FontStyle.Bold);
            guideTitle = UiBuilder.Label("Titulo", g, new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.87f), "", 32, TextAnchor.MiddleCenter, TextMain, FontStyle.Bold);

            Image frame = UiBuilder.Panel("Moldura", g, new Vector2(0.04f, 0.22f), new Vector2(0.30f, 0.74f), new Color(1f, 1f, 1f, 0.07f), 22f);
            guidePicture = UiBuilder.Panel("Figura", frame.transform, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), Color.white, 0f);
            guidePicture.preserveAspect = true;

            guideWhat = GuideSection(g, "OQueFaz", "O QUE FAZ", 0.745f);
            guideWhy = GuideSection(g, "PorQue", "POR QUE A NASA PRECISA DISSO EM MARTE", 0.56f);
            guideFact = GuideSection(g, "DeVerdade", "ACONTECEU DE VERDADE", 0.375f);
            guideSource = UiBuilder.Label("Fonte", g, new Vector2(0.34f, 0.155f), new Vector2(0.96f, 0.195f), "", 12, TextAnchor.MiddleLeft, TextDim, FontStyle.Italic);

            guideButton = UiBuilder.Button("Entendi", g, new Vector2(0.36f, 0.03f), new Vector2(0.64f, 0.135f), "ENTENDI  (E)", 20, Accent, TextMain, out guideButtonText);
            guideButton.onClick.AddListener(() => GuideClosed?.Invoke());
            guideRoot.SetActive(false);

            // Menu (Esc): fica por cima de tudo e congela o jogo
            Image menu = UiBuilder.Overlay("Menu", hud, new Color(0.02f, 0.02f, 0.04f, 0.8f));
            menu.raycastTarget = true;
            menuRoot = menu.gameObject;
            Transform menuBox = UiBuilder.Panel("Caixa", menu.transform, new Vector2(0.36f, 0.32f), new Vector2(0.64f, 0.70f), Panel, 28f).transform;
            UiBuilder.Panel("Faixa", menuBox, new Vector2(0.38f, 0.915f), new Vector2(0.62f, 0.945f), Accent, UiBuilder.Pill);
            UiBuilder.Label("Titulo", menuBox, new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.92f), "JOGO PAUSADO", 30, TextAnchor.MiddleCenter, TextMain, FontStyle.Bold);
            Button quit = UiBuilder.Button("Sair", menuBox, new Vector2(0.15f, 0.34f), new Vector2(0.85f, 0.56f), "SAIR DO JOGO", 20, Accent, TextMain, out _);
            quit.onClick.AddListener(() => QuitRequested?.Invoke());
            UiBuilder.Label("Voltar", menuBox, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.24f), "Esc  volta ao jogo", 15, TextAnchor.MiddleCenter, TextDim);
            menuRoot.SetActive(false);
        }

        // ---------- Construção do mundo ----------

        SpotView BuildSpot(Transform world, SpotDefinition definition)
        {
            var view = new SpotView { Definition = definition };
            view.Position = definition.Home(map);
            view.Root = new GameObject(definition.Id);
            view.Root.transform.SetParent(world, false);
            view.Root.transform.position = view.Position;
            AddWrapped(view.Root.transform, view.Position);
            Transform t = view.Root.transform;

            view.Highlight = MakeSprite(t, "Destaque", ShapeSprites.Ring, Vector2.zero, definition.Radius * 2f, new Color(1, 1, 1, 0.0f), 2);

            if (BuildPropSpot(view, t))
                return view;

            switch (definition.Kind)
            {
                case SpotKind.Panels:
                    for (int row = 0; row < 2; row++)
                    {
                        for (int col = 0; col < 4; col++)
                        {
                            var offset = new Vector2((col - 1.5f) * 0.5f, (row - 0.5f) * 0.36f);
                            MakeSprite(t, "Moldura", ShapeSprites.Square, offset, new Vector2(0.46f, 0.30f), new Color(0.75f, 0.77f, 0.80f), 3);
                            view.Parts.Add(MakeSprite(t, "Painel", ShapeSprites.Square, offset, new Vector2(0.42f, 0.26f), definition.Color, 4));
                        }
                    }
                    view.Caption = WorldText(t, "Legenda", new Vector2(0f, -0.52f), definition.Caption, TextMain, 0.12f, 8);
                    view.Dot = MakeSprite(t, "Estado", ShapeSprites.Circle, new Vector2(1.0f, 0.3f), 0.16f, Good, 9);
                    break;

                case SpotKind.IceDeposit:
                    view.Body = MakeSprite(t, "Sinal", ShapeSprites.Ring, Vector2.zero, 0.5f, new Color(1f, 1f, 1f, 0.8f), 4);
                    view.Code = WorldText(t, "Interrogacao", Vector2.zero, "?", Color.white, 0.24f, 6);
                    view.Parts.Add(MakeSprite(t, "Cristal1", ShapeSprites.Diamond, new Vector2(-0.12f, -0.04f), 0.26f, IceCyan, 5));
                    view.Parts.Add(MakeSprite(t, "Cristal2", ShapeSprites.Diamond, new Vector2(0.12f, 0.02f), 0.32f, IceCyan, 5));
                    view.Parts.Add(MakeSprite(t, "Cristal3", ShapeSprites.Diamond, new Vector2(0.0f, -0.14f), 0.20f, IceCyan, 5));
                    view.Parts.Add(MakeSprite(t, "Rocha", ShapeSprites.Circle, Vector2.zero, 0.36f, new Color(0.38f, 0.30f, 0.27f), 5));
                    view.Caption = WorldText(t, "Legenda", new Vector2(0f, -0.36f), "", TextMain, 0.11f, 8);
                    break;

                case SpotKind.Sample:
                    view.Body = MakeSprite(t, "Amostra", ShapeSprites.Diamond, Vector2.zero, 0.24f, definition.Color, 5);
                    view.Parts.Add(MakeSprite(t, "Brilho", ShapeSprites.Ring, Vector2.zero, 0.42f, new Color(0.8f, 0.65f, 1f, 0.6f), 4));
                    break;

                default:
                    MakeSprite(t, "Sombra", ShapeSprites.Rounded, new Vector2(0.04f, -0.05f), 0.52f, new Color(0, 0, 0, 0.35f), 3);
                    view.Body = MakeSprite(t, "Corpo", ShapeSprites.Rounded, Vector2.zero, 0.48f, definition.Color, 4);
                    view.Code = WorldText(t, "Sigla", Vector2.zero, definition.Code, Color.white, 0.15f, 6);
                    view.Dot = MakeSprite(t, "Estado", ShapeSprites.Circle, new Vector2(0.22f, 0.22f), 0.15f, Good, 7);
                    view.Caption = WorldText(t, "Legenda", new Vector2(0f, -0.36f), definition.Caption, new Color(0.12f, 0.13f, 0.16f), 0.1f, 8);
                    break;
            }

            return view;
        }

        /// <summary>
        /// Monta o ponto com os objetos do pack (estação, painéis, gelo, amostra). Devolve false se as
        /// spritesheets não carregaram ou o ponto não tem objeto; aí ficam as formas simples.
        /// </summary>
        bool BuildPropSpot(SpotView view, Transform t)
        {
            SpotDefinition d = view.Definition;
            if (props == null)
                return false;

            switch (d.Kind)
            {
                case SpotKind.Panels:
                {
                    if (d.Prop == null || props.Get(d.Prop) == null)
                        return false;
                    // Duas fileiras de três; a de baixo na frente.
                    for (int row = 0; row < 2; row++)
                        for (int col = 0; col < 3; col++)
                        {
                            var offset = new Vector2((col - 1f) * 0.62f + (row == 0 ? 0.14f : -0.14f), (0.5f - row) * 0.44f);
                            MakeSprite(t, "Sombra", ShapeSprites.Circle, offset + new Vector2(0f, -0.33f), new Vector2(0.56f, 0.10f), PropShadow, 3);
                            view.Props.Add(MakeProp(t, "Painel", props, d.Prop, offset, 4 + row));
                        }
                    view.Caption = WorldText(t, "Legenda", new Vector2(0f, -0.82f), d.Caption, TextMain, 0.12f, 8);
                    view.Dot = MakeSprite(t, "Estado", ShapeSprites.Circle, new Vector2(1.15f, 0.5f), 0.16f, Good, 9);
                    return true;
                }

                case SpotKind.IceDeposit:
                    view.Body = MakeSprite(t, "Sinal", ShapeSprites.Ring, Vector2.zero, 0.5f, new Color(1f, 1f, 1f, 0.8f), 4);
                    view.Code = WorldText(t, "Interrogacao", Vector2.zero, "?", Color.white, 0.24f, 6);
                    view.Props.Add(MakeProp(t, "Gelo", props, "crystal_cluster", Vector2.zero, 5));
                    view.Props.Add(MakeProp(t, "Rochas", props, "boulders", Vector2.zero, 5));
                    view.Caption = WorldText(t, "Legenda", new Vector2(0f, -0.42f), "", TextMain, 0.11f, 8);
                    return true;

                case SpotKind.Sample:
                    if (items == null || items.Get("sample_case") == null)
                        return false;
                    view.Parts.Add(MakeSprite(t, "Brilho", ShapeSprites.Ring, Vector2.zero, 0.42f, new Color(0.8f, 0.65f, 1f, 0.6f), 4));
                    view.Props.Add(MakeProp(t, "Amostra", items, "sample_case", Vector2.zero, 5));
                    view.Body = view.Props[0].Renderer;
                    return true;

                default:
                    if (d.Prop == null || props.Get(d.Prop) == null)
                        return false;
                    MakeSprite(t, "Sombra", ShapeSprites.Circle, new Vector2(0f, -0.33f), new Vector2(0.62f, 0.12f), PropShadow, 3);
                    view.Props.Add(MakeProp(t, "Objeto", props, d.Prop, Vector2.zero, 4));
                    view.Body = view.Props[0].Renderer;
                    view.Dot = MakeSprite(t, "Estado", ShapeSprites.Circle, new Vector2(0.34f, 0.36f), 0.13f, Good, 7);
                    view.Caption = WorldText(t, "Legenda", new Vector2(0f, -0.46f), d.Caption, new Color(0.12f, 0.13f, 0.16f), 0.1f, 8);
                    return true;
            }
        }

        void AddWrapped(Transform transform, Vector2 home)
        {
            wrapped.Add(new Wrapped { Transform = transform, Home = home });
        }

        /// <summary>Pedras do pack em lugares fixos, longe da base e dos pontos de interesse.</summary>
        void BuildRocks(Transform world)
        {
            if (props == null || props.Get("boulders") == null)
                return;
            var random = new System.Random(77);
            int placed = 0;
            for (int attempt = 0; attempt < 600 && placed < RockCount; attempt++)
            {
                var home = new Vector2(
                    (float)(random.NextDouble() - 0.5) * MarsWorld.Width,
                    (float)(random.NextDouble() - 0.5) * MarsWorld.Height);
                float scale = 0.55f + 0.5f * (float)random.NextDouble();
                bool flip = random.NextDouble() < 0.5;
                if (Mathf.Abs(home.x) < 8f && Mathf.Abs(home.y) < 6f)
                    continue;
                bool nearSpot = false;
                foreach (SpotDefinition spot in BaseLayout.Spots)
                {
                    if (MarsWorld.Distance(home, spot.Home(map)) < 2.2f)
                        nearSpot = true;
                }
                if (nearSpot)
                    continue;

                var rock = new GameObject("Pedra" + placed).transform;
                rock.SetParent(world, false);
                rock.localScale = new Vector3(scale, scale, 1f);
                MakeSprite(rock, "Sombra", ShapeSprites.Circle, new Vector2(0f, -0.3f), new Vector2(0.62f, 0.12f), PropShadow, 1);
                SpriteAnimator boulder = MakeProp(rock, "Rochas", props, "boulders", Vector2.zero, 2);
                boulder.Renderer.flipX = flip;
                AddWrapped(rock, home);
                placed++;
            }
        }

        void BuildArrow(Transform world, string name, Vector2 home, SpotView spot, Color color)
        {
            var root = new GameObject(name);
            root.transform.SetParent(world, false);
            MakeSprite(root.transform, "Fundo", ShapeSprites.Circle, Vector2.zero, 0.44f, new Color(0f, 0f, 0f, 0.55f), 30);
            MakeSprite(root.transform, "Ponta", ShapeSprites.Arrow, new Vector2(0.03f, 0f), 0.30f, color, 31);
            root.SetActive(false);
            arrows.Add(new Arrow { Root = root, Home = home, Spot = spot });
        }

        /// <summary>
        /// Chamado depois de a câmera andar: desenha cada coisa do mundo na cópia do planeta mais perto
        /// dela (o planeta se fecha nas bordas) e põe as setas na borda da tela.
        /// </summary>
        public void FollowCamera(Vector2 center, float halfHeight, float aspect)
        {
            groundRoot.position = new Vector3(
                Mathf.Round(center.x / MarsWorld.Width) * MarsWorld.Width,
                Mathf.Round(center.y / MarsWorld.Height) * MarsWorld.Height, 0f);
            foreach (Wrapped item in wrapped)
            {
                Vector2 p = MarsWorld.NearestImage(item.Home, center);
                item.Transform.position = new Vector3(p.x, p.y, item.Transform.position.z);
            }

            float hy = halfHeight;
            float hx = halfHeight * aspect;
            // Elipse por dentro da tela, para as setas não ficarem embaixo dos painéis do HUD (calculada
            // para 16:9: a maior elipse que passa a meia seta de distância de todos os painéis fixos).
            float ex = hx * 0.54f;
            float ey = hy * 0.66f;
            float scale = halfHeight / ArrowReferenceHalfHeight;
            foreach (Arrow arrow in arrows)
            {
                Vector2 d = MarsWorld.Delta(center, arrow.Home);
                bool onScreen = Mathf.Abs(d.x) < hx - 0.4f * scale && Mathf.Abs(d.y) < hy - 0.4f * scale;
                bool show = arrow.Wanted && !onScreen;
                if (arrow.Root.activeSelf != show)
                    arrow.Root.SetActive(show);
                if (!show)
                    continue;
                float k = Mathf.Max(1f, Mathf.Sqrt((d.x / ex) * (d.x / ex) + (d.y / ey) * (d.y / ey)));
                Vector2 p = center + d / k;
                arrow.Root.transform.position = new Vector3(p.x, p.y, 0f);
                arrow.Root.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                arrow.Root.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        /// <summary>Quais setas valem agora: sinais ainda sem conferir ou com gelo, amostras não coletadas.</summary>
        void RefreshArrows(HabitatMission mission)
        {
            foreach (Arrow arrow in arrows)
            {
                if (arrow.Spot == null)
                    continue;
                SpotDefinition d = arrow.Spot.Definition;
                if (d.Kind == SpotKind.Sample)
                {
                    arrow.Wanted = !mission.IsSampleCollected(d.Target);
                }
                else if (d.Kind == SpotKind.IceDeposit)
                {
                    IceDeposit deposit = mission.FindDeposit(d.Target);
                    arrow.Wanted = deposit != null && (!deposit.Discovered || deposit.RemainingKg > 0);
                }
            }
        }

        static SpriteAnimator MakeProp(Transform parent, string name, SpriteSheet sheet, string animation, Vector2 localPosition, int order)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.transform.localPosition = localPosition;
            renderer.sortingOrder = order;
            return new SpriteAnimator(sheet, renderer, animation);
        }

        static SpriteRenderer MakeSprite(Transform parent, string name, Sprite sprite, Vector2 localPosition, float size, Color color, int order)
        {
            return MakeSprite(parent, name, sprite, localPosition, new Vector2(size, size), color, order);
        }

        static SpriteRenderer MakeSprite(Transform parent, string name, Sprite sprite, Vector2 localPosition, Vector2 size, Color color, int order)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.transform.localPosition = localPosition;
            renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        static TextMesh WorldText(Transform parent, string name, Vector2 localPosition, string text, Color color, float height, int order)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            var mesh = gameObject.AddComponent<TextMesh>();
            mesh.font = UiBuilder.DefaultFont;
            mesh.fontSize = 64;
            mesh.characterSize = height / 6.4f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontStyle = FontStyle.Bold;
            mesh.color = color;
            mesh.text = text;
            var renderer = gameObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = UiBuilder.DefaultFont.material;
            renderer.sortingOrder = order;
            return mesh;
        }

        Gauge BuildGauge(Transform parent, int slot, string title, Color color)
        {
            float bottom = 0.02f + slot * 0.245f;
            Transform row = UiBuilder.Rect(title, parent, new Vector2(0.05f, bottom), new Vector2(0.95f, bottom + 0.235f));
            UiBuilder.Label("Titulo", row, new Vector2(0f, 0.55f), new Vector2(0.5f, 1f), title, 13, TextAnchor.MiddleLeft, TextDim, FontStyle.Bold);
            var gauge = new Gauge();
            gauge.Value = UiBuilder.Label("Valor", row, new Vector2(0.4f, 0.55f), new Vector2(1f, 1f), "", 17, TextAnchor.MiddleRight, TextMain, FontStyle.Bold);
            Image back = UiBuilder.Panel("Barra", row, new Vector2(0f, 0.36f), new Vector2(1f, 0.52f), new Color(0, 0, 0, 0.6f), UiBuilder.Pill);
            gauge.Fill = UiBuilder.Panel("Nivel", back.transform, Vector2.zero, Vector2.one, color, UiBuilder.Pill);
            gauge.Note = UiBuilder.Label("Nota", row, new Vector2(0f, 0f), new Vector2(1f, 0.34f), "", 12, TextAnchor.MiddleLeft, TextDim);
            return gauge;
        }

        // ---------- Atualização ----------

        public void SetIntroVisible(bool visible)
        {
            introRoot.SetActive(visible);
        }

        /// <summary>
        /// Uma coluna da tela "Marte de verdade × Marte do jogo": fundo, título e uma linha com marcador
        /// para cada item (até <see cref="MarsBriefing.MaxLines"/>). Devolve o título.
        /// </summary>
        static Text BriefingColumn(Transform box, string name, Vector2 min, Vector2 max, Color background, Color accent, Text[] lines)
        {
            Transform column = UiBuilder.Panel(name, box, min, max, background, 20f).transform;
            Text heading = UiBuilder.Label("Titulo", column, new Vector2(0.05f, 0.87f), new Vector2(0.95f, 0.97f), "", 18, TextAnchor.MiddleLeft, accent, FontStyle.Bold);
            float step = 0.84f / lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                float top = 0.85f - i * step;
                UiBuilder.Panel("Marca" + i, column, new Vector2(0.045f, top - 0.045f), new Vector2(0.06f, top - 0.02f), accent, UiBuilder.Pill);
                lines[i] = UiBuilder.Label("Linha" + i, column, new Vector2(0.085f, top - step + 0.01f), new Vector2(0.96f, top), "", 17, TextAnchor.UpperLeft, TextMain);
            }
            return heading;
        }

        /// <summary>Abre a tela "Marte de verdade × Marte do jogo". O botão começa bloqueado (ver <see cref="SetBriefingReady"/>).</summary>
        public void ShowBriefing(MarsBriefing briefing)
        {
            if (briefing == null) throw new ArgumentNullException(nameof(briefing));
            briefingGameHeading.text = briefing.GameHeading;
            FillLines(briefingReal, briefing.RealFacts);
            FillLines(briefingGame, briefing.GameRules);
            briefingRoot.SetActive(true);
            SetBriefingReady(false);
        }

        static void FillLines(Text[] labels, IReadOnlyList<string> lines)
        {
            for (int i = 0; i < labels.Length; i++)
            {
                bool used = i < lines.Count;
                labels[i].text = used ? lines[i] : "";
                // Linha vazia esconde também o marcador (mesma coluna, nome "Marca" + i).
                labels[i].transform.parent.Find("Marca" + i).gameObject.SetActive(used);
            }
        }

        /// <summary>Libera o botão depois de um tempo mínimo de leitura.</summary>
        public void SetBriefingReady(bool ready)
        {
            briefingButton.interactable = ready;
            briefingButtonText.color = ready ? TextMain : TextDim;
        }

        public void HideBriefing()
        {
            briefingRoot.SetActive(false);
        }

        /// <summary>Abre a janela de descoberta com o fato. O botão começa bloqueado (ver <see cref="SetDiscoveryReady"/>).</summary>
        public void ShowDiscovery(string fact)
        {
            discoveryText.text = fact;
            discoveryRoot.SetActive(true);
            SetDiscoveryReady(false);
        }

        /// <summary>Libera o botão depois de um tempo mínimo de leitura, para não fechar sem querer.</summary>
        public void SetDiscoveryReady(bool ready)
        {
            discoveryButton.interactable = ready;
            discoveryButtonText.color = ready ? TextMain : TextDim;
        }

        public void SetMenuVisible(bool visible)
        {
            menuRoot.SetActive(visible);
        }

        /// <summary>Um bloco do tutorial: título pequeno e o texto logo abaixo. <paramref name="top"/> em fração da caixa.</summary>
        static Text GuideSection(Transform box, string name, string title, float top)
        {
            UiBuilder.Label(name + "Titulo", box, new Vector2(0.34f, top - 0.045f), new Vector2(0.96f, top), title, 14, TextAnchor.MiddleLeft, Warn, FontStyle.Bold);
            return UiBuilder.Label(name, box, new Vector2(0.34f, top - 0.18f), new Vector2(0.96f, top - 0.045f), "", 18, TextAnchor.UpperLeft, TextMain);
        }

        /// <summary>Abre o tutorial da máquina. A figura é o objeto desenhado no ponto <paramref name="spotId"/>.</summary>
        public void ShowGuide(MachineGuide guide, string spotId)
        {
            guideTitle.text = guide.Name.ToUpperInvariant();
            guideWhat.text = guide.WhatItDoes;
            guideWhy.text = guide.WhyOnMars;
            guideFact.text = guide.RealFact;
            guideSource.text = "Fonte: " + guide.Source;

            Sprite picture = null;
            foreach (SpotDefinition spot in BaseLayout.Spots)
            {
                if (spot.Id != spotId || spot.Prop == null || props == null)
                    continue;
                SpriteAnimation animation = props.Get(spot.Prop);
                if (animation != null && animation.Frames != null && animation.Frames.Length > 0)
                    picture = animation.Frames[0];
            }
            guidePicture.sprite = picture;
            guidePicture.enabled = picture != null;

            guideRoot.SetActive(true);
            SetGuideReady(false);
        }

        /// <summary>Libera o botão depois do tempo mínimo de leitura.</summary>
        public void SetGuideReady(bool ready)
        {
            guideButton.interactable = ready;
            guideButtonText.color = ready ? TextMain : TextDim;
        }

        public void HideGuide()
        {
            guideRoot.SetActive(false);
        }

        public void HideDiscovery()
        {
            discoveryRoot.SetActive(false);
        }

        public void ClearToasts()
        {
            foreach (Toast toast in toasts)
            {
                toast.TimeLeft = 0;
                toast.Root.SetActive(false);
            }
        }

        /// <summary>Mostra um aviso curto no topo. O mais novo fica em cima.</summary>
        public void ShowToast(Notice notice)
        {
            for (int i = toasts.Count - 1; i > 0; i--)
            {
                toasts[i].Text.text = toasts[i - 1].Text.text;
                toasts[i].Text.color = toasts[i - 1].Text.color;
                toasts[i].Background.color = toasts[i - 1].Background.color;
                toasts[i].TimeLeft = toasts[i - 1].TimeLeft;
                toasts[i].Root.SetActive(toasts[i - 1].Root.activeSelf);
            }

            Toast top = toasts[0];
            string prefix;
            Color color;
            Color background = Panel;
            switch (notice.Kind)
            {
                case NoticeKind.Critical: prefix = "!  "; color = Bad; break;
                case NoticeKind.Warning: prefix = "!  "; color = Warn; break;
                case NoticeKind.Good: prefix = "+  "; color = Good; break;
                case NoticeKind.Lesson: prefix = "i  "; color = TextMain; background = LessonBlue; break;
                default: prefix = ""; color = TextMain; break;
            }
            top.Text.text = prefix + notice.Text;
            top.Text.color = color;
            top.Background.color = background;
            top.TimeLeft = notice.Kind == NoticeKind.Lesson ? 8f : 5f;
            top.Root.SetActive(true);
        }

        /// <summary>Redesenha tudo a partir do estado atual.</summary>
        public void Refresh(HabitatMission mission, bool outside, InteractionPrompt prompt, float deltaTime, float time)
        {
            HabitatState state = mission.State;
            ScenarioParameters p = mission.Parameters;
            CycleReport last = mission.LastReport;
            bool blinkOn = Mathf.Repeat(time, 0.8f) < 0.5f;

            // ----- Céu: noite e tempestade escurecem o mundo -----
            float daylight = (float)mission.Daylight(mission.HourOfDay);
            float night = 1f - Mathf.Clamp01(daylight * 2.2f);
            Color sky = new Color(0.01f, 0.02f, 0.10f, 0.74f * night);
            if (mission.ActiveStorm != null)
                sky = Color.Lerp(sky, new Color(0.55f, 0.28f, 0.10f, 0.45f), 0.65f);
            skyOverlay.color = sky;

            // ----- Estações -----
            foreach (SpotView spot in spots)
                RefreshSpot(spot, mission, last, blinkOn, time, deltaTime);
            RefreshArrows(mission);

            // ----- Astronauta -----
            if (astronautAnimator != null)
            {
                bool moving = astronautVelocity.sqrMagnitude > 0.01f;
                string animation =
                    mission.Status == MissionStatus.Victory ? "victory" :
                    mission.Status == MissionStatus.Defeat ? "hurt" :
                    astronautWorking ? "mine" :
                    moving ? "walk" : "idle";
                astronautAnimator.Play(animation);
                astronautAnimator.Face(astronautVelocity.x);
                astronautAnimator.Advance(deltaTime);
            }
            carryIce.gameObject.SetActive(mission.CarriedIceKg > 0);
            carrySample.gameObject.SetActive(mission.SamplesCarried > 0);
            float suit = p.SuitOxygenCapacityKg > 0 ? (float)(state.SuitOxygenKg / p.SuitOxygenCapacityKg) : 1f;
            bool showSuit = outside || suit < 0.999f;
            suitBar.SetActive(showSuit);
            suitFill.localScale = new Vector3(0.52f * suit, 0.05f, 1f);
            suitFill.localPosition = new Vector3(-0.26f + 0.26f * suit, 0f, 0f);
            Color suitColor = suit < 0.25f ? (blinkOn ? Bad : Color.white) : suit < 0.5f ? Warn : IceCyan;
            suitFillRenderer.color = suitColor;

            // ----- Recursos -----
            double net = last != null ? last.Power.GeneratedKWh - last.Power.ConsumedKWh : 0;
            SetGauge(energy, state.BatteryChargeKWh / p.BatteryCapacityKWh, Energy,
                N(state.BatteryChargeKWh, 0) + " kWh",
                (net >= 0 ? "carregando  +" : "descarregando  -") + N(Math.Abs(net), 1) + " kW   painéis " + N(state.SolarPanelEfficiency * 100, 0) + "%");
            if (state.BatteryChargeKWh < 4)
                energy.Fill.color = blinkOn ? Bad : Energy;

            double o2Low = p.OxygenMinimumKg + 3 * p.CrewOxygenDemandKgPerCycle;
            Color o2Color = state.OxygenKg < o2Low ? Bad : state.OxygenKg < o2Low + 2 ? Warn : Good;
            SetGauge(oxygen, state.OxygenKg / p.OxygenTankCapacityKg, o2Color,
                N(state.OxygenKg, 1) + " kg",
                "limite " + N(p.OxygenMinimumKg, 0) + " kg" + (state.SealCondition < 1 ? "   VAZANDO" : ""));

            double pressure = mission.Engine.Co2PressureMmHg;
            Color co2Color = pressure >= p.Co2ActionMmHg ? Bad : pressure >= p.Co2AlertMmHg ? Warn : Good;
            string co2Level = pressure >= p.Co2ActionMmHg ? "PERIGO" : pressure >= p.Co2AlertMmHg ? "ALERTA" : "normal";
            SetGauge(co2, pressure / p.Co2EvacuationMmHg, co2Color, N(pressure, 1) + " mmHg",
                co2Level + "   limite " + N(p.Co2EvacuationMmHg, 0) + " mmHg");

            double waterLow = 12 * p.CrewWaterDemandKgPerCycle;
            Color waterColor = state.WaterKg < waterLow ? Bad : state.WaterKg < 2 * waterLow ? Warn : WaterBlue;
            SetGauge(water, state.WaterKg / p.WaterTankCapacityKg, waterColor, N(state.WaterKg, 1) + " L",
                "usada " + N(state.WasteWaterKg, 0) + " L   gelo " + N(state.IceKg, 0) + " kg");

            // ----- Relógio -----
            solText.text = "SOL " + mission.Sol + " / " + mission.Config.MissionSols;
            double hour = mission.HourOfDay;
            int hh = Mathf.Clamp((int)hour, 0, 24);
            int mm = Mathf.Clamp((int)((hour - Math.Floor(hour)) * 60), 0, 59);
            string phase = mission.ActiveStorm != null ? "TEMPESTADE" : daylight > 0 ? "DIA" : "NOITE";
            clockText.text = hh.ToString("00") + ":" + (mm / 10 * 10).ToString("00") + "   " + phase;
            clockText.color = mission.ActiveStorm != null ? Warn : TextDim;

            // ----- Objetivos -----
            SetObjective(0, mission.IceFound, "Achar gelo");
            SetObjective(1, mission.IceWaterObjectiveMet,
                "Água do gelo  " + N(Math.Min(mission.IceWaterProducedKg, mission.Config.IceWaterObjectiveKg), 0) + " / " + N(mission.Config.IceWaterObjectiveKg, 0) + " L");
            SetObjective(2, mission.Status == MissionStatus.Victory, "Sobreviver " + mission.Config.MissionSols + " sóis");

            // ----- Avisos (somem na tela de fim) -----
            foreach (Toast toast in toasts)
            {
                if (!toast.Root.activeSelf)
                    continue;
                toast.TimeLeft -= deltaTime;
                if (toast.TimeLeft <= 0 || mission.IsFinished)
                    toast.Root.SetActive(false);
            }

            // ----- Ação -----
            promptRoot.SetActive(!string.IsNullOrEmpty(prompt.Text) && !mission.IsFinished && !IsDiscoveryVisible && !IsGuideVisible && !IsMenuVisible);
            if (promptRoot.activeSelf)
            {
                promptText.text = prompt.Enabled ? "[E]  " + prompt.Text : prompt.Text;
                promptText.color = prompt.Enabled ? TextMain : TextDim;
                promptBar.SetActive(prompt.Progress >= 0);
                promptFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(prompt.Progress), 1f);
            }

            // ----- Traje e carga -----
            suitHud.SetActive(showSuit);
            suitHudFill.rectTransform.anchorMax = new Vector2(suit, 1f);
            suitHudFill.color = suitColor;
            suitHudText.text = "TRAJE  " + N(suit * 100, 0) + "%" + (outside ? "   (lá fora)" : "   recarregando");
            string carry = "";
            if (mission.CarriedIceKg > 0)
                carry += "Gelo na mão: " + N(mission.CarriedIceKg, 0) + " kg   ";
            if (mission.SamplesCarried > 0)
                carry += "Amostras: " + mission.SamplesCarried;
            carryText.text = carry;

            // ----- Equipamentos -----
            for (int i = 0; i < EquipmentOrder.Length; i++)
            {
                EquipmentState item = state.FindEquipment(EquipmentOrder[i]);
                EquipmentStatus(item, last, out string label, out Color color, out bool blink);
                equipmentStates[i].text = label;
                equipmentStates[i].color = color;
                equipmentDots[i].color = blink && !blinkOn ? Off : color;
            }

            // ----- Fim -----
            endRoot.SetActive(mission.IsFinished);
            if (mission.IsFinished)
            {
                bool won = mission.Status == MissionStatus.Victory;
                endTitle.text = won ? "MISSÃO CUMPRIDA" : "MISSÃO PERDIDA";
                endTitle.color = won ? Good : Bad;
                endText.text = won ? Explanations.DescribeVictory(mission) : "Sol " + mission.Sol + ": " + Explanations.DescribeDefeat(mission.DefeatReason);
                endStats.text = "Água do gelo: " + N(mission.IceWaterProducedKg, 0) + " L     Amostras analisadas: "
                    + mission.SamplesAnalyzed + " / " + mission.SampleSiteCount;
            }
        }

        void SetObjective(int index, bool done, string text)
        {
            objectiveBoxes[index].color = done ? Good : new Color(1f, 1f, 1f, 0.18f);
            objectiveTexts[index].text = text;
            objectiveTexts[index].color = done ? Good : TextMain;
        }

        static void SetGauge(Gauge gauge, double fraction, Color color, string value, string note)
        {
            gauge.Fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)fraction), 1f);
            gauge.Fill.color = color;
            gauge.Value.text = value;
            gauge.Note.text = note;
        }

        static void EquipmentStatus(EquipmentState item, CycleReport last, out string label, out Color color, out bool blink)
        {
            blink = false;
            if (item == null)
            {
                label = "";
                color = Off;
                return;
            }

            bool unpowered = last != null && Contains(last.Power.UnpoweredEquipmentIds, item.Definition.Id);
            if (item.IsBroken)
            {
                label = "QUEBRADO";
                color = Bad;
                blink = true;
            }
            else if (!item.IsOn)
            {
                label = "DESLIGADO";
                color = Off;
            }
            else if (unpowered)
            {
                label = "SEM ENERGIA";
                color = Warn;
                blink = true;
            }
            else if (item.Condition < 1)
            {
                label = "FRACO " + N(item.Condition * 100, 0) + "%";
                color = Warn;
            }
            else
            {
                label = "LIGADO";
                color = Good;
            }
        }

        void RefreshSpot(SpotView spot, HabitatMission mission, CycleReport last, bool blinkOn, float time, float deltaTime)
        {
            SpotDefinition d = spot.Definition;
            HabitatState state = mission.State;

            switch (d.Kind)
            {
                case SpotKind.Equipment:
                case SpotKind.IceStation:
                {
                    EquipmentState item = state.FindEquipment(d.Target);
                    EquipmentStatus(item, last, out string label, out Color color, out bool blink);
                    spot.Dot.color = color;
                    spot.Dot.enabled = !blink || blinkOn;
                    bool on = item != null && item.IsOn && !item.IsBroken;
                    if (spot.Props.Count > 0)
                    {
                        // Ligado: anima com as cores originais. Parado: congela e fica cinza.
                        spot.Body.color = on ? Color.white : PropOff;
                        spot.Props[0].Advance(on ? deltaTime : 0f);
                    }
                    else
                    {
                        spot.Body.color = on ? d.Color : Color.Lerp(d.Color, Off, 0.65f);
                    }
                    if (d.Kind == SpotKind.IceStation)
                        spot.Caption.text = state.IceKg > 0.05 ? d.Caption + "  (" + N(state.IceKg, 0) + " kg)" : d.Caption;
                    break;
                }

                case SpotKind.Seal:
                {
                    bool leaking = state.SealCondition < 1;
                    spot.Dot.color = leaking ? Bad : Good;
                    spot.Dot.enabled = !leaking || blinkOn;
                    spot.Caption.text = leaking ? "Vedação  VAZANDO" : "Vedação";
                    if (spot.Props.Count > 0)
                    {
                        spot.Body.color = leaking && blinkOn ? new Color(1f, 0.55f, 0.5f) : Color.white;
                        spot.Props[0].Advance(deltaTime);
                    }
                    break;
                }

                case SpotKind.Lab:
                    spot.Dot.color = mission.SamplesCarried > 0 ? Color.white : new Color(0.7f, 0.6f, 0.95f);
                    spot.Dot.enabled = mission.SamplesCarried == 0 || blinkOn;
                    spot.Caption.text = "Laboratório  " + mission.SamplesAnalyzed + "/" + mission.SampleSiteCount;
                    if (spot.Props.Count > 0)
                        spot.Props[0].Advance(deltaTime);
                    break;

                case SpotKind.Panels:
                {
                    float dust = Mathf.Clamp01((float)(1 - state.SolarPanelEfficiency) * 1.6f);
                    Color panel = Color.Lerp(d.Color, new Color(0.62f, 0.40f, 0.26f), dust);
                    foreach (SpriteRenderer part in spot.Parts)
                        part.color = panel;
                    Color dusty = Color.Lerp(Color.white, Dust, dust);
                    foreach (SpriteAnimator panelProp in spot.Props)
                    {
                        panelProp.Renderer.color = dusty;
                        panelProp.Advance(deltaTime);
                    }
                    bool dirty = mission.PanelsDirty;
                    spot.Dot.color = dirty ? Warn : Good;
                    spot.Dot.enabled = !dirty || blinkOn;
                    spot.Caption.text = "Painéis solares  " + N(state.SolarPanelEfficiency * 100, 0) + "%";
                    break;
                }

                case SpotKind.IceDeposit:
                {
                    IceDeposit deposit = mission.FindDeposit(d.Target);
                    bool discovered = deposit != null && deposit.Discovered;
                    bool real = deposit != null && deposit.IsReal;
                    double fraction = real && deposit.InitialKg > 0 ? deposit.RemainingKg / deposit.InitialKg : 0;

                    spot.Body.gameObject.SetActive(!discovered);
                    spot.Code.gameObject.SetActive(!discovered);
                    if (!discovered)
                    {
                        float pulse = 0.45f + 0.08f * Mathf.Sin(time * 4f);
                        spot.Body.transform.localScale = new Vector3(pulse, pulse, 1f);
                    }

                    if (spot.Props.Count > 0)
                    {
                        // Gelo encontrado: cristais que encolhem conforme a coleta. Sinal falso: rochas.
                        SpriteAnimator crystals = spot.Props[0];
                        crystals.Renderer.gameObject.SetActive(discovered && real && fraction > 0.01);
                        float size = 0.55f + 0.45f * (float)fraction;
                        crystals.Renderer.transform.localScale = new Vector3(size, size, 1f);
                        crystals.Advance(deltaTime);
                        SpriteAnimator rocks = spot.Props[1];
                        rocks.Renderer.gameObject.SetActive(discovered && !real);
                        rocks.Renderer.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
                    }
                    else
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            spot.Parts[i].gameObject.SetActive(discovered && real && fraction > 0.01);
                            float s = (i == 1 ? 0.32f : i == 0 ? 0.26f : 0.20f) * (0.45f + 0.55f * (float)fraction);
                            spot.Parts[i].transform.localScale = new Vector3(s, s, 1f);
                        }
                        spot.Parts[3].gameObject.SetActive(discovered && !real);
                    }

                    if (!discovered)
                        spot.Caption.text = "sinal";
                    else if (!real)
                        spot.Caption.text = "só rocha";
                    else if (fraction <= 0.01)
                        spot.Caption.text = "esgotado";
                    else
                        spot.Caption.text = "gelo  " + N(deposit.RemainingKg, 0) + " kg";
                    break;
                }

                case SpotKind.Sample:
                {
                    bool collected = mission.IsSampleCollected(d.Target);
                    spot.Root.SetActive(!collected);
                    float s = 1f + 0.15f * Mathf.Sin(time * 3f);
                    spot.Parts[0].transform.localScale = new Vector3(0.42f * s, 0.42f * s, 1f);
                    break;
                }
            }
        }

        /// <summary>Destaca o ponto com que o astronauta pode interagir.</summary>
        public void SetHighlight(string spotId, float time)
        {
            foreach (SpotView spot in spots)
            {
                bool active = spot.Definition.Id == spotId;
                float alpha = active ? 0.35f + 0.2f * Mathf.Sin(time * 6f) : 0f;
                spot.Highlight.color = new Color(1f, 1f, 1f, alpha);
            }
        }

        /// <summary>Posição do ponto na cópia do planeta mais perto de <paramref name="near"/>.</summary>
        public Vector2 SpotPosition(SpotDefinition definition, Vector2 near)
        {
            return MarsWorld.NearestImage(definition.Home(map), near);
        }

        static bool Contains(IReadOnlyList<string> list, string value)
        {
            foreach (string item in list)
            {
                if (item == value)
                    return true;
            }
            return false;
        }

        static string N(double value, int decimals)
        {
            return Explanations.Number(value, decimals);
        }
    }
}
