using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Cascade.Prototype
{
    [Serializable] public sealed class CascadeCatalog
    {
        public string content_id;
        public int revision;
        // Release testing access is reversible and never fabricates earned unlocks.
        public bool testing_unlock_all_records;
        public CascadeRecord[] records;
        public CascadeTrack[] Tracks => records.OrderBy(r=>r.order).SelectMany(r=>r.tracks.OrderBy(t=>t.order)).ToArray();
        public CascadeRecord RecordFor(string id) => records.First(r=>r.tracks.Any(t=>t.track_id==id));
        public void Validate()
        {
            if(records==null||records.Length!=8||records.Select(r=>r.record_id).Distinct().Count()!=8)throw new InvalidDataException("Expected eight distinct records");
            var ids=new HashSet<string>();
            foreach(var r in records)
            {
                if(r.tracks==null||r.tracks.Length==0||r.unlock_next_requires_track_ids==null)throw new InvalidDataException("Missing record tracks");
                foreach(var t in r.tracks)
                    if(string.IsNullOrEmpty(t.track_id)||!ids.Add(t.track_id)||string.IsNullOrEmpty(t.title)||string.IsNullOrEmpty(t.runtime_audio_path)||string.IsNullOrEmpty(t.beatmap_path)||t.runtime_audio_sha256?.Length!=64||t.runtime_duration_seconds<=0)
                        throw new InvalidDataException("Incomplete track: "+t.track_id);
                if(r.unlock_next_requires_track_ids.Any(id=>!r.tracks.Any(t=>t.track_id==id)))throw new InvalidDataException("Unknown required track");
                if(!string.IsNullOrEmpty(r.prerequisite_record_id)&&!records.Any(p=>p.record_id==r.prerequisite_record_id))throw new InvalidDataException("Unknown prerequisite");
            }
        }
    }
    [Serializable] public sealed class CascadeRecord
    {
        public string record_id,display_name,prerequisite_record_id,cover_file;
        public int order; public bool initially_unlocked;
        public string[] unlock_next_requires_track_ids; public CascadeTrack[] tracks;
    }
    [Serializable] public sealed class CascadeTrack
    {
        public string track_id,title,source_audio_sha256,runtime_audio_path,runtime_audio_sha256,beatmap_path,beatmap_status;
        public int order,content_revision; public double runtime_duration_seconds;
        public string level_settings_path="V3/balance";
    }
    [Serializable] public sealed class CascadeTrackResult
    {
        public string trackId,audioSha256; public int bestScore; public bool completed;
    }
    [Serializable] public sealed class CascadeProgress
    {
        public int saveVersion=3; public string contentId;
        public List<string> unlockedRecordIds=new List<string>();
        public List<CascadeTrackResult> results=new List<CascadeTrackResult>(), archivedResults=new List<CascadeTrackResult>();
        public float volume=.7f; public bool softEffects; public int calibrationMs;
        public int preferencesRevision;
        public bool musicEnabled=true, soundsEnabled=true, hapticsEnabled=true;
        public string language="ru", selectedHero="Mi", dailyDate="";
        public int dailyHits,dailyTracks,dailyCascades;
    }
    // Results are tied to the actual song hash. Earned unlocks never depend on the current track count.
    public sealed class CascadeProgressStore
    {
        [Serializable] sealed class Envelope { public string payload,sha256; }
        public CascadeProgress Data { get; private set; }
        public string PathName { get; }
        readonly CascadeCatalog catalog;
        public bool RecoveredBackup { get; private set; }
        static string Hash(string text) { using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant(); }
        public CascadeProgressStore(string path,CascadeCatalog catalog,float volume=.7f,bool soft=false,int offset=0)
        {
            PathName=path;this.catalog=catalog;
            Data=Read(path);
            if(Data==null){Data=Read(path+".bak");RecoveredBackup=Data!=null;}
            if(Data==null)Data=new CascadeProgress{volume=volume,softEffects=soft,calibrationMs=offset};
            if(Data.preferencesRevision<1)
            {
                Data.musicEnabled=Data.volume>0;Data.soundsEnabled=true;Data.hapticsEnabled=true;
                Data.language="ru";Data.selectedHero="Mi";Data.preferencesRevision=1;
            }
            if(Data.language!="en")Data.language="ru";
            if(Data.selectedHero!="Fa")Data.selectedHero="Mi";
            foreach(var result in Data.results.ToArray())
            {
                var t=catalog.Tracks.FirstOrDefault(x=>x.track_id==result.trackId);
                if(t==null||t.runtime_audio_sha256!=result.audioSha256){Data.archivedResults.Add(result);Data.results.Remove(result);}
            }
            Data.contentId=catalog.content_id;Data.saveVersion=3;
            foreach(var r in catalog.records.Where(r=>r.initially_unlocked))if(!Data.unlockedRecordIds.Contains(r.record_id))Data.unlockedRecordIds.Add(r.record_id);
        }
        static CascadeProgress Read(string path)
        {
            try
            {
                var e=JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
                if(e==null||string.IsNullOrEmpty(e.payload)||Hash(e.payload)!=e.sha256)return null;
                var d=JsonUtility.FromJson<CascadeProgress>(e.payload);
                if(d==null||d.saveVersion<2||d.saveVersion>3||d.unlockedRecordIds==null||d.results==null||d.archivedResults==null||float.IsNaN(d.volume)||d.volume<0||d.volume>1||Math.Abs(d.calibrationMs)>400)return null;
                if(d.results.Any(x=>x==null||string.IsNullOrEmpty(x.trackId)||string.IsNullOrEmpty(x.audioSha256)||x.bestScore<0))return null;
                return d;
            }catch(Exception e) when(e is IOException||e is ArgumentException||e is UnauthorizedAccessException){return null;}
        }
        public bool IsOpen(CascadeRecord r)=>catalog.testing_unlock_all_records||Data.unlockedRecordIds.Contains(r.record_id);
        public void RefreshDaily()
        {
            string today=DateTime.Now.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
            if(Data.dailyDate==today)return;
            Data.dailyDate=today;Data.dailyHits=Data.dailyTracks=Data.dailyCascades=0;
        }
        public void RecordAttempt(int hits,int cascades,bool completed)
        {
            RefreshDaily();Data.dailyHits+=Math.Max(0,hits);Data.dailyCascades+=Math.Max(0,cascades);
            if(completed)Data.dailyTracks++;Save();
        }
        public CascadeTrackResult Result(CascadeTrack t)=>Data.results.FirstOrDefault(r=>r.trackId==t.track_id&&r.audioSha256==t.runtime_audio_sha256);
        public void Complete(CascadeTrack track,int score)
        {
            string before=JsonUtility.ToJson(Data);
            try{CompleteAndSave(track,score);}
            catch{Data=JsonUtility.FromJson<CascadeProgress>(before);throw;}
        }
        void CompleteAndSave(CascadeTrack track,int score)
        {
            var record=catalog.RecordFor(track.track_id);
            if(!IsOpen(record))throw new InvalidOperationException("Locked record cannot be completed");
            var result=Result(track);
            if(result==null){result=new CascadeTrackResult{trackId=track.track_id,audioSha256=track.runtime_audio_sha256};Data.results.Add(result);}
            result.completed=true;result.bestScore=Math.Max(result.bestScore,score);
            if(record.unlock_next_requires_track_ids.All(id=>Result(record.tracks.First(t=>t.track_id==id))?.completed==true))
                foreach(var next in catalog.records.Where(r=>r.prerequisite_record_id==record.record_id))
                    if(!Data.unlockedRecordIds.Contains(next.record_id))Data.unlockedRecordIds.Add(next.record_id);
            Save();
        }
        public void Save()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathName));
            string payload=JsonUtility.ToJson(Data),text=JsonUtility.ToJson(new Envelope{payload=payload,sha256=Hash(payload)});
            string temp=PathName+".tmp",backup=PathName+".bak";
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            {byte[] bytes=Encoding.UTF8.GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(File.Exists(PathName))
            {
                // Never replace the last valid backup with a corrupt primary file during recovery.
                if(Read(PathName)!=null)File.Replace(temp,PathName,backup);
                else File.Replace(temp,PathName,null);
            }
            else File.Move(temp,PathName);
            if(!File.Exists(backup))File.Copy(PathName,backup);
        }
    }
}
