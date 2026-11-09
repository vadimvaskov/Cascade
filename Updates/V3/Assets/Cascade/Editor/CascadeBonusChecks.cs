using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cascade.Prototype;
using UnityEngine;
using UnityEngine.UI;

public static class CascadeBonusChecks
{
    static void Check(bool ok,string label){if(!ok)throw new Exception("Bonus: "+label);}
    static Balance Config()=>JsonUtility.FromJson<Balance>(Resources.Load<TextAsset>("V3/balance").text);
    static BeatMap Map()=>new BeatMap{duration=80,beats=Enumerable.Range(1,79).Select(i=>(double)i).ToArray()};
    sealed class Session
    {
        public BeatMap map;public Rhythm rhythm;public RhythmBonus bonus;public double offset;
        public Session(BeatMap m=null,double calibration=0){map=m??Map();offset=calibration;rhythm=new Rhythm(map,Config());bonus=new RhythmBonus(map,Config());}
        public bool Tap(double musicalTime)=>bonus.Evaluate(rhythm.Evaluate(musicalTime+offset,offset),musicalTime+offset,offset);
        public RhythmBonusView View(double musicalTime)=>bonus.View(musicalTime+offset,offset);
        public void Charge(){for(int i=1;i<=6;i++)Check(!Tap(i),"charging hit stays +1");}
    }
    public static string Run()
    {
        var checks=new List<string>();var cfg=Config();Check(cfg.rhythmBonusHits==6&&cfg.rhythmBonusBeats==4&&cfg.hitsPerStep==3&&cfg.maxMultiplier==10,"production config 6/4 and ×10/3");
        foreach(double offset in new[]{0,.142,-.08})
        {
            var s=new Session(calibration:offset);Check(s.View(0).charge==0&&s.View(0).remaining==0,"new attempt empty");
            for(int i=1;i<6;i++){Check(!s.Tap(i),"ordinary +1 before charge");Check(s.View(i).charge==i,"visible charge");}
            Check(!s.Tap(6)&&s.View(6).remaining==4&&s.View(6).pending,"sixth hit arms next four, not itself");
            Check(!s.Tap(6.01)&&s.View(6.01).remaining==4,"charging beat repeat cannot spend or recharge");
            for(int beat=7;beat<=10;beat++)
            {
                Check(s.Tap(beat),"unique hit gets +2 on beat "+beat);
                Check(!s.Tap(beat+.01),"repeat gets ordinary +1");
                Check(s.View(beat+.121).remaining==10-beat,"counter follows music, not touches");
            }
            Check(!s.Tap(11)&&s.View(11).charge==1,"new cycle only after four beats");
            for(int i=12;i<=16;i++)Check(!s.Tap(i),"recharging uses six fresh hits");
            Check(s.View(16).remaining==4&&s.Tap(17),"second bonus starts at next beat");
            Check(s.rhythm.Streak==17&&s.rhythm.Multiplier==6,"independent score streak");
        }
        checks.Add("6 charging hits +1; next four musical beats +2; no repeat/stack/recharge; second cycle; offsets 0/+142/-80 ms");
        {
            var s=new Session();s.Tap(1);s.Tap(2);s.Tap(2.4);Check(s.View(2.4).charge==0&&s.rhythm.Multiplier==1,"miss resets charge and multiplier");
            s=new Session();s.Charge();Check(!s.Tap(7.4)&&s.View(7.4).remaining==3,"miss leaves active bonus running");Check(s.Tap(8)&&s.View(8).charge==0,"hit recovers inside bonus without charging");
            s=new Session();s.Charge();Check(s.View(8.2).remaining==2&&s.View(10.2).remaining==0,"bonus expires without any moves");Check(!s.Tap(11)&&s.View(11).charge==1,"idle bonus cannot be saved for later");
            s=new Session();s.Tap(1);s.Tap(2);Check(s.View(11.999).charge==2&&s.View(12).charge==0,"charge timeout at 10 active seconds");Check(!s.Tap(12)&&s.View(12).charge==1,"timeout precedes next hit");
        }
        checks.Add("Miss resets only charging; active miss does not cancel; no-input expiry; 10-second charge timeout");
        {
            var s=new Session();s.Charge();Check(s.View(60).remaining==0,"render can be far ahead");Check(s.Tap(7)&&s.Tap(10.12),"late input and inclusive final timing window still work");Check(!s.Tap(10.121),"outside window receives no bonus");
            s=new Session();s.Charge();Check(s.Tap(6.88),"early edge of first bonus beat is included");
            Check(!s.bonus.Evaluate(new RhythmResult{kind="stale"},1,0)&&s.View(7).remaining==4,"stale judgement cannot mutate state");
            var map=Map();map.beats=new[]{1d,2,3,4,5,6,6.2,6.4,6.6,6.8,7};s=new Session(map);s.Charge();Check(s.Tap(6.89)&&!s.Tap(6.891),"dense map final edge obeys neighbour cap");
            map=Map();map.excluded=new[]{new Exclusion{start=6.5,end=9.5}};s=new Session(map);s.Charge();Check(!s.Tap(8)&&s.View(9).remaining==4,"excluded beats do not consume bonus slots");Check(s.Tap(10)&&s.Tap(13)&&s.View(13.2).remaining==0,"four eligible beats after musical break");
            map=Map();map.excluded=new[]{new Exclusion{start=2.1,end=15}};s=new Session(map);s.Tap(1);s.Tap(2);Check(s.View(15).charge==2,"excluded duration does not exhaust charging timeout");
            map=Map();map.beats=Enumerable.Range(1,8).Select(i=>(double)i).ToArray();map.duration=8.05;s=new Session(map);s.Charge();Check(s.View(6).remaining==2&&s.Tap(8.049)&&s.View(8.051).remaining==0,"short remaining song and final duration edge");
        }
        checks.Add("Read-only HUD vs delayed events; early/late bounds; dense beats; exclusions; song-end truncation");
        return "PASS: "+string.Join("; ",checks);
    }
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Read<T>(CascadeGame g,string field)=>(T)typeof(CascadeGame).GetField(field,F).GetValue(g);
    static void Set(CascadeGame g,string field,object value)=>typeof(CascadeGame).GetField(field,F).SetValue(g,value);
    static object Call(CascadeGame g,string method,params object[] a)=>typeof(CascadeGame).GetMethod(method,F).Invoke(g,a);
    public static string RunRuntime()
    {
        var g=UnityEngine.Object.FindAnyObjectByType<CascadeGame>();Check(Application.isPlaying&&g!=null,"Play mode required");
        var store=g.ProgressStore;string dir=Path.Combine(Path.GetTempPath(),"cascade-bonus-"+Guid.NewGuid().ToString("N"));
        var audio=Read<AudioSource>(g,"music");bool mute=audio.mute;int offset=Read<int>(g,"offsetMs");
        string log=Path.Combine(Application.persistentDataPath,"cascade-v3-last-attempt.json");byte[] prior=File.Exists(log)?File.ReadAllBytes(log):null;
        try
        {
            Set(g,"progressStore",new CascadeProgressStore(Path.Combine(dir,"save.json"),g.Catalog));audio.mute=true;
            typeof(CascadeRuntimeChecks).GetMethod("Fixture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{g,new int[36],1d});
            var clock=typeof(CascadeRuntimeChecks).GetMethod("Clock",BindingFlags.Static|BindingFlags.NonPublic);
            int total=0;
            // Controlled board resets isolate charging across real accepted moves without forcing a loss.
            // Clock, Rhythm, Bonus, score, logs and the actual UI remain the same throughout the sequence.
            foreach(double time in new[]{1d,2,3,4,5,6,6.01,7,7.01,7.4,8,9,10,11})
            {
                int[] cells=new int[36];cells[14]=2;
                Set(g,"<Model>k__BackingField",new Board(g.Model.config,731,cells));Call(g,"Render",cells);clock.Invoke(null,new object[]{g,time});
                if(time==6)
                {
                    int charge=g.Bonus.View(time,0).charge;int moves=g.MoveCount;Check(!g.PlaceCell(14,time),"occupied cell rejected");Check(g.MoveCount==moves&&g.Bonus.View(time,0).charge==charge,"invalid move neither charges nor resets");
                }
                bool expected=time==7||time==8||time==9||time==10;
                Check(g.PlaceCell(13,time),"real placement accepted");total++;
                Check(g.Model.cells[14]==(expected?4:3),"actual primary neighbour +1/+2 at "+time);
                var entry=Read<List<CascadeGame.MoveLog>>(g,"journal").Last();Check(entry.bonusApplied==expected,"attempt journal bonus decision");
                Check(g.State==CascadeGame.Phase.Playing,"bonus never adds an input lock");
                if(time==6)
                {
                    var label=Read<Text>(g,"streakText");Check(label.text.Contains("СЛЕДУЮЩИЕ 4 ДОЛИ"),"charged HUD");
                    Check(Math.Abs(label.rectTransform.anchoredPosition.x+label.rectTransform.rect.width/2-470.5f)<.01f&&label.preferredWidth<label.rectTransform.rect.width,"indicator fits and stays centred");
                }
                if(time==7.4)Check(g.Multiplier==1&&g.Bonus.View(time,0).remaining==3,"miss preserves active bonus but resets multiplier");
                if(time==8)
                {
                    g.Pause();double frozen=g.SongPosition;var view=g.Bonus.View(frozen,0);Call(g,"Update");
                    Check(g.SongPosition==frozen&&g.Bonus.View(g.SongPosition,0).remaining==view.remaining&&!g.PlaceCell(0,8.01),"pause freezes bonus and rejects input");g.Resume();
                }
            }
            Check(g.MoveCount==total&&Read<Text>(g,"streakText").text.Contains("ЗАРЯД +2 · 1/6"),"new charge after bonus");
            // Elapsed music alone advances the HUD, including when no touch arrives.
            var r=Read<Rhythm>(g,"rhythm");for(int beat=12;beat<=16;beat++)g.Bonus.Evaluate(r.Evaluate(beat,0),beat,0);
            clock.Invoke(null,new object[]{g,18.2});Call(g,"Update");Check(Read<Text>(g,"streakText").text.Contains("ОСТАЛОСЬ 2 ДОЛИ"),"HUD counts down without placement");
            clock.Invoke(null,new object[]{g,20.2});Call(g,"Update");Check(Read<Text>(g,"streakText").text.Contains("ЗАРЯД +2 · 0/6"),"HUD expires without placement");
            g.Restart();Check(g.State==CascadeGame.Phase.Countdown&&g.Bonus.View(0,0).charge==0&&g.Bonus.View(0,0).remaining==0,"restart clears charge and scheduled bonus");
            string native;var previousTouch=UnityEngine.InputSystem.Touchscreen.current;var probe=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Touchscreen>("Temporary bonus rapid native input");
            try{native=CascadeMenuChecks.FastPlacement(g,probe);}
            finally{UnityEngine.InputSystem.InputSystem.RemoveDevice(probe);if(previousTouch!=null&&previousTouch.added)previousTouch.MakeCurrent();}
            return "PASS: actual +1/+2 neighbour changes, 14 moves, repeat, miss, occupied cell, pause/resume, musical HUD countdown without input, journal, immediate input and restart\n"+native;
        }
        finally
        {
            Set(g,"progressStore",store);Set(g,"offsetMs",offset);Call(g,"ShowMenu");audio.mute=mute;
            if(Directory.Exists(dir))Directory.Delete(dir,true);if(prior==null){if(File.Exists(log))File.Delete(log);}else File.WriteAllBytes(log,prior);
        }
    }
}
