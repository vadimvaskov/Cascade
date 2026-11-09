using System;
using System.Linq;
using UnityEngine;
namespace Cascade.Prototype
{
    public sealed partial class CascadeGame
    {
        CascadeMenuUI menuUI;
        public CascadeMenuUI MenuUI=>menuUI;
        public float MenuVolume=>volume;
        public bool MenuSoftEffects=>reduced;
        public void MenuSetVolume(float value){volume=Mathf.Clamp01(value);music.volume=volume;SaveSettings();}
        public void MenuSetSoftEffects(bool value){reduced=value;SaveSettings();}
        public void StartMenuTrack(string id)
        {
            if(State!=Phase.Menu)return;
            int index=Array.FindIndex(catalogTracks,t=>t.track_id==id);
            if(index<0){Error("Не удалось найти трек. Попробуй ещё раз.");return;}
            StartLevel(index);
        }
        bool HasNextOpenRecord()
        {
            var record=catalog.RecordFor(catalogTracks[level].track_id);
            return catalog.records.Any(r=>r.prerequisite_record_id==record.record_id&&progressStore.IsOpen(r));
        }
        bool IsLastRecordComplete()
        {
            if(developerPreview)return false;
            var record=catalog.RecordFor(catalogTracks[level].track_id);
            return record.order==catalog.records.Max(r=>r.order)&&catalog.Tracks.All(t=>progressStore.Result(t)?.completed==true);
        }
    }
}
