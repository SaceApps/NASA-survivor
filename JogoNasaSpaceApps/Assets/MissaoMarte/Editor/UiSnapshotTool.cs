using System;
using System.IO;
using System.Text;
using MissaoMarte.Game;
using MissaoMarte.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MissaoMarte.EditorTools
{
    /// <summary>
    /// Ferramenta de desenvolvimento (só Editor): desenha o jogo em imagens, sem entrar em Play Mode e
    /// sem tocar na cena aberta, para conferir o visual e a posição das estações no cenário.
    /// Uso: menu "Missão Marte/Capturar telas", ou criar o arquivo TestResults/ui-snapshot.trigger.
    /// As imagens saem em TestResults/ui-*.png e o resultado em TestResults/ui-snapshot.txt.
    /// </summary>
    [InitializeOnLoad]
    static class UiSnapshotTool
    {
        const int Width = 1280;
        const int Height = 720;

        static readonly string ResultsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestResults"));
        static readonly string TriggerPath = Path.Combine(ResultsDir, "ui-snapshot.trigger");
        static readonly string ResultPath = Path.Combine(ResultsDir, "ui-snapshot.txt");

        static double nextCheck;

        static UiSnapshotTool()
        {
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextCheck)
                return;
            nextCheck = EditorApplication.timeSinceStartup + 0.5;

            if (!File.Exists(TriggerPath) || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            File.Delete(TriggerPath);
            CaptureAll();
        }

        [MenuItem("Missão Marte/Capturar telas")]
        static void CaptureAll()
        {
            Directory.CreateDirectory(ResultsDir);
            var result = new StringBuilder();
            try
            {
                MapData map = MapData.Load();
                if (map == null)
                    throw new InvalidOperationException("Cenário não carregado.");

                result.AppendLine("OK");
                result.AppendLine("mapa " + map.Width + "x" + map.Height);
                foreach (SpotDefinition spot in BaseLayout.Spots)
                {
                    bool inside = map.IsInside(MarsWorld.Wrap(spot.Home(map)));
                    result.AppendLine("ponto " + spot.Id + " " + (inside ? "DENTRO" : "FORA"));
                }
                result.AppendLine("spawn " + (map.IsInside(map.PixelToWorld(BaseLayout.SpawnPixel.x, BaseLayout.SpawnPixel.y)) ? "DENTRO" : "FORA"));

                Capture("ui-0-briefing.png", 3.3f, mission => Pixel(map, 690, 330), false, briefing: true);

                Capture("ui-1-base.png", 3.3f, mission =>
                {
                    mission.Update(3, false);
                    return Pixel(map, 690, 330);
                }, false);

                Capture("ui-2-fora.png", 3.3f, mission =>
                {
                    // Consertando tudo a tempo, para chegar viva na tempestade do sol 3.
                    string[] targets = { Simulation.EquipmentIds.WaterRecycler, Simulation.EquipmentIds.Co2Scrubber,
                        Simulation.EquipmentIds.OxygenGenerator, Events.EquipmentFailure.SealTarget };
                    for (int i = 0; i < 54; i++)
                    {
                        foreach (string target in targets)
                        {
                            if (mission.NeedsRepair(target))
                                mission.Repair(target);
                        }
                        mission.Update(1, false);
                    }
                    mission.DiscoverDeposit(PrimeiraFonte.DepositEast);
                    mission.CollectIce(PrimeiraFonte.DepositEast);
                    mission.Update(4, true);
                    return Home(map, "deposito_leste") + new Vector2(-0.5f, 0.3f);
                }, true);

                // Longe da base: as setas na borda apontam para a base, o gelo e as amostras.
                Capture("ui-8-longe.png", 3.3f, mission =>
                {
                    mission.Update(3, true);
                    return new Vector2(-6f, -12f);
                }, true);

                // Na "borda" do planeta (x = ±24): o solo e as pedras continuam do outro lado, sem emenda.
                Capture("ui-9-volta.png", 3.3f, mission =>
                {
                    mission.Update(3, true);
                    return new Vector2(-24.3f, -6.5f);
                }, true);

                // Visão do planeta inteiro (só para conferir o solo e onde ficam as coisas).
                Capture("ui-10-planeta.png", 19f, mission =>
                {
                    mission.Update(3, false);
                    return Vector2.zero;
                }, false);

                Capture("ui-3-mapa.png", 4.75f, mission =>
                {
                    for (int i = 0; i < 16; i++)
                        mission.Update(1, false);
                    return Pixel(map, 637, 470);
                }, false);

                Capture("ui-5-descoberta.png", 3.3f, mission =>
                {
                    mission.Update(3, false);
                    mission.CollectSample("snapshot");
                    mission.AnalyzeSamples();
                    return Pixel(map, 690, 330);
                }, false);

                Capture("ui-6-menu.png", 3.3f, mission =>
                {
                    mission.Update(3, false);
                    return Pixel(map, 690, 330);
                }, false, menu: true);

                Capture("ui-7-tutorial.png", 3.3f, mission =>
                {
                    mission.Update(3, false);
                    return Pixel(map, 855, 200);
                }, false, guideSpot: "o2");

                Capture("ui-4-fim.png", 3.3f, mission =>
                {
                    while (!mission.IsFinished)
                        mission.Update(1, false);
                    return Pixel(map, 650, 390);
                }, false);
            }
            catch (Exception exception)
            {
                result.Clear();
                result.AppendLine("ERROR");
                result.AppendLine(exception.ToString());
            }

            File.WriteAllText(ResultPath, result.ToString());
        }

        static Vector2 Pixel(MapData map, float x, float y) => map.PixelToWorld(x, y);

        static Vector2 Home(MapData map, string spotId) => Array.Find(BaseLayout.Spots, d => d.Id == spotId).Home(map);

        static void Capture(string fileName, float cameraSize, Func<HabitatMission, Vector2> prepare, bool outside, bool menu = false, string guideSpot = null, bool briefing = false)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D image = null;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                var cameraObject = new GameObject("SnapshotCamera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = cameraSize;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = MissionController.BeyondMapColor;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 100f;

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                camera.aspect = (float)Width / Height;

                var root = new GameObject("Raiz");
                SceneManager.MoveGameObjectToScene(root, scene);

                MapData map = MapData.Load();
                var view = new GameView(root.transform, map, camera);
                view.SetIntroVisible(false);

                HabitatMission mission = PrimeiraFonte.Create();
                // Como no jogo: descoberta da pesquisa vai para a janela central, o resto para os avisos.
                mission.Noticed += notice =>
                {
                    if (notice.Kind == NoticeKind.Discovery)
                        view.ShowDiscovery(notice.Text);
                    else
                        view.ShowToast(notice);
                };
                Vector2 astronaut = prepare(mission);
                if (briefing)
                {
                    view.ShowBriefing(MarsBriefing.Create(mission, MissionController.DefaultSecondsPerMarsHour));
                    view.SetBriefingReady(true);
                }
                if (view.IsDiscoveryVisible)
                    view.SetDiscoveryReady(true);
                view.SetMenuVisible(menu);
                if (guideSpot != null)
                {
                    SpotDefinition spot = Array.Find(BaseLayout.Spots, d => d.Id == guideSpot);
                    MachineGuide guide = MachineGuides.For(spot.Target ?? (spot.Kind == SpotKind.Lab ? MachineGuides.Lab : MachineGuides.Panels));
                    view.ShowGuide(guide, guideSpot);
                    view.SetGuideReady(true);
                }
                view.Astronaut.position = astronaut;

                camera.transform.position = new Vector3(astronaut.x, astronaut.y, -10f);

                var prompt = new InteractionPrompt { Text = "Coletar gelo", Progress = 0.6f, Enabled = true };
                view.Refresh(mission, outside, outside ? prompt : InteractionPrompt.None, 0f, 0.1f);
                view.FollowCamera(astronaut, cameraSize, camera.aspect);

                Canvas.ForceUpdateCanvases();
                camera.Render();
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = target;
                image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(ResultsDir, fileName), image.EncodeToPNG());

                camera.targetTexture = null;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                if (image != null)
                    UnityEngine.Object.DestroyImmediate(image);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
