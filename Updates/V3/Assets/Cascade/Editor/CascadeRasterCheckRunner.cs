using System;
using System.IO;
using UnityEngine;

// Execute in the player's Update context: an Editor eval can expose the Editor window's
// Screen dimensions while Camera.pixelRect already describes the portrait Game View.
public static class CascadeRasterCheckRunner
{
    [Serializable] public class Report
    {
        public int width,height;public bool passed;
        public string raster,runtime,calibration,recovery,error;
    }
    [UnityEditor.MenuItem("Cascade/Verify raster layout and input (Play Mode)")]
    public static void Queue()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Start Play Mode before running raster checks.");
        new GameObject("Raster validation runner").AddComponent<CascadeEditorFrame>().action=Run;
    }
    static void Run()
    {
        var report=new Report{width=Screen.width,height=Screen.height};
        try
        {
            report.raster=CascadeRasterChecks.Run();
            report.runtime=CascadeRuntimeChecks.Run();
            report.calibration=CascadeRuntimeChecks.RunCalibrationIntegration();
            report.recovery=CascadeRecoveryChecks.RunRuntimeCadence();
            report.passed=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        Directory.CreateDirectory("Builds/Design");File.WriteAllText("Builds/Design/raster-runtime-checks.json",JsonUtility.ToJson(report,true));
    }
}
