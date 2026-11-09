using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Prototype
{
    // Reusable contour mesh. Runtime strength uses CanvasRenderer alpha, with no mesh rebuild per beat.
    public sealed class CascadeGlowGraphic : MaskableGraphic
    {
        public bool screenAura,circle;
        public float radius=23,softness=7;
        public Color rightColor=Color.white;
        readonly float[] rings={-1,-.55f,-.18f,0,.18f,.55f,1};
        readonly float[] weights={0,.10f,.48f,.8f,.48f,.10f,0};
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Rect r=rectTransform.rect;
            if(screenAura){Aura(vh,r);return;}
            const int count=80;
            for(int j=0;j<rings.Length;j++)
            {
                float spread=rings[j]*softness;
                Rect box=new Rect(r.x-spread,r.y-spread,r.width+2*spread,r.height+2*spread);
                float rad=Mathf.Min(radius+spread,Mathf.Min(box.width,box.height)*.5f);
                for(int i=0;i<count;i++)
                {
                    Vector2 point;
                    if(circle){float a=i*Mathf.PI*2/count;point=box.center+new Vector2(Mathf.Cos(a)*box.width*.5f,Mathf.Sin(a)*box.height*.5f);}
                    else
                    {
                        int c=i/20;float a=(c*90+(i%20)*90f/19)*Mathf.Deg2Rad;
                        float x=c==0||c==3?box.xMax-rad:box.xMin+rad,y=c<2?box.yMax-rad:box.yMin+rad;
                        point=new Vector2(x+rad*Mathf.Cos(a),y+rad*Mathf.Sin(a));
                    }
                    Color tint=Color.Lerp(color,rightColor,Mathf.InverseLerp(r.xMin,r.xMax,point.x));tint.a*=weights[j];vh.AddVert(point,tint,Vector2.zero);
                }
            }
            for(int j=0;j<rings.Length-1;j++)for(int i=0;i<count;i++)
            {int a=j*count+i,b=j*count+(i+1)%count;vh.AddTriangle(a,b,a+count);vh.AddTriangle(b,b+count,a+count);}
        }
        void Aura(VertexHelper vh,Rect r)
        {
            float width=Mathf.Min(r.width,r.height)*.06f;
            float[] x={r.xMin,r.xMin+width*.15f,r.xMin+width*.4f,r.xMin+width*.7f,r.xMin+width,r.xMin+width*2,r.xMin+width*3,r.center.x,r.xMax-width*3,r.xMax-width*2,r.xMax-width,r.xMax-width*.7f,r.xMax-width*.4f,r.xMax-width*.15f,r.xMax};
            float[] y={r.yMin,r.yMin+width*.15f,r.yMin+width*.4f,r.yMin+width*.7f,r.yMin+width,r.yMin+width*2,r.yMin+width*3,r.center.y,r.yMax-width*3,r.yMax-width*2,r.yMax-width,r.yMax-width*.7f,r.yMax-width*.4f,r.yMax-width*.15f,r.yMax};
            Color left=new Color(74/255f,80/255f,1),right=new Color(194/255f,59/255f,1),gold=new Color(1,180/255f,77/255f);
            for(int j=0;j<y.Length;j++)for(int i=0;i<x.Length;i++)
            {
                float across=Mathf.InverseLerp(r.xMin,r.xMax,x[i]);
                float vertical=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Min(x[i]-r.xMin,r.xMax-x[i])/width));
                float horizontal=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Min(y[j]-r.yMin,r.yMax-y[j])/width));
                // The supplied aura is concentrated at the sides and corners, with a soft top/bottom centre.
                horizontal*=.15f+.85f*Mathf.Pow(Mathf.Abs(across-.5f)*2,2);
                float a=Mathf.Max(vertical,horizontal);
                Color tint=Color.Lerp(left,right,across);
                float corner=(1-Mathf.Clamp01((y[j]-r.yMin)/(width*3)))*Mathf.Clamp01(Mathf.Abs(across-.5f)*4-1);
                tint=Color.Lerp(tint,gold,corner);tint.a=a;vh.AddVert(new Vector2(x[i],y[j]),tint,Vector2.zero);
            }
            for(int j=0;j<y.Length-1;j++)for(int i=0;i<x.Length-1;i++)
            {int a=j*x.Length+i;vh.AddTriangle(a,a+1,a+x.Length);vh.AddTriangle(a+1,a+x.Length+1,a+x.Length);}
        }
    }
}
