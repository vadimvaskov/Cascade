using System.Collections.Generic;
using System.Linq;
using Cascade.Dream;
using Cascade.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static class CascadeEveningStyle
{
    const string Folder="Assets/Cascade/Art/Evening/";
    static Color C(string value)=>CascadeApprovedUI.C(value);
    public static void Apply(DreamRunWorld world)
    {
        var replacements=new Dictionary<Material,Material>();
        foreach(var renderer in world.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(renderer.transform.IsChildOf(world.companionOrb))continue;
            renderer.renderingLayerMask=1;
            renderer.sharedMaterials=renderer.sharedMaterials.Select(source=>
            {
                if(!source)return source;
                if(replacements.TryGetValue(source,out var cached))return cached;
                var copy=Copy(source,source.name);Soften(copy,source.name);replacements.Add(source,copy);return copy;
            }).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        var sky=MaterialAsset("EveningSky",Shader.Find("Cascade/Evening/Sky"));
        sky.SetColor("_Top",C("#3B4F9F"));sky.SetColor("_Horizon",C("#D78FA7"));EditorUtility.SetDirty(sky);
        world.sceneCamera.GetComponent<Skybox>().material=sky;RenderSettings.skybox=sky;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=C("#697FAB");RenderSettings.ambientEquatorColor=C("#7C6D90");RenderSettings.ambientGroundColor=C("#49445E");RenderSettings.ambientIntensity=1;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=C("#A994B8");RenderSettings.fogDensity=.0035f;
        foreach(var light in world.GetComponentsInChildren<Light>(true))
        {
            if(light.name=="Peach sunset key light")
            {light.color=C("#FFD6B6");light.intensity=.42f;light.transform.localRotation=Quaternion.Euler(22,-145,0);light.shadows=LightShadows.Soft;light.shadowStrength=.22f;light.shadowBias=.035f;light.shadowNormalBias=.22f;}
            else if(light.name=="Periwinkle sky fill"){light.color=C("#AABFFF");light.intensity=.14f;}
            else if(light.name=="Character warm rim"){light.color=C("#FFD4A4");light.intensity=2.8f;light.range=6;light.transform.localPosition=new Vector3(-1.5f,3.5f,2);}
            light.cullingMask=(1<<27)|(1<<28);
            var lightData=light.GetUniversalAdditionalLightData();
            lightData.renderingLayers=light.name=="Character warm rim"?2u:3u;
            if(light.name=="Character warm rim")light.cullingMask=1<<28;
            PrefabUtility.RecordPrefabInstancePropertyModifications(lightData);
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform);
        }
        var volume=world.GetComponentInChildren<Volume>(true);volume.sharedProfile=Grade();PrefabUtility.RecordPrefabInstancePropertyModifications(volume);
        // A broad, camera-side fill reveals the runner's hair and costume while
        // the sunset remains the key light. It does not occupy a pooled lamp slot.
        var fill=new GameObject("Soft camera-side fill",typeof(Light));fill.layer=27;fill.transform.SetParent(world.transform,false);
        fill.transform.localRotation=Quaternion.Euler(28,25,0);var fillLight=fill.GetComponent<Light>();
        fillLight.type=LightType.Directional;fillLight.color=C("#FFE5CF");fillLight.intensity=1.15f;fillLight.shadows=LightShadows.None;fillLight.cullingMask=1<<28;
        fillLight.GetUniversalAdditionalLightData().renderingLayers=2u;
        // Keep the runner readable without washing out the lantern-lit bridge.
        foreach(var skin in world.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            skin.gameObject.layer=28;PrefabUtility.RecordPrefabInstancePropertyModifications(skin.gameObject);
            skin.renderingLayerMask=2;
            skin.sharedMaterials=skin.sharedMaterials.Select(source=>
            {
                if(!source)return source;
                if(!replacements.TryGetValue(source,out var copy))
                {
                    copy=Copy(source,"Runner "+source.name);replacements.Add(source,copy);
                    if(copy.HasProperty("_BumpScale"))copy.SetFloat("_BumpScale",.38f);
                    if(copy.HasProperty("_Smoothness"))copy.SetFloat("_Smoothness",.30f);
                    EditorUtility.SetDirty(copy);
                }
                return copy;
            }).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
        }
        world.sceneCamera.cullingMask=(1<<27)|(1<<28);PrefabUtility.RecordPrefabInstancePropertyModifications(world.sceneCamera);
        CascadeSoftScenery.Apply(world);
        AddBridgeLighting(world);
        var data=world.sceneCamera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;data.antialiasingQuality=AntialiasingQuality.High;
        PrefabUtility.RecordPrefabInstancePropertyModifications(data);
    }
    static Material Copy(Material source,string name)
    {
        string path=Folder+name+".mat";var result=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!result){result=new Material(source);AssetDatabase.CreateAsset(result,path);}else EditorUtility.CopySerialized(source,result);
        result.name="Evening "+name;return result;
    }
    static Material MaterialAsset(string name,Shader shader)
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+name+".mat");
        if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,Folder+name+".mat");}
        material.shader=shader;return material;
    }
    static void Soften(Material material,string name)
    {
        if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",name.StartsWith("Gold")?.43f:.29f);
        if(name.StartsWith("Gold")){material.SetFloat("_Metallic",.22f);material.SetColor("_BaseColor",C(name=="Gold"?"#D8AC82":"#EAC49C"));}
        else if(name=="Ivory")material.SetColor("_BaseColor",C("#DDD0DA"));
        else if(name=="Porcelain")material.SetColor("_BaseColor",C("#CAB8CC"));
        else if(name.StartsWith("Petal")){material.shader=Shader.Find("Cascade/Evening/Velvet foliage");material.SetColor("_BaseColor",Color.Lerp(material.GetColor("_BaseColor"),C("#FFC6DE"),.12f));}
        else if(name.StartsWith("Paver")){material.SetFloat("_Smoothness",.35f);material.SetFloat("_Metallic",0);material.SetColor("_BaseColor",Color.Lerp(material.GetColor("_BaseColor"),C("#9B87AF"),.8f));}
        else if(name=="Bark")material.SetColor("_BaseColor",C("#A08AA0"));
        else if(name.StartsWith("Cliff"))material.SetColor("_BaseColor",Color.Lerp(material.GetColor("_BaseColor"),C("#A79CB8"),.25f));
        if(name=="Glass warm"||name=="Window")
        {
            // Dedicated HDR glass: URP's material validation previously cleared
            // the emission keyword, leaving opaque brown lanterns in the player.
            material.shader=Shader.Find("Cascade/Evening/Lantern glass");
            material.SetColor("_BaseColor",C("#FF9C40"));material.SetFloat("_Glow",name=="Glass warm"?2.8f:1.7f);
        }
        if(name=="Cloud"){material.SetColor("_BaseColor",C("#E9CCDF"));material.SetColor("_EmissionColor",C("#D6B9D3")*.12f);}
        EditorUtility.SetDirty(material);
    }
    static VolumeProfile Grade()
    {
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder+"EveningGrade.asset");
        if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Folder+"EveningGrade.asset");}
        T Component<T>() where T:VolumeComponent
        {
            if(!profile.TryGet<T>(out var item)){item=profile.Add<T>(true);AssetDatabase.AddObjectToAsset(item,profile);}EditorUtility.SetDirty(item);return item;
        }
        var bloom=Component<Bloom>();bloom.intensity.Override(.42f);bloom.threshold.Override(1.2f);bloom.scatter.Override(.7f);bloom.clamp.Override(5);
        Component<Tonemapping>().mode.Override(TonemappingMode.ACES);
        var colour=Component<ColorAdjustments>();colour.postExposure.Override(.06f);colour.saturation.Override(3);colour.contrast.Override(-4);
        var vignette=Component<Vignette>();vignette.intensity.Override(.035f);vignette.smoothness.Override(.7f);
        EditorUtility.SetDirty(profile);return profile;
    }
    static void AddBridgeLighting(DreamRunWorld world)
    {
        var material=MaterialAsset("BridgeGlow",Shader.Find("Cascade/Evening/Lantern glass"));
        material.SetColor("_BaseColor",C("#FFAB5F"));material.SetFloat("_Glow",1.05f);EditorUtility.SetDirty(material);
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"BridgeRibbon.asset");
        if(!mesh){mesh=DreamGeometry.Rounded(new Vector3(.10f,.06f,32),.025f);AssetDatabase.CreateAsset(mesh,Folder+"BridgeRibbon.asset");}
        var lamps=new List<Light>();
        foreach(var section in world.layers[0].sections)
        {
            foreach(int side in new[]{-1,1})
            {
                var ribbon=new GameObject("Warm bridge edge",typeof(MeshFilter),typeof(MeshRenderer));ribbon.layer=27;ribbon.transform.SetParent(section,false);ribbon.transform.localPosition=new Vector3(side*2.69f,.18f,16);
                ribbon.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=ribbon.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                for(int i=0;i<4;i++)
                {
                    var glow=new GameObject("Lantern light");glow.layer=27;glow.transform.SetParent(section,false);glow.transform.localPosition=new Vector3(side*2.60f,1.77f,2+i*8);
                    var light=glow.AddComponent<Light>();light.type=LightType.Point;light.color=C("#FFD09A");light.intensity=5.8f;light.range=5.8f;light.shadows=LightShadows.None;light.cullingMask=(1<<27)|(1<<28);light.GetUniversalAdditionalLightData().renderingLayers=3u;light.enabled=false;lamps.Add(light);
                }
            }
        }
        var pool=world.gameObject.AddComponent<CascadeBridgeLightPool>();pool.lamps=lamps.ToArray();pool.viewer=world.sceneCamera.transform;pool.maximumActive=8;
    }
}
