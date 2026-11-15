using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MissaoMarte.EditorTools
{
    /// <summary>
    /// Ferramenta de desenvolvimento (só Editor, não entra no build).
    /// Permite rodar os testes EditMode com o Unity aberto, sem clicar em nada:
    ///   1. Crie o arquivo  TestResults/run-editmode.trigger  na raiz do projeto.
    ///   2. O Editor recarrega os assets, compila e roda os testes EditMode.
    ///   3. O resultado é gravado em  TestResults/editmode-summary.txt.
    /// Se houver cena com alterações não salvas, o pedido é recusado (RESULT: BLOCKED).
    /// O resumo também é gravado quando os testes são rodados pela janela Test Runner.
    /// </summary>
    [InitializeOnLoad]
    static class TestRunBridge
    {
        const string RefreshedKey = "MissaoMarte.TestRunBridge.Refreshed";
        const double PollIntervalSeconds = 0.5;

        static readonly string ResultsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestResults"));
        static readonly string TriggerPath = Path.Combine(ResultsDir, "run-editmode.trigger");
        static readonly string SummaryPath = Path.Combine(ResultsDir, "editmode-summary.txt");

        static readonly TestRunnerApi Api;
        static double nextCheck;

        static TestRunBridge()
        {
            Api = ScriptableObject.CreateInstance<TestRunnerApi>();
            Api.RegisterCallbacks(new Callbacks());
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextCheck)
                return;
            nextCheck = EditorApplication.timeSinceStartup + PollIntervalSeconds;

            if (!File.Exists(TriggerPath))
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(TriggerPath);
                WriteSummary("RESULT: BLOCKED\nO Editor está em Play Mode; saia do Play para rodar os testes.\n");
                return;
            }

            // Primeiro passo: importar/compilar o que mudou em disco. Se houver compilação, o domínio
            // recarrega e esta classe reinicia; o SessionState lembra que o refresh já foi feito.
            if (!SessionState.GetBool(RefreshedKey, false))
            {
                SessionState.SetBool(RefreshedKey, true);
                AssetDatabase.Refresh();
                nextCheck = EditorApplication.timeSinceStartup + 1.5;
                return;
            }

            SessionState.SetBool(RefreshedKey, false);
            File.Delete(TriggerPath);

            if (EditorUtility.scriptCompilationFailed)
            {
                WriteSummary("RESULT: COMPILATION_FAILED\nHá erros de compilação; veja o Console ou Logs/Editor.log.\n");
                return;
            }

            // O Test Framework abre um diálogo modal "salvar cena?" quando há cena modificada.
            // Para não interromper quem está usando o Editor, o pedido é recusado nesse caso.
            if (AnySceneDirty())
            {
                WriteSummary("RESULT: BLOCKED\nHá cena com alterações não salvas; salve a cena (Ctrl+S) e peça os testes de novo.\n");
                return;
            }

            Api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
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

        static void WriteSummary(string text)
        {
            Directory.CreateDirectory(ResultsDir);
            string temp = SummaryPath + ".tmp";
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            if (File.Exists(SummaryPath))
                File.Delete(SummaryPath);
            File.Move(temp, SummaryPath);
        }

        sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var text = new StringBuilder();
                text.Append("RESULT: ").Append(result.FailCount == 0 && result.PassCount > 0 ? "PASSED" : "FAILED").Append('\n');
                text.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "passed={0} failed={1} skipped={2} inconclusive={3} duration={4:0.00}s\n",
                    result.PassCount, result.FailCount, result.SkipCount, result.InconclusiveCount, result.Duration);
                text.Append("finished=").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append('\n');
                text.Append('\n');
                AppendLeaves(result, text);

                try
                {
                    WriteSummary(text.ToString());
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[TestRunBridge] Não foi possível gravar o resumo dos testes: " + exception.Message);
                }
            }

            static void AppendLeaves(ITestResultAdaptor result, StringBuilder text)
            {
                if (result.HasChildren)
                {
                    foreach (ITestResultAdaptor child in result.Children)
                        AppendLeaves(child, text);
                    return;
                }

                text.Append('[').Append(result.TestStatus.ToString().ToUpperInvariant()).Append("] ").Append(result.Test.FullName).Append('\n');
                if (result.TestStatus != TestStatus.Passed)
                {
                    if (!string.IsNullOrEmpty(result.Message))
                        text.Append("    ").Append(result.Message.Replace("\n", "\n    ")).Append('\n');
                    if (!string.IsNullOrEmpty(result.StackTrace))
                        text.Append("    ").Append(result.StackTrace.Replace("\n", "\n    ")).Append('\n');
                }
            }
        }
    }
}
