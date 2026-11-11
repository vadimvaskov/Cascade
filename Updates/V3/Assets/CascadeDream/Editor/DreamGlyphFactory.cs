using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cascade.Dream
{
    // Front faces point toward negative Z; the caller owns saving the meshes.
    public static class DreamGlyphFactory
    {
        sealed class Builder
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<int> triangles = new List<int>();
            public readonly List<Color> colors = new List<Color>();
            public int Vertex(Vector3 p, Color c) { vertices.Add(p); colors.Add(c); return vertices.Count - 1; }
            public void Triangle(int a,int b,int c) { triangles.Add(a);triangles.Add(b);triangles.Add(c); }
            public Mesh Finish(string name)
            {
                var mesh=new Mesh {name=name};
                if(vertices.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetColors(colors);
                var uv=new List<Vector2>(vertices.Count);foreach(var v in vertices)uv.Add(new Vector2(v.x,v.y));mesh.SetUVs(0,uv);
                mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }
        }
        static Vector2[] Outline(float width,float height,float radius,int segments)
        {
            radius=Mathf.Clamp(radius,.001f,Mathf.Min(width,height)*.499f);
            segments=Mathf.Max(2,segments);var points=new List<Vector2>(4*(segments+1));
            for(int corner=0;corner<4;corner++)
            {
                float cx=(corner==0||corner==3?1:-1)*(width*.5f-radius);
                float cy=(corner<2?1:-1)*(height*.5f-radius);
                for(int j=0;j<=segments;j++)
                {
                    float a=(corner*90+j*90f/segments)*Mathf.Deg2Rad;
                    points.Add(new Vector2(cx+radius*Mathf.Cos(a),cy+radius*Mathf.Sin(a)));
                }
            }
            return points.ToArray();
        }
        static int Ring(Builder b,Vector2[] points,float z,float brightness)
        {
            int index=b.vertices.Count;var color=new Color(brightness,brightness,brightness,1);
            foreach(var p in points)b.Vertex(new Vector3(p.x,p.y,z),color);return index;
        }
        static void Connect(Builder b,int a,int c,int count,bool inward)
        {
            for(int i=0;i<count;i++)
            {
                int j=(i+1)%count;
                if(inward) {b.Triangle(a+i,c+j,a+j);b.Triangle(a+i,c+i,c+j);}
                else {b.Triangle(a+i,a+j,c+j);b.Triangle(a+i,c+j,c+i);}
            }
        }
        static void Cap(Builder b,int ring,int count,float z,bool front,float brightness=1)
        {
            int centre=b.Vertex(new Vector3(0,0,z),new Color(brightness,brightness,brightness,1));
            for(int i=0;i<count;i++)
            {int j=(i+1)%count;if(front)b.Triangle(centre,ring+j,ring+i);else b.Triangle(centre,ring+i,ring+j);}
        }
        static Vector2 EdgeNormal(Vector2 p,float width,float height,float radius)
        {
            // Analytic normal of the rounded-rectangle outline. This avoids
            // cap-fan triangle areas biasing the shading at straight/arc joins.
            Vector2 centre=new Vector2(Mathf.Clamp(p.x,-width*.5f+radius,width*.5f-radius),Mathf.Clamp(p.y,-height*.5f+radius,height*.5f-radius));
            return (p-centre).normalized;
        }
        static void RingNormal(Vector3[] normals,int ring,int count,Vector2[] outline,float width,float height,float radius,float radial,float forward)
        {
            for(int i=0;i<count;i++){Vector2 r=EdgeNormal(outline[i],width,height,radius);normals[ring+i]=new Vector3(r.x*radial,r.y*radial,forward).normalized;}
        }
        public static Mesh RoundedBox(float width,float height,float depth,float radius,float bevel=.04f,int segments=8)
        {
            if(width<=0||height<=0||depth<=0)throw new ArgumentOutOfRangeException("Mesh dimensions must be positive");
            bevel=Mathf.Clamp(bevel,0,Mathf.Min(depth*.45f,Mathf.Min(width,height)*.15f));
            var b=new Builder();var edge=Outline(width,height,radius,segments);
            var inset=Outline(width-2*bevel,height-2*bevel,Mathf.Max(.001f,radius-bevel),segments);
            int front=Ring(b,inset,-depth*.5f,1.1f),frontBevel=Ring(b,edge,-depth*.5f+bevel,1);
            int backBevel=Ring(b,edge,depth*.5f-bevel,.65f),back=Ring(b,inset,depth*.5f,.45f);
            int n=edge.Length;Connect(b,front,frontBevel,n,false);Connect(b,frontBevel,backBevel,n,false);Connect(b,backBevel,back,n,false);
            Cap(b,front,n,-depth*.5f,true,1.1f);Cap(b,back,n,depth*.5f,false,.45f);
            var mesh=b.Finish("Rounded enamel slab");var normals=mesh.normals;
            RingNormal(normals,front,n,edge,width,height,radius,0,-1);
            RingNormal(normals,frontBevel,n,edge,width,height,radius,1,0);
            RingNormal(normals,backBevel,n,edge,width,height,radius,1,0);
            RingNormal(normals,back,n,edge,width,height,radius,0,1);
            mesh.normals=normals;return mesh;
        }
        // Front lip at z=0; cavity floor at positive depth.
        public static Mesh Recess(float width,float height,float radius,float depth,float lip=.055f,int segments=8)
        {
            if(width<=0||height<=0||depth<=0)throw new ArgumentOutOfRangeException("Mesh dimensions must be positive");
            lip=Mathf.Clamp(lip,.008f,Mathf.Min(width,height)*.18f);
            var b=new Builder();var outer=Outline(width,height,radius,segments);int n=outer.Length;
            int top=Ring(b,outer,0,1.6f);
            int edge=Ring(b,Outline(width-lip*.65f,height-lip*.65f,Mathf.Max(.005f,radius-lip*.325f),segments),.008f,1.05f);
            int wall=Ring(b,Outline(width-lip*1.6f,height-lip*1.6f,Mathf.Max(.005f,radius-lip*.8f),segments),depth*.6f,.6f);
            int floor=Ring(b,Outline(width-lip*2,height-lip*2,Mathf.Max(.005f,radius-lip),segments),depth,.42f);
            Connect(b,top,edge,n,true);Connect(b,edge,wall,n,true);Connect(b,wall,floor,n,true);Cap(b,floor,n,depth,true,.42f);
            var mesh=b.Finish("Recessed rounded cell");var normals=mesh.normals;
            RingNormal(normals,top,n,outer,width,height,radius,0,-1);
            RingNormal(normals,edge,n,outer,width,height,radius,-.7f,-.72f);
            RingNormal(normals,wall,n,outer,width,height,radius,-.96f,-.28f);
            RingNormal(normals,floor,n,outer,width,height,radius,0,-1);
            mesh.normals=normals;return mesh;
        }
        public static Mesh Diamond(float width=1,float height=1,float depth=.5f)
        {
            var b=new Builder();Vector3[] ring={new Vector3(-width*.5f,0,0),new Vector3(0,height*.5f,0),new Vector3(width*.5f,0,0),new Vector3(0,-height*.5f,0)};
            Vector3 front=new Vector3(0,0,-depth*.5f),back=new Vector3(0,0,depth*.5f);
            for(int i=0;i<4;i++)
            {
                int j=(i+1)%4,v=b.vertices.Count;float shade=i%2==0?1:.72f;Color c=new Color(shade,shade,shade,1);
                b.Vertex(front,c);b.Vertex(ring[i],c);b.Vertex(ring[j],c);b.Triangle(v,v+1,v+2);
                v=b.vertices.Count;b.Vertex(back,c);b.Vertex(ring[j],c);b.Vertex(ring[i],c);b.Triangle(v,v+1,v+2);
            }
            return b.Finish("Crystal diamond");
        }
        static float Cross(Vector2 a,Vector2 b,Vector2 c) => (b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        static bool Inside(Vector2 p,Vector2 a,Vector2 b,Vector2 c)
        {return Cross(a,b,p)>=-1e-7f&&Cross(b,c,p)>=-1e-7f&&Cross(c,a,p)>=-1e-7f;}
        static void Polygon(Builder b,Vector2[] points,float depth)
        {
            var p=new List<Vector2>(points);float area=0;
            for(int i=0;i<p.Count;i++){var q=p[(i+1)%p.Count];area+=p[i].x*q.y-q.x*p[i].y;}
            if(area<0)p.Reverse();int count=p.Count,first=b.vertices.Count;
            for(int k=0;k<2;k++)foreach(var v in p)b.Vertex(new Vector3(v.x,v.y,k==0?-depth*.5f:depth*.5f),Color.white);
            var ids=new List<int>();for(int i=0;i<count;i++)ids.Add(i);int guard=count*count;
            while(ids.Count>2&&guard-->0)
            {
                bool found=false;
                for(int i=0;i<ids.Count;i++)
                {
                    int prev=ids[(i+ids.Count-1)%ids.Count],cur=ids[i],next=ids[(i+1)%ids.Count];
                    if(Cross(p[prev],p[cur],p[next])<=1e-8f)continue;
                    bool occupied=false;
                    foreach(int j in ids)if(j!=prev&&j!=cur&&j!=next&&Inside(p[j],p[prev],p[cur],p[next])){occupied=true;break;}
                    if(occupied)continue;
                    b.Triangle(first+prev,first+next,first+cur);b.Triangle(first+count+prev,first+count+cur,first+count+next);
                    ids.RemoveAt(i);found=true;break;
                }
                if(!found)break;
            }
            for(int i=0;i<count;i++){int j=(i+1)%count;b.Triangle(first+i,first+j,first+count+j);b.Triangle(first+i,first+count+j,first+count+i);}
        }
        static void Ellipse(Builder b,Vector2 centre,Vector2 radius,float angle,float depth,int segments=24)
        {
            var p=new Vector2[segments];float c=Mathf.Cos(angle),s=Mathf.Sin(angle);
            for(int i=0;i<p.Length;i++){float a=i*Mathf.PI*2/p.Length;float x=Mathf.Cos(a)*radius.x,y=Mathf.Sin(a)*radius.y;p[i]=centre+new Vector2(x*c-y*s,x*s+y*c);}
            Polygon(b,p,depth);
        }
        static void Rectangle(Builder b,float x0,float y0,float x1,float y1,float depth)
        {Polygon(b,new[]{new Vector2(x0,y0),new Vector2(x1,y0),new Vector2(x1,y1),new Vector2(x0,y1)},depth);}
        // Filled extruded musical silhouette approximately 0.5 units tall.
        public static Mesh Note(bool pair=false,float depth=.035f)
        {
            var b=new Builder();
            if(!pair)
            {
                Ellipse(b,new Vector2(-.055f,-.16f),new Vector2(.115f,.074f),.25f,depth);
                Rectangle(b,.025f,-.15f,.062f,.285f,depth);
                Polygon(b,new[]{new Vector2(.04f,.29f),new Vector2(.09f,.245f),new Vector2(.17f,.225f),new Vector2(.21f,.175f),new Vector2(.21f,.105f),new Vector2(.17f,.055f),new Vector2(.175f,.13f),new Vector2(.15f,.16f),new Vector2(.09f,.17f),new Vector2(.04f,.205f)},depth);
            }
            else
            {
                Ellipse(b,new Vector2(-.16f,-.16f),new Vector2(.105f,.068f),.25f,depth);
                Ellipse(b,new Vector2(.12f,-.11f),new Vector2(.105f,.068f),.25f,depth);
                Rectangle(b,-.085f,-.15f,-.05f,.26f,depth);Rectangle(b,.195f,-.10f,.23f,.31f,depth);
                Polygon(b,new[]{new Vector2(-.085f,.26f),new Vector2(.23f,.31f),new Vector2(.23f,.24f),new Vector2(-.085f,.19f)},depth);
            }
            return b.Finish(pair?"Paired musical note":"Single musical note");
        }
        static Vector2 Bezier(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float t)
        {float u=1-t;return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;}
        static void Curve(List<Vector2> path,Vector2 a,Vector2 b,Vector2 c,Vector2 d,int samples=12)
        {for(int i=path.Count==0?0:1;i<=samples;i++)path.Add(Bezier(a,b,c,d,i/(float)samples));}
        static void Stroke(Builder b,List<Vector2> path,float width,float depth)
        {
            // Overlapping tiny prisms avoid self-intersecting loop polygons.
            for(int i=0;i<path.Count-1;i++)
            {
                Vector2 a=path[i],c=path[i+1],dir=(c-a).normalized;Vector2 n=new Vector2(-dir.y,dir.x)*width*.5f;
                Polygon(b,new[]{a+n,c+n,c-n,a-n},depth);
            }
            for(int i=0;i<path.Count;i+=2)Ellipse(b,path[i],Vector2.one*width*.5f,0,depth,8);
        }
        public static Mesh Treble(float depth=.025f)
        {
            var b=new Builder();var path=new List<Vector2>();
            Curve(path,new Vector2(.055f,-.30f),new Vector2(.075f,-.35f),new Vector2(-.055f,-.38f),new Vector2(-.072f,-.29f));
            Curve(path,new Vector2(-.072f,-.29f),new Vector2(-.075f,-.25f),new Vector2(-.005f,-.24f),new Vector2(.025f,-.28f));Stroke(b,path,.033f,depth);
            path.Clear();
            Curve(path,new Vector2(.055f,-.30f),new Vector2(.035f,-.10f),new Vector2(-.065f,.16f),new Vector2(-.04f,.32f));
            Curve(path,new Vector2(-.04f,.32f),new Vector2(-.01f,.48f),new Vector2(.12f,.43f),new Vector2(.075f,.27f));
            Curve(path,new Vector2(.075f,.27f),new Vector2(.045f,.15f),new Vector2(-.20f,.035f),new Vector2(-.145f,-.10f));
            Curve(path,new Vector2(-.145f,-.10f),new Vector2(-.085f,-.25f),new Vector2(.16f,-.17f),new Vector2(.135f,-.035f));
            Curve(path,new Vector2(.135f,-.035f),new Vector2(.11f,.07f),new Vector2(-.085f,.055f),new Vector2(-.075f,-.04f));
            Curve(path,new Vector2(-.075f,-.04f),new Vector2(-.067f,-.09f),new Vector2(-.01f,-.10f),new Vector2(.027f,-.055f));Stroke(b,path,.033f,depth);
            return b.Finish("Golden treble clef");
        }
    }
}
