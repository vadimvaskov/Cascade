using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Cascade.Sunset
{
 public static class SunsetSceneBuilder
 {
  const string Root="Assets/CascadeSunset";
  public const string ScenePath=Root+"/SunsetBridge.unity";
  static string Output=>SunsetSceneTools.Output;
  static readonly Dictionary<string,Material> mats=new Dictionary<string,Material>();
  static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
  static System.Random random;
  static Transform sceneRoot;
  static Mesh cube,leaf,rock,treble,note;
  static float R(float a,float b)=>a+(float)random.NextDouble()*(b-a);
  static Color Hex(string hex){ColorUtility.TryParseHtmlString(hex,out var c);return c;}
  static T Save<T>(T value,string path)where T:Object{var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(value,old);Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(value,path);return value;}
  static Material Mat(string name,string hex,float metal=0,float smooth=.4f,float emission=0)
  {
   var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};var color=Hex(hex);m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);
   if(emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*emission);}
   m=Save(m,Root+"/Generated/"+name+".mat");mats[name]=m;return m;
  }
  static Transform Group(string name,Transform parent,Vector3 position){var g=new GameObject(name).transform;g.SetParent(parent,false);g.localPosition=position;return g;}
  static GameObject MeshObj(string name,Transform parent,Mesh mesh,Material mat,Vector3 p,Vector3 scale,Quaternion? rotation=null)
  {var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;g.transform.localRotation=rotation??Quaternion.identity;g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=mat;return g;}
  static GameObject Box(string name,Transform parent,Vector3 p,Vector3 scale,Material mat)=>MeshObj(name,parent,cube,mat,p,scale);
  static GameObject Ball(string name,Transform parent,Vector3 p,Vector3 scale,Material mat)=>MeshObj(name,parent,leaf,mat,p,scale);
  static Mesh StoreMesh(string name,Mesh m){m.name=name;var saved=Save(m,Root+"/Generated/"+name+".asset");meshes[name]=saved;return saved;}
  static Mesh Tube(IList<Vector3> points,float radius,int sides=8)
  {
   var vs=new List<Vector3>();var ns=new List<Vector3>();var uv=new List<Vector2>();var ts=new List<int>();
   for(int i=0;i<points.Count;i++){var tangent=(points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)]).normalized;var right=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.95f?Vector3.forward:Vector3.up).normalized;var up=Vector3.Cross(right,tangent);for(int j=0;j<sides;j++){float a=j*Mathf.PI*2/sides;var n=right*Mathf.Cos(a)+up*Mathf.Sin(a);vs.Add(points[i]+n*radius);ns.Add(n);uv.Add(new Vector2((float)j/sides,(float)i/(points.Count-1)));if(i>0){int a0=(i-1)*sides+j,b0=(i-1)*sides+(j+1)%sides,c0=i*sides+j,d0=i*sides+(j+1)%sides;ts.AddRange(new[]{a0,c0,b0,b0,c0,d0});}}}
   var mesh=new Mesh();mesh.SetVertices(vs);mesh.SetNormals(ns);mesh.SetUVs(0,uv);mesh.SetTriangles(ts,0);mesh.RecalculateBounds();return mesh;
  }
  static Mesh OrganicSphere(int rings=7,int slices=12,bool faceted=false)
  {
   var vs=new List<Vector3>();var ns=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
   for(int i=0;i<=rings;i++)for(int j=0;j<=slices;j++){float a=(float)i/rings*Mathf.PI,b=(float)j/slices*Mathf.PI*2;var p=new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b));float k=1+.045f*Mathf.Sin(b*3+a*4);vs.Add(p*k);ns.Add(p);uv.Add(new Vector2((float)j/slices,(float)i/rings));if(i<rings&&j<slices){int n=i*(slices+1)+j;tri.AddRange(new[]{n,n+1,n+slices+1,n+1,n+slices+2,n+slices+1});}}
   var m=new Mesh();if(faceted){var vv=new List<Vector3>();var tt=new List<int>();foreach(var t in tri){tt.Add(vv.Count);vv.Add(vs[t]);}m.SetVertices(vv);m.SetTriangles(tt,0);m.RecalculateNormals();}else{m.SetVertices(vs);m.SetNormals(ns);m.SetUVs(0,uv);m.SetTriangles(tri,0);}m.RecalculateBounds();return m;
  }
  static void Stem(Transform parent,Vector3 a,Vector3 b,float r,Material mat,string name){var pts=new[]{a,b};MeshObj(name,parent,Tube(pts,r,7),mat,Vector3.zero,Vector3.one);}
  static void Tree(Transform parent,Vector3 p,float size,bool pink)
  {
   var tree=Group("Sakura / sculpted canopy",parent,p);tree.localScale=Vector3.one*size;
   var trunk=new List<Vector3>{Vector3.zero,new Vector3(.1f,1.1f,0),new Vector3(-.05f,2.3f,.1f),new Vector3(.1f,3.15f,.08f)};MeshObj("Curved trunk",tree,Tube(trunk,.19f,7),mats["Bark"],Vector3.zero,Vector3.one);
   for(int b=0;b<6;b++){float angle=b*2.4f;var end=new Vector3(Mathf.Cos(angle)*1.2f,2.7f+R(-.25f,.55f),Mathf.Sin(angle));Stem(tree,new Vector3(0,1.6f,0),end,.08f,mats["Bark"],"Branch");for(int j=0;j<18;j++){var q=end+new Vector3(R(-.9f,.9f),R(-.2f,.6f),R(-.8f,.8f));Ball("Blossom cluster",tree,q,new Vector3(R(.22f,.48f),R(.18f,.34f),R(.22f,.48f)),mats[(pink?"Petal":"Leaf")+random.Next(3)]);}}
  }
  static void Arch(Transform parent,Vector3 p,float size,bool clef)
  {
   var a=Group("Musical arch",parent,p);a.localScale=Vector3.one*size;float radius=2.3f;
   foreach(float x in new[]{-radius,radius}){Box("Stone pedestal",a,new Vector3(x,.18f,0),new Vector3(.8f,.36f,.9f),mats["Ivory"]);Box("Fluted column",a,new Vector3(x,1.7f,0),new Vector3(.42f,3,.48f),mats["Ivory"]);Box("Gold collar",a,new Vector3(x,3.08f,0),new Vector3(.65f,.16f,.7f),mats["Gold"]);}
   var points=new List<Vector3>();for(int i=0;i<=40;i++){float t=(float)i/40*Mathf.PI;points.Add(new Vector3(Mathf.Cos(t)*radius,3.1f+Mathf.Sin(t)*radius,0));}
   MeshObj("Arch stone crown",a,Tube(points,.29f,8),mats["Ivory"],Vector3.zero,Vector3.one);MeshObj("Golden inset",a,Tube(points.Select(v=>v+Vector3.back*.28f).ToArray(),.045f,6),mats["GoldLight"],Vector3.zero,Vector3.one);
   if(clef){MeshObj("Suspended golden treble clef",a,treble,mats["GoldLight"],new Vector3(0,3,0),Vector3.one*4);Stem(a,new Vector3(0,5.3f,0),new Vector3(0,4.3f,0),.025f,mats["Gold"],"Hanging chain");}
   for(int i=0;i<10;i++){float angle=R(0,Mathf.PI);Ball("Vine leaf",a,new Vector3(Mathf.Cos(angle)*2.6f,3.1f+Mathf.Sin(angle)*2.6f,.1f),new Vector3(.37f,.15f,.3f),mats["Leaf1"]);}
  }
  static void Island(Transform parent,Vector3 p,float radius,int variant)
  {
   var island=Group("Floating garden "+variant,parent,p);var verts=new List<Vector3>();var tris=new List<int>();int sides=12;var radial=new float[sides];for(int i=0;i<sides;i++)radial[i]=R(.85f,1.16f);
   float[] ys={0,-.65f,-2.9f,-6.5f};float[] rs={1,1.04f,.7f,.13f};
   for(int k=0;k<4;k++)for(int i=0;i<sides;i++){float angle=i*Mathf.PI*2/sides;verts.Add(new Vector3(Mathf.Cos(angle)*radius*rs[k]*radial[i],ys[k]+(k==0?0:R(-.35f,.35f)),Mathf.Sin(angle)*radius*rs[k]*radial[i]));}
   for(int k=0;k<3;k++)for(int i=0;i<sides;i++){int n=(i+1)%sides;tris.AddRange(new[]{k*sides+i,k*sides+n,(k+1)*sides+i,k*sides+n,(k+1)*sides+n,(k+1)*sides+i});}
   var vv=new List<Vector3>();var tt=new List<int>();foreach(int index in tris){tt.Add(vv.Count);vv.Add(verts[index]);}var cliff=new Mesh();cliff.SetVertices(vv);cliff.SetTriangles(tt,0);cliff.RecalculateNormals();MeshObj("Faceted suspended rock",island,cliff,mats["Cliff"],Vector3.zero,Vector3.one);
   var cap=new Mesh();var cv=new List<Vector3>{new Vector3(0,.015f,0)};cv.AddRange(verts.Take(sides).Select(v=>v+Vector3.up*.015f));var ct=new List<int>();for(int i=0;i<sides;i++)ct.AddRange(new[]{0,1+(i+1)%sides,1+i});cap.SetVertices(cv);cap.SetTriangles(ct,0);cap.RecalculateNormals();MeshObj("Moss garden surface",island,cap,mats["Moss"],Vector3.zero,Vector3.one);
   for(int i=0;i<7;i++){float a=R(0,Mathf.PI*2),rr=R(.55f,.95f)*radius;MeshObj("Weathered rim stone",island,rock,mats["CliffLight"],new Vector3(Mathf.Cos(a)*rr,R(-.15f,.05f),Mathf.Sin(a)*rr),new Vector3(R(.5f,1.3f),R(.3f,.7f),R(.6f,1.2f)),Quaternion.Euler(R(-20,20),R(0,360),0));}
   for(int i=0;i<14;i++){float a=i*Mathf.PI*2/14;MeshObj("Hanging rock strata",island,rock,mats[i%3==0?"CliffLight":"Cliff"],new Vector3(Mathf.Cos(a)*radius*.8f,-1.5f,Mathf.Sin(a)*radius*.8f),new Vector3(R(.4f,.8f),R(1.4f,2.4f),R(.4f,.8f)));}
   Tree(island,new Vector3(-radius*.35f,0,radius*.15f),R(.7f,1.15f),true);Tree(island,new Vector3(radius*.36f,0,radius*.2f),R(.5f,.8f),variant%2==0);
   if(variant%3==0)Arch(island,new Vector3(.5f,0,-.2f),.9f,true);
   // A ribbon has authored UVs, so the water flow remains stable while its island moves.
   float x=variant%2==0?-.7f:.6f,z=-radius*.87f;var water=new Mesh();water.vertices=new[]{new Vector3(x-1,0,z),new Vector3(x+1,0,z),new Vector3(x-1.3f,-9,z-.7f),new Vector3(x+1.3f,-9,z-.7f)};water.uv=new[]{new Vector2(0,1),new Vector2(1,1),new Vector2(0,0),new Vector2(1,0)};water.triangles=new[]{0,2,1,1,2,3};water.RecalculateNormals();MeshObj("Flowing waterfall",island,water,mats["Water"],Vector3.zero,Vector3.one);
  }
  static void Bridge(Transform parent,int section)
  {
   Box("Continuous bridge foundation",parent,new Vector3(0,-.4f,12),new Vector3(7,.5f,24),mats["Foundation"]);
   for(int row=0;row<12;row++)for(int col=0;col<7;col++){var material=mats["Paver"+((row*3+col+section)%5)];Box("Bevelled stone paving",parent,new Vector3((col-3)*.987f,-.065f,1+row*2),new Vector3(.968f,.13f,1.97f),material);}
   foreach(int side in new[]{-1,1})
   {
    float x=side*3.65f;Box("Curbstone",parent,new Vector3(side*3.38f,.07f,12),new Vector3(.3f,.28f,24),mats["Ivory"]);Box("Continuous amber light",parent,new Vector3(side*3.2f,.1f,12),new Vector3(.06f,.06f,24),mats["GoldLight"]);
    Stem(parent,new Vector3(x,1.13f,0),new Vector3(x,1.13f,24),.075f,mats["Gold"],"Brass handrail");Stem(parent,new Vector3(x,.34f,0),new Vector3(x,.34f,24),.04f,mats["Gold"],"Lower rail");
    for(int j=0;j<8;j++){float z=j*3+1.5f;Box("Stone post",parent,new Vector3(x,.59f,z),new Vector3(.23f,1.18f,.23f),mats["Ivory"]);Box("Gold post cap",parent,new Vector3(x,1.22f,z),new Vector3(.33f,.11f,.33f),mats["Gold"]);Box("Lantern light",parent,new Vector3(x-side*.13f,.64f,z),new Vector3(.035f,.35f,.14f),mats["GoldLight"]);if(j%4==0){Stem(parent,new Vector3(x,1.28f,z),new Vector3(x,1.8f,z),.035f,mats["Gold"],"Lantern neck");Ball("Lantern glass",parent,new Vector3(x,1.95f,z),new Vector3(.15f,.2f,.15f),mats["PearlLight"]);}
     var arc=new List<Vector3>();for(int k=0;k<=16;k++){float u=(float)k/16;arc.Add(new Vector3(x,.42f+Mathf.Sin(u*Mathf.PI)*.46f,z+u*3));}MeshObj("Ornamental rail arch",parent,Tube(arc,.027f,6),mats["Gold"],Vector3.zero,Vector3.one);
    }
   }
   Island(parent,new Vector3(-12-R(0,4),-.5f,7),R(3.5f,5),section);Island(parent,new Vector3(14+R(0,3),R(-.4f,1),20),R(4,5.5f),section+3);
   if(section%2==0)Arch(parent,new Vector3(0,0,20),1.55f,false);
   var orb=Group("Passing musical glass",parent,new Vector3(1.6f,.95f,11));Ball("Cyan sphere",orb,Vector3.zero,Vector3.one*.31f,mats["Orb"]);MeshObj("Musical note",orb,note,mats["CyanLight"],new Vector3(0,0,-.3f),Vector3.one*.5f);
  }
  static void CombineChunk(Transform root,string label)
  {
   var filters=root.GetComponentsInChildren<MeshFilter>();var groups=filters.Where(f=>f.sharedMesh&&f.GetComponent<MeshRenderer>()).GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial).ToArray();int idx=0;
   foreach(var group in groups){var combine=group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray();var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combine,true,true);mesh.RecalculateBounds();var saved=StoreMesh(label+"_"+idx++,mesh);MeshObj("Batched / "+group.Key.name,root,saved,group.Key,Vector3.zero,Vector3.one);}
   // Only the just-generated source children are removed; batched replacements remain.
   foreach(var t in root.Cast<Transform>().Where(t=>!t.name.StartsWith("Batched / ")).ToArray())Object.DestroyImmediate(t.gameObject);
  }
  static void ImportHero()
  {
   string path=Root+"/Art/Fa/Fa_Run.fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.animationCompression=ModelImporterAnimationCompression.Off;
   var clips=importer.defaultClipAnimations;if(clips.Length==0)throw new Exception("Fa FBX has no animation take");foreach(var clip in clips){clip.name="Run";clip.loopTime=true;clip.loopPose=true;clip.lockRootPositionXZ=true;clip.lockRootRotation=true;clip.lockRootHeightY=true;clip.keepOriginalOrientation=true;clip.keepOriginalPositionXZ=true;clip.keepOriginalPositionY=true;}importer.clipAnimations=clips;importer.SaveAndReimport();
   foreach(string file in new[]{"Fa_BaseColor.jpg","Fa_Normal.png","Fa_ORM.jpg"}){var ti=(TextureImporter)AssetImporter.GetAtPath(Root+"/Art/Fa/Textures/"+file);ti.sRGBTexture=file.Contains("BaseColor");ti.textureType=file.Contains("Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;ti.maxTextureSize=4096;ti.mipmapEnabled=true;ti.textureCompression=TextureImporterCompression.CompressedHQ;ti.SaveAndReimport();}
  }
  static Animator AddHero(Transform parent)
  {
   var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Art/Fa/Fa_Run.fbx");var go=(GameObject)PrefabUtility.InstantiatePrefab(model);go.name="Fa / Two Cycle Sprint";go.transform.SetParent(parent,false);var animator=go.GetComponent<Animator>();if(!animator)animator=go.AddComponent<Animator>();if(!animator.avatar||!animator.avatar.isValid)throw new Exception("Fa avatar is invalid");
   var clip=AssetDatabase.LoadAllAssetsAtPath(Root+"/Art/Fa/Fa_Run.fbx").OfType<AnimationClip>().First(c=>c.name=="Run");var path=Root+"/Generated/FaRun.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(path);var machine=controller.layers[0].stateMachine;var state=machine.states.FirstOrDefault(s=>s.state.name=="Run").state;if(!state)state=machine.AddState("Run");state.motion=clip;machine.defaultState=state;animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
   var mat=new Material(Shader.Find("Cascade/Sunset/Fa ORM")){name="Fa / original PBR maps"};mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/Fa/Textures/Fa_BaseColor.jpg"));mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/Fa/Textures/Fa_Normal.png"));mat.SetTexture("_ORM",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/Fa/Textures/Fa_ORM.jpg"));mat=Save(mat,Root+"/Generated/FaMaterial.mat");
   foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>()){r.sharedMaterials=r.sharedMaterials.Select(_=>mat).ToArray();r.updateWhenOffscreen=true;}
   var bounds=go.GetComponentsInChildren<Renderer>()[0].bounds;foreach(var renderer in go.GetComponentsInChildren<Renderer>())bounds.Encapsulate(renderer.bounds);go.transform.localScale=Vector3.one*(1.95f/bounds.size.y);bounds=go.GetComponentsInChildren<Renderer>()[0].bounds;foreach(var renderer in go.GetComponentsInChildren<Renderer>())bounds.Encapsulate(renderer.bounds);go.transform.position+=new Vector3(-.35f,.025f-bounds.min.y,0);return animator;
  }
  [MenuItem("Cascade/Sunset/Build sunset bridge")]
  public static void Build()
  {
   if(SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save your current scene before rebuilding the sunset bridge.");
   Directory.CreateDirectory(Root+"/Generated");Directory.CreateDirectory(Output);AssetDatabase.Refresh();ImportHero();mats.Clear();meshes.Clear();random=new System.Random(1706);
   Mat("Foundation","#282447",.15f,.35f);Mat("Ivory","#A390AF",.18f,.46f);Mat("Gold","#CB9B50",.72f,.67f);Mat("GoldLight","#FFAD63",.15f,.5f,2.2f);Mat("PearlLight","#FFE8BB",.1f,.5f,3.4f);Mat("CyanLight","#88EEFF",.1f,.5f,2.6f);Mat("Orb","#1593D2",.7f,.9f);Mat("Cliff","#4C436D",0,.35f);Mat("CliffLight","#706180",0,.42f);Mat("Moss","#67745D",0,.3f);Mat("Bark","#605064",0,.3f);
   string[] pavers={"#625783","#71618B","#575479","#8A6B92","#635078"};for(int i=0;i<5;i++)Mat("Paver"+i,pavers[i],.08f,.43f);
   string[] petals={"#DA8ABB","#F2B9DA","#B980BB"},leaves={"#899575","#687F6C","#BAC08C"};for(int i=0;i<3;i++){Mat("Petal"+i,petals[i],0,.2f,.06f);Mat("Leaf"+i,leaves[i],0,.25f);}
   var water=new Material(Shader.Find("Cascade/Sunset/Waterfall")){name="Water"};mats["Water"]=Save(water,Root+"/Generated/Water.mat");
   cube=StoreMesh("Bevelled unit stone",SunsetMeshFactory.RoundedBox(1,1,1,.07f,.035f,3));leaf=StoreMesh("Organic canopy",OrganicSphere());rock=StoreMesh("Rim rock",OrganicSphere(4,7,true));treble=StoreMesh("Treble clef",SunsetMeshFactory.Treble(.07f));note=StoreMesh("Music note",SunsetMeshFactory.Note());
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);sceneRoot=new GameObject("CASCADE / Sunset Bridge").transform;
   var world=sceneRoot.gameObject.AddComponent<SunsetRunWorld>();world.roadSections=new Transform[7];world.sectionLength=24;world.speed=5.2f;
   for(int i=0;i<world.roadSections.Length;i++){var chunk=Group("Moving section "+i,sceneRoot,new Vector3(0,0,(i-1)*24));Bridge(chunk,i);CombineChunk(chunk,"Section"+i);world.roadSections[i]=chunk;}
   var distant=Group("Distant floating skyline",sceneRoot,Vector3.zero);for(int i=0;i<8;i++){float x=(i<4?-1:1)*R(30,75);Island(distant,new Vector3(x,R(-2,13),R(100,170)),R(6,12),i+20);}CombineChunk(distant,"Distant");
   world.hero=AddHero(sceneRoot);
   var camGo=new GameObject("Sunset camera",typeof(Camera),typeof(AudioListener));camGo.transform.SetParent(sceneRoot,false);var cam=camGo.GetComponent<Camera>();camGo.tag="MainCamera";cam.transform.position=new Vector3(.15f,2.5f,-5.1f);cam.transform.LookAt(new Vector3(0,1.7f,9));cam.fieldOfView=56;cam.nearClipPlane=.1f;cam.farClipPlane=250;cam.allowHDR=true;cam.clearFlags=CameraClearFlags.Skybox;var cd=camGo.AddComponent<UniversalAdditionalCameraData>();cd.renderPostProcessing=true;cd.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;cd.antialiasingQuality=AntialiasingQuality.High;world.sceneCamera=cam;world.pulseMaterials=new[]{mats["GoldLight"]};
   var sunlight=Group("Warm setting sun",sceneRoot,Vector3.zero).gameObject.AddComponent<Light>();sunlight.type=LightType.Directional;sunlight.transform.rotation=Quaternion.Euler(25,-145,0);sunlight.color=Hex("#FFD3A9");sunlight.intensity=1.65f;sunlight.shadows=LightShadows.Soft;sunlight.shadowStrength=.72f;sunlight.shadowBias=.03f;sunlight.shadowNormalBias=.25f;
   var fill=Group("Cool sky fill",sceneRoot,Vector3.zero).gameObject.AddComponent<Light>();fill.type=LightType.Directional;fill.transform.rotation=Quaternion.Euler(38,35,0);fill.color=Hex("#BEC9FF");fill.intensity=.95f;fill.shadows=LightShadows.None;
   var heroFill=Group("Soft portrait light",sceneRoot,new Vector3(-1,3,-3)).gameObject.AddComponent<Light>();heroFill.type=LightType.Point;heroFill.color=Hex("#D4E7FF");heroFill.intensity=4;heroFill.range=8;heroFill.shadows=LightShadows.None;
   RenderSettings.sun=sunlight;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=Hex("#9093C2");RenderSettings.ambientEquatorColor=Hex("#9186A6");RenderSettings.ambientGroundColor=Hex("#4D3C62");RenderSettings.ambientIntensity=1;RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=Hex("#B89AC8");RenderSettings.fogDensity=.007f;var sky=new Material(Shader.Find("Cascade/Sunset/Sky")){name="Sunset sky"};RenderSettings.skybox=Save(sky,Root+"/Generated/SunsetSky.mat");
   var volume=Group("Sunset grade / gentle glow",sceneRoot,Vector3.zero).gameObject.AddComponent<Volume>();volume.isGlobal=true;var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Sunset grade";var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.38f);bloom.threshold.Override(1.1f);bloom.scatter.Override(.65f);bloom.clamp.Override(6);var tonemap=profile.Add<Tonemapping>(true);tonemap.mode.Override(TonemappingMode.ACES);var colors=profile.Add<ColorAdjustments>(true);colors.postExposure.Override(.15f);colors.saturation.Override(8);colors.contrast.Override(6);var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(.12f);vignette.smoothness.Override(.5f);var assetPath=Root+"/Generated/SunsetVolume.asset";var oldProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(assetPath);if(oldProfile){foreach(var sub in AssetDatabase.LoadAllAssetsAtPath(assetPath).Where(o=>o!=oldProfile))Object.DestroyImmediate(sub,true);EditorUtility.CopySerialized(profile,oldProfile);Object.DestroyImmediate(profile);profile=oldProfile;}else AssetDatabase.CreateAsset(profile,assetPath);foreach(var component in profile.components)if(!AssetDatabase.Contains(component))AssetDatabase.AddObjectToAsset(component,profile);volume.sharedProfile=profile;
   EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Selection.activeGameObject=sceneRoot.gameObject;
   File.WriteAllText(Output+"/build-status.json",JsonUtility.ToJson(new BuildStatus{scene=ScenePath,heroAvatarValid=world.hero.avatar.isValid,heroAvatarHuman=world.hero.avatar.isHuman,sections=world.roadSections.Length,renderers=sceneRoot.GetComponentsInChildren<Renderer>().Length,meshes=meshes.Count,animationLength=1.066667f},true));Debug.Log("SUNSET_SCENE_BUILT "+ScenePath);
  }
  [Serializable] class BuildStatus{public string scene;public bool heroAvatarValid,heroAvatarHuman;public int sections,renderers,meshes;public float animationLength;}
  [MenuItem("Cascade/Sunset/Open sunset bridge")]
  public static void Open(){EditorSceneManager.OpenScene(ScenePath);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);}
 }
}
