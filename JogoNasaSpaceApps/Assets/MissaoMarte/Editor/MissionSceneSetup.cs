using System.Collections.Generic;
using System.IO;
using MissaoMarte.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MissaoMarte.EditorTools
{
    /// <summary>
    /// Cria a cena do jogo (câmera + objeto com o <see cref="MissionController"/>) e a coloca como
    /// primeira cena do build. A cena é criada automaticamente se ainda não existir; o menu
    /// "Missão Marte" permite abri-la ou recriá-la.
    /// </summary>
    [InitializeOnLoad]
    static class MissionSceneSetup
    {
        public const string ScenePath = "Assets/MissaoMarte/Scenes/MissaoMarte.unity";

        static MissionSceneSetup()
        {
            // Usa "update" porque ele roda mesmo com o Editor em segundo plano; com delayCall a cena
            // não era criada enquanto a janela do Unity estava sem foco.
            EditorApplication.update += CreateIfMissing;
            EditorApplication.update += PollOpenRequest;
        }

        // ---------- Abrir a cena por arquivo de pedido (ferramenta de desenvolvimento) ----------
        // Criar TestResults/open-game-scene.trigger abre a cena do jogo; o resultado sai em
        // TestResults/open-game-scene.txt. O pedido é recusado se houver cena com alterações não salvas.

        static readonly string ResultsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestResults"));
        static readonly string OpenTriggerPath = Path.Combine(ResultsDir, "open-game-scene.trigger");
        static readonly string OpenResultPath = Path.Combine(ResultsDir, "open-game-scene.txt");
        static double nextOpenCheck;

        static void PollOpenRequest()
        {
            if (EditorApplication.timeSinceStartup < nextOpenCheck)
                return;
            nextOpenCheck = EditorApplication.timeSinceStartup + 0.5;

            if (!File.Exists(OpenTriggerPath) || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            File.Delete(OpenTriggerPath);

            string result;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                result = "BLOCKED: o Editor está em Play Mode.";
            }
            else if (AnySceneDirty())
            {
                result = "BLOCKED: há cena com alterações não salvas.";
            }
            else
            {
                if (!File.Exists(ScenePath))
                    CreateScene();
                EditorSceneManager.OpenScene(ScenePath);
                result = "OK: " + SceneManager.GetActiveScene().path;
            }

            File.WriteAllText(OpenResultPath, result + "\n");
        }

        static bool AnySceneDirty()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                    return true;
            }
            return false;
        }

        static void CreateIfMissing()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            EditorApplication.update -= CreateIfMissing;
            if (File.Exists(ScenePath) || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            CreateScene();
        }

        [MenuItem("Missão Marte/Abrir cena do jogo")]
        static void OpenScene()
        {
            if (!File.Exists(ScenePath))
                CreateScene();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Missão Marte/Recriar cena do jogo")]
        static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            // A cena é montada à parte (modo aditivo) para não mexer na cena que está aberta.
            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);

                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
                cameraObject.AddComponent<AudioListener>();

                var game = new GameObject("Missao Marte");
                game.AddComponent<MissionController>();

                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                if (previousActive.IsValid())
                    SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
            }

            AddToBuildSettingsAsFirstScene();
            Debug.Log("[Missão Marte] Cena do jogo criada em " + ScenePath);
        }

        static void AddToBuildSettingsAsFirstScene()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
