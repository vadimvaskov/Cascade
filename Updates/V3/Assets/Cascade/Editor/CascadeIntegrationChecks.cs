using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Cascade.Prototype;
using UnityEngine;
using UnityEditor;

public static class CascadeIntegrationChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception("Integration: "+message);}
    [MenuItem("Cascade/Approved game/Check rhythm and saves")]
    public static string Run()
    {
        var report=new List<string>{CascadeRhythmChecks.Run(),CascadeBonusChecks.Run(),CascadeBuildValidation.Run(),CascadeRunPulseChecks.Run(),CascadeCompanionEnergyChecks.Run()};
        var source=new BeatMap{duration=12,nominalBpm=120,beats=Enumerable.Range(1,23).Select(i=>i*.5).ToArray(),excluded=Array.Empty<Exclusion>()};
        var grid=new RhythmGrid(source);var settings=new Balance();var judge=new Rhythm(grid.Map,settings);
        var taps=new[]{1d,1.5,1.75,2,2.125,2.25,2.375,2.5,3,3.5,3.75,3.875,4,4.5};
        foreach(var tap in taps)Check(judge.Evaluate(tap,0).kind=="hit","free quarter/eighth/sixteenth transition at "+tap);
        Check(judge.Streak==taps.Length,"slower rhythms do not incur skipped-tick misses");
        Check(judge.Evaluate(4.51,0).kind=="repeat","duplicate tick cannot award twice");
        Check(new Rhythm(grid.Map,settings).Evaluate(2.08,.08).kind=="hit","calibration on subdivision grid");
        Check(new Rhythm(grid.Map,settings).Evaluate(2.0625,0).kind=="miss","between-tick gap remains a miss");
        var delayed=new Rhythm(grid.Map,settings);delayed.Evaluate(1,0);delayed.Advance(2);Check(delayed.Evaluate(1.125,0).kind=="hit","queued original timestamps survive later render updates");
        source.excluded=new[]{new Exclusion{start=3,end=4}};grid=new RhythmGrid(source);
        Check(new Rhythm(grid.Map,settings).Evaluate(3.125,0).kind=="excluded","subdivision inside musical rest is excluded");
        Check(new Rhythm(grid.Map,settings).Evaluate(4,0).kind=="hit","rest ends on exact tick");
        source.excluded=Array.Empty<Exclusion>();grid=new RhythmGrid(source);
        var bonusBalance=new Balance{rhythmPrimaryBonus=true,rhythmBonusHits=6,rhythmBonusBeats=4};var bonus=new RhythmBonus(grid.Map,bonusBalance,4);judge=new Rhythm(grid.Map,bonusBalance);
        for(int i=0;i<6;i++){double t=1+i*.125;bonus.Evaluate(judge.Evaluate(t,0),t,0);}
        Check(bonus.View(1.625,0).remaining==4,"four quarter-beat bonus remains four quarters");
        Check(bonus.Evaluate(judge.Evaluate(3.625,0),3.625,0),"sixteenth hit near end of four-quarter bonus is eligible");
        Check(bonus.View(3.75,0).remaining==0,"bonus expires on shared grid");
        report.Add("PASS: free 1/4 → 1/8 → 1/16 → 1/4 transitions, repeats, calibration, rests, delayed events, musical bonus duration");
        foreach(var asset in Resources.LoadAll<TextAsset>("V3/Beatmaps"))
        {
            var map=JsonUtility.FromJson<BeatMap>(asset.text);var timing=new RhythmGrid(map);
            Check(timing.Map.beats.Length>map.duration*4,"full subdivision coverage "+map.trackId);
            Check(timing.Map.beats.Zip(timing.Map.beats.Skip(1),(a,b)=>b-a).All(d=>d>.055&&d<.2),"no double subdivision of dense detected attacks "+map.trackId);
            Check(timing.Map.verified==map.verified&&timing.Map.reviewStatus==map.reviewStatus,"no promotion of music analysis provenance");
            var j=new Rhythm(timing.Map,settings);int hits=0;
            foreach(var time in timing.Map.beats)if(!map.IsExcluded(time)){Check(j.Evaluate(time,0).kind=="hit","tick alignment "+map.trackId);hits++;}
            report.Add("PASS: "+map.trackId+" · "+timing.Quarters.Length+" quarter anchors · "+hits+" playable ticks");
        }
        var catalog=JsonUtility.FromJson<CascadeCatalog>(Resources.Load<TextAsset>("V3/catalog").text);
        string path=Path.Combine(Path.GetTempPath(),"CascadeIntegration-"+Guid.NewGuid()+"/progress.json");
        var store=new CascadeProgressStore(path,catalog);store.Data.musicEnabled=false;store.Data.soundsEnabled=false;store.Data.hapticsEnabled=false;store.Data.selectedHero="Fa";store.Data.language="en";store.RecordAttempt(10,3,true);
        var reread=new CascadeProgressStore(path,catalog);Check(!reread.Data.musicEnabled&&!reread.Data.soundsEnabled&&!reread.Data.hapticsEnabled&&reread.Data.selectedHero=="Fa"&&reread.Data.language=="en","settings persist through relaunch");
        Check(reread.Data.dailyHits==10&&reread.Data.dailyTracks==1&&reread.Data.dailyCascades==3,"daily task data persists");
        reread.Data.dailyDate="2000-01-01";reread.RefreshDaily();Check(reread.Data.dailyHits==0&&reread.Data.dailyTracks==0,"daily rollover");
        report.Add("PASS: persistent settings and daily rollover in an isolated save");
        Directory.CreateDirectory(CascadeIntegrationTools.Output);File.WriteAllText(CascadeIntegrationTools.Output+"/checks.txt",string.Join("\n",report));
        return string.Join("\n",report);
    }
}
