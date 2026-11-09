#!/usr/bin/env python3
"""90-second testing edits; originals remain in Documentation/Inputs/V3/Package.

Neon uses the strongest tempo in its first 90s, with a percussion attack tracker
instead of the incorrect 3:2 bass grid. No phone taps enter map generation.
"""
import argparse
import array
import hashlib
import json
import math
import statistics
import subprocess
import unicodedata
import wave
from pathlib import Path
from build_maps import features, tempo_candidates, track_dp, merge_intervals

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'Assets/Cascade/Resources/V3'
REPORT = ROOT/'Documentation/V3/ShortTracks'
PACKAGE = ROOT/'Documentation/Inputs/V3/Package'
FFMPEG = '/private/tmp/cascade-ffmpeg'
DURATION = 90.0

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def neon_map(wav, data):
    csv = REPORT/'neon-features.csv'
    subprocess.run(['/private/tmp/cascade-v3-onsets',str(wav),str(csv)],check=True,capture_output=True)
    times, novelty, rms = features(csv)
    candidates = tempo_candidates(novelty)
    bpm = candidates[0]['bpm']
    assert 103 < bpm < 108, candidates
    raw = subprocess.check_output([FFMPEG,'-v','error','-i',str(wav),'-af','highpass=f=1500',
        '-ar','12000','-ac','1','-f','f32le','pipe:1'])
    samples = array.array('f'); samples.frombytes(raw)
    energy = [math.sqrt(sum(v*v for v in samples[i:i+120])/120) for i in range(0,len(samples)-120,120)]
    attacks = [0.0]+[max(0,energy[i]-energy[i-1]) for i in range(1,len(energy))]
    mean = statistics.mean(attacks)
    onset = [max(0,min(v/mean,8)-.65) for v in attacks]
    scale = sorted(onset)[int(.95*len(onset))]
    onset = [v/scale for v in onset]
    indices = track_dp(onset,bpm)
    beats = [round(i*.01+.005,6) for i in indices]
    # A missing kick is not a silent passage. Only sustained broadband silence
    # or genuinely untracked edges suppress judgement.
    intervals = [(0,max(0,beats[0]-.12)),(min(DURATION,beats[-1]+.12),DURATION)]
    start = None
    for time, value in zip(times,rms):
        if value < .006 and start is None: start = max(0,time-.005)
        if value >= .006 and start is not None:
            if time-start >= 1: intervals.append((start,time-.005))
            start = None
    if start is not None and DURATION-start>=1: intervals.append((start,DURATION))
    gaps = [b-a for a,b in zip(beats,beats[1:])]
    supported = sum(onset[i]>=.2 for i in indices)/len(indices)
    assert len(beats)>140 and .49<=min(gaps)<=max(gaps)<=.67 and supported>.8
    data.update(version='v3-neon-percussion-2-short90',nominalBpm=bpm,beats=beats,
        excluded=merge_intervals(intervals),analysisPassed=True,verified=False,
        reviewStatus='automated-review-pending-listen',analysisMethod='PCM-highpass1500-RMS10ms-DP-strongest-spectral-tempo',
        reviewNotes='Tempo and pulse derived only from audio. No touch offsets or journals used. Listening confirmation pending.')
    report={'tempoCandidates':candidates,'chosenBpm':bpm,'beats':len(beats),'attackSupport':supported,
        'minInterval':min(gaps),'maxInterval':max(gaps),'excluded':data['excluded'],'listeningVerified':False}
    (REPORT/'neon-analysis.json').write_text(json.dumps(report,indent=2)+'\n')

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--write',action='store_true');args=parser.parse_args()
    if not args.write: parser.error('Use --write to publish the explicit 90-second edits')
    REPORT.mkdir(parents=True,exist_ok=True)
    catalog=json.loads((OUT/'catalog.json').read_text())
    original=json.loads((PACKAGE/'Data/Content_Catalog_v3.json').read_text())
    source_tracks={t['track_id']:t for r in original['records'] for t in r['tracks']}
    backup=REPORT/'before';backup.mkdir(exist_ok=True)
    if not (backup/'catalog.json').exists(): (backup/'catalog.json').write_bytes((OUT/'catalog.json').read_bytes())
    edits=[]
    for record in catalog['records']:
        for t in record['tracks']:
            source_t=source_tracks[t['track_id']];rel=Path(source_t['source_audio_path'])
            source=next(p for p in (PACKAGE/rel.parent).iterdir() if unicodedata.normalize('NFC',p.name)==rel.name)
            assert sha(source)==source_t['source_audio_sha256']
            wav=ROOT/'Assets/Cascade/Resources'/(t['runtime_audio_path']+'.wav')
            map_path=ROOT/'Assets/Cascade/Resources'/(t['beatmap_path']+'.json')
            if not (backup/map_path.name).exists(): (backup/map_path.name).write_bytes(map_path.read_bytes())
            data=json.loads((backup/map_path.name).read_text())
            old_hash=data['audioSha256'];old_duration=data['duration']
            temporary=REPORT/(t['track_id']+'.wav')
            subprocess.run([FFMPEG,'-v','error','-i',str(source),'-vn','-af','atrim=end=90,afade=t=out:st=89:d=1',
                '-ar','48000','-ac','2','-c:a','pcm_s16le','-y',str(temporary)],check=True)
            with wave.open(str(temporary)) as w:
                assert w.getnframes()==4320000 and w.getframerate()==48000 and w.getnchannels()==2
            # Verify decode identity before the intentional fade, independent of container headers.
            with wave.open(str(wav)) as old, wave.open(str(temporary)) as new:
                assert old.readframes(48000*89)==new.readframes(48000*89),'Unexpected start/content shift'
            temporary.replace(wav); digest=sha(wav)
            data.update(duration=DURATION,audioSha256=digest,version=data['version']+'-short90',
                beats=[b for b in data['beats'] if b<DURATION],
                excluded=[dict(start=e['start'],end=min(DURATION,e['end'])) for e in data['excluded'] if e['start']<DURATION])
            if t['track_id']=='synthwave_electro_01': neon_map(wav,data)
            map_path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
            t.update(runtime_duration_seconds=DURATION,runtime_audio_sha256=digest,content_revision=4,
                testing_edit=dict(start_seconds=0,duration_seconds=DURATION,fade_out_seconds=1))
            edits.append(dict(trackId=t['track_id'],originalSourceSha256=sha(source),previousRuntimeSha256=old_hash,
                previousDuration=old_duration,runtimeSha256=digest,duration=DURATION,unchangedFirst89Seconds=True,beats=len(data['beats'])))
            print(t['track_id'],DURATION,digest,flush=True)
    catalog.update(testing_unlock_all_records=True,revision=max(4,catalog['revision']))
    (OUT/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n')
    (REPORT/'audio-edits.json').write_text(json.dumps(edits,ensure_ascii=False,indent=2)+'\n')

if __name__=='__main__': main()
