using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

// Called explicitly by release tooling; loading the Editor never starts a build.
public static class CascadeIOSBuild
{
    public const string ScenePath = "Assets/Scenes/CascadePrototype.unity";
    public const string IconPath = "Assets/Cascade/Art/AppIcon.png";
    public const string StatusPath = "Builds/iOS/build-status.json";
    const string BundleIdentifier = "fitness.quokka.caskade";
    const string DeveloperTeam = "RUT46C38YA";

    [Serializable]
    public sealed class BuildStatus
    {
        public string status;
        public bool succeeded;
        public string outputPath, version, buildNumber, startedAtUtc, finishedAtUtc;
        public string[] errors = Array.Empty<string>();
        public BuildSummaryData summary;
    }

    [Serializable]
    public sealed class BuildSummaryData
    {
        public string result, platform;
        public int totalErrors, totalWarnings;
        public long totalBytes;
        public double totalSeconds;
    }

    public static string Configure(string version, string build)
    {
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("A release version is required.", nameof(version));
        if (string.IsNullOrWhiteSpace(build)) throw new ArgumentException("An iOS build number is required.", nameof(build));
        if (!File.Exists(ScenePath)) throw new FileNotFoundException("Cascade release scene is missing.", ScenePath);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (texture == null) throw new FileNotFoundException("Import the Cascade app icon before configuring iOS.", IconPath);

        var target = NamedBuildTarget.iOS;
        var kinds = PlayerSettings.GetSupportedIconKinds(target);
        if (kinds.Length == 0) throw new InvalidOperationException("No iOS icon kinds are available. Check the installed iOS Build Support module.");

        PlayerSettings.productName = "Cascade";
        PlayerSettings.companyName = "Quokka";
        PlayerSettings.bundleVersion = version.Trim();
        PlayerSettings.SetApplicationIdentifier(target, BundleIdentifier);
        PlayerSettings.iOS.buildNumber = build.Trim();
        PlayerSettings.iOS.appleDeveloperTeamID = DeveloperTeam;
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
        PlayerSettings.iOS.requiresFullScreen = true;
        if (!Version.TryParse(PlayerSettings.iOS.targetOSVersionString, out var minimum) || minimum < new Version(15, 0))
            PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedCodeVariant(target, ManagedCodeVariant.Release);
        PlayerSettings.SetIl2CppCompilerConfiguration(target, Il2CppCompilerConfiguration.Release);
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.allowDebugging = false;
        EditorUserBuildSettings.connectProfiler = false;

        int slots = 0;
        foreach (var kind in kinds)
        {
            var icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (var icon in icons) { icon.SetTexture(texture, 0); slots++; }
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        string result = "Configured Cascade " + PlayerSettings.bundleVersion + " (" + PlayerSettings.iOS.buildNumber + ") for iOS: " +
                        BundleIdentifier + ", IL2CPP Release, " + slots + " icon slots, " + ScenePath;
        Debug.Log(result);
        return result;
    }

    // Invoke from EditorApplication.delayCall after any target switch or settings-triggered reload.
    // This exports the Xcode project; signing, archiving and uploading are separate release steps.
    public static string Build(string outputPath)
    {
        var state = new BuildStatus
        {
            status = "building", succeeded = false, outputPath = outputPath,
            version = PlayerSettings.bundleVersion, buildNumber = PlayerSettings.iOS.buildNumber,
            startedAtUtc = DateTime.UtcNow.ToString("O")
        };
        WriteStatus(state);
        try
        {
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("An Xcode export directory is required.", nameof(outputPath));
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for Edit Mode and all script compilation/imports before building.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                throw new InvalidOperationException("Switch the active build target to iOS and wait for its reload before building.");
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Cascade release scene is missing.", ScenePath);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None
            });
            if (report == null) throw new InvalidOperationException("Unity did not return a build report.");
            var summary = report.summary;
            state.summary = new BuildSummaryData
            {
                result = summary.result.ToString(), platform = summary.platform.ToString(),
                totalErrors = (int)summary.totalErrors, totalWarnings = (int)summary.totalWarnings,
                totalBytes = (long)summary.totalSize, totalSeconds = summary.totalTime.TotalSeconds
            };
            var errors = new List<string>();
            foreach (var step in report.steps)
                foreach (var message in step.messages)
                    if (message.type == LogType.Error || message.type == LogType.Exception || message.type == LogType.Assert)
                        errors.Add(step.name + ": " + message.content);
            state.errors = errors.ToArray();
            state.succeeded = summary.result == BuildResult.Succeeded;
            state.status = state.succeeded ? "succeeded" : "failed";
        }
        catch (Exception exception)
        {
            state.status = "failed";
            state.succeeded = false;
            state.errors = new[] { exception.ToString() };
            Debug.LogError("Cascade iOS build failed: " + exception.Message);
        }
        state.finishedAtUtc = DateTime.UtcNow.ToString("O");
        WriteStatus(state);
        string json = JsonUtility.ToJson(state, true);
        Debug.Log("Cascade iOS export " + state.status + ". Report: " + StatusPath);
        return json;
    }

    static void WriteStatus(BuildStatus state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatusPath));
        File.WriteAllText(StatusPath, JsonUtility.ToJson(state, true));
    }

#if UNITY_IOS
    [PostProcessBuild(1000)]
    public static void ConfigureExportedPlist(BuildTarget target, string outputPath)
    {
        if (target != BuildTarget.iOS) return;
        string path = Path.Combine(outputPath, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(path);
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        plist.root.SetBoolean("UIRequiresFullScreen", true);
        plist.WriteToFile(path);
    }
#endif
}
