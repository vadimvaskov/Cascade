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
        try
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-cascadeBuildNumber");
            if(i<0||i+1>=args.Length)throw new ArgumentException("Pass -cascadeBuildNumber NUMBER");
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.iOS)throw new InvalidOperationException("Start Unity with -buildTarget iOS and install iOS Build Support.");
            SessionState.SetBool(Pending,true);
            CascadeIOSBuild.Configure("1.0",args[i+1]);
            EditorApplication.delayCall+=Export;
        }
        catch(Exception e){SessionState.SetBool(Pending,false);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Export()
    {
        if(!SessionState.GetBool(Pending,false))return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Export;return;}
        SessionState.SetBool(Pending,false);
        try
        {
            CascadeReleaseChecks.Run();
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-cascadeOutputPath");
            string output=i>=0&&i+1<args.Length?args[i+1]:"Builds/iOS/Xcode";
            var json=CascadeIOSBuild.Build(output);
            File.WriteAllText("Builds/iOS/build-status-1.0-"+PlayerSettings.iOS.buildNumber+".json",json);
            EditorApplication.Exit(JsonUtility.FromJson<CascadeIOSBuild.BuildStatus>(json).succeeded?0:1);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
