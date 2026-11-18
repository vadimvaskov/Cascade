using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Cascade.DevBridge
{
    /// <summary>Запускает тесты ядра по файлу DevBridge/tests.cmd и пишет итог в DevBridge/tests.txt.</summary>
    [InitializeOnLoad]
    public static class DevBridgeTests
    {
        private static readonly string Dir;
        private static readonly string CommandPath;
        private static readonly string ResultPath;
        private static TestRunnerApi api;
        private static Callbacks callbacks;
        private static double nextPoll;

        static DevBridgeTests()
        {
            Dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "DevBridge"));
            CommandPath = Path.Combine(Dir, "tests.cmd");
            ResultPath = Path.Combine(Dir, "tests.txt");
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextPoll)
            {
                return;
            }
            nextPoll = now + 1.0;
            if (!File.Exists(CommandPath))
            {
                return;
            }
            try
            {
                File.Delete(CommandPath);
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    File.WriteAllText(ResultPath, "SKIPPED: stop play mode before running tests\n");
                    return;
                }
                Run();
            }
            catch (Exception e)
            {
                File.WriteAllText(ResultPath, "EXCEPTION " + e);
            }
        }

        private static void Run()
        {
            if (api == null)
            {
                api = ScriptableObject.CreateInstance<TestRunnerApi>();
                callbacks = new Callbacks(ResultPath);
                api.RegisterCallbacks(callbacks);
            }
            File.WriteAllText(ResultPath, "RUNNING " + DateTime.Now.ToString("HH:mm:ss") + "\n");
            Filter filter = new Filter();
            filter.testMode = TestMode.EditMode;
            api.Execute(new ExecutionSettings(filter));
        }

        private sealed class Callbacks : ICallbacks
        {
            private readonly string resultPath;
            private readonly StringBuilder lines = new StringBuilder();

            public Callbacks(string resultPath)
            {
                this.resultPath = resultPath;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                lines.Length = 0;
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                string head = "RESULT " + result.TestStatus + " passed=" + result.PassCount + " failed=" + result.FailCount
                    + " skipped=" + result.SkipCount + " time=" + DateTime.Now.ToString("HH:mm:ss") + "\n";
                File.WriteAllText(resultPath, head + lines);
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren)
                {
                    return;
                }
                lines.Append(result.TestStatus).Append(' ').Append(result.Test.FullName).Append('\n');
                if (result.TestStatus == TestStatus.Failed)
                {
                    lines.Append("    ").Append(result.Message).Append('\n');
                }
            }
        }
    }
}
