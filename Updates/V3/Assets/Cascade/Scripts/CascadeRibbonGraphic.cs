using UnityEngine;
using UnityEngine.UI;
namespace Cascade.Prototype
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CascadeRibbonGraphic : MaskableGraphic
    {
        public Vector2[] points = new Vector2[0];
        public float thickness=2;
        public bool softEnds;
        public Color endColor=Color.white;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(points.Length<2)return;
            for(int i=0;i<points.Length;i++)
            {Vector2 d=points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(0,i-1)];float t=(float)i/(points.Length-1);float taper=softEnds?Mathf.Sin(t*Mathf.PI):1;var n=new Vector2(-d.y,d.x).normalized*thickness*.5f*taper;var c=Color.Lerp(color,endColor,t);c.a*=taper;vh.AddVert(points[i]+n,c,Vector2.zero);vh.AddVert(points[i]-n,c,Vector2.one);if(i>0){int k=i*2;vh.AddTriangle(k-2,k-1,k);vh.AddTriangle(k-1,k+1,k);}}
        }
    }
}
