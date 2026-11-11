using System;
using System.Linq;
using Cascade.Dream;
using UnityEngine;
using UnityEngine.UI;
using A=Cascade.Prototype.CascadeApprovedUI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cascade.Prototype
{
    public sealed partial class CascadeGame
    {
        [SerializeField] bool approvedDesign;
        [SerializeField] DreamRunWorld dreamWorld;
        [SerializeField] Animator miRunner,faRunner;
        [SerializeField] RawImage worldImage;
        [SerializeField] CascadeRhythmGuide rhythmGuide;
        [SerializeField] Text guideCaption;
        [SerializeField] CascadeRasterOrb companionArtwork;
        [SerializeField] Image companionGroundGlow;
        [SerializeField] RectTransform worldFade;
        int companionCharge=-1;
        RenderTexture worldTexture;
        AudioSource feedbackAudio;AudioClip feedbackClip;
        double lastFeedback;
        bool attemptRecorded;
        Phase worldPhase=Phase.Error;
        public bool UsesApprovedDesign=>approvedDesign;
        public DreamRunWorld World=>dreamWorld;
        public RhythmGrid TimingGrid=>rhythmGrid;
        public CascadeRhythmGuide RhythmGuide=>rhythmGuide;
        public int CompanionCharge=>companionCharge;
        public Sprite CompanionSprite=>companionArtwork?companionArtwork.image.sprite:null;
        public bool IsEnglish=>progressStore?.Data.language=="en";
        string T(string ru,string en)=>IsEnglish?en:ru;
        public void MenuSetMusic(bool value){progressStore.Data.musicEnabled=value;if(value&&volume<.001f)volume=.7f;music.volume=volume;music.mute=!value;SaveSettings();}
        public void MenuSetSounds(bool value){progressStore.Data.soundsEnabled=value;SaveSettings();}
        public void MenuSetHaptics(bool value){progressStore.Data.hapticsEnabled=value;SaveSettings();}
        public void MenuSetLanguage(string value){progressStore.Data.language=value=="en"?"en":"ru";SaveSettings();}
        public void MenuSetHero(string value){progressStore.Data.selectedHero=value=="Fa"?"Fa":"Mi";SaveSettings();}
        [Serializable] class ServiceLinks{public string support,terms,privacy;}
        public bool OpenServiceLink(string key)
        {
            var asset=Resources.Load<TextAsset>("Approved/service-links");if(!asset)return false;
            var links=JsonUtility.FromJson<ServiceLinks>(asset.text);string url=key=="support"?links.support:key=="terms"?links.terms:links.privacy;
            if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https")return false;
            Application.OpenURL(url);return true;
        }
        void InitializeApprovedRuntime()
        {
            if(!approvedDesign)return;
            music.mute=!progressStore.Data.musicEnabled;
            feedbackAudio=gameObject.AddComponent<AudioSource>();feedbackAudio.playOnAwake=false;feedbackAudio.spatialBlend=0;
            int rate=24000;var samples=new float[(int)(rate*.055f)];
            for(int i=0;i<samples.Length;i++){float t=i/(float)rate;float env=Mathf.Sin(Mathf.PI*i/samples.Length)*Mathf.Exp(-t*55);samples[i]=Mathf.Sin(t*2*Mathf.PI*880)*env*.12f;}
            feedbackClip=AudioClip.Create("Soft rhythm confirmation",samples.Length,1,rate,false);feedbackClip.SetData(samples,0);
            worldTexture=new RenderTexture(1242,960,24,RenderTextureFormat.DefaultHDR){name="Cascade world view",antiAliasing=1};worldTexture.Create();
            worldImage.texture=worldTexture;dreamWorld.sceneCamera.targetTexture=worldTexture;dreamWorld.sceneCamera.aspect=414f/320;
            dreamWorld.enableDemoKeys=false;dreamWorld.enabled=false;dreamWorld.reducedMotion=true;
            // The companion keeps its world-space anchor. Its approved artwork is projected
            // into the scene viewport, so lighting/ACES cannot change its charge colour or note.
            foreach(var renderer in dreamWorld.companionOrb.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            foreach(var light in dreamWorld.companionOrb.GetComponentsInChildren<Light>())light.enabled=false;
            StyleApprovedModal();
            InitializeMusicEnergy();
        }
        void ApprovedStart()
        {
            if(!approvedDesign)return;attemptRecorded=false;
            runPulse=new CascadeRunPulse(rhythmGrid);
            dreamWorld.gameObject.SetActive(true);
            bool fa=progressStore.Data.selectedHero=="Fa"&&faRunner;
            miRunner.gameObject.SetActive(!fa);if(faRunner)faRunner.gameObject.SetActive(fa);
            dreamWorld.hero=fa?faRunner:miRunner;dreamWorld.Initialize();dreamWorld.ResetRun();dreamWorld.SetPaused(true);
            ResetMusicEnergy();
            dreamWorld.musicBpm=(float)(beatMap.nominalBpm>0?beatMap.nominalBpm:120);music.mute=!progressStore.Data.musicEnabled;
            levelText.text=T("УРОВЕНЬ ","LEVEL ")+(level+1);
            hud.transform.Find("Score caption").GetComponent<Text>().text=T("ОЧКИ","SCORE");
        }
        void UpdateApprovedRuntime()
        {
            if(!approvedDesign||!dreamWorld)return;
            bool visible=State!=Phase.Menu&&State!=Phase.Error;
            if(dreamWorld.gameObject.activeSelf!=visible)dreamWorld.gameObject.SetActive(visible);
            bool running=(State==Phase.Playing||State==Phase.Reaction)&&AudioSettings.dspTime>=scheduledDsp;
            if(worldPhase!=State||dreamWorld.paused==running){dreamWorld.SetPaused(!running);worldPhase=State;}
            if(running){dreamWorld.Advance(Mathf.Min(Time.deltaTime,.1f));AnimateCompanionFlight();}
            // This is the same corrected DSP clock used by touch judgement. Muting audio
            // changes no timestamps. Calibration cannot silently move the visual target.
            double guideTime=Position-offsetMs/1000d;
            bool rest=beatMap==null||beatMap.IsExcluded(Position)||beatMap.IsExcluded(guideTime);
            rhythmGuide.Present(rhythmGrid,guideTime,balance.rhythmWindow,(running||State==Phase.Paused)&&!rest);
            guideCaption.text=State==Phase.Countdown?T("Огонёк в кольце — нажимай","Light in the ring — tap"):
                rest?T("Музыкальная пауза · свободный ритм","Musical break · free rhythm"):
                T("Огонёк в кольце — нажимай · ¼  ⅛  ¹⁄₁₆","Light in the ring — tap · ¼  ⅛  ¹⁄₁₆");
            SyncCompanionArtwork();
            UpdateMusicEnergy();
            if(State==Phase.Result&&!attemptRecorded){RecordApprovedAttempt(wonAttempt);}
        }
        void SyncCompanionArtwork()
        {
            if(Model==null||!companionArtwork)return;
            if(companionCharge!=Model.NextCharge)
            {
                companionCharge=Model.NextCharge;
                companionArtwork.SetCharge(companionCharge,effectClock,false);
            }
            var camera=dreamWorld.sceneCamera;var position=dreamWorld.companionOrb.position;
            var point=camera.WorldToViewportPoint(position);
            var top=camera.WorldToViewportPoint(position+camera.transform.up*.62f);
            var viewport=worldImage.rectTransform;
            float diameter=Mathf.Clamp(Mathf.Abs(top.y-point.y)*viewport.rect.height*2,38,65);
            var rect=(RectTransform)companionArtwork.transform;
            rect.anchoredPosition=viewport.anchoredPosition+new Vector2(point.x*viewport.rect.width,-(1-point.y)*viewport.rect.height);
            companionArtwork.content.localScale=Vector3.one*(diameter/56);
            // A restrained contact halo locates the single hovering sphere on the bridge.
            var ground=camera.WorldToViewportPoint(new Vector3(position.x,.09f,position.z));
            companionGroundGlow.rectTransform.anchoredPosition=viewport.anchoredPosition+new Vector2(ground.x*viewport.rect.width-diameter*.7f,-(1-ground.y)*viewport.rect.height+5);
            companionGroundGlow.rectTransform.sizeDelta=new Vector2(diameter*1.4f,15);
        }
        void LayoutApprovedWorld()
        {
            if(!approvedDesign||!worldImage||!dreamWorld)return;
            // Extend the scenery behind the safe composition, including shorter phones.
            // Board/control geometry keeps its uniform scale; the camera owns the extra view.
            float width=Screen.width/rasterScaler.scaleFactor;
            float left=(414-width)*.5f-rasterRoot.anchoredPosition.x;
            float top=-(Screen.height/rasterScaler.scaleFactor-850)*.5f+rasterRoot.anchoredPosition.y;
            worldImage.rectTransform.anchoredPosition=new Vector2(left,-top);
            worldImage.rectTransform.sizeDelta=new Vector2(width,320-top);
            dreamWorld.sceneCamera.aspect=width/(320-top);
            if(worldFade){worldFade.anchoredPosition=new Vector2(left,-258);worldFade.sizeDelta=new Vector2(width,62);}
        }
        void RecordApprovedAttempt(bool won)
        {
            if(!approvedDesign||attemptRecorded||developerPreview||Model==null)return;attemptRecorded=true;
            try{progressStore.RecordAttempt(rhythm?.Hits??0,journal.Count(j=>j.waves>0),won);}catch(Exception e){Debug.LogWarning("Daily progress: "+e.Message);}
        }
        void ApprovedHit(RhythmResult judgement)
        {
            if(!approvedDesign||judgement.kind!="hit")return;
            if(progressStore.Data.soundsEnabled&&feedbackAudio)feedbackAudio.PlayOneShot(feedbackClip);
            if(progressStore.Data.hapticsEnabled&&Time.unscaledTimeAsDouble-lastFeedback>.12)
            {
                CascadeHaptics.Tick();
                lastFeedback=Time.unscaledTimeAsDouble;
            }
        }
        void DisposeApproved()
        {
            if(worldTexture){worldTexture.Release();Destroy(worldTexture);}if(feedbackClip)Destroy(feedbackClip);
        }
        void StyleApprovedModal()
        {
            BuildGlassModal();
        }
#if UNITY_EDITOR
        public void BuildApprovedVisualsV1(DreamRunWorld world,Animator fa)
        {
            logoFont=AssetDatabase.LoadAssetAtPath<Font>(ArtFolder+"Fonts/CormorantGaramond-Italic.ttf");
            approvedDesign=true;dreamWorld=world;miRunner=world.hero;faRunner=fa;
            rasterBackground.GetComponent<Image>().preserveAspect=false;
            foreach(Transform child in hud.transform.Cast<Transform>().ToArray())DestroyImmediate(child.gameObject);
            hud.GetComponent<RectTransform>().sizeDelta=new Vector2(414,850);
            worldImage=A.Rect("Live musical world",hud.transform,0,0,414,278).gameObject.AddComponent<RawImage>();worldImage.raycastTarget=false;
            A.Panel("World fade",hud.transform,0,238,414,58,"#25123900","#251239FF",0,false);
            var logo=A.Text("Cascade brand",hud.transform,"CASCADE",78,8,260,47,34,false,"#FFF8D5",TextAnchor.MiddleCenter);logo.font=logoFont;
            A.Button("Pause",hud.transform,"Ⅱ",359,15,40,40,null,false,23);
            levelText=A.Text("Level",hud.transform,"УРОВЕНЬ 1",110,58,194,20,12,true,"#FFF1D8",TextAnchor.MiddleCenter);
            trackText=A.Text("Track",hud.transform,"",90,78,234,19,12,false,"#FFF1D8",TextAnchor.MiddleCenter);
            A.Text("Score caption",hud.transform,"ОЧКИ",82,282,48,26,12);
            scoreText=A.Text("Score",hud.transform,"0",138,274,143,37,25,false,"#FFF1D8");
            multiplierText=A.Text("Multiplier",hud.transform,"×1",310,267,70,51,28,true,"#FFDE7F",TextAnchor.MiddleCenter);
            A.Panel("Progress well",hud.transform,44,319,256,7,"#382044","#382044",4);
            progress=A.Rect("Progress",hud.transform,44,319,256,7).gameObject.AddComponent<Image>();progress.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");progress.type=Image.Type.Filled;progress.fillMethod=Image.FillMethod.Horizontal;progress.color=A.C("#FFDE7F");progress.raycastTarget=false;
            timeText=A.Text("Time",hud.transform,"0:00 / 1:30",311,309,94,26,11,false);
            A.Panel("Rhythm guide well",hud.transform,31,341,352,55,"#442658","#342041",15,false);
            rhythmGuide=A.Rect("Rhythm guide",hud.transform,43,345,328,28).gameObject.AddComponent<CascadeRhythmGuide>();rhythmGuide.raycastTarget=false;
            guideCaption=A.Text("Guide caption",hud.transform,"",40,375,334,18,10,true,"#DDC8E8",TextAnchor.MiddleCenter);
            const float scale=352f/802.296f;
            var oldLayout=JsonUtility.FromJson<CascadeRasterLayout>(rasterLayoutAsset.text);
            var board=oldLayout.Asset("empty-board.png");
            RasterImage("Board",hud.transform,new CascadeRasterLayer{file=board.file,x=31+(board.x-68.54f)*scale,y=407+(board.y-561.136f)*scale,width=board.width*scale,height=board.height*scale});
            var sprites=new[]{RasterSprite("sphere-1-cyan.png"),RasterSprite("sphere-2-green.png"),RasterSprite("sphere-3-amber.png"),RasterSprite("sphere-4-purple.png")};var rects=oldLayout.OrbRects();
            for(int i=0;i<36;i++)
            {
                var old=oldLayout.Center(i);var center=new Vector2(31+(old.x-68.54f)*scale,407+(old.y-561.136f)*scale);var view=views[i];
                view.transform.localPosition=new Vector3((center.x-207)/100,(425-center.y)/100,0);
                view.GetComponent<BoxCollider>().size=new Vector3(1.194f*scale,1.13f*scale,.12f);
                view.raster=RasterOrb("Cell "+i,hud.transform,center,sprites,rects,scale);view.orb=view.raster.content;
                foreach(var g in view.raster.GetComponentsInChildren<CascadePanelGraphic>(true)){g.rectTransform.sizeDelta*=scale;g.rectTransform.anchoredPosition*=scale;g.radius*=scale;g.border*=scale;}
                view.SetCharge(0,orbMaterials,0,false);
            }
            preview.raster=RasterOrb("Next sphere",hud.transform,new Vector2(78,786),sprites,rects,.31f,false);preview.orb=preview.raster.content;preview.charge=-1;
            nextText=A.Text("Next",hud.transform,"СЛЕДУЮЩАЯ",112,765,220,22,11,true,"#DDC8E8");
            streakText=A.Text("Streak",hud.transform,"",112,787,275,25,11,true,"#FFDE7F");
            statusText=A.Text("Feedback",hud.transform,"",25,818,364,23,11,true,"#FFF1D8",TextAnchor.MiddleCenter);
            countdownText=A.Text("Countdown",hud.transform,"",107,450,200,160,92,true,"#FFF8D5",TextAnchor.MiddleCenter);
            rasterFramePulse=RasterPanel("Rhythm frame rim",hud.transform,new Rect(31,407,352,765.864f*scale),Color.clear,21,CascadePanelGraphic.Shape.Border,1.3f);
            rasterFramePulse.transform.SetSiblingIndex(1);
            foreach(var renderer in GetComponentsInChildren<Renderer>(true))if(!renderer.transform.IsChildOf(world.transform))renderer.enabled=false;
            foreach(var light in GetComponentsInChildren<Light>(true))if(!light.transform.IsChildOf(world.transform))light.enabled=false;
            world.gameObject.SetActive(false);UpdateScreenLayout();EditorUtility.SetDirty(this);
        }
#endif
    }
}
