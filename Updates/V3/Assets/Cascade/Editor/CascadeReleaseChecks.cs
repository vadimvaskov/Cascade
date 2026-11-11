using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class CascadeReleaseChecks
{
    [Serializable] sealed class DependencyReport { public string scene,unity;public string[] assets; }
    [MenuItem("Cascade/iOS/Check release sources")]
    public static string Run()
    {
        string scene=CascadeIOSBuild.ScenePath;
        if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(scene))throw new BuildFailedException("Missing approved game scene");
        var enabled=EditorBuildSettings.scenes.Where(s=>s.enabled).ToArray();
        if(enabled.Length!=1||enabled[0].path!=scene)throw new BuildFailedException("The release must start with CascadeGame.unity");
        if(!File.ReadAllText(scene).Contains("approvedDesign: 1"))throw new BuildFailedException("Release scene is not the approved integrated design");
        var resources=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)&&p.Contains("/Resources/")&&!AssetDatabase.IsValidFolder(p));
        var paths=AssetDatabase.GetDependencies(resources.Concat(new[]{scene}).ToArray(),true)
            .Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        foreach(string path in paths)
            if(!File.Exists(path)||!File.Exists(path+".meta"))throw new BuildFailedException("Missing source or stable GUID: "+path);
        foreach(string path in new[]{"Assets/Cascade/Art/FaRodin/Fa_Rodin.fbx","Assets/CascadeDream/Art/Mi/Mi_Rodin.fbx","Assets/Plugins/iOS/CascadeHaptics.mm","Assets/Plugins/iOS/CascadeAudioOutputTiming.mm"})
            if(!File.Exists(path)||!File.Exists(path+".meta"))throw new BuildFailedException("Missing runner or iOS plugin: "+path);
        if(!Resources.Load<Shader>("Approved/MusicEnergyVolume"))throw new BuildFailedException("3D energy shader must be included in Resources");
        string core=CascadeIntegrationChecks.Run();
        Directory.CreateDirectory("Documentation/Release");
        File.WriteAllText("Documentation/Release/dependencies.json",JsonUtility.ToJson(new DependencyReport{scene=scene,unity=Application.unityVersion,assets=paths},true)+"\n");
        string report="PASS: approved release scene, "+paths.Length+" scene/Resources dependencies and GUIDs, both runners and native iOS plugins\n"+core;
        File.WriteAllText("Documentation/Release/source-checks.txt",report+"\n");
        Debug.Log(report);return report;
    }
}
