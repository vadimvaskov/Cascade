using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Cascade.Dream
{
    public static partial class DreamSceneBuilder
    {
        public const string Root="Assets/CascadeDream";
        public const string ScenePath=Root+"/MiDreamworld.unity";
        public const string HeroPath=Root+"/Art/Mi/Mi_Rodin.fbx";
        public static string Output=>Path.GetFullPath("Builds/DreamworldExport");
        static Dictionary<string,Material> mats=new Dictionary<string,Material>();
        static Dictionary<string,Mesh> meshCache=new Dictionary<string,Mesh>();
        static System.Random rng;
        static Mesh sphere,organic,canopy,petal,note,clef;
        static float R(float a,float b)=>a+(float)rng.NextDouble()*(b-a);
        static Color C(string hex){ColorUtility.TryParseHtmlString(hex,out var c);return c;}
        static T Save<T>(T asset,string path)where T:Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old)
            {
                if(old is Mesh target && asset is Mesh source)
                {
                    target.Clear();target.indexFormat=source.indexFormat;target.vertices=source.vertices;target.normals=source.normals;target.uv=source.uv;target.colors=source.colors;
                    target.subMeshCount=source.subMeshCount;for(int i=0;i<source.subMeshCount;i++)target.SetTriangles(source.GetTriangles(i),i);
                    target.RecalculateBounds();target.UploadMeshData(false);
                }
                else EditorUtility.CopySerialized(asset,old);
                Object.DestroyImmediate(asset);EditorUtility.SetDirty(old);return old;
            }
            AssetDatabase.CreateAsset(asset,path);return asset;
        }
        static Transform Group(string name,Transform parent,Vector3 p=default){var g=new GameObject(name).transform;g.SetParent(parent,false);g.localPosition=p;return g;}
        static Material Mat(string name,string hex,float metal=0,float smooth=.35f,float emission=0)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};m.SetColor("_BaseColor",C(hex));m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);if(emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",C(hex)*emission);}m=Save(m,Root+"/Generated/"+name+".mat");mats[name]=m;return m;}
        static Mesh Store(string name,Mesh m){m.name=name;return Save(m,Root+"/Generated/"+name+".asset");}
        static GameObject Piece(string name,Transform parent,Mesh mesh,Material mat,Vector3 p,Vector3 s,Quaternion? q=null)
        {var o=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));o.transform.SetParent(parent,false);o.transform.localPosition=p;o.transform.localScale=s;o.transform.localRotation=q??Quaternion.identity;o.GetComponent<MeshFilter>().sharedMesh=mesh;var r=o.GetComponent<MeshRenderer>();r.sharedMaterial=mat;r.receiveShadows=true;r.shadowCastingMode=ShadowCastingMode.On;return o;}
        static GameObject Blob(string name,Transform parent,Vector3 p,Vector3 s,string mat,Quaternion? q=null)=>Piece(name,parent,organic,mats[mat],p,s,q);
        static GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,string mat,float bevel=.07f,Quaternion? q=null)
        {string key=size+"/"+bevel;if(!meshCache.TryGetValue(key,out var m)){m=DreamGeometry.Rounded(size,bevel);meshCache[key]=m;}return Piece(name,parent,m,mats[mat],p,Vector3.one,q);}
        static void Tube(string name,Transform p,IList<Vector3> pts,float a,float b,string mat,int sides=12)=>Piece(name,p,DreamGeometry.Tube(pts,a,b,sides),mats[mat],Vector3.zero,Vector3.one);
        static void Curve(string name,Transform p,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float r0,float r1,string mat,int steps=24)=>Tube(name,p,DreamGeometry.Curve(a,b,c,d,steps),r0,r1,mat);

        public static void ImportHero()
        {
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();var mi=(ModelImporter)AssetImporter.GetAtPath(HeroPath);
            mi.animationType=ModelImporterAnimationType.Human;mi.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation=true;mi.materialImportMode=ModelImporterMaterialImportMode.None;mi.animationCompression=ModelImporterAnimationCompression.Off;
            mi.importBlendShapes=true;mi.meshCompression=ModelImporterMeshCompression.Off;mi.optimizeGameObjects=false;
            var clips=mi.defaultClipAnimations;
            foreach(var clip in clips)
            {
                clip.name=clip.name.Contains("000067")?"Run":clip.name.Contains("000089")?"Walk":clip.name.Contains("000093")?"Idle":clip.name;
                clip.loopTime=true;clip.loopPose=true;clip.lockRootRotation=true;clip.lockRootPositionXZ=clip.name=="Idle";clip.lockRootHeightY=true;
                clip.keepOriginalOrientation=true;clip.keepOriginalPositionXZ=false;clip.keepOriginalPositionY=false;clip.heightFromFeet=true;
            }
            mi.clipAnimations=clips;mi.SaveAndReimport();
            foreach(var path in Directory.GetFiles(Root+"/Art/Mi","*.png"))
            {var ti=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));if(ti==null)continue;ti.sRGBTexture=path.Contains("diffuse")||path.Contains("shaded");ti.textureType=path.Contains("normal")?TextureImporterType.NormalMap:TextureImporterType.Default;ti.maxTextureSize=2048;ti.mipmapEnabled=true;ti.anisoLevel=4;ti.textureCompression=TextureImporterCompression.CompressedHQ;ti.SaveAndReimport();}
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(HeroPath);var avatar=model.GetComponent<Animator>()?.avatar;
            File.WriteAllText(Output+"/hero-import.txt","Human="+(avatar&&avatar.isHuman)+" Valid="+(avatar&&avatar.isValid)+"\n"+string.Join("\n",AssetDatabase.LoadAllAssetsAtPath(HeroPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).Select(c=>c.name+" "+c.length+" sec looping="+c.isLooping)));
            if(!avatar||!avatar.isHuman||!avatar.isValid)throw new Exception("Mi humanoid avatar needs bone mapping; see hero-import.txt");
        }
        static Animator Hero(Transform parent)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HeroPath));go.name="Mi | Rodin character";go.transform.SetParent(parent,false);
            var a=go.GetComponent<Animator>();var path=Root+"/Generated/MiRun.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path)??AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm=controller.layers[0].stateMachine;
            foreach(string label in new[]{"Run","Walk","Idle"}){var state=sm.states.FirstOrDefault(s=>s.state.name==label).state??sm.AddState(label);state.motion=AssetDatabase.LoadAllAssetsAtPath(HeroPath).OfType<AnimationClip>().First(c=>c.name==label);if(label=="Run")sm.defaultState=state;}
            a.runtimeAnimatorController=controller;a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var mat=Mat("Mi original colors","#FFFFFF",.08f,.28f);mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/Mi/texture_diffuse.png"));mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/Mi/texture_normal.png"));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.65f);
            foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>()){r.sharedMaterials=r.sharedMaterials.Select(_=>mat).ToArray();r.updateWhenOffscreen=true;}
            var bounds=go.GetComponentsInChildren<Renderer>()[0].bounds;foreach(var r in go.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
            go.transform.localScale=Vector3.one*(1.85f/bounds.size.y);bounds=go.GetComponentsInChildren<Renderer>()[0].bounds;foreach(var r in go.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
            go.transform.localPosition=new Vector3(-.3f,.04f-bounds.min.y,0);
            PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/MiRunner.prefab");return a;
        }
        static void Palette()
        {
            Mat("Porcelain","#E5C6BB",.05f,.43f);Mat("Ivory","#F5DAC0",.02f,.46f);Mat("Shadow stone","#70678D",.04f,.32f);
            Mat("Warm sandstone","#BE97B0",0,.32f);Mat("Cliff","#8987B1",0,.26f);Mat("Cliff lit","#ACA2C1",0,.28f);
            Mat("Moss","#719A9B",0,.2f);Mat("Moss bright","#99BDA9",0,.2f);Mat("Bark","#936880",0,.25f);
            Mat("Gold","#D6A15B",.68f,.7f);Mat("Gold rim","#F7CB86",.48f,.63f);Mat("Glass warm","#FFE1A7",.05f,.5f,1.7f);
            Mat("Aqua","#75DFE5",.18f,.6f,1.1f);Mat("Glass blue","#2C9CBF",.42f,.88f,.18f);
            Mat("Roof lavender","#887AAE",.1f,.45f);Mat("Roof rose","#C890AD",.1f,.45f);Mat("Roof teal","#619EA7",.16f,.5f);
            Mat("Window","#FFC881",.05f,.5f,1.6f);Mat("Cloud","#E0C9DB",0,.05f,.12f);
            string[] leafColors={"#CF81B5","#E89BC4","#F4B4CE","#F8CAD9","#A96DA8"};for(int i=0;i<5;i++)Mat("Petal"+i,leafColors[i],0,.24f);
            string[] stones={"#B5A9C5","#ADA7C4","#BCB0CD","#A9A4C2"};for(int i=0;i<4;i++)Mat("Paver"+i,stones[i],.08f,.47f);
            var sky=new Material(Shader.Find("Cascade/Dream/Sky")){name="Peach and periwinkle sunset"};sky.SetColor("_Top",new Color(.35f,.52f,.85f));sky.SetColor("_Horizon",new Color(.98f,.65f,.48f));mats["Sky"]=Save(sky,Root+"/Generated/DreamSky.mat");
            var water=new Material(Shader.Find("Cascade/Dream/Water")){name="Silky waterfall"};mats["Water"]=Save(water,Root+"/Generated/DreamWater.mat");
            PaintSurfaces();
        }
        static void PaintSurfaces()
        {
            var stone=new Texture2D(512,512,TextureFormat.RGB24,true);var leaves=new Texture2D(512,512,TextureFormat.RGB24,true);
            for(int y=0;y<512;y++)for(int x=0;x<512;x++)
            {
                float u=x/512f,v=y/512f;
                float grain=Mathf.PerlinNoise(u*25,v*25),broad=Mathf.PerlinNoise(u*5+12,v*6+7);
                float strata=Mathf.Sin(v*90+Mathf.PerlinNoise(u*6,v*4)*5)*.025f;
                float g=.78f+.15f*broad+.07f*grain+strata;stone.SetPixel(x,y,new Color(g,g*.985f,g*.99f));
                float l=.80f+.12f*Mathf.PerlinNoise(u*6+3,v*7)+.08f*Mathf.PerlinNoise(u*28,v*25);leaves.SetPixel(x,y,new Color(l,l,l));
            }
            stone.Apply();leaves.Apply();File.WriteAllBytes(Root+"/Art/PaintedStone.png",stone.EncodeToPNG());File.WriteAllBytes(Root+"/Art/BlossomVariation.png",leaves.EncodeToPNG());Object.DestroyImmediate(stone);Object.DestroyImmediate(leaves);AssetDatabase.Refresh();
            foreach(string key in new[]{"Cliff","Cliff lit","Warm sandstone","Porcelain","Bark"})mats[key].SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/PaintedStone.png"));
            for(int i=0;i<5;i++){var m=mats["Petal"+i];m.shader=Shader.Find("Cascade/Dream/Soft foliage");m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/BlossomVariation.png"));}
        }
        static void Batch(Transform root,string label)
        {
            var filters=root.GetComponentsInChildren<MeshFilter>();var groups=filters.Where(f=>f.sharedMesh).GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial).ToArray();int index=0;
            foreach(var group in groups)
            {
                var instances=group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray();
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(instances,true,true);mesh.RecalculateBounds();mesh=Store(label+"_"+index++,mesh);
                var g=Piece("Batch | "+group.Key.name,root,mesh,group.Key,Vector3.zero,Vector3.one);
                if(group.Key==mats["Water"]||group.Key==mats["Cloud"])g.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            }
            foreach(var child in root.Cast<Transform>().Where(t=>!t.name.StartsWith("Batch | ")).ToArray())Object.DestroyImmediate(child.gameObject);
        }
        [MenuItem("Cascade/Dreamworld/Build Mi scene")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode before building.");
            if(SceneManager.GetActiveScene().isDirty)throw new Exception("Save the current scene before building Dreamworld.");
            Directory.CreateDirectory(Output+"/Review");Directory.CreateDirectory(Root+"/Generated");Directory.CreateDirectory(Root+"/Prefabs");
            ImportHero();rng=new System.Random(7311);mats.Clear();meshCache.Clear();Palette();
            sphere=Store("Smooth sphere",DreamGeometry.Pebble(24,40,0));organic=Store("Sculpted pebble",DreamGeometry.Pebble());canopy=Store("Sculpted blossom crown",DreamGeometry.Canopy());petal=Store("Leaf petal",DreamGeometry.Pebble(5,8,.02f));
            note=Store("Musical eighth note",DreamGlyphFactory.Note());clef=Store("Treble clef",DreamGlyphFactory.Treble(.035f));
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("CASCADE | Mi's musical sky gardens").transform;
            var w=root.gameObject.AddComponent<DreamRunWorld>();w.targetSpeed=4.6f;w.strideSpeed=4.6f;w.reducedMotion=true;
            w.layers=new[]{new DreamLayer{spacing=32,speedFactor=1,recycleBehind=-45,sections=new Transform[8]},new DreamLayer{spacing=70,speedFactor=.42f,recycleBehind=-120,sections=new Transform[6]}};
            for(int i=0;i<8;i++)
            {
                var segment=Group("Bridge garden section "+i,root,new Vector3(0,0,(i-1)*32));Bridge(segment,i);Batch(segment,"Bridge"+i);w.layers[0].sections[i]=segment;
                PrefabUtility.SaveAsPrefabAsset(segment.gameObject,Root+"/Prefabs/BridgeGarden_"+i+".prefab");
            }
            for(int i=0;i<6;i++)
            {
                var far=Group("Distant city section "+i,root,new Vector3(0,0,(i-1)*70));
                Island(far,new Vector3(-50-i%2*9,5+i%3*2,100),9,i+12,true);Island(far,new Vector3(60+i%2*11,10-i%3*4,130),12,i+18,true);Batch(far,"Skyline"+i);w.layers[1].sections[i]=far;
            }
            w.hero=Hero(root);MakeOrb(root,w);Lighting(root,w);
            Shader.SetGlobalFloat("_DreamTime",0);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject,Root+"/Prefabs/MiDreamworld.prefab");
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Selection.activeGameObject=root.gameObject;
            File.WriteAllText(Output+"/build-report.txt","Scene: "+ScenePath+"\nHero: Mi Rodin\nAvatar valid: "+w.hero.avatar.isValid+"\nSingle companion sphere: "+(root.GetComponentsInChildren<Transform>().Count(t=>t.name=="Companion music sphere")==1)+"\nRenderers: "+root.GetComponentsInChildren<Renderer>().Length+"\n");
            Debug.Log("DREAM_SCENE_BUILT");
        }
        [MenuItem("Cascade/Dreamworld/Open Mi scene")]
        public static void Open(){EditorSceneManager.OpenScene(ScenePath);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);}
    }
}
