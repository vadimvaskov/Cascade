using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Cascade.Prototype;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static class CascadePhoneFixChecks
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Dir="Documentation/V3/PhoneFixes";
    static T Read<T>(CascadeGame g,string name)=>(T)typeof(CascadeGame).GetField(name,F).GetValue(g);
    static void Set(CascadeGame g,string name,object value)=>typeof(CascadeGame).GetField(name,F).SetValue(g,value);
    static object Call(CascadeGame g,string name,params object[] args)=>typeof(CascadeGame).GetMethod(name,F).Invoke(g,args);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<.0001,message+": "+a+" / "+b);
    static void Fixture(CascadeGame g,int cell=26)
    {
        int[] cells=Enumerable.Repeat(1,36).ToArray();cells[cell]=0;
        typeof(CascadeRuntimeChecks).GetMethod("Fixture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{g,cells,1d});
        Call(g,"UpdateScreenLayout");Canvas.ForceUpdateCanvases();Physics.SyncTransforms();
    }
    static void Clock(CascadeGame g,double time)=>typeof(CascadeRuntimeChecks).GetMethod("Clock",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{g,time});
    static Vector2 Cell(CascadeGame g,int index)
    {
        var p=JsonUtility.FromJson<CascadeRasterLayout>(g.RasterLayoutAsset.text).Center(index);
        return Screen.safeArea.center+new Vector2(p.x-470.5f,836-p.y)*Mathf.Min(Screen.safeArea.width/941,Screen.safeArea.height/1672);
    }
    static Button Action(CascadeGame g,string name)=>Read<GameObject>(g,"modal").transform.Find(name).GetComponent<Button>();
    static Vector2 Point(Button button)
    {
        var canvas=button.GetComponentInParent<Canvas>();
        return RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,button.transform.position);
    }
    static void UI(Touchscreen touch,Vector2 point,int phase)=>typeof(CascadeMenuChecks).GetMethod("UIEvent",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{touch,point,phase});
    static IEnumerator Tap(Touchscreen touch,Vector2 point)
    {UI(touch,point,1);yield return null;UI(touch,point,3);yield return null;}
    static IEnumerator Ready(CascadeGame g)
    {
        double until=Time.unscaledTimeAsDouble+2;
        while(!Read<bool>(g,"resultActionsReady")&&Time.unscaledTimeAsDouble<until)yield return null;
        Check(Read<bool>(g,"resultActionsReady"),"Result actions did not become ready after release");
    }
    public static void Begin()
    {
        var g=UnityEngine.Object.FindAnyObjectByType<CascadeGame>();Check(Application.isPlaying&&g!=null,"Play Mode required");
        Directory.CreateDirectory(Dir);File.WriteAllText(Dir+"/runtime.txt","RUNNING");g.StartCoroutine(Guard(Cases(g)));
    }
    static IEnumerator Guard(IEnumerator run)
    {
        while(true)
        {
            bool more;object value;
            try{more=run.MoveNext();value=more?run.Current:null;}
            catch(Exception e){(run as IDisposable)?.Dispose();File.WriteAllText(Dir+"/runtime.txt","FAIL: "+e);Debug.LogException(e);yield break;}
            if(!more)break;yield return value;
        }
    }
    static IEnumerator Cases(CascadeGame g)
    {
        var store=g.ProgressStore;var source=Read<AudioSource>(g,"music");bool mute=source.mute;int offset=Read<int>(g,"offsetMs");bool soft=Read<bool>(g,"reduced");
        string temp=Path.Combine(Path.GetTempPath(),"cascade-phone-fixes-"+Guid.NewGuid().ToString("N"));
        string log=Path.Combine(Application.persistentDataPath,"cascade-v3-last-attempt.json");byte[] oldLog=File.Exists(log)?File.ReadAllBytes(log):null;
        Touchscreen previous=Touchscreen.current,touch=null;
        try
        {
            source.mute=true;Set(g,"progressStore",new CascadeProgressStore(Path.Combine(temp,"save.json"),g.Catalog));
            touch=InputSystem.AddDevice<Touchscreen>("Phone result regression");
            // Reproduce a terminal placement followed by UI processing of the same touch.
            foreach(int index in new[]{0,5,17,26,30,35})
            {
                Fixture(g,index);yield return null;Clock(g,1);
                UI(touch,Cell(g,index),1);Check(g.State==CascadeGame.Phase.Result,"Last native press must show result at cell "+index);
                Check(Read<GameObject>(g,"modal").activeInHierarchy&&!g.MenuUI.gameObject.activeInHierarchy,"Result visible, menu hidden");
                Check(!Action(g,"Menu").IsInteractable(),"Last press cannot activate Menu");
                if(index==26)
                {
                    yield return new WaitForSecondsRealtime(.45f);
                    Check(!Read<bool>(g,"resultActionsReady"),"Holding final finger beyond delay must not arm buttons");
                }
                UI(touch,Cell(g,index),3);yield return null;
                Check(g.State==CascadeGame.Phase.Result,"Releasing last finger must not leave result");
                if(index!=26)
                {
                    UI(touch,Point(Action(g,"Menu")),1);UI(touch,Point(Action(g,"Menu")),3);yield return null;
                    Check(g.State==CascadeGame.Phase.Result,"Rapid follow-up tap must not skip result");
                }
                yield return Ready(g);
                Check(Action(g,"Restart").IsInteractable()&&Action(g,"Menu").IsInteractable(),"Both loss actions available");
                Check(!Action(g,"Next").gameObject.activeSelf,"No next level after loss");
                Check(Action(g,"Restart").GetComponentInChildren<Text>().text=="ПОПРОБОВАТЬ ЕЩЁ РАЗ","Explicit retry label");
                Check(g.ProgressStore.Data.unlockedRecordIds.Count==1&&g.ProgressStore.Data.results.Count==0,"Loss cannot unlock/save a win");
                if(index==26){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Dir+"/defeat.png");yield return null;}
                yield return Tap(touch,Point(Action(g,"Restart")));
                Check(g.State==CascadeGame.Phase.Countdown&&g.TrackTitle=="Disco Dancer"&&g.Score==0&&g.MoveCount==0&&g.Model.Occupied==6,"Retry starts a fresh same-track attempt");
            }
            Fixture(g);yield return null;Clock(g,1);
            UI(touch,Cell(g,26),1);UI(touch,Cell(g,26),3);yield return Ready(g);
            yield return Tap(touch,Point(Action(g,"Menu")));
            Check(g.State==CascadeGame.Phase.Menu&&g.MenuUI.gameObject.activeInHierarchy,"Explicit menu action works");

            // Independently inject known speaker delays and observe the real four-channel renderer.
            Fixture(g);var map=Read<BeatMap>(g,"beatMap");var rhythm=Read<Rhythm>(g,"rhythm");for(int i=1;i<=16;i++)rhythm.Evaluate(i,0);
            var config=JsonUtility.FromJson<CascadeLightingConfig>(Resources.Load<TextAsset>("V3/lighting").text);
            Set(g,"<State>k__BackingField",CascadeGame.Phase.Paused);Set(g,"reduced",false);
            foreach(double delay in new[]{0d,.18,.32})foreach(int calibration in new[]{0,142,-80})
            {
                var light=new CascadeRhythmLighting(map,config);Set(g,"lighting",light);Set(g,"outputTiming",new CascadeAudioOutputTiming{visualDelay=delay});
                Set(g,"offsetMs",calibration);Set(g,"pausedPosition",16d+delay);
                light.Tick(15.8,Time.unscaledTimeAsDouble-.15,16,true,false);Call(g,"UpdateV3Lighting",true,true);
                Near(g.VisualSongPosition,16,"Audible song position");Near(Read<CascadeGlowGraphic>(g,"screenGlow").canvasRenderer.GetAlpha(),.4,"Visual peak at audible beat");
                Near(Read<CascadeGlowGraphic>(g,"frameGlow").canvasRenderer.GetAlpha(),1,"Board rim uses same audible beat");
                Check(g.Streak==16,"Presentation compensation cannot affect scoring");
                Set(g,"pausedPosition",16d+delay+.25);Call(g,"UpdateV3Lighting",true,true);
                Near(Read<CascadeGlowGraphic>(g,"screenGlow").canvasRenderer.GetAlpha(),0,"No queued late flashes");
            }
            Near(CascadeAudioOutputTiming.Estimate(1024,4,24000,.012,.0426667),.1826667,"24kHz phone queue estimate");
            Near(CascadeAudioOutputTiming.Estimate(256,4,48000,.004,.0053333),.0253333,"48kHz small buffer estimate");
            Near(CascadeAudioOutputTiming.Estimate(0,0,0,double.NaN,-1),0,"Invalid unavailable timing is safe");
            Near(CascadeAudioOutputTiming.Estimate(int.MaxValue,int.MaxValue,1,1,1),2,"Broken platform report is bounded");
            var resumedLight=new CascadeRhythmLighting(map,config);Set(g,"lighting",resumedLight);
            Set(g,"outputTiming",new CascadeAudioOutputTiming{visualDelay=.18});Set(g,"pausedPosition",16.18d);
            Set(g,"scheduledDsp",AudioSettings.dspTime);resumedLight.Tick(15.8,Time.unscaledTimeAsDouble-.15,16,true,false);
            Call(g,"UpdateV3Lighting",true,true);Near(Read<CascadeGlowGraphic>(g,"screenGlow").canvasRenderer.GetAlpha(),0,"Resume cannot flash while output queue is still silent");
            File.WriteAllText(Dir+"/runtime.txt","PASS: six terminal native touch positions; final release and rapid follow-up cannot select Menu; long-held finger; visible loss modal; fresh same-track retry; explicit menu; no loss unlock; audible peak renderer at 0/180/320ms independently of touch calibration 0/+142/-80ms; no scoring changes; buffer estimates and invalid input bounds; resume suppresses light until the output queue is audible. Physical acoustic/video alignment still requires device verification.");
        }
        finally
        {
            if(touch!=null)InputSystem.RemoveDevice(touch);if(previous!=null&&previous.added)previous.MakeCurrent();
            Set(g,"progressStore",store);Set(g,"offsetMs",offset);Set(g,"reduced",soft);Call(g,"ShowMenu");source.mute=mute;
            if(Directory.Exists(temp))Directory.Delete(temp,true);
            if(oldLog==null){if(File.Exists(log))File.Delete(log);}else File.WriteAllBytes(log,oldLog);
        }
    }
}
