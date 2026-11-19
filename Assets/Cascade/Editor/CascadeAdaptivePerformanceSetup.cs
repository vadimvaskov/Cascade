using UnityEditor;
using UnityEditor.AdaptivePerformance.Editor;
using UnityEditor.AdaptivePerformance.Editor.Metadata;
using UnityEngine;
using UnityEngine.AdaptivePerformance;

[InitializeOnLoad]
public static class CascadeAdaptivePerformanceSetup
{
    const string Folder = "Assets/Adaptive Performance";
    const string GeneralPath = Folder + "/AdaptivePerformanceGeneralSettings.asset";
    const string AppleLoader = "UnityEngine.AdaptivePerformance.Apple.AppleProviderLoader";

    static CascadeAdaptivePerformanceSetup()
    {
        EditorApplication.delayCall += Ensure;
    }

    public static void Ensure()
    {
        try
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "Adaptive Performance");

            bool changed = false;
            var general = AdaptivePerformanceGeneralSettingsPerBuildTarget.AdaptivePerformanceGeneralSettingsForBuildTarget(BuildTargetGroup.iOS);
            if (general == null)
            {
                general = AssetDatabase.LoadAssetAtPath<AdaptivePerformanceGeneralSettings>(GeneralPath);
                if (general == null)
                {
                    general = ScriptableObject.CreateInstance<AdaptivePerformanceGeneralSettings>();
                    AssetDatabase.CreateAsset(general, GeneralPath);
                }
                AdaptivePerformanceGeneralSettingsPerBuildTarget.SetSettingsForBuildTarget(BuildTargetGroup.iOS, general);
                changed = true;
            }

            var manager = general.Manager;
            if (manager == null)
            {
                manager = ScriptableObject.CreateInstance<AdaptivePerformanceManagerSettings>();
                manager.name = "AdaptivePerformanceManagerSettings";
                AssetDatabase.AddObjectToAsset(manager, general);
                general.Manager = manager;
                changed = true;
            }

            if (!general.InitManagerOnStart)
            {
                general.InitManagerOnStart = true;
                changed = true;
            }
            if (!manager.automaticLoading)
            {
                manager.automaticLoading = true;
                changed = true;
            }
            if (!manager.automaticRunning)
            {
                manager.automaticRunning = true;
                changed = true;
            }
            if (!HasApple(manager))
            {
                changed |= AdaptivePerformancePackageMetadataStore.AssignLoader(manager, AppleLoader, BuildTargetGroup.iOS);
            }
            if (!changed) return;
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Cascade] Adaptive Performance setup skipped: " + e.Message);
        }
    }

    static bool HasApple(AdaptivePerformanceManagerSettings manager)
    {
        if (manager.loaders == null) return false;
        foreach (var loader in manager.loaders)
        {
            if (loader != null && loader.GetType().FullName == AppleLoader) return true;
        }
        return false;
    }
}
