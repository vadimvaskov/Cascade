using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Command line entrypoint: -batchmode -executeMethod CascadeAllTrackBatchChecks.Run
[InitializeOnLoad]
public static class CascadeAllTrackBatchChecks
{
    const string Pending="Cascade.AllTrackBatch.Pending";
    static double started;
    static CascadeAllTrackBatchChecks()
    {
        if(SessionState.GetBool(Pending,false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}
    }
    public static void Run()
    {
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(CascadeV3Checks.Run());Debug.Log(CascadeBonusChecks.Run());
            EditorSceneManager.OpenScene("Assets/Scenes/CascadePrototype.unity");
            SessionState.SetBool(Pending,true);started=EditorApplication.timeSinceStartup;
            EditorApplication.update-=Poll;EditorApplication.update+=Poll;
            EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Finish(e.ToString(),1);}
    }
    static void Poll()
    {
        if(EditorApplication.timeSinceStartup-started>180){Finish("FAIL: Play Mode startup timed out",1);return;}
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||UnityEngine.Object.FindAnyObjectByType<Cascade.Prototype.CascadeGame>()==null)return;
        EditorApplication.update-=Poll;
        try{Finish(CascadeShortTrackChecks.RunRuntime(),0);}catch(Exception e){Finish(e.ToString(),1);}
    }
    static void Finish(string text,int code)
    {
        SessionState.SetBool(Pending,false);
        File.WriteAllText("Documentation/V3/AllTrackRhythm/unity-runtime.txt",text+"\n");
        Debug.Log(text);EditorApplication.Exit(code);
    }
}
