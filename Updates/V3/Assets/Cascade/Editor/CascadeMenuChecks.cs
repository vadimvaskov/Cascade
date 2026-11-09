using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Cascade.Prototype;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

// All progress, settings and journals used here are isolated and restored in finally.
public static class CascadeMenuChecks
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Read<T>(CascadeGame g,string n)=>(T)typeof(CascadeGame).GetField(n,F).GetValue(g);
    static void Set(CascadeGame g,string n,object v)=>typeof(CascadeGame).GetField(n,F).SetValue(g,v);
    static object Call(CascadeGame g,string n,params object[] a)=>typeof(CascadeGame).GetMethod(n,F).Invoke(g,a);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Dir="Documentation/V3/Menu";
    static int id=1100;
    static Vector2 Pixel(float x,float y)=>Screen.safeArea.center+new Vector2(x-470.5f,836-y)*Mathf.Min(Screen.safeArea.width/941,Screen.safeArea.height/1672);
    [StructLayout(LayoutKind.Explicit,Size=36)] struct Native:IInputStateTypeInfo
    {
        [FieldOffset(0)] public int id;[FieldOffset(4)] public Vector2 position;[FieldOffset(20)] public float pressure;[FieldOffset(32)] public byte phase;
        public FourCC format=>TouchState.Format;
    }
    static void Queue(Touchscreen t,int touch,Vector2 p,int phase,double time)=>InputSystem.QueueStateEvent(t,new Native{id=touch,position=p,pressure=phase==1?1:0,phase=(byte)phase},time);
    static void UIEvent(Touchscreen t,Vector2 p,int phase){Canvas.ForceUpdateCanvases();Queue(t,id,p,phase,InputState.currentTime);InputSystem.Update();EventSystem.current.currentInputModule.Process();}
    static IEnumerator Tap(Touchscreen t,float x,float y){yield return null;id++;UIEvent(t,Pixel(x,y),1);yield return null;UIEvent(t,Pixel(x,y),3);}
    static Button Button(CascadeMenuUI ui,string name)=>ui.GetComponentsInChildren<Button>().First(x=>x.name=="Action "+name);
    static string Label(CascadeMenuUI ui,string name)=>ui.Elements[name].GetComponent<Text>().text;
    public static void Begin(string subdirectory="")
    {
        Dir="Documentation/V3/Menu"+(string.IsNullOrEmpty(subdirectory)?"":"/"+subdirectory);
        var g=UnityEngine.Object.FindAnyObjectByType<CascadeGame>();Check(Application.isPlaying&&g!=null,"Play Mode required");
        Directory.CreateDirectory(Dir);File.WriteAllText(Dir+"/runtime.txt","RUNNING");g.StartCoroutine(Guard(Cases(g)));
    }
    static IEnumerator Guard(IEnumerator run)
    {
        while(true)
        {
            bool more;object next;
            try{more=run.MoveNext();next=more?run.Current:null;}
            catch(Exception e){(run as IDisposable)?.Dispose();File.WriteAllText(Dir+"/runtime.txt","FAIL: "+e);Debug.LogException(e);yield break;}
            if(!more)break;yield return next;
        }
    }
    static IEnumerator Cases(CascadeGame g)
    {
        var ui=g.MenuUI;var oldStore=g.ProgressStore;bool testing=g.Catalog.testing_unlock_all_records;g.Catalog.testing_unlock_all_records=false;float volume=g.MenuVolume;bool soft=g.MenuSoftEffects;int offset=Read<int>(g,"offsetMs");var source=Read<AudioSource>(g,"music");bool mute=source.mute;
        string temp=Path.Combine(Path.GetTempPath(),"cascade-menu-"+Guid.NewGuid().ToString("N"));var store=new CascadeProgressStore(Path.Combine(temp,"save.json"),g.Catalog);
        var oldTouch=Touchscreen.current;Touchscreen touch=null;var extraRecord=g.Catalog.records[0];var tracks=extraRecord.tracks;
        string journal=Path.Combine(Application.persistentDataPath,"cascade-v3-last-attempt.json");byte[] previous=File.Exists(journal)?File.ReadAllBytes(journal):null;
        var report=new List<string>();float maxError=0;int geometry=0;
        try
        {
            source.mute=true;Set(g,"progressStore",store);touch=InputSystem.AddDevice<Touchscreen>("Menu QA native touch");Call(g,"ShowMenu");ui.ShowRecords(0);yield return null;
            Check(Button(ui,"previous").interactable==false,"first left arrow");Check(Label(ui,"Completion_Progress")=="Пройдено 0 из 1","new save progress");
            yield return Tap(touch,863,805);Check(ui.Busy,"native arrow begins slide");ui.Step(1);ui.OpenTracks();yield return new WaitForSecondsRealtime(.32f);
            Check(ui.SelectedRecord==1&&!ui.Busy&&ui.CurrentPage==CascadeMenuUI.Page.Records,"one step, no queued tap");
            Check(!Button(ui,"primary").interactable&&Label(ui,"Primary_Button_Label")=="ПЛАСТИНКА ЗАКРЫТА","locked record CTA");
            yield return Tap(touch,470,805);Check(ui.CurrentPage==CascadeMenuUI.Page.Records,"locked centre ignored");yield return null;
            id++;UIEvent(touch,Pixel(350,805),1);yield return null;UIEvent(touch,Pixel(570,805),2);yield return null;UIEvent(touch,Pixel(570,805),3);yield return new WaitForSecondsRealtime(.32f);
            Check(ui.SelectedRecord==0&&ui.CurrentPage==CascadeMenuUI.Page.Records,"native swipe and suppressed click: "+ui.SelectedRecord+" / "+ui.CurrentPage+" / "+ui.Busy);
            yield return Tap(touch,470,805);Check(ui.CurrentPage==CascadeMenuUI.Page.Tracks,"native record opens tracks");yield return null;
            Check(Label(ui,"Track_01_Title")==tracks[0].title&&Label(ui,"Track_01_Best")=="Рекорд: —","live track title / no fabricated best");
            yield return Tap(touch,854,130);Check(ui.CurrentPage==CascadeMenuUI.Page.Settings,"settings from tracks");yield return null;
            yield return Tap(touch,470,797);yield return null;Check(Mathf.Abs(g.MenuVolume-.5f)<.015f,"native volume midpoint");
            yield return Tap(touch,760,937);yield return null;Check(g.MenuSoftEffects,"native soft toggle");
            var loaded=new CascadeProgressStore(store.PathName,g.Catalog);Check(loaded.Data.softEffects&&Math.Abs(loaded.Data.volume-.5f)<.015f,"settings persist on each edit");
            Check(Mathf.Abs(ui.GetComponentsInChildren<CascadePanelGraphic>().First(x=>x.name=="Volume fill").rectTransform.rect.height-12)<.01f,"slider fill height");
            yield return Tap(touch,470,1510);Check(ui.CurrentPage==CascadeMenuUI.Page.Tracks&&ui.SelectedTrackId==tracks[0].track_id,"settings return to originating track");yield return null;
            yield return Tap(touch,78,425);Check(ui.CurrentPage==CascadeMenuUI.Page.Records&&ui.SelectedRecord==0,"back preserves record");
            yield return Tap(touch,854,130);yield return null;yield return Tap(touch,78,425);Check(ui.CurrentPage==CascadeMenuUI.Page.Records,"settings return to records");
            report.Add("PASS: real Input System 36-byte touches -> GraphicRaycaster -> buttons, swipe, slider, toggle; locked record, no queued navigation, persisted settings, context back.");
            // Launch is still the existing countdown, and a second touch cannot launch twice.
            ui.OpenTracks();yield return null;yield return Tap(touch,470,1510);Check(ui.Loading||g.State==CascadeGame.Phase.Countdown,"native play begins loading/countdown");ui.PlaySelected();yield return null;yield return null;
            Check(g.State==CascadeGame.Phase.Countdown&&!ui.gameObject.activeSelf,"selected stable track launches once");
            Call(g,"ShowMenu");ui.ShowRecords(0);ui.OpenTracks();string audio=tracks[0].runtime_audio_path;
            try{tracks[0].runtime_audio_path="V3/QA-missing-track";ui.PlaySelected();yield return null;yield return null;}
            finally{tracks[0].runtime_audio_path=audio;}
            Check(g.State==CascadeGame.Phase.Menu&&ui.gameObject.activeSelf,"real missing resource returns to track list");
            Check(!ui.Busy&&ui.SelectedTrackId==tracks[0].track_id&&Button(ui,"primary").interactable,"retry retains track");
            // Three rows are data, not new menu scenes. Selection remains separate from completion.
            extraRecord.tracks=Enumerable.Range(0,5).Select(i=>{var t=JsonUtility.FromJson<CascadeTrack>(JsonUtility.ToJson(tracks[0]));t.track_id="qa-"+i;t.title="Тестовый трек "+(i+1);t.order=i+1;return t;}).ToArray();ui.ShowRecords(0);ui.OpenTracks();yield return null;
            var scroll=ui.GetComponentInChildren<ScrollRect>();Check(scroll!=null&&scroll.content.rect.height==660,"future catalog scroll list");
            yield return Tap(touch,470,1125);Check(ui.SelectedTrackId=="qa-1","native second row selection");extraRecord.tracks=tracks;
            report.Add("PASS: loading/double launch, retry preserves selection, five-track data fixture and native row selection.");
            // All supplied reference states, with isolated progress to match each state.
            var layout=JsonUtility.FromJson<CascadeMenuLayout>(Resources.Load<TextAsset>("V3/Menu/Layout_941x1672").text);
            Set(g,"volume",.8f);Set(g,"reduced",false);
            foreach(var state in layout.screens)
            {
                int index=Array.FindIndex(g.Catalog.records,r=>r.record_id==state.record_id);if(index<0)index=0;
                store.Data.unlockedRecordIds.Clear();store.Data.results.Clear();
                int lastOpen=state.name.EndsWith("locked")?index-1:index;
                for(int i=0;i<=lastOpen;i++)store.Data.unlockedRecordIds.Add(g.Catalog.records[i].record_id);
                if(state.name.EndsWith("completed"))store.Data.results.Add(new CascadeTrackResult{trackId=tracks[0].track_id,audioSha256=tracks[0].runtime_audio_sha256,completed=true,bestScore=8120});
                ui.ShowRecords(index);if(state.kind=="record_tracks")ui.OpenTracks();else if(state.kind=="settings")ui.Settings();yield return null;Canvas.ForceUpdateCanvases();
                foreach(var e in state.elements_back_to_front.Where(e=>e.type=="image"&&e.id!="Background_World"))
                {
                    var actual=ui.GetComponentsInChildren<RectTransform>().FirstOrDefault(t=>t.name==e.id);
                    if(actual==null&&e.id=="Completed_Check")continue;
                    Check(actual!=null,"element exists: "+state.name+" / "+e.id);
                    var corners=new Vector3[4];actual.GetWorldCorners(corners);var tl=ui.SafeRoot.InverseTransformPoint(corners[1]);var br=ui.SafeRoot.InverseTransformPoint(corners[3]);
                    float delta=Mathf.Max(Mathf.Abs(tl.x+470.5f-e.x),Mathf.Abs(836-tl.y-e.y),Mathf.Abs(br.x-tl.x-e.width),Mathf.Abs(tl.y-br.y-e.height));maxError=Mathf.Max(delta,maxError);geometry++;
                    Check(delta<1,"geometry "+state.name+" "+e.id+" "+delta);
                }
                yield return new WaitForEndOfFrame();var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Dir+"/"+state.name+".png",texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);
            }
            ui.ShowRecords(7);ui.Step(1);Check(!ui.Busy&&ui.SelectedRecord==7&&!Button(ui,"next").interactable,"eighth right boundary");
            report.Add("PASS: 19 reference states captured; "+geometry+" image rectangles <1 layout pixel, max "+maxError.ToString("F5")+"; eighth record boundary.");
            report.Add(FastPlacement(g,touch));
            report.Add(CascadeV3RuntimeChecks.Run());report.Add(CascadeV3RuntimeChecks.RunLifecycle());
            File.WriteAllText(Dir+"/runtime.txt",string.Join("\n",report)+"\nScreen="+Screen.width+"x"+Screen.height);
        }
        finally
        {
            g.Catalog.testing_unlock_all_records=testing;extraRecord.tracks=tracks;if(touch!=null)InputSystem.RemoveDevice(touch);if(oldTouch!=null&&oldTouch.added)oldTouch.MakeCurrent();
            Set(g,"progressStore",oldStore);Set(g,"volume",volume);Set(g,"reduced",soft);Set(g,"offsetMs",offset);source.Stop();source.volume=volume;source.mute=mute;Call(g,"ShowMenu");ui.ShowRecords(0);
            if(Directory.Exists(temp))Directory.Delete(temp,true);if(previous==null){if(File.Exists(journal))File.Delete(journal);}else File.WriteAllBytes(journal,previous);
        }
    }
    public static string FastPlacement(CascadeGame g,Touchscreen touch)
    {
        var fixture=typeof(CascadeRuntimeChecks).GetMethod("Fixture",BindingFlags.NonPublic|BindingFlags.Static);
        int accepted=0,cascades=0;
        for(int trial=0;trial<12;trial++)
        {
            bool charged=trial>=6;
            int[] initial=Enumerable.Range(0,36).Select(i=>i==0?0:trial%3==0?4:(i+trial)%5).ToArray();fixture.Invoke(null,new object[]{g,initial,charged?7d:1d});Call(g,"UpdateScreenLayout");Canvas.ForceUpdateCanvases();Physics.SyncTransforms();
            var oracle=new Board(g.Model.config,731,initial);var rhythm=new Rhythm(Read<BeatMap>(g,"beatMap"),g.Model.config);var bonus=new RhythmBonus(Read<BeatMap>(g,"beatMap"),g.Model.config);int expectedScore=0,moves=0;
            if(charged)for(int beat=1;beat<=6;beat++)
            {
                bonus.Evaluate(rhythm.Evaluate(beat,0),beat,0);
                g.Bonus.Evaluate(Read<Rhythm>(g,"rhythm").Evaluate(beat,0),beat,0);
            }
            Read<HashSet<int>>(g,"downTouches").Clear();Set(g,"pointerTaken",false);
            double at=InputState.currentTime,baseSong=Read<double>(g,"dspAnchor")+at-Read<double>(g,"eventAnchor")-Read<double>(g,"startDsp");
            for(int n=0;n<24&&oracle.Occupied<36;n++)
            {
                int cell=Array.FindIndex(oracle.cells,c=>c==0);double delta=n*.002;var assessment=rhythm.Evaluate(baseSong+delta,0);var turn=oracle.Place(cell,assessment.multiplier,bonus.Evaluate(assessment,baseSong+delta,0));expectedScore+=turn.points;moves++;cascades+=turn.waves.Count;
                var position=Read<Camera>(g,"gameCamera").WorldToScreenPoint(Read<CascadeCell[]>(g,"views")[cell].transform.position);
                Queue(touch,++id,position,1,at+delta);Queue(touch,id,position,3,at+delta+.001);
            }
            InputSystem.Update();Check(g.MoveCount==moves,"all rapid native taps accepted: "+g.MoveCount+"/"+moves);Check(g.Score==expectedScore&&g.Model.cells.SequenceEqual(oracle.cells),"rapid input preserves Board/Rhythm outcomes");
            Check(g.State==CascadeGame.Phase.Playing||g.State==CascadeGame.Phase.Result,"no reaction input lock");
            Check(Read<CascadeCell[]>(g,"views").Select(v=>v.charge).SequenceEqual(g.Model.cells),"render commits final state immediately");
            Call(g,"Update");Check(g.Model.cells.SequenceEqual(oracle.cells),"deferred effects never restore stale cells");accepted+=moves;
        }
        return "PASS: "+accepted+" native rapid placements in 12 single-update batches, 2 ms spacing; "+cascades+" cascade waves; six batches charging / six with active +2; exact model, score, final visuals and terminal state; no reaction cooldown.";
    }
}
