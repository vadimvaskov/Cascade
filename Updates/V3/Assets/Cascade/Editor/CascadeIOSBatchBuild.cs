using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CascadeIOSBatchBuild
{
    const string Pending="Cascade.IOSBatchBuild.Pending";
    static CascadeIOSBatchBuild(){if(SessionState.GetBool(Pending,false))EditorApplication.delayCall+=Export;}
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-cascadeBuildNumber");
        if(i<0||i+1>=args.Length)throw new ArgumentException("Pass -cascadeBuildNumber NUMBER");
        SessionState.SetBool(Pending,true);
        CascadeIOSBuild.Configure("1.0",args[i+1]);
        EditorApplication.delayCall+=Export;
    }
    static void Export()
    {
        if(!SessionState.GetBool(Pending,false))return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Export;return;}
        SessionState.SetBool(Pending,false);
        try
        {
            var json=CascadeIOSBuild.Build("Builds/iOS/Xcode");
            File.WriteAllText("Builds/iOS/build-status-1.0-"+PlayerSettings.iOS.buildNumber+".json",json);
            EditorApplication.Exit(JsonUtility.FromJson<CascadeIOSBuild.BuildStatus>(json).succeeded?0:1);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
