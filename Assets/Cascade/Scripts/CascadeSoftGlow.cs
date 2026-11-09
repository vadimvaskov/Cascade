using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Prototype
{
    // Soft UI light for live lettering; never applied to the supplied raster artwork.
    public sealed class CascadeSoftGlow : BaseMeshEffect
    {
        public Color tint = new Color(1,.7f,.45f,.12f);
        public float radius=7;
        public override void ModifyMesh(VertexHelper mesh)
        {
            if(!IsActive()||mesh.currentVertCount==0)return;
            var original=new List<UIVertex>();mesh.GetUIVertexStream(original);
            var result=new List<UIVertex>(original.Count*25);
            for(int ring=3;ring>=1;ring--)
                for(int step=0;step<8;step++)
                {
                    float angle=step*Mathf.PI/4;
                    Vector3 offset=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle))*radius*ring/3;
                    foreach(var vertex in original)
                    {
                        var v=vertex;v.position+=offset;
                        var color=tint;color.a*=vertex.color.a/255f*(4-ring)/3f;
                        v.color=color;result.Add(v);
                    }
                }
            result.AddRange(original);mesh.Clear();mesh.AddUIVertexTriangleStream(result);
        }
    }
}
