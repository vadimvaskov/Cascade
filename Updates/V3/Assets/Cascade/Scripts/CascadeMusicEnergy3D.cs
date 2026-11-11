using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cascade.Prototype
{
    // Real world-space tubes and particles, rendered by the scene camera with depth testing.
    // A fixed flight pool and reusable mesh keep rapid taps bounded; no particle GameObjects.
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class CascadeMusicEnergy3D : MonoBehaviour
    {
        const int Capacity=16,Segments=22,Sides=8;
        const float Arrival=.30f;
        struct Flight { public bool used,onBeat;public Color tint;public double born;public float duration,seed; }
        readonly Flight[] flights=new Flight[Capacity];
        readonly List<Vector3> vertices=new List<Vector3>(40000),normals=new List<Vector3>(40000);
        readonly List<Color> colours=new List<Color>(40000);
        readonly List<int> indices=new List<int>(220000);
        Mesh mesh;Material material;MeshRenderer streamRenderer;int cursor;
        public double Clock { get; private set; }
        public Vector3 Source { get; private set; }
        public Vector3 Target { get; private set; }
        public float Absorption { get; private set; }
        public Color AbsorptionColor { get; private set; }
        public int EmissionCount { get; private set; }
        public int ActiveCount { get; private set; }
        public int LastCharge { get; private set; }
        public float LastDuration { get; private set; }
        public Vector3 LastOrigin { get; private set; }
        public Mesh StreamMesh=>mesh;
        public MeshRenderer StreamRenderer=>streamRenderer;

        void Awake()
        {
            Initialize();
        }
        void Initialize()
        {
            if(mesh)return;
            mesh=new Mesh{name="Pooled 3D musical current"};mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh=mesh;
            var shader=Resources.Load<Shader>("Approved/MusicEnergyVolume");
            if(!shader)throw new System.InvalidOperationException("Missing musical energy volume shader");
            material=new Material(shader){name="Soft luminous musical current"};
            streamRenderer=GetComponent<MeshRenderer>();streamRenderer.sharedMaterial=material;
            streamRenderer.shadowCastingMode=ShadowCastingMode.Off;streamRenderer.receiveShadows=false;
            streamRenderer.lightProbeUsage=LightProbeUsage.Off;streamRenderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            streamRenderer.enabled=false;
        }
        void OnDestroy()
        {
            if(Application.isPlaying){Destroy(mesh);Destroy(material);}
            else{DestroyImmediate(mesh);DestroyImmediate(material);}
        }
        public void Emit(Vector3 origin,int charge,bool onBeat,double at)
        {
            LastDuration=Mathf.Clamp(Vector3.Distance(origin,Target)/7.5f,.65f,.9f);
            flights[cursor]=new Flight{used=true,onBeat=onBeat,tint=CascadeGame.Palette(charge),born=at,duration=LastDuration,seed=EmissionCount*2.39996f};
            cursor=(cursor+1)%Capacity;EmissionCount++;LastCharge=charge;LastOrigin=origin;
        }
        public void Clear()
        {
            for(int i=0;i<Capacity;i++)flights[i].used=false;
            ActiveCount=0;Absorption=0;
            if(mesh)mesh.Clear();if(streamRenderer)streamRenderer.enabled=false;
        }
        public void Present(double time,Vector3 source,Vector3 target,bool reduced)
        {
            Initialize();
            Clock=time;Source=source;Target=target;ActiveCount=0;Absorption=0;
            vertices.Clear();normals.Clear();colours.Clear();indices.Clear();
            var start=transform.InverseTransformPoint(source);var end=transform.InverseTransformPoint(target);
            for(int i=0;i<Capacity;i++)
            {
                if(!flights[i].used)continue;var f=flights[i];float age=(float)(time-f.born);
                if(age>f.duration+Arrival){flights[i].used=false;continue;}
                if(age<0)continue;ActiveCount++;
                float strength=(f.onBeat?1:.72f)*(reduced?.55f:1);
                // The tail continues flowing in after the head arrives; no abrupt beam cut.
                {
                    float u=Mathf.Clamp01(age/f.duration),head=.28f*u+.72f*u*u;
                    float v=Mathf.Clamp01((age-Arrival)/f.duration),tail=.28f*v+.72f*v*v;
                    // A translucent envelope encloses a luminous core and three winding filaments.
                    Tube(start,end,tail,head,age,f.seed,-1,.32f,WithAlpha(f.tint,.22f*strength));
                    Tube(start,end,tail,head,age,f.seed,-1,.09f,WithAlpha(Color.Lerp(f.tint,Color.white,.2f),.52f*strength));
                    for(int strand=0;strand<(reduced?2:3);strand++)
                        Tube(start,end,tail,head,age,f.seed,strand,.032f,WithAlpha(Color.Lerp(f.tint,Color.white,.4f),.65f*strength));
                    for(int mote=0;mote<(reduced?4:9);mote++)
                    {
                        float part=(mote+.5f)/9f,t=Mathf.Lerp(tail,head,part);
                        Vector3 p=Centre(start,end,t,age,f.seed,mote%3);
                        Particle(p,.03f+.025f*part,WithAlpha(Color.Lerp(f.tint,Color.white,.5f),strength*Mathf.Sin(part*Mathf.PI)));
                    }
                }
                if(age>f.duration)
                {
                    float p=(age-f.duration)/Arrival,glow=Mathf.Sin(p*Mathf.PI)*(f.onBeat?1:.65f);
                    if(glow>Absorption){Absorption=glow;AbsorptionColor=f.tint;}
                    // Small sparks converge in three dimensions and disappear inside the torso.
                    for(int mote=0;mote<(reduced?4:8);mote++)
                    {
                        float a=mote*Mathf.PI*.25f+f.seed+p*2;
                        var offset=new Vector3(Mathf.Cos(a),Mathf.Sin(a),Mathf.Sin(a*2)*.5f)*(.24f*(1-p));
                        Particle(end+offset,.035f*(1-p),WithAlpha(f.tint,glow*strength*.6f));
                    }
                }
            }
            mesh.Clear();streamRenderer.enabled=vertices.Count>0;
            if(vertices.Count==0)return;
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colours);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        }
        static Color WithAlpha(Color c,float a){c.a=a;return c;}
        static Vector3 Path(Vector3 start,Vector3 end,float t)
        {
            var a=Vector3.Lerp(start,end,.28f)+Vector3.up*.2f+Vector3.right*.9f;
            var b=Vector3.Lerp(start,end,.76f)+Vector3.up*.2f+Vector3.right*.8f;float u=1-t;
            return u*u*u*start+3*u*u*t*a+3*u*t*t*b+t*t*t*end;
        }
        static void Basis(Vector3 direction,out Vector3 right,out Vector3 up)
        {
            right=Vector3.Cross(direction,Mathf.Abs(direction.y)>.95f?Vector3.forward:Vector3.up).normalized;
            up=Vector3.Cross(right,direction).normalized;
        }
        static Vector3 Centre(Vector3 start,Vector3 end,float t,float age,float seed,int strand)
        {
            var p=Path(start,end,t);if(strand<0)return p;
            var direction=(Path(start,end,Mathf.Min(1,t+.003f))-Path(start,end,Mathf.Max(0,t-.003f))).normalized;
            Basis(direction,out var right,out var up);
            float angle=t*Mathf.PI*4-age*7+seed+strand*Mathf.PI*2/3;
            float radius=.22f*Mathf.Sin(Mathf.PI*t)*(1-.55f*t);
            return p+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius;
        }
        void Tube(Vector3 start,Vector3 end,float tail,float head,float age,float seed,int strand,float radius,Color tint)
        {
            if(head-tail<.002f)return;int first=vertices.Count;
            for(int segment=0;segment<=Segments;segment++)
            {
                float v=segment/(float)Segments,t=Mathf.Lerp(tail,head,v);
                var p=Centre(start,end,t,age,seed,strand);
                var direction=(Centre(start,end,Mathf.Min(1,t+.003f),age,seed,strand)-Centre(start,end,Mathf.Max(0,t-.003f),age,seed,strand)).normalized;
                Basis(direction,out var right,out var up);
                float envelope=Mathf.Pow(Mathf.Max(0,Mathf.Sin(v*Mathf.PI)),.65f);
                float r=radius*envelope*Mathf.Lerp(1,.28f,t);
                for(int side=0;side<Sides;side++)
                {
                    float a=side*Mathf.PI*2/Sides;var n=right*Mathf.Cos(a)+up*Mathf.Sin(a);
                    vertices.Add(p+n*r);normals.Add(n);colours.Add(WithAlpha(tint,tint.a*envelope));
                    if(segment==0)continue;int current=first+segment*Sides+side,next=first+segment*Sides+(side+1)%Sides;
                    indices.Add(current-Sides);indices.Add(current);indices.Add(next-Sides);
                    indices.Add(next-Sides);indices.Add(current);indices.Add(next);
                }
            }
        }
        void Particle(Vector3 p,float radius,Color tint)
        {
            // Small octahedra, with normals giving a soft radial fade in the shader.
            int first=vertices.Count;
            for(int axis=0;axis<6;axis++)
            {
                var n=axis<2?Vector3.right:axis<4?Vector3.up:Vector3.forward;if(axis%2!=0)n=-n;
                vertices.Add(p+n*radius);normals.Add(n);colours.Add(tint);
            }
            for(int i=0;i<4;i++)
            {
                int a=i==0?0:i==1?2:i==2?1:3,b=i==0?2:i==1?1:i==2?3:0;
                indices.Add(first+4);indices.Add(first+a);indices.Add(first+b);
                indices.Add(first+5);indices.Add(first+b);indices.Add(first+a);
            }
        }
    }
}
