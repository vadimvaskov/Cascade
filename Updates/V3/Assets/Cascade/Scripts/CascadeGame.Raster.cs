using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace Cascade.Prototype
{
    public sealed partial class CascadeGame
    {
        [SerializeField] TextAsset rasterLayoutAsset;
        [SerializeField] CanvasScaler rasterScaler;
        [SerializeField] RectTransform rasterRoot, rasterBackground;
        [SerializeField] CascadePanelGraphic rasterFramePulse;
        public RectTransform RasterRoot => rasterRoot;
        public TextAsset RasterLayoutAsset => rasterLayoutAsset;
        const float RasterWidth=941, RasterHeight=1672, RasterPixelsPerUnit=100;

        void UpdateScreenLayout()
        {
            Rect area=Screen.safeArea;
            float legacyScale=Mathf.Max(.01f,Mathf.Min(area.width/720f,area.height/1280f));
            safeRoot.GetComponentInParent<CanvasScaler>().scaleFactor=legacyScale;
            safeRoot.anchorMin=safeRoot.anchorMax=Vector2.one*.5f;
            safeRoot.sizeDelta=new Vector2(720,1280);safeRoot.localScale=Vector3.one;
            Vector2 offset=area.center-new Vector2(Screen.width*.5f,Screen.height*.5f);
            safeRoot.anchoredPosition=offset/legacyScale;
            if(rasterRoot!=null)
            {
                float scale=Mathf.Max(.01f,Mathf.Min(area.width/RasterWidth,area.height/RasterHeight));
                rasterScaler.scaleFactor=scale;
                rasterRoot.sizeDelta=new Vector2(RasterWidth,RasterHeight);
                rasterRoot.anchoredPosition=offset/scale;
                // The complete gameplay composition fits uniformly in the safe area.
                // Only the background covers the screen, also with uniform scaling.
                float cover=Mathf.Max(Screen.width/(scale*RasterWidth),Screen.height/(scale*RasterHeight));
                rasterBackground.sizeDelta=new Vector2(RasterWidth,RasterHeight)*cover;
                gameCamera.orthographicSize=Screen.height/(2*scale*RasterPixelsPerUnit);
                gameCamera.transform.SetPositionAndRotation(new Vector3(-offset.x/(scale*RasterPixelsPerUnit),-offset.y/(scale*RasterPixelsPerUnit),-18),Quaternion.identity);
                return;
            }
            gameCamera.orthographicSize=Screen.height/(2*legacyScale*(1280f/13.5f));
            gameCamera.transform.position=new Vector3(-offset.x/(legacyScale*(1280f/13.5f)),-3f-offset.y/(legacyScale*(1280f/13.5f)),-18);
        }
        void ConfigureRasterFeedback()
        {
            if(rasterRoot==null)return;
            statusText.fontSize=26;statusText.rectTransform.sizeDelta=new Vector2(881,48);
            streakText.fontSize=20;
            if(useV3)
            {
                streakText.fontSize=24;
                streakText.rectTransform.sizeDelta=new Vector2(881,37);
                streakText.rectTransform.anchoredPosition=new Vector2(30,streakText.rectTransform.anchoredPosition.y);
            }
        }
        void UpdateRasterPulse(float beat,float light,bool reducedEffects)
        {
            if(rasterFramePulse==null)return;
            float alpha=beat*light*(reducedEffects?.035f:.24f);
            rasterFramePulse.color=new Color(1,.8f,.28f,alpha);
            rasterFramePulse.rightColor=new Color(1,.25f,.96f,alpha);
        }

#if UNITY_EDITOR
        public const string RasterArtFolder="Assets/Cascade/Art/Raster/";
        static RectTransform RasterRect(string name,Transform parent,float x,float y,float width,float height)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);return rect;
        }
        static void ImportRasterAssets()
        {
            foreach(string path in Directory.GetFiles(RasterArtFolder,"*.png"))
            {
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.npotScale=TextureImporterNPOTScale.None;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.crunchedCompression=false;
                importer.maxTextureSize=2048;importer.filterMode=FilterMode.Trilinear;importer.wrapMode=TextureWrapMode.Clamp;
                importer.spritePixelsPerUnit=100;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteExtrude=0;importer.SetTextureSettings(settings);
                importer.ClearPlatformTextureSettings("iPhone");importer.ClearPlatformTextureSettings("Standalone");
                importer.SaveAndReimport();
            }
            AssetDatabase.ImportAsset(RasterArtFolder+"Layout_941x1672.json",ImportAssetOptions.ForceSynchronousImport);
        }
        static Sprite RasterSprite(string file)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(RasterArtFolder+Path.GetFileName(file));
            if(sprite==null)throw new InvalidOperationException("Missing supplied raster sprite: "+file);
            return sprite;
        }
        static Image RasterImage(string name,Transform parent,CascadeRasterLayer layer)
        {
            var image=RasterRect(name,parent,layer.x,layer.y,layer.width,layer.height).gameObject.AddComponent<Image>();
            image.sprite=RasterSprite(layer.file);image.color=Color.white;image.type=Image.Type.Simple;
            image.preserveAspect=true;image.raycastTarget=false;return image;
        }
        static CascadePanelGraphic RasterPanel(string name,Transform parent,Rect rect,Color color,float radius=0,CascadePanelGraphic.Shape shape=CascadePanelGraphic.Shape.Panel,float border=1)
        {
            var panel=RasterRect(name,parent,rect.x,rect.y,rect.width,rect.height).gameObject.AddComponent<CascadePanelGraphic>();
            panel.color=color;panel.rightColor=color;panel.radius=radius;panel.shape=shape;panel.border=border;panel.raycastTarget=false;return panel;
        }
        Text RasterText(string name,Transform parent,string value,Rect rect,int fontSize,Color color,FontStyle style=FontStyle.Normal,float spacing=0,Font font=null,TextAnchor align=TextAnchor.MiddleCenter)
        {
            var text=RasterRect(name,parent,rect.x,rect.y,rect.width,rect.height).gameObject.AddComponent<Text>();
            text.font=font??uiFont;text.fontSize=fontSize;text.fontStyle=style;text.color=color;text.text=value;text.alignment=align;
            text.raycastTarget=false;text.supportRichText=false;text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;
            if(spacing!=0)text.gameObject.AddComponent<CascadeLetterSpacing>().spacing=spacing;
            var glow=text.gameObject.AddComponent<CascadeSoftGlow>();glow.tint=new Color(color.r,color.g,color.b,.012f);glow.radius=5;
            return text;
        }
        void RasterOrbit(string name,Vector2[] points,Color tint)
        {
            var smooth=new System.Collections.Generic.List<Vector2>();
            for(int i=0;i<points.Length-1;i++)for(int j=0;j<10;j++)
            {
                float t=j/10f;var a=points[Mathf.Max(0,i-1)];var b=points[i];var c=points[i+1];var d=points[Mathf.Min(points.Length-1,i+2)];
                smooth.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));
            }
            smooth.Add(points[points.Length-1]);
            for(int layer=5;layer>=0;layer--)
            {
                var ribbon=RasterRect(name+" "+layer,hud.transform,624,427,108,61).gameObject.AddComponent<CascadeRibbonGraphic>();
                ribbon.points=smooth.ToArray();ribbon.thickness=layer==0?1.3f:layer*3;ribbon.softEnds=true;
                ribbon.color=new Color(tint.r,tint.g,tint.b,0);tint.a=layer==0?.65f:.035f;ribbon.endColor=tint;ribbon.raycastTarget=false;
            }
        }
        void RasterHairline(string name,Transform parent,float x,float y,float width,bool reverse=false)
        {
            var line=RasterPanel(name,parent,new Rect(x,y,width,1.3f),new Color(1,.76f,.2f,.75f));
            line.color=new Color(1,.76f,.2f,reverse?.75f:0);line.rightColor=new Color(1,.76f,.2f,reverse?0:.75f);
        }
        CascadeRasterOrb RasterOrb(string name,Transform parent,Vector2 center,Sprite[] sprites,Vector4[] rects,float scale=1,bool cell=true)
        {
            var root=RasterRect(name,parent,center.x,center.y,0,0);
            var orb=root.gameObject.AddComponent<CascadeRasterOrb>();orb.sprites=sprites;orb.rects=rects;orb.sizeMultiplier=scale;
            if(cell)
            {
                orb.rim=RasterPanel("Rhythm cell rim",root,new Rect(-59.7f,-56.5f,119.4f,113),Color.clear,23,CascadePanelGraphic.Shape.Border,2);
                orb.burst=RasterPanel("Cascade pulse",root,new Rect(-58,-55,116,110),Color.clear,24,CascadePanelGraphic.Shape.Border,3);
                orb.burst.rectTransform.pivot=Vector2.one*.5f;orb.burst.rectTransform.anchoredPosition=Vector2.zero;
                orb.burst.gameObject.SetActive(false);
            }
            orb.content=RasterRect("Independent sphere",root,0,0,0,0);
            orb.image=RasterRect("Supplied PNG",orb.content,0,0,1,1).gameObject.AddComponent<Image>();
            orb.image.color=Color.white;orb.image.raycastTarget=false;orb.image.preserveAspect=true;
            orb.SetCharge(0,0,false);return orb;
        }
        static void HideCellMeshes(CascadeCell view)
        {
            foreach(Transform child in view.transform)child.gameObject.SetActive(false);
            view.shell=null;view.symbols=Array.Empty<GameObject>();view.border=null;view.burst=null;view.sparks=null;
            view.charge=-1;view.orbSize=1;
        }
        public void BuildRasterVisuals()
        {
            if(rasterRoot!=null)throw new InvalidOperationException("Rebuild via CascadeSceneTools.Build so raster conversion starts from a fresh scene.");
            ImportRasterAssets();
            rasterLayoutAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(RasterArtFolder+"Layout_941x1672.json");
            var layout=JsonUtility.FromJson<CascadeRasterLayout>(rasterLayoutAsset.text);layout.Validate();
            uiFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");logoFont=AssetDatabase.LoadAssetAtPath<Font>(ArtFolder+"Fonts/CormorantGaramond-Italic.ttf")??uiFont;
            var canvasObject=new GameObject("Cascade raster canvas 941 x 1672",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform,false);
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=9;
            rasterScaler=canvasObject.GetComponent<CanvasScaler>();rasterScaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            var background=RasterImage("Supplied background",canvasObject.transform,layout.Asset("background.png"));
            rasterBackground=background.rectTransform;rasterBackground.anchorMin=rasterBackground.anchorMax=rasterBackground.pivot=Vector2.one*.5f;rasterBackground.anchoredPosition=Vector2.zero;
            rasterRoot=UIObject("Safe composition 941 x 1672",canvasObject.transform,Vector2.zero,layout.Size);
            foreach(Transform child in hud.transform.Cast<Transform>().ToArray())DestroyImmediate(child.gameObject);
            hud.transform.SetParent(rasterRoot,false);
            var hudRect=hud.GetComponent<RectTransform>();hudRect.anchorMin=hudRect.anchorMax=hudRect.pivot=Vector2.one*.5f;hudRect.sizeDelta=layout.Size;hudRect.anchoredPosition=Vector2.zero;

            RasterImage("Empty board 6 x 6",hud.transform,layout.Asset("empty-board.png"));
            RasterImage("Cascade logo",hud.transform,layout.Asset("logo-cascade.png"));
            var pauseImage=RasterImage("Pause",hud.transform,layout.Asset("pause-button.png"));pauseImage.raycastTarget=true;
            var pause=pauseImage.gameObject.AddComponent<Button>();pause.targetGraphic=pauseImage;
            var colors=pause.colors;colors.pressedColor=new Color(.78f,.7f,.85f);pause.colors=colors;
            var navigation=pause.navigation;navigation.mode=Navigation.Mode.None;pause.navigation=navigation;
            BuildRasterHud();

            var sprites=new[]{RasterSprite("sphere-1-cyan.png"),RasterSprite("sphere-2-green.png"),RasterSprite("sphere-3-amber.png"),RasterSprite("sphere-4-purple.png")};
            var rects=layout.OrbRects();boardRoot.localPosition=Vector3.zero;boardRoot.localRotation=Quaternion.identity;boardRoot.localScale=Vector3.one;
            foreach(Transform child in boardRoot)if(child.GetComponent<CascadeCell>()==null)child.gameObject.SetActive(false);
            for(int i=0;i<36;i++)
            {
                var center=layout.Center(i);var view=views[i];HideCellMeshes(view);
                view.transform.localPosition=new Vector3((center.x-RasterWidth*.5f)/RasterPixelsPerUnit,(RasterHeight*.5f-center.y)/RasterPixelsPerUnit,0);
                view.transform.localRotation=Quaternion.identity;view.transform.localScale=Vector3.one;
                var collider=view.GetComponent<BoxCollider>();collider.center=Vector3.zero;collider.size=new Vector3(1.194f,1.13f,.12f);
                view.raster=RasterOrb("Cell "+(i/6+1)+","+(i%6+1),hud.transform,center,sprites,rects);
                view.orb=view.raster.content;view.SetCharge(0,orbMaterials,0,false);
            }
            var next=layout.layers_back_to_front.First(x=>x.name=="Next sphere cyan");
            float nextScale=next.width/rects[0].z;
            Vector2 nextCenter=new Vector2(next.x-rects[0].x*nextScale,next.y-rects[0].y*nextScale);
            HideCellMeshes(preview);preview.raster=RasterOrb("Next sphere",hud.transform,nextCenter,sprites,rects,nextScale,false);
            preview.orb=preview.raster.content;preview.SetCharge(1,orbMaterials,0,false);
            rasterFramePulse=RasterPanel("Rhythm frame rim",hud.transform,new Rect(68.54f,561.136f,802.296f,765.864f),Color.clear,48,CascadePanelGraphic.Shape.Border,3);
            countdownText.transform.SetAsLastSibling();
            foreach(string name in new[]{"Atmospheric violet background","Soft glow around the board"})
            {var old=transform.Find(name);if(old!=null)old.gameObject.SetActive(false);}
            gameCamera.transform.SetPositionAndRotation(new Vector3(0,0,-18),Quaternion.identity);
            gameCamera.orthographicSize=RasterHeight/(2*RasterPixelsPerUnit);
            ConfigureRasterFeedback();UpdateScreenLayout();
            EditorUtility.SetDirty(this);
        }
        void BuildRasterHud()
        {
            Color ivory=new Color(.97f,.91f,.76f), muted=new Color(.76f,.64f,.86f);
            levelText=RasterText("Level",hud.transform,"УРОВЕНЬ 1",new Rect(360,357,232,45),26,ivory,FontStyle.Normal,2.4f);
            trackText=RasterText("Track",hud.transform,"Midnight Floor",new Rect(310,390,340,40),25,muted,FontStyle.Italic,1.7f);
            RasterHairline("Left level divider",hud.transform,292,379.5f,74);RasterHairline("Right level divider",hud.transform,584,379.5f,70,true);
            RasterText("Score caption",hud.transform,"ОЧКИ",new Rect(218,442,95,40),28,ivory,FontStyle.Normal,1.4f,align:TextAnchor.MiddleLeft);
            scoreText=RasterText("Score",hud.transform,"0",new Rect(326,431,215,57),40,new Color(1,.91f,.63f),FontStyle.Bold,.4f,align:TextAnchor.MiddleLeft);
            RasterPanel("Score divider",hud.transform,new Rect(555,444,1.2f,39),new Color(.93f,.7f,.16f,.7f));
            RasterOrbit("Multiplier gold orbit",new[]{new Vector2(19,-9),new Vector2(66,1),new Vector2(95,-5),new Vector2(103,-16),new Vector2(98,-28)},new Color(1,.58f,.12f));
            RasterOrbit("Multiplier violet orbit",new[]{new Vector2(1,-39),new Vector2(0,-49),new Vector2(9,-57),new Vector2(30,-59),new Vector2(55,-55)},new Color(.9f,.25f,1));
            multiplierText=RasterText("Multiplier",hud.transform,"×1",new Rect(626,416,105,82),58,new Color(1,.84f,.48f),FontStyle.Italic);
            multiplierText.GetComponent<CascadeSoftGlow>().tint=new Color(1,.5f,.2f,.025f);
            multiplierText.GetComponent<CascadeSoftGlow>().radius=7;
            RasterPanel("Progress interior",hud.transform,new Rect(115,507,579,19),new Color(.105f,.012f,.19f,.95f),10);
            var progressBorder=RasterPanel("Progress rim",hud.transform,new Rect(114,505,581,23),new Color(.88f,.34f,.79f,.9f),11.5f,CascadePanelGraphic.Shape.Border,1.3f);progressBorder.rightColor=new Color(.7f,.2f,.86f,.9f);
            progress=RasterRect("Progress",hud.transform,118,509,573,15).gameObject.AddComponent<Image>();
            progress.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");progress.type=Image.Type.Filled;progress.fillMethod=Image.FillMethod.Horizontal;progress.fillAmount=0;progress.color=Color.clear;progress.raycastTarget=false;
            var fill=RasterRect("Live luminous progress",hud.transform,118,509,573,15).gameObject.AddComponent<CascadeProgressGraphic>();fill.source=progress;fill.raycastTarget=false;
            timeText=RasterText("Time",hud.transform,"0:00 / 3:07",new Rect(719,496,152,40),23,ivory,align:TextAnchor.MiddleLeft);
            nextText=RasterText("Next",hud.transform,"СЛЕДУЮЩАЯ",new Rect(365,1373,215,43),24,ivory,FontStyle.Normal,1.2f);
            RasterHairline("Left next divider",hud.transform,294,1396,67);RasterHairline("Right next divider",hud.transform,579,1396,68,true);
            streakText=RasterText("Streak",hud.transform,"",new Rect(220,1559,501,37),20,muted);
            statusText=RasterText("Feedback",hud.transform,"",new Rect(30,1598,881,48),26,ivory);
            countdownText=RasterText("Countdown",hud.transform,"",new Rect(220,780,501,280),180,ivory,font:logoFont);
        }
#endif
    }
}
