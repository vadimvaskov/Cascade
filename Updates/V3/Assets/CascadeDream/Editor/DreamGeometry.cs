using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cascade.Dream
{
    public static class DreamGeometry
    {
        public static Mesh Pebble(int rings=18,int slices=28,float organic=.06f)
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int y=0;y<=rings;y++)for(int x=0;x<=slices;x++)
            {
                float a=y*Mathf.PI/rings,b=x*Mathf.PI*2/slices;
                Vector3 p=new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b));
                float f=1+organic*(Mathf.Sin(p.x*3+p.y*2)*Mathf.Cos(p.z*3)+.4f*Mathf.Sin(p.y*5-p.z));
                v.Add(p*f);n.Add(p);uv.Add(new Vector2(x/(float)slices,y/(float)rings));
                if(y<rings&&x<slices){int i=y*(slices+1)+x;t.AddRange(new[]{i,i+1,i+slices+1,i+1,i+slices+2,i+slices+1});}
            }
            var m=new Mesh();m.SetVertices(v);m.SetNormals(n);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateBounds();return m;
        }
        public static Mesh Rounded(Vector3 size,float radius,int divisions=6)
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            var half=size*.5f;radius=Mathf.Min(radius,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.95f);
            Vector3 inner=half-Vector3.one*radius;
            Vector3[] normals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            foreach(var normal in normals)
            {
                Vector3 u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up;Vector3 w=Vector3.Cross(normal,u);
                int start=v.Count;
                for(int j=0;j<=divisions;j++)for(int i=0;i<=divisions;i++)
                {
                    Vector3 q=Vector3.Scale(normal,half)+u*EdgeCoordinate(i,divisions,Vector3.Dot(Vector3.Scale(u,u),half),radius)+w*EdgeCoordinate(j,divisions,Vector3.Dot(Vector3.Scale(w,w),half),radius);
                    Vector3 k=new Vector3(Mathf.Clamp(q.x,-inner.x,inner.x),Mathf.Clamp(q.y,-inner.y,inner.y),Mathf.Clamp(q.z,-inner.z,inner.z));
                    var d=(q-k).normalized;v.Add(k+d*radius);n.Add(d);uv.Add(new Vector2(i/(float)divisions,j/(float)divisions));
                    if(i<divisions&&j<divisions){int a=start+j*(divisions+1)+i;t.AddRange(new[]{a,a+1,a+divisions+1,a+1,a+divisions+2,a+divisions+1});}
                }
            }
            var m=new Mesh();m.SetVertices(v);m.SetNormals(n);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateBounds();return m;
        }
        static float EdgeCoordinate(int i,int divisions,float half,float r)
        {if(i==0)return -half;if(i==1)return -half+r*.293f;if(i==2)return -half+r;if(i==divisions)return half;if(i==divisions-1)return half-r*.293f;if(i==divisions-2)return half-r;return 0;}
        public static Mesh Canopy()
        {
            var m=Pebble(20,32,0);var v=m.vertices;
            for(int i=0;i<v.Length;i++){var p=v[i];float lobes=.075f*Mathf.Sin(p.x*6+p.z*2)*Mathf.Sin(p.z*7-p.y*3)+.027f*Mathf.Cos(p.y*10+p.x*8);v[i]=p*(1+lobes);}
            m.vertices=v;m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        public static Mesh IslandBody(float radius,bool cap)
        {
            const int rings=24,slices=64;var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int j=0;j<=rings;j++)for(int i=0;i<=slices;i++)
            {
                float f=j/(float)rings,a=i*Mathf.PI*2/slices;
                float wave=1+.055f*Mathf.Sin(a*3+.5f)+.04f*Mathf.Cos(a*5-1.2f)+.02f*Mathf.Sin(a*9+f*2);
                float r,y;
                if(cap){r=radius*f*wave;y=.08f+.11f*(1-f*f)+.025f*Mathf.Sin(a*4)*f;}
                else {r=radius*Mathf.Pow(Mathf.Max(.001f,Mathf.Cos(f*Mathf.PI*.5f)),.70f)*(1+.045f*Mathf.Sin(f*Mathf.PI*2))*wave;y=-f*radius*1.55f;}
                v.Add(new Vector3(Mathf.Cos(a)*r+(!cap?.28f*Mathf.Sin(f*2):0),y,Mathf.Sin(a)*r*.88f));uv.Add(new Vector2(i/(float)slices,f));
                if(j<rings&&i<slices){int k=j*(slices+1)+i;t.AddRange(new[]{k,k+1,k+slices+1,k+1,k+slices+2,k+slices+1});}
            }
            var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        public static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t){float s=1-t;return s*s*s*a+3*s*s*t*b+3*s*t*t*c+t*t*t*d;}
        public static Vector3[] Curve(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int steps=24){var p=new Vector3[steps+1];for(int i=0;i<=steps;i++)p[i]=Bezier(a,b,c,d,i/(float)steps);return p;}
        public static Mesh Tube(IList<Vector3> path,float r0,float r1,int sides=12)
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int i=0;i<path.Count;i++)
            {
                var tangent=(path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(0,i-1)]).normalized;
                var right=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.94f?Vector3.forward:Vector3.up).normalized;
                var up=Vector3.Cross(right,tangent).normalized;
                for(int j=0;j<=sides;j++)
                {
                    float a=j*Mathf.PI*2/sides;var normal=right*Mathf.Cos(a)+up*Mathf.Sin(a);
                    v.Add(path[i]+normal*Mathf.Lerp(r0,r1,i/(float)(path.Count-1)));n.Add(normal);uv.Add(new Vector2(j/(float)sides,i/(float)(path.Count-1)));
                    if(i>0&&j<sides){int k=(i-1)*(sides+1)+j;t.AddRange(new[]{k,k+sides+1,k+1,k+1,k+sides+1,k+sides+2});}
                }
            }
            var m=new Mesh();m.SetVertices(v);m.SetNormals(n);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateBounds();return m;
        }
        public static Mesh Ring(float radius,float tube,int segments=64,int sides=10)
        {var p=new Vector3[segments+1];for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;p[i]=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0);}return Tube(p,tube,tube,sides);}
        public static Mesh Dome(float radius,float height,int slices=40,int rings=14)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int y=0;y<=rings;y++)for(int x=0;x<=slices;x++)
            {float f=y/(float)rings,a=x*Mathf.PI*2/slices;float r=radius*Mathf.Cos(f*Mathf.PI*.5f)*(1+.12f*Mathf.Sin(f*Mathf.PI));v.Add(new Vector3(Mathf.Cos(a)*r,height*Mathf.Sin(f*Mathf.PI*.5f),Mathf.Sin(a)*r));uv.Add(new Vector2(x/(float)slices,f));if(y<rings&&x<slices){int i=y*(slices+1)+x;t.AddRange(new[]{i,i+slices+1,i+1,i+1,i+slices+1,i+slices+2});}}
            var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        public static Mesh Water(float width,float height)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();int rows=24,cols=12;
            for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++){float u=x/(float)cols,f=y/(float)rows;v.Add(new Vector3((u-.5f)*width*(1+.22f*f),-f*height,-.65f*f*f));uv.Add(new Vector2(u,1-f));if(x<cols&&y<rows){int i=y*(cols+1)+x;t.AddRange(new[]{i,i+1,i+cols+1,i+1,i+cols+2,i+cols+1});}}
            var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
        }
    }
}
