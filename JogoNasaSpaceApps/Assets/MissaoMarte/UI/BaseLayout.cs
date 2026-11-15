using MissaoMarte.Events;
using MissaoMarte.Game;
using MissaoMarte.Simulation;
using UnityEngine;

namespace MissaoMarte.UI
{
    public enum SpotKind
    {
        Equipment,
        Seal,
        Panels,
        IceDeposit,
        IceStation,
        Lab,
        Sample,
    }

    /// <summary>
    /// Um ponto de interesse do mapa. Na base, a posição é em pixels da imagem do cenário (x da esquerda,
    /// y de cima); lá fora (<see cref="Outdoor"/>), em unidades do mundo a partir do centro da base.
    /// </summary>
    public sealed class SpotDefinition
    {
        public SpotDefinition(string id, SpotKind kind, string target, float pixelX, float pixelY, float radius, string code, string caption, Color color, string prop = null)
        {
            Id = id;
            Kind = kind;
            Target = target;
            Pixel = new Vector2(pixelX, pixelY);
            Radius = radius;
            Code = code;
            Caption = caption;
            Color = color;
            Prop = prop;
        }

        /// <summary>Ponto no solo do planeta, longe da planta da base (posição em unidades do mundo).</summary>
        public static SpotDefinition Outdoor(string id, SpotKind kind, string target, float worldX, float worldY, float radius, Color color)
        {
            return new SpotDefinition(id, kind, target, 0f, 0f, radius, "", "", color)
            {
                World = new Vector2(worldX, worldY),
                UsesWorld = true,
            };
        }

        public string Id { get; }

        public SpotKind Kind { get; }

        /// <summary>Equipamento, depósito ou amostra a que o ponto se refere.</summary>
        public string Target { get; }

        public Vector2 Pixel { get; }

        /// <summary>Posição em unidades do mundo (só vale com <see cref="UsesWorld"/>).</summary>
        public Vector2 World { get; private set; }

        public bool UsesWorld { get; private set; }

        /// <summary>Onde o ponto fica na cópia do planeta centrada na base.</summary>
        public Vector2 Home(MapData map) => UsesWorld ? World : map.PixelToWorld(Pixel.x, Pixel.y);

        /// <summary>Distância (unidades do mundo) em que o astronauta consegue interagir.</summary>
        public float Radius { get; }

        /// <summary>Sigla curta desenhada no ícone.</summary>
        public string Code { get; }

        /// <summary>Nome curto mostrado embaixo do ícone.</summary>
        public string Caption { get; }

        public Color Color { get; }

        /// <summary>Objeto da spritesheet Resources/Props/props_128_spritesheet desenhado no lugar (null = forma simples).</summary>
        public string Prop { get; }
    }

    /// <summary>
    /// Onde cada coisa fica na planta (Resources/Cenario). Módulos claros = dentro da base;
    /// área escura = solo de Marte (lá fora o traje gasta oxigênio).
    ///   Hub central: iluminação e a escotilha (vedação).
    ///   Módulo de cima à direita: gerador de O2.   Módulo da direita: suporte atmosférico (CO2).
    ///   Módulo de baixo: tratamento de água e estação de gelo.
    ///   Prédio separado à esquerda: laboratório (só se chega por fora).
    ///   Ao lado da base: painéis solares.
    ///   Longe, no solo do planeta: sinais de gelo dos sensores e amostras (13 a 18 unidades da base;
    ///   ida e volta gasta metade do traje ou mais). O planeta se fecha nas bordas (<see cref="MarsWorld"/>).
    ///
    /// Objetos (Space Explorers Pixel Pack, CC0), escolhidos pelo que cada estação faz:
    ///   O2 = oxygen_station (tanques)          CO2 = generator (ventoinha e exaustão)
    ///   água = fabricator (bico gotejando)     gelo = heater (aquece e derrete o gelo)
    ///   iluminação = lamp_post                 vedação = airlock (escotilha)
    ///   laboratório = workstation (monitor)    painéis = solar_panel
    ///   gelo no solo = crystal_cluster, sinal falso = boulders, amostra = sample_case (ver GameView).
    /// </summary>
    public static class BaseLayout
    {
        public static readonly Vector2 SpawnPixel = new Vector2(650, 390);

        static readonly Color Air = new Color(0.25f, 0.75f, 0.85f);
        static readonly Color Co2 = new Color(0.35f, 0.75f, 0.40f);
        static readonly Color Water = new Color(0.25f, 0.50f, 0.95f);
        static readonly Color Ice = new Color(0.55f, 0.85f, 0.95f);
        static readonly Color Light = new Color(0.95f, 0.80f, 0.25f);
        static readonly Color Seal = new Color(0.95f, 0.55f, 0.20f);
        static readonly Color Lab = new Color(0.65f, 0.45f, 0.90f);
        static readonly Color Solar = new Color(0.20f, 0.30f, 0.60f);

        public static readonly SpotDefinition[] Spots =
        {
            new SpotDefinition("o2", SpotKind.Equipment, EquipmentIds.OxygenGenerator, 855, 125, 0.65f, "O2", "Gerador de O2", Air, "oxygen_station"),
            new SpotDefinition("co2", SpotKind.Equipment, EquipmentIds.Co2Scrubber, 1065, 415, 0.65f, "CO2", "Suporte atmosférico", Co2, "generator"),
            new SpotDefinition("agua", SpotKind.Equipment, EquipmentIds.WaterRecycler, 688, 700, 0.5f, "H2O", "Tratamento de água", Water, "fabricator"),
            new SpotDefinition("gelo", SpotKind.IceStation, EquipmentIds.IceProcessor, 778, 765, 0.5f, "GELO", "Estação de gelo", Ice, "heater"),
            new SpotDefinition("luz", SpotKind.Equipment, EquipmentIds.Infrastructure, 680, 290, 0.6f, "LUZ", "Iluminação", Light, "lamp_post"),
            new SpotDefinition("vedacao", SpotKind.Seal, EquipmentFailure.SealTarget, 482, 335, 0.55f, "VED", "Vedação", Seal, "airlock"),
            new SpotDefinition("lab", SpotKind.Lab, null, 295, 315, 0.65f, "LAB", "Laboratório", Lab, "workstation"),
            new SpotDefinition("paineis", SpotKind.Panels, null, 300, 795, 1.4f, "", "Painéis solares", Solar, "solar_panel"),
            // [DIDÁTICO] Distâncias de jogo (unidades do mundo, x para o leste, y para o norte).
            SpotDefinition.Outdoor("deposito_leste", SpotKind.IceDeposit, PrimeiraFonte.DepositEast, 15f, -7f, 0.6f, Ice),
            SpotDefinition.Outdoor("deposito_oeste", SpotKind.IceDeposit, PrimeiraFonte.DepositWest, -16f, 8f, 0.6f, Ice),
            SpotDefinition.Outdoor("sinal_norte", SpotKind.IceDeposit, PrimeiraFonte.FalseSignal, 1f, 13.5f, 0.6f, Ice),
            SpotDefinition.Outdoor("amostra_1", SpotKind.Sample, "amostra_1", 12f, 10f, 0.45f, Lab),
            SpotDefinition.Outdoor("amostra_2", SpotKind.Sample, "amostra_2", 7f, -14f, 0.45f, Lab),
            SpotDefinition.Outdoor("amostra_3", SpotKind.Sample, "amostra_3", -14f, -10f, 0.45f, Lab),
        };

        /// <summary>Distância em que o astronauta descobre o que há num sinal dos sensores.</summary>
        public const float DiscoveryRadius = 1.2f;
    }
}
