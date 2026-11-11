using System;
using Cascade.Prototype;
using UnityEngine;

public static class CascadeCompanionEnergyChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception("Companion energy: "+message);}
    public static string Run()
    {
        float min=10,max=-10;Vector3 previous=CascadeCompanionFlight.Offset(0,false);
        for(int i=1;i<=18000;i++)
        {
            var p=CascadeCompanionFlight.Offset(i*.01,false);
            Check(p.z>=0&&p.z<=2.201f&&Math.Abs(p.x)<.401f&&Math.Abs(p.y)<.24f,"flight stays within its forward lane");
            Check(Vector3.Distance(p,previous)<.04f,"motion stays continuous across cycle and long-track boundaries");
            min=Mathf.Min(min,p.z);max=Mathf.Max(max,p.z);previous=p;
        }
        Check(max-min>2.19f,"companion visibly escapes and returns");
        Check(CascadeCompanionFlight.Offset(1.5,true).magnitude<CascadeCompanionFlight.Offset(1.5,false).magnitude,"soft effects retains gentler movement");
        var root=new GameObject("Isolated music energy check");
        try
        {
            var energy=root.AddComponent<CascadeMusicEnergy3D>();var source=new Vector3(0,1.6f,5);var target=new Vector3(-1,1.3f,0);
            energy.Present(0,source,target,false);
            energy.Emit(source,3,true,0);float duration=energy.LastDuration;
            energy.Present(duration*.5,source,target,false);
            Check(energy.ActiveCount==1&&energy.Absorption==0&&energy.LastCharge==3&&energy.LastOrigin==source,"companion energy travels before absorption and retains its charge");
            Check(energy.StreamMesh.bounds.size.z>.8f&&energy.StreamMesh.vertexCount>500&&energy.StreamRenderer.enabled,"stream occupies real 3D depth with volumetric geometry");
            Check(energy.StreamRenderer.sharedMaterial.shader.isSupported,"volume shader is supported");
            var movedSource=source+Vector3.right*.2f;var movedTarget=target+Vector3.up*.1f;
            energy.Present(duration+.15,movedSource,movedTarget,false);
            Check(energy.Absorption>.99f&&energy.Target==movedTarget&&energy.Source==movedSource,"energy follows live companion and hero anchors");
            energy.Present(duration+.31,source,target,false);Check(energy.ActiveCount==0&&energy.Absorption==0&&!energy.StreamRenderer.enabled,"energy expires without lingering light or geometry");
            for(int i=0;i<100;i++)energy.Emit(source,1,false,2);
            energy.Present(2.1,source,target,false);Check(energy.ActiveCount==16&&energy.StreamMesh.vertexCount<40000,"rapid inputs use a bounded stream pool and mesh");
            energy.Clear();Check(energy.ActiveCount==0&&energy.Absorption==0,"restart clears all streams");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
        return "PASS: continuous companion flight over 180s, lane bounds, soft motion, 3D companion-to-hero volume, live anchors, shader, expiry and bounded stream reuse";
    }
}
