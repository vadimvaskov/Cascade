using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CascadeEditorPlay
{
    const string ScenePath = "Assets/Scenes/CascadeGame.unity";

    static CascadeEditorPlay()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-cascadePlay") < 0) return;
        EditorApplication.update += EnterPlay;
    }

    static void EnterPlay()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= EnterPlay;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);
        Debug.Log("Cascade editor play: " + ScenePath);
        EditorApplication.EnterPlaymode();
    }
}
