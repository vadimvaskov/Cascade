using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Cascade.Sunset
{
 public static class SunsetSceneTools
 {
  public static string Output=>Path.GetFullPath("Builds/SunsetPreviews");
  public static SunsetRunWorld World()=>Object.FindAnyObjectByType<SunsetRunWorld>();
  public static void Open(){if(!World())EditorSceneManager.OpenScene(SunsetSceneBuilder.ScenePath);}
  public static AnimationClip Clip()=>AssetDatabase.LoadAllAssetsAtPath("Assets/CascadeSunset/Art/Fa/Fa_Run.fbx").OfType<AnimationClip>().First(c=>c.name=="Run");
  public static void Capture(string file,int width,int height)
  {
   Open();Directory.CreateDirectory(Path.GetDirectoryName(file));var camera=World().sceneCamera;var oldTarget=camera.targetTexture;float oldAspect=camera.aspect;camera.aspect=(float)width/height;
   var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.Create();var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};
   try
   {
    RenderPipeline.SubmitRenderRequest(camera,request);var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());Object.DestroyImmediate(image);RenderTexture.active=previous;
   }
   finally{camera.targetTexture=oldTarget;camera.aspect=oldAspect;rt.Release();Object.DestroyImmediate(rt);}
  }
  [MenuItem("Cascade/Sunset/Capture preview")]
  public static void Preview()
  {
   Open();var w=World();w.Initialize();Clip().SampleAnimation(w.hero.gameObject,.18f);Capture(Output+"/SunsetBridge.png",1600,900);Capture(Output+"/SunsetBridge-portrait.png",900,1200);Debug.Log("SUNSET_CAPTURED");
  }
  [MenuItem("Cascade/Sunset/Check movement and animation")]
  public static void Verify()
  {
   Open();var w=World();w.Initialize();var originalPositions=w.roadSections.Select(t=>t.localPosition).ToArray();var originalHero=w.hero.transform.position;var clip=Clip();
   if(!w.hero.avatar.isHuman||!w.hero.avatar.isValid||!clip.isLooping||w.hero.applyRootMotion)throw new Exception("Hero import is not a looped in-place Humanoid");
   var hand=w.hero.GetBoneTransform(HumanBodyBones.LeftHand);clip.SampleAnimation(w.hero.gameObject,.1f);var firstHand=hand.position;clip.SampleAnimation(w.hero.gameObject,.35f);float handMotion=Vector3.Distance(firstHand,hand.position);if(handMotion<.02f)throw new Exception("Run animation does not change hand pose");
   float firstZ=w.roadSections[1].position.z;w.Advance(1);if(Mathf.Abs(w.roadSections[1].position.z-firstZ+w.speed)>.001f)throw new Exception("World moves in the wrong direction");
   float maxSeam=0;
   for(int frame=0;frame<60*180;frame++)
   {
    w.Advance(1f/60);var zs=w.roadSections.Select(t=>t.localPosition.z).OrderBy(z=>z).ToArray();for(int i=1;i<zs.Length;i++)maxSeam=Mathf.Max(maxSeam,Mathf.Abs(zs[i]-zs[i-1]-w.sectionLength));
   }
   if(maxSeam>.03f)throw new Exception("Road segment seam exceeds tolerance: "+maxSeam);
   var beforePause=w.roadSections[0].position;float timeBefore=w.SceneTime;w.SetPaused(true);w.Advance(1);if(w.roadSections[0].position!=beforePause||w.SceneTime!=timeBefore||w.hero.speed!=0)throw new Exception("Pause does not freeze world and hero");w.SetPaused(false);
   if(Vector3.Distance(w.hero.transform.position,originalHero)>.001f)throw new Exception("Hero root drifted");
   var shaderMessages=new System.Collections.Generic.List<string>();foreach(var shader in AssetDatabase.FindAssets("t:Shader",new[]{"Assets/CascadeSunset"}).Select(g=>AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(g))))foreach(var m in ShaderUtil.GetShaderMessages(shader))if(m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)shaderMessages.Add(shader.name+": "+m.message);
   if(shaderMessages.Count>0)throw new Exception(string.Join("\n",shaderMessages));
   File.WriteAllText(Output+"/verification.json",JsonUtility.ToJson(new Report{animationLoops=clip.isLooping,avatarHuman=w.hero.avatar.isHuman,animationLength=clip.length,handMotion=handMotion,simulationSeconds=181,maximumRoadSeam=maxSeam,worldMovesTowardsCamera=true,pauseFreezesWorldAndHero=true,heroRootStationary=true,shaderErrors=0},true));
   EditorSceneManager.OpenScene(SunsetSceneBuilder.ScenePath);Debug.Log("SUNSET_VERIFIED: 181 seconds / wrapped road / run pose / pause / shader compilation");
  }
  [Serializable]class Report{public bool animationLoops,avatarHuman,worldMovesTowardsCamera,pauseFreezesWorldAndHero,heroRootStationary;public float animationLength,handMotion,simulationSeconds,maximumRoadSeam;public int shaderErrors;}
  [MenuItem("Cascade/Sunset/Export Unity package")]
  public static void Export(){Directory.CreateDirectory(Output);AssetDatabase.SaveAssets();AssetDatabase.ExportPackage(new[]{"Assets/CascadeSunset/Art","Assets/CascadeSunset/Generated","Assets/CascadeSunset/Runtime","Assets/CascadeSunset/Shaders","Assets/CascadeSunset/SunsetBridge.unity","Assets/CascadeSunset/Editor/SunsetSceneBuilder.cs","Assets/CascadeSunset/Editor/SunsetMeshFactory.cs","Assets/CascadeSunset/Editor/SunsetSceneTools.cs"},Output+"/Cascade-Sunset-Bridge.unitypackage",ExportPackageOptions.Recurse);Debug.Log("SUNSET_EXPORTED");}
  [MenuItem("Cascade/Sunset/Build Windows preview")]
  public static void BuildPlayer()
  {
   Directory.CreateDirectory(Output+"/Windows");var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{SunsetSceneBuilder.ScenePath},locationPathName=Output+"/Windows/Cascade Sunset.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
   if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows scene build failed: "+report.summary.result);Debug.Log("SUNSET_PLAYER_BUILT");
  }
 }
}
