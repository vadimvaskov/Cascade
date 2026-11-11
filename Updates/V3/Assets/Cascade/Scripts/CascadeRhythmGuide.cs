using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Prototype
{
    // Light beads approach a fixed ring. Their sizes distinguish the subdivisions.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CascadeRhythmGuide : MaskableGraphic
    {
        RhythmGrid grid;double clock,window;bool active;
        public double Clock=>clock;
        public bool Active=>active;
        public void Present(RhythmGrid value,double time,double hitWindow,bool running)
        {grid=value;clock=time;window=hitWindow;active=running;SetVerticesDirty();}
        static Color Alpha(Color tint,float opacity){tint.a*=opacity;return tint;}
        void Vertex(VertexHelper vh,float x,float y,Color tint)
        {
            var r=rectTransform.rect;
            vh.AddVert(new Vector3(r.xMin+x,r.yMin+y,0),tint,Vector2.zero);
        }
        void Disc(VertexHelper vh,float x,float y,float radius,Color centre,Color edge)
        {
            const int sides=32;int start=vh.currentVertCount;Vertex(vh,x,y,centre);
            for(int i=0;i<=sides;i++)
            {
                float angle=i*Mathf.PI*2/sides;Vertex(vh,x+Mathf.Cos(angle)*radius,y+Mathf.Sin(angle)*radius,edge);
                if(i>0)vh.AddTriangle(start,start+i,start+i+1);
            }
        }
        void Ring(VertexHelper vh,float x,float y,float radius,float width,Color tint)
        {
            const int sides=48;
            // Transparent edge bands keep small circles smooth at phone resolution.
            float[] radii={radius+width*.5f+.65f,radius+width*.5f,radius-width*.5f,radius-width*.5f-.65f};
            int start=vh.currentVertCount;
            for(int i=0;i<=sides;i++)
            {
                float angle=i*Mathf.PI*2/sides;
                for(int band=0;band<4;band++)Vertex(vh,x+Mathf.Cos(angle)*radii[band],y+Mathf.Sin(angle)*radii[band],Alpha(tint,band==0||band==3?0:1));
                if(i==0)continue;
                for(int band=0;band<3;band++)
                {
                    int a=start+(i-1)*4+band,b=a+4;vh.AddTriangle(a,b,a+1);vh.AddTriangle(a+1,b,b+1);
                }
            }
        }
        void Rail(VertexHelper vh,float width,float centreY,float halfHeight,Color tint)
        {
            const int steps=32;int start=vh.currentVertCount;
            for(int i=0;i<=steps;i++)
            {
                float t=i/(float)steps;
                float opacity=Mathf.SmoothStep(0,1,t/.12f)*Mathf.SmoothStep(0,1,(1-t)/.14f);
                Vertex(vh,width*t,centreY-halfHeight,Alpha(tint,0));
                Vertex(vh,width*t,centreY,Alpha(tint,opacity));
                Vertex(vh,width*t,centreY+halfHeight,Alpha(tint,0));
                if(i==0)continue;
                for(int row=0;row<2;row++)
                {
                    int a=start+(i-1)*3+row,b=a+3;vh.AddTriangle(a,b,a+1);vh.AddTriangle(a+1,b,b+1);
                }
            }
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float w=rectTransform.rect.width,h=rectTransform.rect.height,target=w*.22f,y=h*.5f;
            var gold=new Color(1,.82f,.51f);var ivory=new Color(1,.96f,.85f);
            Rail(vh,w,y,7,new Color(.68f,.46f,.94f,.16f));
            Rail(vh,w,y,1.25f,new Color(.84f,.69f,1,.48f));
            float proximity=0;int nearest=0;double span=1;
            if(grid!=null&&active)
            {
                nearest=grid.Nearest(clock);double spacing=grid.Spacing(nearest);span=spacing*8;
                if(!grid.Map.IsExcluded(clock)&&clock>=grid.Map.HitWindowStart(nearest,window)&&clock<=grid.Map.HitWindowEnd(nearest,window))
                    proximity=1-Mathf.SmoothStep(0,1,(float)(Math.Abs(grid.Map.beats[nearest]-clock)/Math.Max(.001,spacing*.5)));
            }
            Disc(vh,target,y,13,Alpha(gold,.16f+proximity*.12f),Alpha(gold,0));
            Disc(vh,target,y,8.5f,new Color(.19f,.10f,.31f,.8f),new Color(.22f,.12f,.35f,.6f));
            if(grid!=null&&active)
                for(int i=Math.Max(0,nearest-3);i<Math.Min(grid.Map.beats.Length,nearest+10);i++)
                {
                    if(grid.Map.IsExcluded(grid.Map.beats[i]))continue;
                    float x=target+(float)((grid.Map.beats[i]-clock)/span)*(w-target-10);
                    if(x<2||x>w-3)continue;
                    int rank=i%4==0?0:i%2==0?1:2;
                    float radius=rank==0?4.6f:rank==1?3.2f:2.1f;
                    float opacity=Mathf.SmoothStep(0,1,x/18)*Mathf.SmoothStep(0,1,(w-x)/20);
                    if(x<target)opacity*=Mathf.Lerp(.12f,1,Mathf.Clamp01(1-(target-x)/32));
                    var tint=Alpha(rank==0?gold:rank==1?new Color(.66f,.91f,1):new Color(.84f,.77f,1),opacity);
                    Disc(vh,x,y,radius*2.4f,Alpha(tint,.36f),Alpha(tint,0));
                    Disc(vh,x,y,radius,tint,Alpha(tint,.75f));
                    Ring(vh,x,y,radius-.2f,.45f,Alpha(ivory,opacity*.7f));
                    Disc(vh,x-.6f,y+.8f,radius*.38f,Alpha(ivory,opacity*.9f),Alpha(ivory,0));
                    if(rank==0)Ring(vh,x,y,6.3f,.55f,Alpha(gold,opacity*.5f));
                }
            Ring(vh,target,y,9.6f,1.35f,Alpha(gold,.85f+proximity*.15f));
            Disc(vh,target,y+9.6f,1.8f,ivory,Alpha(gold,0));
        }
    }
}
