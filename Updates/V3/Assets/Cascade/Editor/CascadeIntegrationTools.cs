using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cascade.Prototype;
using Cascade.Dream;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CascadeIntegrationTools
{
    public const string ScenePath="Assets/Scenes/CascadeGame.unity";
    const string FaPath="Assets/Cascade/Art/FaRodin/Fa_Rodin.fbx";
    public static string Output=>Path.GetFullPath("Documentation/IntegrationReview");
    static double next;
    static readonly Dictionary<string,string> PlayerFolders=new Dictionary<string,string>
    {
        {"player","DesktopReview"},{"sunset-player","SunsetReview"},{"evening-player","EveningReview"},
        {"clear-evening-player","ClearEveningReview"},{"glass-player","GlassMenuReview"},
        {"lantern-player","LanternRhythmReview"},{"pulse-player","ContinuousPulseReview"},
        {"aura-player","ScreenFieldAuraReview"},{"energy-player","CompanionEnergyReview"},
        {"volume-player","CompanionVolumeReview"}
    };
    static CascadeIntegrationTools(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        next=EditorApplication.timeSinceStartup+.5;
        const string request="Temp/CascadeIntegration.request";if(!File.Exists(request))return;
        string command=File.ReadAllText(request).Trim();File.Delete(request);Directory.CreateDirectory(Output);
        try
        {
            string result="OK";
            if(command=="build"||command=="evening-build")Build();
            else if(command=="check")result=CascadeIntegrationChecks.Run();
            else if(command=="releasecheck")result=CascadeReleaseChecks.Run();
            else if(command=="playcheck")CascadeIntegrationPlayChecks.Start();
            else if(PlayerFolders.TryGetValue(command,out string folder))
            {
                if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode before building a player");
                string playerPath=Path.Combine("Builds",folder,"Cascade.exe");
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=playerPath,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
                result=report.summary.result+" · "+report.summary.totalErrors+" errors · "+report.summary.totalWarnings+" warnings · "+report.summary.totalSize+" bytes";
                File.WriteAllText(Path.Combine(Output,command+"-build.txt"),result);if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception(result);
            }
            else if(command=="demo"){EditorSceneManager.OpenScene(ScenePath);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);EditorApplication.EnterPlaymode();}
            else if(command=="stop")EditorApplication.ExitPlaymode();
            else if(command.StartsWith("capture:"))ScreenCapture.CaptureScreenshot(Output+"/"+command.Substring(8)+".png");
            else throw new Exception("Unknown integration command: "+command);
            File.WriteAllText("Temp/CascadeIntegration.result",command+": "+result);
        }
        catch(Exception e){File.WriteAllText("Temp/CascadeIntegration.result",command+": "+e);Debug.LogException(e);}
    }
    [MenuItem("Cascade/Approved game/Build integrated scene")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode before building");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new Exception("Current scene contains unsaved changes");
        AssetDatabase.Refresh();
        foreach(var path in Directory.GetFiles("Assets/Cascade/Resources/Approved","*.png",SearchOption.AllDirectories))
        {
            var ti=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));ti.textureType=TextureImporterType.Default;ti.sRGBTexture=true;ti.alphaIsTransparency=true;ti.mipmapEnabled=false;
            ti.npotScale=TextureImporterNPOTScale.None;ti.maxTextureSize=2048;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.wrapMode=TextureWrapMode.Clamp;ti.filterMode=FilterMode.Bilinear;ti.SaveAndReimport();
        }
        ImportFa();
        var scene=EditorSceneManager.OpenScene(CascadeSceneTools.Path,OpenSceneMode.Single);var game=Object.FindAnyObjectByType<CascadeGame>();
        if(!game)throw new Exception("Base game scene is missing");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CascadeDream/Prefabs/MiDreamworld.prefab");
        var worldObject=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);worldObject.transform.SetParent(game.transform,false);var world=worldObject.GetComponent<DreamRunWorld>();
        var fa=CreateFa(world.transform,world.hero.transform.localPosition);fa.gameObject.SetActive(false);
        foreach(var t in world.GetComponentsInChildren<Transform>(true))t.gameObject.layer=27;
        foreach(var listener in world.GetComponentsInChildren<AudioListener>(true))Object.DestroyImmediate(listener);
        foreach(var light in world.GetComponentsInChildren<Light>(true))light.cullingMask=1<<27;
        world.sceneCamera.cullingMask=1<<27;world.sceneCamera.tag="Untagged";world.sceneCamera.depth=-10;
        world.sceneCamera.GetUniversalAdditionalCameraData().volumeLayerMask=1<<27;
        world.sceneCamera.gameObject.AddComponent<Skybox>().material=AssetDatabase.LoadAssetAtPath<Material>("Assets/CascadeDream/Generated/DreamSky.mat");
        world.enableDemoKeys=false;world.enabled=false;world.reducedMotion=true;
        RenderSettings.skybox=AssetDatabase.LoadAssetAtPath<Material>("Assets/CascadeDream/Generated/DreamSky.mat");RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=CascadeApprovedUI.C("#A4B5DB");RenderSettings.ambientEquatorColor=CascadeApprovedUI.C("#B3A5C9");RenderSettings.ambientGroundColor=CascadeApprovedUI.C("#716584");RenderSettings.ambientIntensity=1;
        RenderSettings.sun=world.GetComponentsInChildren<Light>().First(l=>l.name=="Peach sunset key light");
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=CascadeApprovedUI.C("#BAAED1");RenderSettings.fogDensity=.006f;
        CascadeEveningStyle.Apply(world);
        game.BuildApprovedVisuals(world,fa);game.name="Cascade | approved musical world";
        EditorSceneManager.SaveScene(scene,ScenePath);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);AssetDatabase.SaveAssets();
        File.WriteAllText(Output+"/build.txt","Integrated scene: "+ScenePath+"\nMi + Fa humanoid avatars, single companion orb, approved home/menu/board, free sixteenth grid.\n");
    }
    static void ImportFa()
    {
        AssetDatabase.ImportAsset(FaPath,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(FaPath);importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.animationCompression=ModelImporterAnimationCompression.Off;importer.optimizeGameObjects=false;
        var run=importer.defaultClipAnimations.First(c=>c.name.Contains("000067"));run.name="Run";run.loopTime=true;run.loopPose=true;run.lockRootRotation=true;run.lockRootHeightY=true;run.lockRootPositionXZ=false;run.keepOriginalOrientation=true;run.keepOriginalPositionXZ=false;run.keepOriginalPositionY=false;run.heightFromFeet=true;
        importer.clipAnimations=new[]{run};importer.SaveAndReimport();
        foreach(var path in Directory.GetFiles("Assets/Cascade/Art/FaRodin","*.png"))
        {
            var ti=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));ti.textureType=path.Contains("normal")?TextureImporterType.NormalMap:TextureImporterType.Default;ti.sRGBTexture=!path.Contains("normal");ti.maxTextureSize=2048;ti.mipmapEnabled=true;ti.textureCompression=TextureImporterCompression.CompressedHQ;ti.SaveAndReimport();
        }
    }
    static Animator CreateFa(Transform parent,Vector3 location)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FaPath));go.name="Fa | approved Rodin runner";go.transform.SetParent(parent,false);
        var animator=go.GetComponent<Animator>();if(!animator||!animator.avatar||!animator.avatar.isValid||!animator.avatar.isHuman)throw new Exception("Fa avatar import is invalid");
        const string controllerPath="Assets/Cascade/Art/FaRodin/FaRun.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)??AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine=controller.layers[0].stateMachine;var state=machine.states.FirstOrDefault(x=>x.state.name=="Run").state??machine.AddState("Run");state.motion=AssetDatabase.LoadAllAssetsAtPath(FaPath).OfType<AnimationClip>().First(c=>c.name=="Run");machine.defaultState=state;
        animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        const string materialPath="Assets/Cascade/Art/FaRodin/Fa.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,materialPath);}
        mat.SetColor("_BaseColor",Color.white);mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Cascade/Art/FaRodin/texture_diffuse.png"));mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Cascade/Art/FaRodin/texture_normal.png"));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.65f);mat.SetFloat("_Metallic",.08f);mat.SetFloat("_Smoothness",.28f);
        foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>()){r.sharedMaterials=r.sharedMaterials.Select(_=>mat).ToArray();r.updateWhenOffscreen=true;}
        var renderers=go.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);go.transform.localScale=Vector3.one*(1.85f/bounds.size.y);
        bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);go.transform.localPosition=new Vector3(location.x,.04f-bounds.min.y,location.z);EditorUtility.SetDirty(mat);return animator;
    }
}
