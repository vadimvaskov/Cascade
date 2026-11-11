using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cascade.Dream
{
    public static partial class DreamSceneBuilder
    {
        static void Tree(Transform parent,Vector3 position,float scale,int variation)
        {
            var tree=Group("Blossom tree | curved branches",parent,position);tree.localScale=Vector3.one*scale;tree.localRotation=Quaternion.Euler(0,variation*47,0);
            Curve("Sculpted trunk",tree,Vector3.zero,new Vector3(-.35f,1.2f,.1f),new Vector3(.4f,2.6f,0),new Vector3(.2f,3.7f,.15f),.32f,.09f,"Bark",28);
            for(int i=0;i<5;i++)
            {
                float a=i*2.4f+variation*.5f;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var end=dir*R(1.1f,1.8f)+Vector3.up*R(2.8f,3.8f);
                Curve("Graceful bough",tree,new Vector3(0,1.5f+i*.15f,0),dir*.35f+Vector3.up*2.3f,dir*1.4f+Vector3.up*(end.y-.5f),end,.17f,.035f,"Bark");
                var canopy=Group("Layered blossom crown",tree,end);
                for(int j=0;j<5;j++)
                {
                    float aa=j*2.39f;var p=new Vector3(Mathf.Cos(aa)*.58f,R(-.04f,.3f),Mathf.Sin(aa)*.5f);
                    Piece("Soft sculpted foliage",canopy,DreamSceneBuilder.canopy,mats["Petal"+((i+j+variation)%4)],p,new Vector3(R(.65f,1.0f),R(.50f,.72f),R(.6f,.85f)),Quaternion.Euler(R(-12,12),R(0,360),R(-12,12)));
                }
                // Small leaf shapes break the crown outline; large coherent volumes carry the silhouette.
                for(int j=0;j<(scale<1?24:56);j++)
                {
                    float aa=R(0,Mathf.PI*2),rr=R(.25f,1.1f);var p=new Vector3(Mathf.Cos(aa)*rr,R(.22f,.65f),Mathf.Sin(aa)*rr*.8f);
                    Piece("Blossom petal",canopy,petal,mats["Petal"+rng.Next(1,4)],p,new Vector3(.13f,.035f,.075f),Quaternion.Euler(R(-30,30),R(0,360),R(-20,20)));
                }
            }
            for(int k=0;k<4;k++){float a=k*Mathf.PI*.5f;var p=new Vector3(Mathf.Cos(a)*.9f,.04f,Mathf.Sin(a)*.9f);Curve("Tree root",tree,new Vector3(0,.25f,0),p*.3f+Vector3.up*.1f,p*.75f,p,.16f,.015f,"Bark",14);}
        }
        static void Flowers(Transform parent,Vector3 p,float scale,int color)
        {
            var g=Group("Little bell flowers",parent,p);g.localScale=Vector3.one*scale;
            for(int i=0;i<5;i++)
            {float a=i*2.39f;var top=new Vector3(Mathf.Cos(a)*.24f,R(.28f,.55f),Mathf.Sin(a)*.24f);Tube("Flower stem",g,new[]{new Vector3(top.x,0,top.z),top},.014f,.008f,"Moss");for(int j=0;j<5;j++){float b=j*Mathf.PI*2/5;Piece("Flower petal",g,petal,mats["Petal"+color],top+new Vector3(Mathf.Cos(b)*.07f,.02f,Mathf.Sin(b)*.07f),new Vector3(.09f,.03f,.055f),Quaternion.Euler(0,-b*Mathf.Rad2Deg,20));}Blob("Golden flower heart",g,top+Vector3.up*.04f,Vector3.one*.035f,"Gold rim");}
        }
        static void Island(Transform parent,Vector3 p,float radius,int variant,bool city)
        {
            var island=Group(city?"Floating music village":"Floating blossom garden",parent,p);
            // Smooth carved strata and rounded shoulders replace pointed low-poly fragments.
            Piece("Suspended carved island",island,DreamGeometry.IslandBody(radius,false),mats["Cliff"],Vector3.zero,Vector3.one);
            Piece("Organic moss plateau",island,DreamGeometry.IslandBody(radius*.98f,true),mats["Moss"],Vector3.zero,Vector3.one);
            for(int i=0;i<11;i++)
            {
                float a=i*Mathf.PI*2/11,rr=radius*R(.64f,.82f);
                Blob("Round weathered outcrop",island,new Vector3(Mathf.Cos(a)*rr,-R(1.5f,2.8f),Mathf.Sin(a)*rr*.88f),new Vector3(R(.55f,.85f),R(.7f,1.3f),R(.5f,.9f)),i%3==0?"Cliff lit":"Cliff",Quaternion.Euler(R(-12,12),R(0,180),R(-12,12)));
                if(i%2==0)Blob("Moss overhang",island,new Vector3(Mathf.Cos(a)*radius*.83f,.05f,Mathf.Sin(a)*radius*.78f),new Vector3(.85f,.16f,.55f),"Moss bright");
            }
            for(int i=0;i<6;i++){float a=i*2.4f;Blob("Smooth garden pebble",island,new Vector3(Mathf.Cos(a)*radius*.72f,.16f,Mathf.Sin(a)*radius*.64f),new Vector3(R(.25f,.6f),R(.16f,.35f),R(.3f,.6f)),"Warm sandstone");}
            if(city)
            {
                House(island,new Vector3(-radius*.3f,.17f,0),1.45f,variant);
                House(island,new Vector3(radius*.3f,.17f,radius*.15f),1.15f,variant+1);
                House(island,new Vector3(0,.17f,radius*.42f),1.8f,variant+2);
                MusicPavilion(island,new Vector3(.2f,.15f,-radius*.35f),1.1f);
                Tree(island,new Vector3(-radius*.62f,.14f,radius*.25f),.85f,variant);
                Tree(island,new Vector3(radius*.55f,.14f,-radius*.25f),.7f,variant+1);
            }
            else
            {
                Tree(island,new Vector3(-radius*.25f,.12f,.5f),1.1f+variant%3*.14f,variant);
                Tree(island,new Vector3(radius*.4f,.12f,.8f),.68f+variant%2*.2f,variant+2);
                if(variant%3==0)MusicPavilion(island,new Vector3(radius*.15f,.12f,-radius*.32f),.7f);
                for(int i=0;i<6;i++){float a=i*2.4f;Flowers(island,new Vector3(Mathf.Cos(a)*radius*.6f,.18f,Mathf.Sin(a)*radius*.55f),R(.8f,1.35f),i%3+1);}
            }
            float waterX=variant%2==0?-radius*.28f:radius*.25f;
            Piece("Silken flowing waterfall",island,DreamGeometry.Water(radius*.36f,11+radius*.7f),mats["Water"],new Vector3(waterX,.07f,-radius*.85f),Vector3.one);
            Blob("Stream lip",island,new Vector3(waterX,.08f,-radius*.74f),new Vector3(radius*.2f,.035f,radius*.22f),"Aqua");
            CloudBank(island,new Vector3(0,-radius*1.6f,0),radius*.7f,variant);
        }
        static void CloudBank(Transform parent,Vector3 p,float size,int seed)
        {
            var cloud=Group("Low soft cloud bank",parent,p);
            for(int i=0;i<7;i++){float a=i*2.4f;var q=new Vector3(Mathf.Cos(a)*size*.7f,R(-.4f,.5f),Mathf.Sin(a)*size*.4f);Blob("Cloud billow",cloud,q,new Vector3(size*R(.55f,1.2f),size*R(.22f,.38f),size*R(.5f,.9f)),"Cloud");}
        }
        static void House(Transform parent,Vector3 p,float scale,int variant)
        {
            var g=Group("Music-box house",parent,p);g.localScale=Vector3.one*scale;g.localRotation=Quaternion.Euler(0,(variant%3-1)*15,0);
            Tube("Round porcelain walls",g,new[]{new Vector3(0,.1f,0),new Vector3(0,2.7f,0)},1.12f,.94f,"Porcelain",40);
            var roofMat=new[]{"Roof lavender","Roof rose","Roof teal"}[variant%3];
            Piece("Curved music-box roof",g,DreamGeometry.Dome(1.4f,1.65f),mats[roofMat],new Vector3(0,2.62f,0),Vector3.one);
            Piece("Gold eaves",g,DreamGeometry.Ring(1.38f,.055f),mats["Gold rim"],new Vector3(0,2.62f,0),Vector3.one,Quaternion.Euler(90,0,0));
            Piece("Roof finial note",g,note,mats["Gold rim"],new Vector3(0,4.65f,0),Vector3.one*.85f);
            for(int level=0;level<2;level++)for(int j=-1;j<=1;j++)
            {float a=j*.8f;Vector3 q=new Vector3(Mathf.Sin(a)*1.01f,.85f+level*1.05f,-Mathf.Cos(a)*1.01f);Box("Rounded gold window frame",g,q,new Vector3(.45f,.7f,.14f),"Gold",.12f,Quaternion.Euler(0,-a*Mathf.Rad2Deg,0));Box("Amber arched window",g,q+new Vector3(Mathf.Sin(a)*.08f,0,-Mathf.Cos(a)*.08f),new Vector3(.32f,.53f,.08f),"Window",.13f,Quaternion.Euler(0,-a*Mathf.Rad2Deg,0));}
            for(int i=0;i<5;i++){float y=.23f+i*.10f;Piece("Musical staff frieze",g,DreamGeometry.Ring(1.14f,.012f),mats["Gold"],new Vector3(0,y,0),Vector3.one,Quaternion.Euler(90,0,0));}
            Box("Doorway",g,new Vector3(0,.47f,-1.13f),new Vector3(.5f,.9f,.15f),"Shadow stone",.18f);
            Blob("Door handle",g,new Vector3(.12f,.46f,-1.25f),Vector3.one*.045f,"Gold rim");
        }
        static void MusicPavilion(Transform parent,Vector3 p,float size)
        {
            var g=Group("Golden harp pavilion",parent,p);g.localScale=Vector3.one*size;
            Box("Pavilion plinth",g,new Vector3(0,.12f,0),new Vector3(3.7f,.24f,1.4f),"Porcelain",.14f);
            foreach(int s in new[]{-1,1})Curve("Harp shoulder",g,new Vector3(s*1.45f,.2f,0),new Vector3(s*1.65f,2.1f,0),new Vector3(s*1.2f,3.6f,0),new Vector3(0,3.8f,0),.17f,.11f,"Gold rim",36);
            for(int i=-5;i<=5;i++){float x=i*.22f;Tube("Harp string",g,new[]{new Vector3(x,.4f,.02f),new Vector3(x,3.5f-Mathf.Abs(x)*.42f,.02f)},.008f,.008f,"Gold");}
            Piece("Pavilion musical heart",g,clef,mats["Gold rim"],new Vector3(0,2.0f,-.1f),Vector3.one*2.4f);
        }
        static void Lamp(Transform parent,float x,float z)
        {
            int s=x<0?-1:1;var g=Group("Curved lantern",parent,new Vector3(x,0,z));
            Box("Lantern pedestal",g,new Vector3(0,.21f,0),new Vector3(.42f,.42f,.42f),"Porcelain",.07f);
            Curve("Swan neck brass",g,new Vector3(0,.35f,0),new Vector3(s*.12f,2.1f,0),new Vector3(-s*.7f,2.5f,0),new Vector3(-s*.55f,1.94f,0),.045f,.035f,"Gold",30);
            Blob("Warm lantern glass",g,new Vector3(-s*.55f,1.77f,0),new Vector3(.15f,.21f,.15f),"Glass warm");
            Piece("Lantern little roof",g,DreamGeometry.Dome(.22f,.12f,20,8),mats["Gold"],new Vector3(-s*.55f,1.94f,0),Vector3.one);
            for(int k=0;k<4;k++){float a=k*Mathf.PI*.5f;Tube("Lantern frame",g,new[]{new Vector3(-s*.55f+Mathf.Cos(a)*.15f,1.95f,Mathf.Sin(a)*.15f),new Vector3(-s*.55f+Mathf.Cos(a)*.11f,1.57f,Mathf.Sin(a)*.11f)},.012f,.012f,"Gold");}
        }
        static void Arch(Transform parent,Vector3 p)
        {
            var g=Group("Symphony gateway",parent,p);
            foreach(int s in new[]{-1,1})
            {
                Box("Carved arch base",g,new Vector3(s*3.15f,.28f,0),new Vector3(.95f,.56f,1.2f),"Ivory",.12f);
                Curve("Sculpted ivory arch",g,new Vector3(s*3.15f,.4f,0),new Vector3(s*3.6f,4.3f,0),new Vector3(s*2.9f,6.6f,0),new Vector3(0,6.85f,0),.24f,.20f,"Ivory",40);
                Curve("Fine golden arch inlay",g,new Vector3(s*3.15f,.45f,-.24f),new Vector3(s*3.6f,4.3f,-.24f),new Vector3(s*2.9f,6.6f,-.24f),new Vector3(0,6.85f,-.24f),.025f,.025f,"Gold rim",40);
                for(int k=0;k<3;k++)Box("Arch gold collar",g,new Vector3(s*3.25f,1+k*.34f,0),new Vector3(.55f,.055f,.56f),"Gold",.025f);
                Blob("Garden on pedestal",g,new Vector3(s*3.5f,.5f,.35f),new Vector3(.65f,.3f,.6f),"Moss");Flowers(g,new Vector3(s*3.4f,.6f,.25f),1.2f,3);
            }
            Piece("Gateway musical crest",g,DreamGeometry.Ring(.6f,.045f),mats["Gold rim"],new Vector3(0,6.45f,-.25f),Vector3.one);
            Piece("Gateway clef",g,clef,mats["Gold rim"],new Vector3(0,6.43f,-.26f),Vector3.one*1.35f);
        }
        static void Bridge(Transform parent,int section)
        {
            Box("Bridge understructure",parent,new Vector3(0,-.45f,16),new Vector3(5.95f,.7f,32),"Shadow stone",.25f);
            for(int row=0;row<20;row++)for(int col=0;col<5;col++)
            {float z=.8f+row*1.6f;Box("Soft edged paving",parent,new Vector3((col-2)*1.10f,-.045f,z),new Vector3(1.083f,.16f,1.58f),"Paver"+((row/3+col+section)%4),.036f);}
            foreach(int s in new[]{-1,1})
            {
                Box("Continuous curved curb",parent,new Vector3(s*2.88f,.075f,16),new Vector3(.30f,.34f,32),"Porcelain",.1f);
                Box("Warm light ribbon",parent,new Vector3(s*2.70f,.08f,16),new Vector3(.04f,.035f,32),"Glass warm",.012f);
                Tube("Polished handrail",parent,new[]{new Vector3(s*3.04f,1.1f,0),new Vector3(s*3.04f,1.1f,32)},.065f,.065f,"Gold rim");
                Tube("Rail foot",parent,new[]{new Vector3(s*3.04f,.38f,0),new Vector3(s*3.04f,.38f,32)},.035f,.035f,"Gold");
                for(int j=0;j<8;j++)
                {
                    float z=j*4;
                    Box("Rounded ivory baluster",parent,new Vector3(s*3.04f,.58f,z),new Vector3(.24f,1.16f,.24f),"Ivory",.07f);
                    Blob("Baluster crown",parent,new Vector3(s*3.04f,1.18f,z),new Vector3(.16f,.075f,.16f),"Gold rim");
                    Curve("Rail musical flourish",parent,new Vector3(s*3.04f,.43f,z),new Vector3(s*3.04f,1.12f,z+1.2f),new Vector3(s*3.04f,1.12f,z+2.8f),new Vector3(s*3.04f,.43f,z+4),.022f,.022f,"Gold");
                    if(j%2==0)Lamp(parent,s*3.15f,z+2);
                }
            }
            Island(parent,new Vector3(-11.6f-section%2*1.7f,-.6f,8),4.7f,section,false);
            Island(parent,new Vector3(12.4f+section%2*1.5f,-.3f,26),5.2f,section+3,false);
            if(section==2||section==6)Arch(parent,new Vector3(0,0,23));
        }
        static void MakeOrb(Transform root,DreamRunWorld world)
        {
            var orb=Group("Companion music sphere",root,new Vector3(-.30f,1.65f,3.8f));world.companionOrb=orb;
            Piece("Blue musical glass",orb,sphere,mats["Glass blue"],Vector3.zero,Vector3.one*.29f);
            Piece("Bright note facing runner",orb,note,mats["Aqua"],new Vector3(-.025f,-.025f,-.285f),Vector3.one*.75f);
            Piece("Orb rim",orb,DreamGeometry.Ring(.295f,.008f),mats["Aqua"],new Vector3(0,0,-.03f),Vector3.one);
            var halo=Piece("Companion orbit",orb,DreamGeometry.Ring(.37f,.009f),mats["Gold rim"],Vector3.zero,Vector3.one,Quaternion.Euler(74,0,12));world.orbHalo=halo.transform;
            // One sphere outside pooled sections: it stays ahead of the runner for the entire level.
            var light=Group("Companion soft light",orb,Vector3.zero).gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=C("#55CFE9");light.range=2;light.intensity=.5f;light.shadows=LightShadows.None;
            PrefabUtility.SaveAsPrefabAsset(orb.gameObject,Root+"/Prefabs/CompanionOrb.prefab");
        }
        static void Lighting(Transform root,DreamRunWorld world)
        {
            var cameraObject=Group("Dreamworld camera",root,new Vector3(.30f,3.1f,-5.8f)).gameObject;
            var cam=cameraObject.AddComponent<Camera>();cameraObject.tag="MainCamera";cameraObject.AddComponent<AudioListener>();cam.transform.LookAt(new Vector3(0,1.85f,14));cam.fieldOfView=53;cam.nearClipPlane=.1f;cam.farClipPlane=360;cam.allowHDR=true;
            var cd=cameraObject.AddComponent<UniversalAdditionalCameraData>();cd.renderPostProcessing=true;cd.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;cd.antialiasingQuality=AntialiasingQuality.High;world.sceneCamera=cam;
            var sun=Group("Peach sunset key light",root).gameObject.AddComponent<Light>();sun.type=LightType.Directional;sun.color=C("#FFD2AE");sun.intensity=1.8f;sun.transform.rotation=Quaternion.Euler(24,-145,0);sun.shadows=LightShadows.Soft;sun.shadowStrength=.7f;sun.shadowBias=.03f;sun.shadowNormalBias=.18f;
            var fill=Group("Periwinkle sky fill",root).gameObject.AddComponent<Light>();fill.type=LightType.Directional;fill.color=C("#BDD0FF");fill.intensity=.65f;fill.transform.rotation=Quaternion.Euler(45,25,0);fill.shadows=LightShadows.None;
            var rim=Group("Character warm rim",root,new Vector3(1,3.5f,2)).gameObject.AddComponent<Light>();rim.type=LightType.Point;rim.color=C("#FFE3BA");rim.intensity=2.1f;rim.range=7;
            RenderSettings.sun=sun;RenderSettings.skybox=mats["Sky"];RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=C("#A4B5DB");RenderSettings.ambientEquatorColor=C("#B3A5C9");RenderSettings.ambientGroundColor=C("#716584");RenderSettings.ambientIntensity=1;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=C("#BAAED1");RenderSettings.fogDensity=.006f;
            var v=Group("Gentle film grade",root).gameObject.AddComponent<Volume>();v.isGlobal=true;var profile=ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.24f);bloom.threshold.Override(1.2f);bloom.scatter.Override(.65f);bloom.clamp.Override(4);
            var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);
            var color=profile.Add<ColorAdjustments>(true);color.postExposure.Override(-.02f);color.saturation.Override(5);color.contrast.Override(10);
            var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(.11f);vignette.smoothness.Override(.55f);
            string profilePath=Root+"/Generated/DreamGrade.asset";var old=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if(old){foreach(var sub in AssetDatabase.LoadAllAssetsAtPath(profilePath))if(sub!=old)UnityEngine.Object.DestroyImmediate(sub,true);EditorUtility.CopySerialized(profile,old);UnityEngine.Object.DestroyImmediate(profile);profile=old;}else AssetDatabase.CreateAsset(profile,profilePath);
            foreach(var component in profile.components)if(!AssetDatabase.Contains(component))AssetDatabase.AddObjectToAsset(component,profile);v.sharedProfile=profile;
        }
    }
}
