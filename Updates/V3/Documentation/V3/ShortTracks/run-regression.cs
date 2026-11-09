using System;
using System.IO;
using System.Collections;
using UnityEngine;
using Cascade.Prototype;
IEnumerator Run()
{
    yield return null;
    try
    {
        var result="Screen "+Screen.width+"x"+Screen.height+"\n\n"+CascadeShortTrackChecks.RunRuntime()+"\n\n"+CascadeV3Checks.Run()+"\n\n"+CascadeBonusChecks.Run()+"\n\n"+CascadeV3RuntimeChecks.Run()+"\n\n"+CascadeV3RuntimeChecks.RunLifecycle()+"\n\n"+CascadeBonusChecks.RunRuntime();
        File.WriteAllText("Documentation/V3/ShortTracks/regression.txt",result);
    }
    catch(Exception e){File.WriteAllText("Documentation/V3/ShortTracks/regression.txt","FAIL: "+e);Debug.LogException(e);}
}
File.WriteAllText("Documentation/V3/ShortTracks/regression.txt","RUNNING");
UnityEngine.Object.FindAnyObjectByType<CascadeGame>().StartCoroutine(Run());
return "Regression scheduled for player frame";
