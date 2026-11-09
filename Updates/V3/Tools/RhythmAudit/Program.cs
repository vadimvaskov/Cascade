using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using Cascade.Prototype;
class Sequence { public string name=""; public double[] times=Array.Empty<double>(); }
class Fixture { public string trackId="",title="";public int level=0;public Sequence[] sequences=Array.Empty<Sequence>();public double[] negativeTimes=Array.Empty<double>(); }
class ReplayStats { public int maxMultiplier,hits,misses,bestStreak; public Dictionary<string,int> kinds; }
class Journal { public string track="";public int offsetMs=0;public Move[] moves=Array.Empty<Move>(); }
class Move { public double time=0;public int multiplier=0;public string rhythm="",rejection=""; }
class Program {
 static JsonSerializerOptions json=new(){IncludeFields=true,WriteIndented=true};
 static T Load<T>(string path)=>JsonSerializer.Deserialize<T>(File.ReadAllText(path),json);
 static ReplayStats Replay(BeatMap map,Balance balance,double[] times,double offset){
  var rhythm=new Rhythm(map,balance);int maximum=1;var kinds=new Dictionary<string,int>();
  foreach(double t in times){var hit=rhythm.Evaluate(t+offset,offset);maximum=Math.Max(maximum,hit.multiplier);kinds[hit.kind]=kinds.GetValueOrDefault(hit.kind)+1;}
  return new ReplayStats {maxMultiplier=maximum,hits=rhythm.Hits,misses=rhythm.Misses,bestStreak=rhythm.Best,kinds=kinds};
 }
 static void Require(bool condition,string label){if(!condition)throw new Exception(label);}
 static void Main(string[] args){
  string root=Path.GetFullPath(args[0]),report=Path.Combine(root,"Documentation/V3/AllTrackRhythm"),resources=Path.Combine(root,"Assets/Cascade/Resources/V3");
  var balance=Load<Balance>(Path.Combine(resources,"balance.json"));var fixtures=Load<Fixture[]>(Path.Combine(report,"independent-events.json"));var results=new List<object>();
  foreach(var f in fixtures){
   var old=Load<BeatMap>(Path.Combine(Directory.Exists(Path.Combine(report,"before"))?Path.Combine(report,"before"):Path.Combine(resources,"Beatmaps"),f.trackId+".json"));
   var candidate=Load<BeatMap>(Path.Combine(report,f.trackId+"-candidate.json"));
   Require(candidate.Valid(90,candidate.audioSha256,true),f.title+" map structure/hash");
   var sequences=new List<object>();
   foreach(var seq in f.sequences){
    var before=Replay(old,balance,seq.times,0);var after=Replay(candidate,balance,seq.times,0);var common=seq.times.Where(t=>t+.142<candidate.duration&&!candidate.IsExcluded(t)&&!candidate.IsExcluded(t+.142)).ToArray();
    var uncalibrated=Replay(candidate,balance,common,0);var calibrated=Replay(candidate,balance,common,.142);
    if(seq.name=="high_flux")Require(after.maxMultiplier==10,f.title+" independent percussion must reach x10");
    Require(after.hits>=before.hits-1,f.title+" bass/percussion regression");
    Require(uncalibrated.hits==calibrated.hits&&uncalibrated.misses==calibrated.misses&&uncalibrated.maxMultiplier==calibrated.maxMultiplier,f.title+" calibration independence");
    sequences.Add(new{seq.name,before,after,commonScorableEvents=common.Length,commonWithoutCalibration=uncalibrated,afterWithSavedCalibration=calibrated});
   }
   // Actual production C# rules: thresholds, duplicate, and saved calibration.
   var r=new Rhythm(candidate,balance);int count=0;double last=0;
   foreach(var b in candidate.beats.Where(t=>t+.142<90&&!candidate.IsExcluded(t)&&!candidate.IsExcluded(t+.142)).Take(30)){
    var hit=r.Evaluate(b+.142,.142);count++;last=b+.142;
    Require(hit.kind=="hit"&&hit.multiplier==Math.Min(10,1+count/3),f.title+" threshold "+count);
   }
   Require(count==30&&r.Multiplier==10,f.title+" x10 cap");Require(r.Evaluate(last,.142).kind=="repeat",f.title+" duplicate");
   var wrong=new Rhythm(candidate,balance);int rejected=0,judged=0;
   foreach(double t in f.negativeTimes){var rr=wrong.Evaluate(t,0);if(rr.kind=="miss")rejected++;if(rr.kind!="excluded")judged++;}
   Require(judged>0&&rejected==judged,f.title+" offbeat negative "+rejected+"/"+judged);
   results.Add(new{f.level,f.title,sequences,thresholds="PASS: x2 at3, x10 at27, cap, duplicate, +142ms calibration",offbeatRejected=rejected,offbeatJudged=judged});
  }
  var journalPath=Path.Combine(report,"device-attempt-before.json");
  if(File.Exists(journalPath)){
   var journal=Load<Journal>(journalPath);var old=Load<BeatMap>(Path.Combine(report,"before",journal.track+".json"));var map=Load<BeatMap>(Path.Combine(resources,"Beatmaps",journal.track+".json"));
   var moves=journal.moves.Where(m=>string.IsNullOrEmpty(m.rejection)).ToArray();var prior=new Rhythm(old,balance);
   foreach(var move in moves){var hit=prior.Evaluate(move.time,journal.offsetMs/1000d);Require(hit.kind==move.rhythm&&hit.multiplier==move.multiplier,"Exact device replay before repair at "+move.time);}
   var times=moves.Select(m=>m.time-journal.offsetMs/1000d).ToArray();var before=Replay(old,balance,times,journal.offsetMs/1000d);var after=Replay(map,balance,times,journal.offsetMs/1000d);
   File.WriteAllText(Path.Combine(report,"device-replay.json"),JsonSerializer.Serialize(new{journal.track,journal.offsetMs,acceptedMoves=moves.Length,before,after,exactBeforeReplay=true,newPhysicalDeviceTest=false},json)+"\n");
  }
  File.WriteAllText(Path.Combine(report,"rhythm-regression.json"),JsonSerializer.Serialize(results,json)+"\n");
  Console.WriteLine(JsonSerializer.Serialize(results,json));
 }
}
