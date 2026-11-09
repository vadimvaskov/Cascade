using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cascade.Prototype;
using UnityEditor;
using UnityEngine;

public static class CascadeV3Checks
{
    static void Require(bool c,string name){if(!c)throw new Exception(name);}
    static void Near(float a,float b,string name){Require(Math.Abs(a-b)<.0001,name+": "+a+" != "+b);}
    public static string Run()
    {
        var checks=new List<string>();
        var b=JsonUtility.FromJson<Balance>(Resources.Load<TextAsset>("V3/balance").text);
        for(int charge=1;charge<=4;charge++)foreach(bool hit in new[]{false,true})
        {
            int[] cells=new int[36];cells[14]=charge;
            var turn=new Board(b,42,cells,1).Place(13,1,hit);
            int expected=charge==4?5:Math.Min(4,charge+(hit?2:1));
            Require(turn.afterPlacement[14]==expected,"Primary transition");
            Require(turn.afterPlacement[13]==1,"Placed orb gets no own bonus");
            Require((turn.waves.Count>0)==(charge==4),"Only original charge4 explodes");
        }
        checks.Add("8 primary transitions; saturation; placed orb unaffected");
        {
            var c=new int[36];c[8]=4;c[14]=2;c[15]=3;
            var turn=new Board(b,37,c,1).Place(9,3,true);
            Require(turn.afterPlacement[8]==5&&turn.afterPlacement[15]==4,"Initial wave");
            Require(turn.afterWaves[0][14]==3,"Cascade +1 only");
            Require(turn.points==(10+50*turn.destroyed)*3,"Whole cascade uses move multiplier");
            c=new int[36];c[7]=4;c[8]=4;c[14]=3;
            turn=new Board(b,37,c,1).Place(13,1,true);
            Require(turn.afterPlacement[14]==4,"Saturate 3 to4");
            // Square neighbor is charged by the separate wave: 8 explodes into14.
            Require(turn.afterWaves[1][14]>=5&&turn.waves.Count>=3,"Saturated orb can explode later");
            Require(turn.autoCell>=0&&turn.final[turn.autoCell]==turn.autoCharge,"One passive auto orb");
            Require(new Board(b,1,c).Place(8)==null,"Occupied placement ignored");
        }
        checks.Add("Cascade impulses +1, secondary explosion, whole-move score, passive auto, invalid input");
        var map=new BeatMap{duration=200,beats=Enumerable.Range(1,180).Select(x=>(double)x).ToArray()};
        var rhythm=new Rhythm(map,b);
        Require(rhythm.Multiplier==1,"Initial multiplier is ×1");
        int[] thresholds={3,6,9,12,15,18,21,24,27};
        for(int i=1;i<=40;i++)
        {
            var hit=rhythm.Evaluate(i,0);Require(hit.kind=="hit"&&hit.multiplier==1+thresholds.Count(n=>i>=n),"Multiplier threshold/cap at hit "+i);
        }
        int before=rhythm.Streak;var repeat=rhythm.Evaluate(40.01,0);Require(repeat.kind=="repeat"&&repeat.multiplier==1&&rhythm.Streak==before,"Repeat doesn't advance/reset");
        var miss=rhythm.Evaluate(40.4,0);Require(miss.kind=="miss"&&rhythm.Streak==0&&rhythm.Multiplier==1,"Miss resets to ×1");
        Require(rhythm.Evaluate(41,0).kind=="hit"&&rhythm.Streak==1,"Next beat recovers");
        Require(rhythm.Evaluate(51,0).multiplier==1&&rhythm.Streak==1,"Exact10s resets before evaluation");
        checks.Add("×1–×10: every third unique hit; ×10 from hit27; cap through hit40; repeat, miss recovery, exact10s timeout");
        var cfg=JsonUtility.FromJson<CascadeLightingConfig>(Resources.Load<TextAsset>("V3/lighting").text);
        var light=new CascadeRhythmLighting(map,cfg);
        foreach(var knot in cfg.peak_gain_knots)
        {var gains=light.Gains(knot.streak);for(int i=0;i<4;i++)Near(gains[i],knot.gain[i],"Gain knot");}
        Near(light.Gains(2).x,.25f,"Linear interpolation");Require(light.Gains(100)==light.Gains(16),"Visual cap16");
        Near(CascadeRhythmLighting.Envelope(1,1,.045,.16),1,"Peak on beat");
        Near(CascadeRhythmLighting.Envelope(.955,1,.045,.16),0,"Attack start");
        Near(CascadeRhythmLighting.Envelope(1.16,1,.045,.16),0,"Decay end");
        light.Tick(.8,0,16,true,false);var output=light.Tick(1,.12,16,true,false);
        Near(output.w,.4f,"Aura .4, no second multiplier");
        var pause=light.Tick(1,.13,16,false,false);var faded=light.Tick(1,.25,16,false,false);
        Require(pause.w>0&&faded==Vector4.zero,"Pause fade on UI clock");
        Require(light.Tick(1,.26,16,true,true)==Vector4.zero,"Soft disables all global channels");
        light.LocalHit(3,1);Near(light.Local(3,1,true,true),.15f,"Soft local limit");Near(light.Local(2,1,false,true),0,"Local only accepted cell");Near(light.Local(3,1.12,false,true),0,"Local ends120ms");
        var fast=new CascadeRhythmLighting(new BeatMap{duration=3,beats=Enumerable.Range(0,20).Select(i=>i*.13).ToArray()},cfg);
        Require(fast.PeakStride==3,"Smallest integer stride");var peaks=fast.VisualPeaks;Require(peaks.Zip(peaks.Skip(1),(a,z)=>z-a).All(x=>x>=1d/3),"No >3Hz peaks");
        checks.Add("All light knots/interpolation/cap; envelopes; 120ms pause fade; soft/local; ≤3Hz subset");
        var cat=JsonUtility.FromJson<CascadeCatalog>(Resources.Load<TextAsset>("V3/catalog").text);cat.Validate();
        foreach(var t in cat.Tracks)
        {
            var clip=Resources.Load<AudioClip>(t.runtime_audio_path);var bm=JsonUtility.FromJson<BeatMap>(Resources.Load<TextAsset>(t.beatmap_path).text);
            Require(clip!=null&&clip.channels==2&&clip.frequency==48000,"PCM stereo48k");
            Require(bm.trackId==t.track_id&&bm.Valid(clip.length,t.runtime_audio_sha256,true),"Audio map pair "+t.track_id);
            Require(!bm.verified&&bm.IsProvisional,"No fabricated listening review");
        }
        checks.Add("8 exact catalog audio/map pairs; durations, hashes, stereo48k, provisional status");
        string dir=Path.Combine(Path.GetTempPath(),"cascade-v3-check-"+Guid.NewGuid().ToString("N"));string path=Path.Combine(dir,"save.json");
        try
        {
            Require(cat.testing_unlock_all_records,"Testing catalog opens all records");
            var testing=new CascadeProgressStore(Path.Combine(dir,"testing.json"),cat,.4f,true,142);
            Require(cat.records.All(testing.IsOpen)&&testing.Data.unlockedRecordIds.Count==1&&testing.Data.results.Count==0,"Testing access must not fabricate completion/unlocks");
            testing.Save();
            Require(cat.records.All(new CascadeProgressStore(testing.PathName,cat).IsOpen),"All-eight access survives restart");
            cat.testing_unlock_all_records=false;
            Require(cat.records.Count(new CascadeProgressStore(testing.PathName,cat).IsOpen)==1,"Disabling testing restores earned gates");
            var store=new CascadeProgressStore(path,cat,.4f,true,142);
            Require(store.Data.unlockedRecordIds.SequenceEqual(new[]{"disco_funk"}),"Only record1 initially unlocked");
            Require(store.Data.calibrationMs==142&&store.Data.softEffects,"Legacy settings migration");
            store.Complete(cat.Tracks[0],0);Require(store.IsOpen(cat.records[1]),"Win with zero moves/score opens next");
            store.Complete(cat.Tracks[0],50);store.Complete(cat.Tracks[0],10);
            Require(store.Data.unlockedRecordIds.Count==2&&store.Result(cat.Tracks[0]).bestScore==50,"Replay idempotent/best completed only");
            store.Save();File.WriteAllText(path,"corrupt");
            var recovered=new CascadeProgressStore(path,cat);Require(recovered.RecoveredBackup&&recovered.Result(cat.Tracks[0]).bestScore==50,"Backup recovery");recovered.Save();
            Require(new CascadeProgressStore(path,cat).Result(cat.Tracks[0]).bestScore==50,"Recovered primary persisted");
            foreach(var t in cat.Tracks)recovered.Complete(t,100);
            Require(recovered.Data.unlockedRecordIds.Count==8,"Sequential8 unlocks");
            var old=cat.Tracks[4].runtime_audio_sha256;cat.Tracks[4].runtime_audio_sha256=new string('b',64);
            var migrated=new CascadeProgressStore(path,cat);
            Require(migrated.Data.unlockedRecordIds.Count==8&&migrated.Result(cat.Tracks[4])==null&&migrated.Data.archivedResults.Count==1,"Song replacement archives result, retains unlocks");
            cat.Tracks[4].runtime_audio_sha256=old;
            var freshPath=Path.Combine(dir,"multi.json");var first=cat.records[0];var added=new CascadeTrack{track_id="future-test",runtime_audio_sha256=new string('c',64)};
            first.tracks=first.tracks.Concat(new[]{added}).ToArray();first.unlock_next_requires_track_ids=first.unlock_next_requires_track_ids.Concat(new[]{added.track_id}).ToArray();
            var multi=new CascadeProgressStore(freshPath,cat);multi.Complete(first.tracks[0],10);Require(!multi.IsOpen(cat.records[1]),"Multiple required tracks gate unlock");multi.Complete(added,10);Require(multi.IsOpen(cat.records[1]),"Final required track opens next");
        }
        finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        checks.Add("Reversible all-eight testing access without fake wins; atomic/backup save; zero-score win; replay;8 unlocks; multi-track record; song-hash migration");
        return JsonUtility.ToJson(new Report{passed=true,checks=checks.ToArray()},true);
    }
    [Serializable] class Report{public bool passed;public string[] checks;}
}
