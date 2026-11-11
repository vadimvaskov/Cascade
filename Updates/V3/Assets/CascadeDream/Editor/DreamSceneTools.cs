using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Cascade.Dream
{
    [InitializeOnLoad] public static class DreamSceneTools
    {
        static string Output=>DreamSceneBuilder.Output;
        const string CheckKey="Cascade.Dream.PlayCheck";
        static bool recording,sampled;
        static int frame;
        static Vector3 handStart,rootStart;
        static double started;
        static float liveHipMin,liveHipMax;
        static DreamSceneTools(){EditorApplication.update+=Tick;}
        public static DreamRunWorld World()=>Object.FindAnyObjectByType<DreamRunWorld>();
        public static AnimationClip Clip()=>AssetDatabase.LoadAllAssetsAtPath(DreamSceneBuilder.HeroPath).OfType<AnimationClip>().First(c=>c.name=="Run");
        static void Pose(Animator animator,float seconds)
        {
            animator.Rebind();animator.Update(0);animator.Play("Run",0,(seconds/Clip().length)%1);animator.Update(0);
        }
        static void Open(){if(!World())DreamSceneBuilder.Open();}
        public static void Capture(string path,int width,int height)
        {
            Open();Directory.CreateDirectory(Path.GetDirectoryName(path));var camera=World().sceneCamera;var oldTarget=camera.targetTexture;float oldAspect=camera.aspect;
            camera.aspect=width/(float)height;var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.Create();
            try
            {RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);RenderTexture.active=previous;}
            finally{camera.targetTexture=oldTarget;camera.aspect=oldAspect;rt.Release();Object.DestroyImmediate(rt);}
        }
        [MenuItem("Cascade/Dreamworld/Capture previews")]
        public static void Preview()
        {
            Open();var w=World();w.Initialize();Pose(w.hero,.16f);Capture(Output+"/Review/MiDreamworld.png",1920,1080);Capture(Output+"/Review/MiDreamworld-Portrait.png",1080,1440);
            var c=w.sceneCamera;var p=c.transform.position;var q=c.transform.rotation;c.transform.position=new Vector3(-8,4,-5);c.transform.LookAt(new Vector3(-3,1.8f,9));Capture(Output+"/Review/Garden-Detail.png",1600,1000);c.transform.position=p;c.transform.rotation=q;
            EditorSceneManager.OpenScene(DreamSceneBuilder.ScenePath);Debug.Log("DREAM_PREVIEWS_SAVED");
        }
        [MenuItem("Cascade/Dreamworld/Verify scene")]
        public static void Verify()
        {
            Open();var w=World();w.Initialize();var clip=Clip();var origin=w.hero.transform.position;
            if(!w.hero.avatar.isHuman||!w.hero.avatar.isValid||!clip.isLooping||w.hero.applyRootMotion)throw new Exception("Mi must have valid looping in-place humanoid animation");
            int orbs=w.GetComponentsInChildren<Transform>().Count(t=>t.name=="Companion music sphere");if(orbs!=1)throw new Exception("Scene must contain exactly one companion sphere");
            var hand=w.hero.GetBoneTransform(HumanBodyBones.LeftHand);Pose(w.hero,.1f);var handA=hand.position;Pose(w.hero,.35f);float displacement=Vector3.Distance(handA,hand.position);if(displacement<.015f)throw new Exception("Running pose is static");
            float minHipX=float.MaxValue,maxHipX=float.MinValue,minHipZ=float.MaxValue,maxHipZ=float.MinValue;
            for(int i=0;i<60;i++){Pose(w.hero,clip.length*i/60f);var hip=w.hero.GetBoneTransform(HumanBodyBones.Hips).position;minHipX=Mathf.Min(minHipX,hip.x);maxHipX=Mathf.Max(maxHipX,hip.x);minHipZ=Mathf.Min(minHipZ,hip.z);maxHipZ=Mathf.Max(maxHipZ,hip.z);}
            float hipTravel=Mathf.Max(maxHipX-minHipX,maxHipZ-minHipZ);File.WriteAllText(Output+"/hip-motion-check.txt","Horizontal hip range across full run cycle: "+hipTravel+" m");
            if(hipTravel>.20f)throw new Exception("Skeleton drifts inside the run cycle: "+hipTravel);
            var orbOffset=w.companionOrb.position-w.hero.transform.position;float seam=0,orbDrift=0;
            for(int frame=0;frame<60*600;frame++)
            {
                w.Advance(1f/60);var zs=w.layers[0].sections.Select(s=>s.localPosition.z).OrderBy(x=>x).ToArray();for(int j=1;j<zs.Length;j++)seam=Mathf.Max(seam,Mathf.Abs(zs[j]-zs[j-1]-w.layers[0].spacing));
                var d=w.companionOrb.position-w.hero.transform.position;orbDrift=Mathf.Max(orbDrift,Mathf.Abs(d.x-orbOffset.x),Mathf.Abs(d.z-orbOffset.z));
            }
            if(seam>.002f||orbDrift>.001f)throw new Exception("Road seam or companion drift outside tolerance");
            if(Vector3.Distance(origin,w.hero.transform.position)>.001f)throw new Exception("Hero root moved");
            w.SetPaused(true);var before=w.layers[0].sections[0].position;var beforeOrb=w.companionOrb.position;double time=w.SceneTime;w.Advance(1);if(w.SceneTime!=time||w.layers[0].sections[0].position!=before||w.companionOrb.position!=beforeOrb||w.hero.speed!=0)throw new Exception("Pause failed");
            foreach(var id in AssetDatabase.FindAssets("t:Shader",new[]{DreamSceneBuilder.Root}))foreach(var message in ShaderUtil.GetShaderMessages(AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(id))))if(message.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)throw new Exception(message.message);
            File.WriteAllText(Output+"/verification.json",JsonUtility.ToJson(new Report{avatarValid=true,avatarHuman=true,animationLoops=true,heroRootStationary=true,hipHorizontalRange=hipTravel,companionCount=orbs,companionXZDrift=orbDrift,maximumRoadSeam=seam,simulationSeconds=600,handMotion=displacement,pauseFreezesAll=true,shaderErrors=0},true));
            EditorSceneManager.OpenScene(DreamSceneBuilder.ScenePath);Debug.Log("DREAM_VERIFIED_600_SECONDS");
        }
        [Serializable] class Report {public bool avatarValid,avatarHuman,animationLoops,heroRootStationary,pauseFreezesAll;public int companionCount,shaderErrors;public float companionXZDrift,maximumRoadSeam,simulationSeconds,handMotion,hipHorizontalRange;}
        public static void PlayCheck(){DreamSceneBuilder.Open();sampled=false;SessionState.SetBool(CheckKey,true);EditorApplication.EnterPlaymode();}
        public static void Record(){DreamSceneBuilder.Open();World().Initialize();Directory.CreateDirectory(Output+"/Review/Frames");frame=0;recording=true;}
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(recording)
            {
                try
                {var w=World();if(frame==0)for(int i=0;i<180;i++)w.Advance(1f/60);Pose(w.hero,frame/30f);Capture(Output+"/Review/Frames/frame-"+frame.ToString("D4")+".png",1280,720);w.Advance(1f/30);frame++;if(frame>=180){recording=false;EditorSceneManager.OpenScene(DreamSceneBuilder.ScenePath);File.WriteAllText(Output+"/recording-complete.txt","180 frames at 30 fps, 1280 x 720");}}
                catch(Exception e){recording=false;File.WriteAllText(Output+"/recording-error.txt",e.ToString());Debug.LogException(e);}
            }
            if(!SessionState.GetBool(CheckKey,false)||!EditorApplication.isPlaying)return;
            var world=World();if(!world||world.SceneTime<3)return;
            if(!sampled){handStart=world.hero.GetBoneTransform(HumanBodyBones.LeftHand).position;rootStart=world.hero.transform.position;started=world.SceneTime;liveHipMin=liveHipMax=world.hero.GetBoneTransform(HumanBodyBones.Hips).position.z;sampled=true;return;}
            float liveHip=world.hero.GetBoneTransform(HumanBodyBones.Hips).position.z;liveHipMin=Mathf.Min(liveHipMin,liveHip);liveHipMax=Mathf.Max(liveHipMax,liveHip);
            if(world.SceneTime<started+2.37)return;
            try
            {
                float handChange=Vector3.Distance(handStart,world.hero.GetBoneTransform(HumanBodyBones.LeftHand).position);var state=world.hero.GetCurrentAnimatorStateInfo(0);
                float rootDrift=Vector3.Distance(rootStart,world.hero.transform.position);
                if(!world.hero.isInitialized||!state.IsName("Run")||handChange<.01f||world.Distance<10||rootDrift>.001f||liveHipMax-liveHipMin>.20f)throw new Exception("Live running check failed; hip forward range: "+(liveHipMax-liveHipMin));
                Capture(Output+"/Review/Live-PlayMode.png",1920,1080);
                File.WriteAllText(Output+"/play-mode-verification.txt","PASS\nMi Humanoid animator initialized and in Run\nHand displacement: "+handChange.ToString("F3")+" m\nRoot drift: "+rootDrift.ToString("F6")+" m\nHip forward range: "+(liveHipMax-liveHipMin).ToString("F4")+" m\nWorld travelled: "+world.Distance.ToString("F2")+" m\nCompanion offset: "+(world.companionOrb.position-world.hero.transform.position)+"\n");
            }
            catch(Exception e){File.WriteAllText(Output+"/play-mode-verification.txt",e.ToString());Debug.LogException(e);}
            finally{SessionState.SetBool(CheckKey,false);sampled=false;EditorApplication.ExitPlaymode();}
        }
        [MenuItem("Cascade/Dreamworld/Export scene package")]
        public static void Export()
        {
            AssetDatabase.SaveAssets();AssetDatabase.ExportPackage(new[]{DreamSceneBuilder.Root+"/Art",DreamSceneBuilder.Root+"/Generated",DreamSceneBuilder.Root+"/Prefabs",DreamSceneBuilder.Root+"/Runtime",DreamSceneBuilder.Root+"/Shaders",DreamSceneBuilder.ScenePath},Output+"/Cascade-Mi-Dreamworld.unitypackage",ExportPackageOptions.Recurse);Debug.Log("DREAM_PACKAGE_EXPORTED");
        }
    }
}
