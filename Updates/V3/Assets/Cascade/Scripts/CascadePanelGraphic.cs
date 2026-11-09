using UnityEngine;
using UnityEngine.UI;
namespace Cascade.Prototype
{
    // A vector UI surface: rounded plates, thin borders and soft halos at any resolution.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CascadePanelGraphic : MaskableGraphic
    {
        public enum Shape { Panel, Border, Halo }
        public Shape shape;
        public Color rightColor = Color.white;
        public float radius = 12, border = 1.5f;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=rectTransform.rect;
            if(shape==Shape.Halo)
            {
                int count=64;vh.AddVert(Vector3.zero,color,Vector2.one*.5f);
                for(int i=0;i<=count;i++){float a=i*Mathf.PI*2/count;vh.AddVert(new Vector3(Mathf.Cos(a)*r.width*.5f,Mathf.Sin(a)*r.height*.5f,0),new Color(color.r,color.g,color.b,0),Vector2.zero);if(i>0)vh.AddTriangle(0,i,i+1);}return;
            }
            int n=40;var outer=Contour(r,Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f));
            if(shape==Shape.Border)
            {
                Rect inner=new Rect(r.x+border,r.y+border,Mathf.Max(0,r.width-2*border),Mathf.Max(0,r.height-2*border));var inside=Contour(inner,Mathf.Max(0,radius-border));
                for(int i=0;i<n;i++){vh.AddVert(outer[i],Tint(outer[i].x,r),Vector2.zero);vh.AddVert(inside[i],Tint(inside[i].x,r),Vector2.zero);}
                for(int i=0;i<n;i++){int j=(i+1)%n;vh.AddTriangle(2*i,2*j,2*i+1);vh.AddTriangle(2*j,2*j+1,2*i+1);}return;
            }
            vh.AddVert(r.center,Tint(r.center.x,r),Vector2.zero);
            for(int i=0;i<n;i++)vh.AddVert(outer[i],Tint(outer[i].x,r),Vector2.zero);
            for(int i=0;i<n;i++)vh.AddTriangle(0,i+1,(i+1)%n+1);
        }
        Color Tint(float x,Rect r)=>Color.Lerp(color,rightColor,Mathf.InverseLerp(r.xMin,r.xMax,x));
        static Vector2[] Contour(Rect r,float rad)
        {
            var p=new Vector2[40];for(int c=0;c<4;c++)for(int j=0;j<10;j++)
            {float a=(c*90+j*90f/9)*Mathf.Deg2Rad;float x=c==0||c==3?r.xMax-rad:r.xMin+rad;float y=c<2?r.yMax-rad:r.yMin+rad;p[c*10+j]=new Vector2(x+rad*Mathf.Cos(a),y+rad*Mathf.Sin(a));}return p;
        }
    }
}
