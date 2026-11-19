using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class CascadeV4Build
{
    const string Pending = "Cascade.V4Build.Pending";
    const string Configured = "Cascade.V4Build.Configured";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    static int settle;

    static CascadeV4Build()
    {
        if (SessionState.GetBool(Pending, false)) EditorApplication.delayCall += Export;
    }

    public static void Run()
    {
        try
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                throw new InvalidOperationException("Start Unity with -buildTarget iOS and install iOS Build Support.");
            SessionState.SetBool(Pending, true);
            EditorApplication.delayCall += Export;
        }
        catch (Exception e)
        {
            SessionState.SetBool(Pending, false);
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    static void Export()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Export;
            return;
        }
        if (!SessionState.GetBool(Configured, false))
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("V4 scene is missing.", ScenePath);
            var target = NamedBuildTarget.iOS;
            PlayerSettings.productName = "Cascade";
            PlayerSettings.bundleVersion = "1.0";
            PlayerSettings.SetApplicationIdentifier(target, "evgeniy.vvv.cascade.local");
            PlayerSettings.iOS.buildNumber = "4";
            PlayerSettings.iOS.appleDeveloperTeamID = "7P27V7D963";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            bool development = Array.IndexOf(Environment.GetCommandLineArgs(), "-cascadeDevelopment") >= 0;
            EditorUserBuildSettings.development = development;
            EditorUserBuildSettings.allowDebugging = development;
            EditorUserBuildSettings.connectProfiler = development;
            SessionState.SetBool(Configured, true);
            settle = 0;
            EditorApplication.delayCall += Export;
            return;
        }
        if (settle < 3)
        {
            settle++;
            EditorApplication.delayCall += Export;
            return;
        }
        SessionState.SetBool(Pending, false);
        try
        {
            var args = Environment.GetCommandLineArgs();
            int pathIndex = Array.IndexOf(args, "-cascadeOutputPath");
            string output = pathIndex >= 0 && pathIndex + 1 < args.Length
                ? args[pathIndex + 1]
                : "Builds/iOS/Xcode";
            Directory.CreateDirectory(output);
            bool development = EditorUserBuildSettings.development;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.iOS,
                options = development ? BuildOptions.Development : BuildOptions.None
            });
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log(ok
                ? "Cascade V4 iOS export succeeded: " + output
                : "Cascade V4 iOS export failed: " + report.summary.result);
            EditorApplication.Exit(ok ? 0 : 1);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}
