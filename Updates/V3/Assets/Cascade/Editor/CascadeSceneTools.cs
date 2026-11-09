using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Cascade.Prototype;
public static class CascadeSceneTools
{
    public const string Path = "Assets/Scenes/CascadePrototype.unity";
    [MenuItem("Cascade/Open prototype")]
    public static void Open()
    {
        var scene=SceneManager.GetSceneByPath(Path);
        if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(Path,OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Path);
    }
    [MenuItem("Cascade/Rebuild prototype visuals")]
    public static void Build()
    {
        var scene=SceneManager.GetSceneByPath(Path);
        if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        foreach(var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
        var obj=new GameObject("Cascade Prototype");var game=obj.AddComponent<CascadeGame>();game.BuildVisuals();
        foreach(var tr in obj.GetComponentsInChildren<Transform>(true))tr.gameObject.layer=8;
        var cam=obj.GetComponentInChildren<Camera>();cam.cullingMask=1<<8;cam.depth=10;
        foreach(var light in obj.GetComponentsInChildren<Light>())light.cullingMask=1<<8;
        EditorSceneManager.SaveScene(scene,Path);AssetDatabase.SaveAssets();
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Path);
    }
}
