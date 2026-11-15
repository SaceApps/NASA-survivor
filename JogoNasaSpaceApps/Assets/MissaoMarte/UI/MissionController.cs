using System;
using System.Collections.Generic;
using System.Reflection;
using MissaoMarte.Events;
using MissaoMarte.Game;
using MissaoMarte.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace MissaoMarte.UI
{
    /// <summary>
    /// Liga o jogo: cria o cenário a partir da planta (Resources/Cenario), o astronauta (movido pelo
    /// PlayerBehavior do projeto, com WASD), a câmera que segue e o HUD; faz o tempo correr e
    /// executa as ações com a tecla E. Basta um objeto com este componente na cena.
    /// Não há paredes: o planeta se fecha nas bordas (<see cref="MarsWorld"/>). O astronauta e a
    /// câmera andam livres; o resto do mundo é desenhado na cópia do planeta mais perto da câmera.
    /// </summary>
    public sealed class MissionController : MonoBehaviour
    {
        /// <summary>Valor padrão do relógio (também usado pelas capturas de tela).</summary>
        public const float DefaultSecondsPerMarsHour = 2.5f;

        [Tooltip("Segundos reais por hora marciana. 2,5 s = um sol a cada minuto.")]
        public float secondsPerMarsHour = DefaultSecondsPerMarsHour;

        [Tooltip("Velocidade do astronauta (unidades por segundo).")]
        public float astronautSpeed = 3f;

        [Tooltip("Zoom da câmera (metade da altura visível, em unidades).")]
        public float cameraSize = 3.3f;

        /// <summary>Segundos segurando E para consertar (o dobro sem iluminação).</summary>
        const float RepairSeconds = 2.5f;
        const float CleanSeconds = 3f;
        const float CollectIceSeconds = 1.5f;
        const float SampleSeconds = 1f;
        const float AnalyzeSeconds = 1.5f;

        /// <summary>Tempo mínimo com a janela de descoberta aberta antes de poder fechar (para dar tempo de ler).</summary>
        const float DiscoveryMinSeconds = 1.5f;

        /// <summary>Tempo mínimo com o tutorial de uma máquina aberto (tem mais texto que a descoberta).</summary>
        const float GuideMinSeconds = 3f;

        /// <summary>Tempo mínimo na tela "Marte de verdade × Marte do jogo" antes de poder continuar.</summary>
        const float BriefingMinSeconds = 3f;

        float briefingOpenTime;

        MapData map;
        GameView view;
        HabitatMission mission;
        Rigidbody2D body;
        Behaviour mover;
        Camera mainCamera;
        bool started;
        readonly LapCounter laps = new LapCounter(MarsWorld.Globe);

        SpotDefinition holdSpot;
        float holdTime;
        bool releasedSinceAction = true;

        /// <summary>Descobertas da pesquisa esperando para aparecer (uma janela por vez).</summary>
        readonly Queue<string> discoveries = new Queue<string>();
        float discoveryOpenTime;

        /// <summary>Máquinas cujo tutorial já foi visto. Vale para a sessão toda (não volta ao reiniciar).</summary>
        readonly HashSet<string> learnedMachines = new HashSet<string>();
        MachineGuide openGuide;
        float guideOpenTime;

        void Awake()
        {
            map = MapData.Load();
            if (map == null)
            {
                enabled = false;
                return;
            }

            EnsureEventSystem();
            view = new GameView(transform, map);
            view.StartRequested += StartMission;
            view.RestartRequested += Restart;
            view.DiscoveryClosed += CloseDiscovery;
            view.GuideClosed += CloseGuide;
            view.QuitRequested += QuitGame;
            view.BriefingClosed += CloseBriefing;

            SetupAstronaut(view.Astronaut.gameObject);
            SetupCamera();

            NewMission();
            view.SetIntroVisible(true);
            // Primeiro o "Marte de verdade × Marte do jogo"; ao continuar, fica a abertura da missão por baixo.
            view.ShowBriefing(MarsBriefing.Create(mission, secondsPerMarsHour));
            briefingOpenTime = 0;
        }

        void CloseBriefing()
        {
            if (!view.IsBriefingVisible || briefingOpenTime < BriefingMinSeconds)
                return;
            view.HideBriefing();
            // A tecla que fechou esta tela não pode já começar a missão.
            releasedSinceAction = false;
        }

        // ---------- Montagem ----------

        void SetupAstronaut(GameObject astronaut)
        {
            body = astronaut.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var collider = astronaut.AddComponent<CircleCollider2D>();
            collider.radius = 0.15f;

            // O movimento é o PlayerBehavior do projeto (Assets/Scipts). Ele fica no assembly padrão,
            // que não pode ser referenciado daqui; por isso é ligado pelo nome.
            Type playerType = Type.GetType("PlayerBehavior, Assembly-CSharp");
            if (playerType != null && typeof(Behaviour).IsAssignableFrom(playerType))
            {
                mover = (Behaviour)astronaut.AddComponent(playerType);
                FieldInfo speed = playerType.GetField("PlayerSpeed", BindingFlags.Public | BindingFlags.Instance);
                if (speed != null && speed.FieldType == typeof(float))
                    speed.SetValue(mover, astronautSpeed);
            }
            else
            {
                Debug.LogWarning("[Missão Marte] PlayerBehavior não encontrado; usando movimento simples.");
                var simple = astronaut.AddComponent<SimpleMover>();
                simple.speed = astronautSpeed;
                mover = simple;
            }
        }

        void SetupCamera()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
            }
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = cameraSize;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = BeyondMapColor;
            SnapCamera();
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        // ---------- Partida ----------

        void NewMission()
        {
            if (mission != null)
                mission.Noticed -= OnNotice;

            mission = PrimeiraFonte.Create();
            mission.Noticed += OnNotice;
            view.ClearToasts();
            discoveries.Clear();
            view.HideDiscovery();
            openGuide = null;
            view.HideGuide();

            body.position = map.PixelToWorld(BaseLayout.SpawnPixel.x, BaseLayout.SpawnPixel.y);
            view.Astronaut.position = body.position;
            body.linearVelocity = Vector2.zero;
            holdSpot = null;
            holdTime = 0;
            laps.Reset(body.position.x, body.position.y);
            SnapCamera();
        }

        void StartMission()
        {
            if (started)
                return;
            started = true;
            view.SetIntroVisible(false);
            releasedSinceAction = false;
            view.ShowToast(new Notice(NoticeKind.Lesson, "WASD anda, E interage. Fora dos módulos o traje gasta oxigênio."));
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Restart()
        {
            NewMission();
            started = true;
            view.SetIntroVisible(false);
            releasedSinceAction = false;
        }

        /// <summary>Descobertas sobre Marte vão para a janela central; o resto vira aviso curto no topo.</summary>
        void OnNotice(Notice notice)
        {
            if (notice.Kind == NoticeKind.Discovery)
                discoveries.Enqueue(notice.Text);
            else
                view.ShowToast(notice);
        }

        /// <summary>Abre a próxima descoberta da fila, se houver e se nenhuma estiver aberta.</summary>
        void OpenNextDiscovery()
        {
            if (view.IsDiscoveryVisible || view.IsGuideVisible || discoveries.Count == 0)
                return;
            view.ShowDiscovery(discoveries.Dequeue());
            discoveryOpenTime = 0;
        }

        void CloseDiscovery()
        {
            if (!view.IsDiscoveryVisible || discoveryOpenTime < DiscoveryMinSeconds)
                return;
            view.HideDiscovery();
            // A tecla que fechou a janela não pode disparar outra ação logo em seguida.
            releasedSinceAction = false;
            OpenNextDiscovery();
        }

        /// <summary>Qual tutorial vale para o ponto (null se o ponto não for uma máquina).</summary>
        static MachineGuide GuideFor(SpotDefinition spot)
        {
            if (spot == null)
                return null;
            switch (spot.Kind)
            {
                case SpotKind.Equipment:
                case SpotKind.IceStation:
                case SpotKind.Seal:
                    return MachineGuides.For(spot.Target);
                case SpotKind.Lab:
                    return MachineGuides.For(MachineGuides.Lab);
                case SpotKind.Panels:
                    return MachineGuides.For(MachineGuides.Panels);
                default:
                    return null;
            }
        }

        void OpenGuide(MachineGuide guide, SpotDefinition spot)
        {
            openGuide = guide;
            guideOpenTime = 0;
            view.ShowGuide(guide, spot.Id);
        }

        void CloseGuide()
        {
            if (!view.IsGuideVisible || guideOpenTime < GuideMinSeconds)
                return;
            if (openGuide != null)
                learnedMachines.Add(openGuide.Id);
            openGuide = null;
            view.HideGuide();
            releasedSinceAction = false;
        }

        // ---------- Quadro a quadro ----------

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            bool interactHeld = keyboard != null && (keyboard.eKey.isPressed || keyboard.spaceKey.isPressed);
            if (!interactHeld)
                releasedSinceAction = true;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                view.SetMenuVisible(!view.IsMenuVisible);
                // A tecla E segurada ao fechar o menu não pode disparar uma ação.
                releasedSinceAction = false;
            }
            if (view.IsMenuVisible)
            {
                // Menu aberto: tempo congelado, astronauta parado, nada de ações.
                SetMoving(false);
                holdSpot = null;
                holdTime = 0;
                Draw(InteractionPrompt.None, IsOutside(), 0f);
                return;
            }

            if (!started && view.IsBriefingVisible)
            {
                briefingOpenTime += Time.unscaledDeltaTime;
                bool ready = briefingOpenTime >= BriefingMinSeconds;
                view.SetBriefingReady(ready);
                SetMoving(false);
                bool confirm = interactHeld && releasedSinceAction
                    || keyboard != null && keyboard.enterKey.wasPressedThisFrame;
                if (ready && confirm)
                    CloseBriefing();
                Draw(InteractionPrompt.None, outside: false);
                return;
            }

            if (!started)
            {
                SetMoving(false);
                if (interactHeld && releasedSinceAction || keyboard != null && keyboard.enterKey.wasPressedThisFrame)
                    StartMission();
                Draw(InteractionPrompt.None, outside: false);
                return;
            }

            if (mission.IsFinished)
            {
                SetMoving(false);
                if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                    Restart();
                Draw(InteractionPrompt.None, IsOutside());
                return;
            }

            if (keyboard != null && keyboard.rKey.wasPressedThisFrame && keyboard.leftShiftKey.isPressed)
            {
                Restart();
                return;
            }

            OpenNextDiscovery();
            if (view.IsDiscoveryVisible)
            {
                // Tempo congelado: a missão não avança, o astronauta não anda e os avisos não somem.
                discoveryOpenTime += Time.unscaledDeltaTime;
                bool ready = discoveryOpenTime >= DiscoveryMinSeconds;
                view.SetDiscoveryReady(ready);
                SetMoving(false);
                holdSpot = null;
                holdTime = 0;
                bool confirm = interactHeld && releasedSinceAction
                    || keyboard != null && keyboard.enterKey.wasPressedThisFrame;
                if (ready && confirm)
                    CloseDiscovery();
                Draw(InteractionPrompt.None, IsOutside(), 0f);
                return;
            }

            if (view.IsGuideVisible)
            {
                // Tutorial aberto: mesmo congelamento da descoberta.
                guideOpenTime += Time.unscaledDeltaTime;
                bool ready = guideOpenTime >= GuideMinSeconds;
                view.SetGuideReady(ready);
                SetMoving(false);
                holdSpot = null;
                holdTime = 0;
                bool confirm = interactHeld && releasedSinceAction
                    || keyboard != null && keyboard.enterKey.wasPressedThisFrame;
                if (ready && confirm)
                    CloseGuide();
                Draw(InteractionPrompt.None, IsOutside(), 0f);
                return;
            }

            Vector2 position = body.position;
            bool outside = IsOutside();
            mission.Update(Time.deltaTime / Mathf.Max(0.1f, secondsPerMarsHour), outside);

            DiscoverNearbySignals(position);
            if (laps.Move(position.x, position.y))
                view.ShowToast(new Notice(NoticeKind.Lesson, MarsGlobe.LapMessage));

            SpotDefinition spot = NearestSpot(position);
            InteractionPrompt prompt = BuildPrompt(spot, out float holdSeconds, out Action action);

            // Antes do primeiro uso de cada máquina, o E abre o tutorial dela.
            MachineGuide guide = GuideFor(spot);
            if (guide != null && !learnedMachines.Contains(guide.Id))
            {
                SpotDefinition guideSpot = spot;
                prompt = Ready(guide.Question);
                holdSeconds = 0;
                action = () => OpenGuide(guide, guideSpot);
            }

            bool holding = spot != null && prompt.Enabled && interactHeld && releasedSinceAction && action != null;
            if (holding)
            {
                if (holdSpot != spot)
                {
                    holdSpot = spot;
                    holdTime = 0;
                }
                holdTime += Time.deltaTime;
                prompt.Progress = holdSeconds > 0 ? Mathf.Clamp01(holdTime / holdSeconds) : 1f;
                if (holdTime >= holdSeconds)
                {
                    action();
                    holdSpot = null;
                    holdTime = 0;
                    releasedSinceAction = false;
                }
            }
            else
            {
                holdSpot = null;
                holdTime = 0;
            }

            // Parado enquanto trabalha numa ação longa.
            SetMoving(!(holding && holdSeconds > 0));
            view.SetHighlight(spot != null ? spot.Id : null, Time.time);
            Draw(prompt, outside);
        }

        /// <summary>Fundo da câmera (o solo do planeta cobre a tela toda; aparece só se ele não carregar).</summary>
        public static readonly Color BeyondMapColor = new Color(0.16f, 0.08f, 0.05f);

        void LateUpdate()
        {
            if (mainCamera == null || map == null)
                return;
            Vector3 target = CameraTarget(view.Astronaut.position);
            Vector3 current = mainCamera.transform.position;
            Vector3 next = Vector3.Lerp(current, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
            mainCamera.transform.position = new Vector3(next.x, next.y, -10f);
            FollowCamera();
        }

        void SnapCamera()
        {
            if (mainCamera == null)
                return;
            Vector3 target = CameraTarget(view.Astronaut.position);
            mainCamera.transform.position = new Vector3(target.x, target.y, -10f);
            FollowCamera();
        }

        /// <summary>Desenha o mundo na cópia do planeta em volta da câmera e posiciona as setas.</summary>
        void FollowCamera()
        {
            view.FollowCamera(mainCamera.transform.position, mainCamera.orthographicSize, mainCamera.aspect);
        }

        /// <summary>
        /// A câmera fica centrada no astronauta (sem travar na borda do mapa): assim o que está perto
        /// dele nunca fica escondido atrás dos painéis do HUD, que ocupam os cantos da tela.
        /// </summary>
        static Vector3 CameraTarget(Vector3 astronaut)
        {
            return new Vector3(astronaut.x, astronaut.y, -10f);
        }

        void Draw(InteractionPrompt prompt, bool outside)
        {
            Draw(prompt, outside, Time.deltaTime);
        }

        void Draw(InteractionPrompt prompt, bool outside, float deltaTime)
        {
            view.SetAstronautMotion(body.linearVelocity, holdSpot != null);
            view.Refresh(mission, outside, prompt, deltaTime, Time.time);
        }

        bool IsOutside()
        {
            return !map.IsInside(MarsWorld.Wrap(body.position));
        }

        void SetMoving(bool canMove)
        {
            if (mover != null && mover.enabled != canMove)
                mover.enabled = canMove;
            if (!canMove)
                body.linearVelocity = Vector2.zero;
        }

        // ---------- Pontos de interesse ----------

        void DiscoverNearbySignals(Vector2 position)
        {
            foreach (SpotDefinition spot in BaseLayout.Spots)
            {
                if (spot.Kind != SpotKind.IceDeposit)
                    continue;
                IceDeposit deposit = mission.FindDeposit(spot.Target);
                if (deposit == null || deposit.Discovered)
                    continue;
                if (Vector2.Distance(position, view.SpotPosition(spot, position)) <= BaseLayout.DiscoveryRadius)
                    mission.DiscoverDeposit(spot.Target);
            }
        }

        SpotDefinition NearestSpot(Vector2 position)
        {
            SpotDefinition best = null;
            float bestDistance = float.MaxValue;
            foreach (SpotDefinition spot in BaseLayout.Spots)
            {
                if (spot.Kind == SpotKind.Sample && mission.IsSampleCollected(spot.Target))
                    continue;
                float distance = Vector2.Distance(position, view.SpotPosition(spot, position));
                if (distance <= spot.Radius && distance < bestDistance)
                {
                    best = spot;
                    bestDistance = distance;
                }
            }
            return best;
        }

        InteractionPrompt BuildPrompt(SpotDefinition spot, out float holdSeconds, out Action action)
        {
            holdSeconds = 0;
            action = null;
            if (spot == null)
                return InteractionPrompt.None;

            float repairSeconds = RepairSeconds * (mission.LightsWorking ? 1f : 2f);
            string repairNote = mission.LightsWorking ? "" : "  (sem luz: demora mais)";

            switch (spot.Kind)
            {
                case SpotKind.Equipment:
                case SpotKind.IceStation:
                {
                    string target = spot.Target;
                    string name = Explanations.EquipmentName(target);
                    if (spot.Kind == SpotKind.IceStation && mission.CarriedIceKg > 0)
                    {
                        holdSeconds = 0.4f;
                        action = () => mission.DeliverIce();
                        return Ready("Entregar " + Explanations.Number(mission.CarriedIceKg, 0) + " kg de gelo");
                    }
                    if (mission.NeedsRepair(target))
                    {
                        holdSeconds = repairSeconds;
                        action = () => mission.Repair(target);
                        return Ready("Consertar " + name + repairNote);
                    }
                    action = () => mission.ToggleEquipment(target);
                    return Ready((mission.IsEquipmentOn(target) ? "Desligar " : "Ligar ") + name);
                }

                case SpotKind.Seal:
                    if (!mission.NeedsRepair(EquipmentFailure.SealTarget))
                        return Blocked("Vedação da escotilha: sem vazamento");
                    holdSeconds = repairSeconds;
                    action = () => mission.Repair(EquipmentFailure.SealTarget);
                    return Ready("Consertar vedação" + repairNote);

                case SpotKind.Panels:
                    if (!mission.PanelsDirty)
                        return Blocked("Painéis solares limpos");
                    holdSeconds = CleanSeconds;
                    action = () => mission.CleanPanels();
                    return Ready("Limpar a poeira dos painéis");

                case SpotKind.IceDeposit:
                {
                    if (!mission.CanCollectIce(spot.Target, out string reason))
                        return Blocked(reason);
                    holdSeconds = CollectIceSeconds;
                    string target = spot.Target;
                    action = () => mission.CollectIce(target);
                    return Ready("Coletar gelo");
                }

                case SpotKind.Lab:
                    if (mission.SamplesCarried == 0)
                        return Blocked("Laboratório: traga amostras de fora (losangos roxos)");
                    holdSeconds = AnalyzeSeconds;
                    action = () => mission.AnalyzeSamples();
                    return Ready("Analisar amostras");

                case SpotKind.Sample:
                {
                    holdSeconds = SampleSeconds;
                    string target = spot.Target;
                    action = () => mission.CollectSample(target);
                    return Ready("Coletar amostra");
                }
            }

            return InteractionPrompt.None;
        }

        static InteractionPrompt Ready(string text) => new InteractionPrompt { Text = text, Progress = -1f, Enabled = true };

        static InteractionPrompt Blocked(string text) => new InteractionPrompt { Text = text, Progress = -1f, Enabled = false };
    }

    /// <summary>Movimento com WASD usado só se o PlayerBehavior do projeto não for encontrado.</summary>
    public sealed class SimpleMover : MonoBehaviour
    {
        public float speed = 3f;
        Rigidbody2D body;

        void Start()
        {
            body = GetComponent<Rigidbody2D>();
        }

        void FixedUpdate()
        {
            Keyboard keyboard = Keyboard.current;
            if (body == null || keyboard == null)
                return;
            var input = new Vector2(
                (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            body.linearVelocity = input.normalized * speed;
        }
    }
}
