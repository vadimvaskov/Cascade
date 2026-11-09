using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Prototype
{
    public sealed partial class CascadeGame
    {
        [SerializeField] bool useV3=true;
        CascadeCatalog catalog;
        CascadeTrack[] catalogTracks;
        CascadeProgressStore progressStore;
        CascadeLightingConfig lightingConfig;
        CascadeRhythmLighting lighting;
        CascadeAudioOutputTiming outputTiming = new CascadeAudioOutputTiming();
        public double VisualSongPosition => outputTiming.SongTime(Position);
        CascadeGlowGraphic frameGlow,screenGlow;
        CascadeGlowGraphic[] cellGlows,orbGlows;
        bool wonAttempt,developerPreview;
        public bool UsesV3=>useV3;
        public CascadeCatalog Catalog=>catalog;
        public CascadeProgressStore ProgressStore=>progressStore;
        public CascadeRhythmLighting Lighting=>lighting;
        public string TrackTitle=>useV3?catalogTracks[level].title:trackNames[level];
        void InitializeV3()
        {
            if(!useV3)return;
            catalog=JsonUtility.FromJson<CascadeCatalog>(Resources.Load<TextAsset>("V3/catalog").text);catalog.Validate();catalogTracks=catalog.Tracks;
            trackNames=catalogTracks.Select(t=>t.track_id).ToArray();
            progressStore=new CascadeProgressStore(Path.Combine(Application.persistentDataPath,"V3","progress.json"),catalog,volume,reduced,offsetMs);
            volume=progressStore.Data.volume;reduced=progressStore.Data.softEffects;offsetMs=progressStore.Data.calibrationMs;
            lightingConfig=JsonUtility.FromJson<CascadeLightingConfig>(Resources.Load<TextAsset>("V3/lighting").text);
            BuildV3TrackList();BuildV3Lighting();
        }
        public void PreviewV3Track(int index)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!useV3)return;
            ShowMenu();developerPreview=true;StartLevel(index);
#endif
        }
        void SaveSettings()
        {
            if(useV3&&progressStore!=null)
            {
                progressStore.Data.volume=volume;progressStore.Data.softEffects=reduced;progressStore.Data.calibrationMs=offsetMs;
                try{progressStore.Save();}catch(Exception e){Debug.LogError("Cascade settings save: "+e.Message);}
            }
            else{PlayerPrefs.SetFloat("cascade.volume",volume);PlayerPrefs.SetInt("cascade.reduced",reduced?1:0);PlayerPrefs.SetInt("cascade.offset",offsetMs);PlayerPrefs.Save();}
        }
        void BuildV3TrackList()
        {
            var root=new GameObject("Cascade vinyl menu",typeof(RectTransform));root.transform.SetParent(transform,false);
            menuUI=root.AddComponent<CascadeMenuUI>();menuUI.Initialize(this);
        }
        void RefreshV3Menu(){if(useV3&&menuUI!=null)menuUI.ShowRecords();}
        void NextRecord()
        {
            if(State==Phase.Result&&!resultActionsReady)return;
            if(!useV3){StartLevel(level+1);return;}
            var ordered=catalog.records.OrderBy(r=>r.order).ToArray();int current=Array.IndexOf(ordered,catalog.RecordFor(catalogTracks[level].track_id));
            ShowMenu();if(current+1<ordered.Length&&progressStore.IsOpen(ordered[current+1]))menuUI.ShowRecords(current+1);
        }
        static CascadeGlowGraphic NewGlow(string name,Transform parent,Vector2 position,Vector2 size,float radius,Color left,Color right,bool circle=false)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(CascadeGlowGraphic));
            var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.anchoredPosition=position;rect.sizeDelta=size;
            var g=go.GetComponent<CascadeGlowGraphic>();g.radius=radius;g.color=left;g.rightColor=right;g.circle=circle;g.raycastTarget=false;g.canvasRenderer.SetAlpha(0);return g;
        }
        void BuildV3Lighting()
        {
            if(rasterRoot==null)return;
            rasterFramePulse.gameObject.SetActive(false);
            frameGlow=NewGlow("V3 Board rim",hud.transform,new Vector2(68.54f+802.296f/2-470.5f,836-561.136f-765.864f/2),new Vector2(802.296f,765.864f),48,new Color(1,.72f,.2f),new Color(1,.15f,.85f));
            frameGlow.transform.SetSiblingIndex(1);frameGlow.softness=10;
            cellGlows=new CascadeGlowGraphic[views.Length];orbGlows=new CascadeGlowGraphic[views.Length];
            for(int i=0;i<views.Length;i++)
            {
                var orb=views[i].raster;if(orb==null)continue;orb.rim.gameObject.SetActive(false);
                cellGlows[i]=NewGlow("V3 Cell outline",orb.transform,Vector2.zero,new Vector2(119.4f,113),23,new Color(.8f,.3f,1),new Color(1,.4f,.9f));
                cellGlows[i].transform.SetAsFirstSibling();cellGlows[i].softness=5;
                orbGlows[i]=NewGlow("V3 Orb rim",orb.transform,Vector2.zero,new Vector2(92,92),46,Color.white,Color.white,true);
                orbGlows[i].softness=7;
            }
            screenGlow=NewGlow("V3 Physical screen aura",rasterRoot.parent,Vector2.zero,Vector2.one,0,Color.white,Color.white);
            screenGlow.screenAura=true;var sr=screenGlow.rectTransform;sr.anchorMin=Vector2.zero;sr.anchorMax=Vector2.one;sr.offsetMin=sr.offsetMax=Vector2.zero;
            screenGlow.transform.SetSiblingIndex(rasterBackground.GetSiblingIndex()+1);
        }
        void UpdateV3Lighting(bool active,bool frozenVisuals)
        {
            if(frameGlow==null)return;
            double visualPosition=VisualSongPosition;
            bool outputStarted=AudioSettings.dspTime>=scheduledDsp+outputTiming.visualDelay;
            bool allowed=active&&outputStarted&&visualPosition>=0&&beatMap!=null&&!beatMap.IsExcluded(visualPosition);
            double clock=Time.unscaledTimeAsDouble;
            var channels=lighting==null?Vector4.zero:lighting.Tick(visualPosition,clock,Streak,allowed,reduced);
            lightStrength=channels.x;frameGlow.canvasRenderer.SetAlpha(channels.x);screenGlow.canvasRenderer.SetAlpha(channels.w);
            for(int i=0;i<views.Length;i++)
            {
                views[i].Animate(effectClock,0,0,reduced,frozenVisuals);
                if(cellGlows[i]==null)continue;
                cellGlows[i].canvasRenderer.SetAlpha(Mathf.Max(channels.y,lighting?.Local(i,clock,reduced,allowed)??0));
                var orb=orbGlows[i];Color tint=Palette(Mathf.Clamp(views[i].charge,1,4));
                if(orb.color!=tint){orb.color=tint;orb.rightColor=tint;orb.SetVerticesDirty();}
                orb.canvasRenderer.SetAlpha(views[i].charge>0?channels.z:0);
            }
            preview.Animate(effectClock,0,0,reduced,frozenVisuals);
        }
    }
}
