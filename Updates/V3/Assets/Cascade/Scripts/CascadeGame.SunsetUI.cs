#if UNITY_EDITOR
using System.Linq;
using Cascade.Dream;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using A=Cascade.Prototype.CascadeApprovedUI;

namespace Cascade.Prototype
{
    public sealed partial class CascadeGame
    {
        // Native reconstruction of the approved Sunset gameplay frame. Decorative assets
        // are the exact exported Figma vectors; the upper viewport remains live 3D.
        public void BuildApprovedVisuals(DreamRunWorld world,Animator fa)
        {
            approvedDesign=true;dreamWorld=world;miRunner=world.hero;faRunner=fa;
            logoFont=AssetDatabase.LoadAssetAtPath<Font>(ArtFolder+"Fonts/CormorantGaramond-Italic.ttf");
            var oldBackground=rasterBackground.GetComponent<Image>();if(oldBackground)DestroyImmediate(oldBackground);
            var atmosphere=rasterBackground.gameObject.GetComponent<CascadePanelGraphic>()??rasterBackground.gameObject.AddComponent<CascadePanelGraphic>();
            atmosphere.color=atmosphere.rightColor=A.C("#38205E");atmosphere.radius=0;atmosphere.raycastTarget=false;
            foreach(Transform child in hud.transform.Cast<Transform>().ToArray())DestroyImmediate(child.gameObject);
            hud.GetComponent<RectTransform>().sizeDelta=new Vector2(414,850);
            world.sceneCamera.transform.localPosition=new Vector3(1,2.6f,-3.7f);
            world.sceneCamera.transform.LookAt(world.transform.TransformPoint(new Vector3(0,1,3)));
            world.sceneCamera.fieldOfView=51;
            var companionPosition=world.companionOrb.localPosition;companionPosition.y=.85f;world.companionOrb.localPosition=companionPosition;
            PrefabUtility.RecordPrefabInstancePropertyModifications(world.companionOrb);
            PrefabUtility.RecordPrefabInstancePropertyModifications(world.sceneCamera.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(world.sceneCamera);
            worldImage=A.Rect("Live musical world",hud.transform,0,0,414,320).gameObject.AddComponent<RawImage>();worldImage.raycastTarget=false;

            Sprite[] sprites=ApprovedSphereSprites();
            var rects=new[]{new Vector4(-28,-28,56,56),new Vector4(-28,-28,56,51.625f),new Vector4(-28,-28,52.5f,56),new Vector4(-28,-28,56,56)};
            companionGroundGlow=A.Image("Companion ground glow",hud.transform,"Gameplay/ground-glow",0,0,68,15,false);
            companionArtwork=RasterOrb("World anchored current sphere",hud.transform,Vector2.zero,sprites,rects,1,false);
            companionArtwork.SetCharge(1,0,false);
            worldFade=A.Panel("Scene to UI atmospheric blend",hud.transform,0,258,414,62,"#38205E00","#38205EFF",0,false).rectTransform;

            // Compact gameplay header keeps the world visible; the brand stays in the menu.
            A.Image("Vinyl badge",hud.transform,"Gameplay/vinyl",-33,-32,122,122);
            var pause=A.Panel("Pause surface",hud.transform,365,14,36,36,"#6C2D85","#26164A",10,false);A.Border(pause.transform,36,36,"#FFD8FB",10,.8f);
            A.Image("Original pause icon",pause.transform,"Gameplay/pause",7,7,22,22);
            A.Hit("Pause",hud.transform,359,9,46,46,null);
            levelText=A.Text("Level",hud.transform,"УРОВЕНЬ 1",130,17,154,21,12,true,"#FFF4E6",TextAnchor.MiddleCenter);
            trackText=A.Text("Track",hud.transform,"",106,38,202,22,12,false,"#ECE5FF",TextAnchor.MiddleCenter);trackText.fontStyle=FontStyle.Italic;
            RasterHairline("Level rule left",hud.transform,119,28,24);RasterHairline("Level rule right",hud.transform,271,28,24,true);
            A.Text("Score caption",hud.transform,"ОЧКИ",90,300,50,29,13);
            scoreText=A.Text("Score",hud.transform,"0",148,294,131,40,25,false,"#FFF4E6");
            A.Panel("Score divider",hud.transform,281,303,1,22,"#F1BD74B3","#F1BD7400",0,false);
            multiplierText=RasterText("Multiplier",hud.transform,"×1",new Rect(302,287,76,52),31,A.C("#FFE8A7"),FontStyle.Italic,font:A.Font());
            multiplierText.GetComponent<CascadeSoftGlow>().tint=new Color(1,.4f,.22f,.06f);multiplierText.GetComponent<CascadeSoftGlow>().radius=5;
            var timeline=A.Panel("Progress well",hud.transform,44,339,256,8,"#261844","#261844",4,false);A.Border(timeline.transform,256,8,"#CC7CED",4,.7f);
            progress=A.Rect("Progress source",hud.transform,45,340,254,6).gameObject.AddComponent<Image>();progress.type=Image.Type.Filled;progress.fillMethod=Image.FillMethod.Horizontal;progress.color=Color.clear;progress.raycastTarget=false;
            var fill=A.Rect("Luminous song progress",hud.transform,45,340,254,6).gameObject.AddComponent<CascadeProgressGraphic>();fill.source=progress;fill.raycastTarget=false;
            timeText=A.Text("Time",hud.transform,"0:00 / 1:30",312,330,92,25,11,false);
            rhythmGuide=A.Rect("Rhythm guide",hud.transform,44,354,326,28).gameObject.AddComponent<CascadeRhythmGuide>();rhythmGuide.raycastTarget=false;

            const float left=31,top=396,size=352,unit=352f/360;
            var bevel=A.Panel("Board lower golden bevel",hud.transform,left,top+342*unit,size,15*unit,"#FFB9F4","#FFD398",12,false);
            for(int i=7;i>=1;i--)
            {
                var rim=A.Panel("Board rim glow "+i,hud.transform,left-i,top-i,size+i*2,size+i*2,"#FF6ED500","#FF6ED500",17+i,false);
                rim.shape=CascadePanelGraphic.Shape.Border;rim.border=i*1.3f;rim.color=new Color(1,.35f,.82f,.026f);rim.rightColor=new Color(1,.65f,.32f,.025f);
            }
            var board=A.Panel("Approved board 6 x 6",hud.transform,left,top,size,size,"#5D2C63","#522D72",17,false);A.Border(board.transform,size,size,"#FFF5BF",17,1.5f);
            for(int i=0;i<36;i++)
            {
                float x=left+(6.2f+i%6*58)*unit,y=top+(6.2f+i/6*58)*unit,cellSize=54*unit;
                var cell=A.Panel("Cell surface "+i,hud.transform,x,y,cellSize,cellSize,"#1D1334","#432254",11*unit,false);A.Border(cell.transform,cellSize,cellSize,"#D88AF1",11*unit,.7f);
                cell.edgeFeather=.55f;cell.GetComponentsInChildren<CascadePanelGraphic>().Last().edgeFeather=.55f;
                var center=new Vector2(x+cellSize/2,y+cellSize/2);var view=views[i];
                view.transform.localPosition=new Vector3((center.x-207)/100,(425-center.y)/100,0);
                view.GetComponent<BoxCollider>().size=new Vector3(cellSize/100,cellSize/100,.12f);
                view.raster=RasterOrb("Cell "+i,hud.transform,center,sprites,rects,unit);
                foreach(var g in view.raster.GetComponentsInChildren<CascadePanelGraphic>(true))
                {g.rectTransform.sizeDelta=new Vector2(cellSize,cellSize);g.rectTransform.anchoredPosition=g==view.raster.burst?Vector2.zero:new Vector2(-cellSize/2,cellSize/2);g.radius=11*unit;g.border=1;}
                view.orb=view.raster.content;view.charge=-1;view.SetCharge(0,orbMaterials,0,false);
            }
            A.Image("Rim sparkle left",hud.transform,"Gameplay/sparkle",61,top-5,10,10);
            A.Image("Rim sparkle right",hud.transform,"Gameplay/sparkle",331,top+size-5,10,10);
            A.Panel("Footer dusk",hud.transform,0,748,414,102,"#38205E00","#291743FF",0,false);
            A.Image("Musical ribbons",hud.transform,"Gameplay/ribbons",-5,801,424,63);
            // The next-piece preview and world companion share these exact sprite references.
            preview.raster=RasterOrb("Next sphere",hud.transform,new Vector2(77,784),sprites,rects,.82f,false);preview.orb=preview.raster.content;preview.charge=-1;
            nextText=A.Text("Next",hud.transform,"СЛЕДУЮЩАЯ",112,762,260,22,11,true,"#DDC8E8");
            streakText=A.Text("Streak",hud.transform,"",112,784,275,23,11,true,"#FFDE7F");
            guideCaption=A.Text("Guide caption",hud.transform,"",35,380,344,14,9,true,"#DDC8E8",TextAnchor.MiddleCenter);
            statusText=A.Text("Feedback",hud.transform,"",25,819,364,23,11,true,"#FFF1D8",TextAnchor.MiddleCenter);
            countdownText=A.Text("Countdown",hud.transform,"",107,466,200,160,92,true,"#FFF8D5",TextAnchor.MiddleCenter);
            rasterFramePulse=RasterPanel("Rhythm frame rim",hud.transform,new Rect(left,top,size,size),Color.clear,17,CascadePanelGraphic.Shape.Border,1.3f);
            foreach(var renderer in GetComponentsInChildren<Renderer>(true))if(!renderer.transform.IsChildOf(world.transform))renderer.enabled=false;
            foreach(var light in GetComponentsInChildren<Light>(true))if(!light.transform.IsChildOf(world.transform))light.enabled=false;
            world.gameObject.SetActive(false);UpdateScreenLayout();EditorUtility.SetDirty(this);
        }
        static Sprite[] ApprovedSphereSprites()
        {
            const string folder="Assets/Cascade/Resources/Approved/Gameplay/";
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"orbs-atlas.png");
            float half=tex.width*.5f;
            var crops=new[]{new Rect(0,half,half,half),new Rect(half,half+half*.078125f,half,half*.921875f),new Rect(0,0,half*.9375f,half),new Rect(half,0,half,half)};
            var result=new Sprite[4];
            for(int i=0;i<4;i++)
            {
                string path=folder+"sphere-"+(i+1)+".asset";
                var sprite=Sprite.Create(tex,crops[i],Vector2.one*.5f,100,0,SpriteMeshType.FullRect);sprite.name="Approved sphere "+(i+1);
                var old=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if(old){EditorUtility.CopySerialized(sprite,old);DestroyImmediate(sprite);EditorUtility.SetDirty(old);result[i]=old;}
                else{AssetDatabase.CreateAsset(sprite,path);result[i]=sprite;}
            }
            return result;
        }
    }
}
#endif
