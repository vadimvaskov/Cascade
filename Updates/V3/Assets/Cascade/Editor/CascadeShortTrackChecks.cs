using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cascade.Prototype;
using UnityEngine;
using UnityEngine.UI;

public static class CascadeShortTrackChecks
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Read<T>(CascadeGame g,string n)=>(T)typeof(CascadeGame).GetField(n,F).GetValue(g);
    static void Set(CascadeGame g,string n,object v)=>typeof(CascadeGame).GetField(n,F).SetValue(g,v);
    static object Call(CascadeGame g,string n,params object[] a)=>typeof(CascadeGame).GetMethod(n,F).Invoke(g,a);
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);}
    static void Clock(CascadeGame g,double t)=>typeof(CascadeRuntimeChecks).GetMethod("Clock",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{g,t});
    [Serializable] class Journal{public string track;public int offsetMs;public Move[] moves;}
    [Serializable] class Move{public double time;public string rhythm,rejection;public int multiplier;}
    public static string Replay()
    {
        var journal=JsonUtility.FromJson<Journal>(File.ReadAllText("Documentation/V3/ShortTracks/device-attempt-before.json"));
        Check(journal.track=="synthwave_electro_01","Actual Neon device journal required");
        var balance=JsonUtility.FromJson<Balance>(Resources.Load<TextAsset>("V3/balance").text);
        var oldMap=JsonUtility.FromJson<BeatMap>(File.ReadAllText("Documentation/V3/ShortTracks/before/synthwave_electro_01.json"));
        var newMap=JsonUtility.FromJson<BeatMap>(Resources.Load<TextAsset>("V3/Beatmaps/synthwave_electro_01").text);
        var oldRhythm=new Rhythm(oldMap,balance);var newRhythm=new Rhythm(newMap,balance);int max=1,oldMax=1,excluded=0;
        foreach(var move in journal.moves.Where(m=>string.IsNullOrEmpty(m.rejection)))
        {
            var prior=oldRhythm.Evaluate(move.time,journal.offsetMs/1000d);
            Check(prior.kind==move.rhythm&&prior.multiplier==move.multiplier,"Reproduce device judgement before fix");
            var now=newRhythm.Evaluate(move.time,journal.offsetMs/1000d);max=Math.Max(max,now.multiplier);oldMax=Math.Max(oldMax,prior.multiplier);
            if(now.kind=="excluded")excluded++;
        }
        Check(oldRhythm.Hits==17&&oldMax==1,"Reproduce reported failure");
        Check(newRhythm.Hits>=45&&max==10,"Same phone taps should recover ×10 without changing calibration/window");
        return "PASS actual device replay: old hits="+oldRhythm.Hits+", misses="+oldRhythm.Misses+", max=×"+oldMax+"; corrected hits="+newRhythm.Hits+", misses="+newRhythm.Misses+", excluded="+excluded+", max=×"+max+"; unchanged +142 ms / ±120 ms. This is replay, not a new physical-device test.";
    }
    public static string RunRuntime()
    {
        var g=UnityEngine.Object.FindAnyObjectByType<CascadeGame>();Check(Application.isPlaying&&g!=null,"Play Mode required");
        var original=g.ProgressStore;var source=Read<AudioSource>(g,"music");bool mute=source.mute;int offset=Read<int>(g,"offsetMs");
        string dir=Path.Combine(Path.GetTempPath(),"cascade-short-"+Guid.NewGuid().ToString("N"));
        string log=Path.Combine(Application.persistentDataPath,"cascade-v3-last-attempt.json");byte[] previous=File.Exists(log)?File.ReadAllBytes(log):null;
        var report=new List<string>();
        try
        {
            var store=new CascadeProgressStore(Path.Combine(dir,"save.json"),g.Catalog,.7f,false,142);
            Set(g,"progressStore",store);Set(g,"offsetMs",142);source.mute=true;
            Check(g.Catalog.testing_unlock_all_records&&g.Catalog.records.All(store.IsOpen),"All eight accessible on clean save");
            Check(store.Data.results.Count==0&&store.Data.unlockedRecordIds.Count==1,"No fabricated progression");
            // Start with level 8: access is independent from progression and must not show collection complete.
            foreach(int level in new[]{7,1,0,2,3,4,5,6})
            {
                Call(g,"ShowMenu");g.MenuUI.ShowRecords(level);g.MenuUI.OpenTracks();
                Check(g.MenuUI.CurrentPage==CascadeMenuUI.Page.Tracks,"Menu opens record "+level);
                g.StartMenuTrack(g.Catalog.Tracks[level].track_id);
                Check(g.State==CascadeGame.Phase.Countdown,"Public menu launch "+level);
                Check(source.clip.samples==4320000&&Math.Abs(source.clip.length-90)<.001,"Exactly 90 seconds "+level);
                Set(g,"<State>k__BackingField",CascadeGame.Phase.Playing);Set(g,"inputOpenTime",0d);
                var map=Read<BeatMap>(g,"beatMap");var rhythm=Read<Rhythm>(g,"rhythm");
                var beats=map.beats.Where(t=>t+.142<90&&!map.IsExcluded(t)&&!map.IsExcluded(t+.142)).Take(30).ToArray();
                Check(beats.Length==30,"At least 30 scorable pulses "+level);
                for(int i=0;i<beats.Length;i++)
                {
                    // Isolate capacity while exercising actual placement, scoring, bonus and HUD.
                    Set(g,"<Model>k__BackingField",new Board(g.Model.config,731,new int[36],1));
                    double time=beats[i]+.142;Clock(g,time);
                    Check(g.PlaceCell(0,time),"Placement accepted "+level+" / "+i);
                    int expected=Math.Min(10,1+(i+1)/3);
                    Check(rhythm.Multiplier==expected&&Read<Text>(g,"multiplierText").text=="×"+expected,"Real × threshold "+level+" / "+i);
                }
                Clock(g,89.95);Call(g,"Update");Call(g,"LateUpdate");Check(g.State==CascadeGame.Phase.Playing,"Not complete early");
                Clock(g,90.001);Call(g,"Update");Call(g,"LateUpdate");Check(g.State==CascadeGame.Phase.Result,"Completes at 90 seconds");
                Check(Read<Text>(g,"timeText").text=="1:30 / 1:30","HUD final duration");
                if(level==7)Check(Read<Text>(g,"modalTitle").text!="КОЛЛЕКЦИЯ ПРОЙДЕНА","Level8 alone is not whole collection");
                report.Add(g.Catalog.Tracks[level].title+": menu launch, 90s, ×2 at3 / ×10 at27 / cap at30, end result PASS");
            }
            return string.Join("\n",report);
        }
        finally
        {
            Set(g,"progressStore",original);Set(g,"offsetMs",offset);source.mute=mute;Call(g,"ShowMenu");
            if(Directory.Exists(dir))Directory.Delete(dir,true);
            if(previous==null){if(File.Exists(log))File.Delete(log);}else File.WriteAllBytes(log,previous);
        }
    }
}
