using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Cascade.DevBridge
{
    /// <summary>
    /// Мост для разработки: пишет ошибки компиляции и лог игры в папку DevBridge рядом с Assets
    /// и выполняет простые команды из файла command.txt (refresh, play, stop, shot, menu, call, delete).
    /// В сборку игры не попадает (только редактор).
    /// </summary>
    [InitializeOnLoad]
    public static class DevBridge
    {
        private static readonly string Dir;
        private static readonly string CommandPath;
        private static readonly string CompilePath;
        private static readonly string LogPath;
        private static readonly string StatusPath;
        private static readonly List<string> CompileMessages = new List<string>();
        private static readonly object LogLock = new object();
        private static double nextPoll;
        private static double nextHeartbeat;
        private static int errorCount;
        private static int warningCount;
        private static string lastCommand = "";
        private static string lastResult = "";

        static DevBridge()
        {
            Dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "DevBridge"));
            CommandPath = Path.Combine(Dir, "command.txt");
            CompilePath = Path.Combine(Dir, "compile.txt");
            LogPath = Path.Combine(Dir, "log.txt");
            StatusPath = Path.Combine(Dir, "status.txt");
            Directory.CreateDirectory(Dir);
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            Application.logMessageReceivedThreaded += OnLog;
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            WriteStatus("domain loaded");
        }

        private static void OnCompilationStarted(object context)
        {
            CompileMessages.Clear();
            errorCount = 0;
            warningCount = 0;
            SafeWrite(CompilePath, "COMPILING " + DateTime.Now.ToString("HH:mm:ss") + "\n");
        }

        private static void OnAssemblyFinished(string assemblyPath, CompilerMessage[] messages)
        {
            for (int i = 0; i < messages.Length; i++)
            {
                CompilerMessage m = messages[i];
                bool isError = m.type == CompilerMessageType.Error;
                if (isError)
                {
                    errorCount++;
                }
                else
                {
                    warningCount++;
                }
                if (isError || CompileMessages.Count < 400)
                {
                    CompileMessages.Add((isError ? "ERROR " : "WARN  ") + m.message);
                }
            }
        }

        private static void OnCompilationFinished(object context)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("DONE ").Append(DateTime.Now.ToString("HH:mm:ss"));
            sb.Append(" errors=").Append(errorCount).Append(" warnings=").Append(warningCount).Append('\n');
            for (int i = 0; i < CompileMessages.Count; i++)
            {
                if (CompileMessages[i].StartsWith("ERROR"))
                {
                    sb.Append(CompileMessages[i]).Append('\n');
                }
            }
            for (int i = 0; i < CompileMessages.Count; i++)
            {
                if (!CompileMessages[i].StartsWith("ERROR"))
                {
                    sb.Append(CompileMessages[i]).Append('\n');
                }
            }
            SafeWrite(CompilePath, sb.ToString());
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            bool important = type == LogType.Error || type == LogType.Exception || type == LogType.Assert || type == LogType.Warning;
            if (!important && (condition == null || !condition.StartsWith("[Cascade]")))
            {
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append(' ').Append(type).Append(": ").Append(condition).Append('\n');
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !string.IsNullOrEmpty(stackTrace))
            {
                string[] lines = stackTrace.Split('\n');
                int count = Math.Min(lines.Length, 10);
                for (int i = 0; i < count; i++)
                {
                    if (lines[i].Length > 0)
                    {
                        sb.Append("    ").Append(lines[i]).Append('\n');
                    }
                }
            }
            lock (LogLock)
            {
                try
                {
                    File.AppendAllText(LogPath, sb.ToString());
                }
                catch (Exception)
                {
                }
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                lock (LogLock)
                {
                    SafeWrite(LogPath, "PLAY SESSION " + DateTime.Now.ToString("HH:mm:ss") + "\n");
                }
            }
            WriteStatus("playmode " + change);
        }

        private static void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextPoll)
            {
                return;
            }
            nextPoll = now + 0.4;
            if (File.Exists(CommandPath))
            {
                string text = "";
                try
                {
                    text = File.ReadAllText(CommandPath);
                    File.Delete(CommandPath);
                }
                catch (Exception e)
                {
                    lastResult = "cannot read command: " + e.Message;
                }
                string[] lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length > 0)
                    {
                        Execute(line);
                    }
                }
                WriteStatus("command");
                nextHeartbeat = now + 2.0;
                return;
            }
            if (now >= nextHeartbeat)
            {
                nextHeartbeat = now + 2.0;
                WriteStatus("heartbeat");
            }
        }

        private static void Execute(string line)
        {
            lastCommand = line;
            try
            {
                int space = line.IndexOf(' ');
                string name = space < 0 ? line : line.Substring(0, space);
                string arg = space < 0 ? "" : line.Substring(space + 1).Trim();
                if (name == "refresh")
                {
                    AssetDatabase.Refresh();
                    lastResult = "refreshed";
                }
                else if (name == "play")
                {
                    EditorApplication.isPlaying = true;
                    lastResult = "play requested";
                }
                else if (name == "stop")
                {
                    EditorApplication.isPlaying = false;
                    lastResult = "stop requested";
                }
                else if (name == "shot")
                {
                    string file = Path.Combine(Dir, (arg.Length > 0 ? arg : "shot") + ".png");
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                    ScreenCapture.CaptureScreenshot(file);
                    lastResult = "screenshot " + file;
                }
                else if (name == "menu")
                {
                    bool ok = EditorApplication.ExecuteMenuItem(arg);
                    lastResult = "menu " + arg + " -> " + ok;
                }
                else if (name == "delete")
                {
                    bool ok = AssetDatabase.DeleteAsset(arg);
                    lastResult = "delete " + arg + " -> " + ok;
                }
                else if (name == "clearlog")
                {
                    lock (LogLock)
                    {
                        SafeWrite(LogPath, "");
                    }
                    lastResult = "log cleared";
                }
                else if (name == "call")
                {
                    lastResult = Call(arg);
                }
                else
                {
                    lastResult = "unknown command";
                }
            }
            catch (Exception e)
            {
                lastResult = "EXCEPTION " + e;
            }
        }

        private static string Call(string arg)
        {
            string[] parts = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "call: no target";
            }
            int dot = parts[0].LastIndexOf('.');
            if (dot < 0)
            {
                return "call: use Namespace.Type.Method";
            }
            string typeName = parts[0].Substring(0, dot);
            string methodName = parts[0].Substring(dot + 1);
            string[] args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);
            System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(typeName, false);
                if (type == null)
                {
                    continue;
                }
                MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    return "call: method not found " + methodName;
                }
                object result = method.Invoke(null, new object[] { args });
                return "call ok: " + (result == null ? "null" : result.ToString());
            }
            return "call: type not found " + typeName;
        }

        private static void WriteStatus(string reason)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("time=").Append(DateTime.Now.ToString("HH:mm:ss")).Append('\n');
            sb.Append("reason=").Append(reason).Append('\n');
            sb.Append("isPlaying=").Append(EditorApplication.isPlaying).Append('\n');
            sb.Append("isCompiling=").Append(EditorApplication.isCompiling).Append('\n');
            sb.Append("isUpdating=").Append(EditorApplication.isUpdating).Append('\n');
            sb.Append("lastCommand=").Append(lastCommand).Append('\n');
            sb.Append("lastResult=").Append(lastResult).Append('\n');
            SafeWrite(StatusPath, sb.ToString());
        }

        private static void SafeWrite(string path, string text)
        {
            try
            {
                File.WriteAllText(path, text);
            }
            catch (Exception)
            {
            }
        }
    }
}
