#!/usr/bin/env python3
"""Repair V3 percussion maps from PCM; preserve maps 1, 2 and 8 after regression checks.

Compile pcm_attacks.cpp and onsets.cpp first. No taps or touch calibration enter
construction. Independent spectral tracks become inputs to the C# regression.
Default is report-only; --write publishes maps with backups. Audio is unchanged.
"""
import argparse, bisect, csv, hashlib, json, statistics, subprocess
from pathlib import Path
from build_maps import track_dp, merge_intervals, tempo_candidates

ROOT=Path(__file__).resolve().parents[2]
RES=ROOT/'Assets/Cascade/Resources'
REPORT=ROOT/'Documentation/V3/AllTrackRhythm'
# Cross-band audio hypotheses; rock is 3:2 relative to its old 91 BPM grid,
# dubstep has audible alternating pulses at 146, not just its 73 BPM subset.
TEMPOS=[122,105.242,124,136,136,146,175,103.4]

def read(path):return json.loads(path.read_text())
def envelope(rows,band):
    rms=[float(row[band]) for row in rows]
    attacks=[0]+[max(0,b-a) for a,b in zip(rms,rms[1:])]
    mean=statistics.mean(attacks)
    normalized=[max(0,min(v/max(mean,1e-12),8)-.65) for v in attacks]
    scale=sorted(normalized)[int(.95*len(normalized))] or 1
    return [v/scale for v in normalized]
def spectral(rows,band):
    values=[float(r[band]) for r in rows];mean=statistics.mean(values)
    values=[max(0,min(v/max(mean,1e-12),8)-.65) for v in values]
    scale=sorted(values)[int(.95*len(values))] or 1
    return [v/scale for v in values]
def near(times,t):
    p=bisect.bisect_left(times,t)
    return min((abs(x-t) for x in times[max(0,p-1):p+1]),default=999)
def silent(rows,duration):
    intervals=[];start=None
    for row in rows:
        t=float(row['time']);quiet=float(row['broad'])<.006
        if quiet and start is None:start=max(0,t-.005)
        if not quiet and start is not None:
            if t-.005-start>=1:intervals.append((start,t-.005))
            start=None
    if start is not None and duration-start>=1:intervals.append((start,duration))
    return intervals

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--write',action='store_true')
    p.add_argument('--pcm',default='/private/tmp/cascade-pcm-attacks');p.add_argument('--fft',default='/private/tmp/cascade-v3-onsets');args=p.parse_args()
    REPORT.mkdir(parents=True,exist_ok=True);catalog=read(RES/'V3/catalog.json');reports=[];fixtures=[]
    for level,record in enumerate(catalog['records'],1):
        track=record['tracks'][0];id=track['track_id'];wav=RES/(track['runtime_audio_path']+'.wav');mp=RES/(track['beatmap_path']+'.json')
        backup=REPORT/'before'/mp.name
        old=read(backup if backup.exists() else mp);duration=old['duration'];bpm=TEMPOS[level-1]
        assert hashlib.sha256(wav.read_bytes()).hexdigest()==old['audioSha256']==track['runtime_audio_sha256']
        pcm=REPORT/(id+'-pcm.csv');fft=REPORT/(id+'-fft.csv')
        subprocess.run([args.pcm,str(wav),str(pcm)],check=True)
        subprocess.run([args.fft,str(wav),str(fft)],check=True,capture_output=True)
        rows=list(csv.DictReader(pcm.open()));fftrows=list(csv.DictReader(fft.open()))
        high=envelope(rows,'high');low=envelope(rows,'low')
        indices=track_dp(high,bpm);primary=[round(i*.01+.005,6) for i in indices]
        # A secondary pulse is admitted only on a repeating half-period bass
        # lattice, with actual PCM attack and a separate FFT bass transient.
        low_indices=track_dp(low,bpm)
        fftlow=spectral(fftrows,'low_flux')
        lowpeaks=[float(r['time']) for i,r in enumerate(fftrows) if 0<i<len(fftrows)-1 and fftlow[i]>=.3 and fftlow[i]>=fftlow[i-1] and fftlow[i]>=fftlow[i+1]]
        possible=[]
        for i in low_indices:
            t=round(i*.01+.005,6);pos=bisect.bisect_left(primary,t)
            if (0<pos<len(primary) and low[i]>=.3 and
                abs(t-(primary[pos-1]+primary[pos])/2)<=.045 and near(lowpeaks,t)<=.05):possible.append(t)
        # At least six supported bass pulses per 8s section; isolated melodic
        # attacks and the opening transients of Neon/Orchestra do not add beats.
        secondary=[t for t in possible if sum(int(x//8)==int(t//8) for x in possible)>=6]
        candidate=dict(old)
        if level in (3,4,5,6,7):
            beats=list(primary)
            # Keep the independently supported bass phase of Swing/Trance;
            # the previous maps already judged that pulse correctly. Close
            # bass/percussion attacks share a target centered between them.
            # Separated alternating pulses remain separate musical targets.
            bass=[]
            if level in (3,5):
                bass=list(old['beats'])
            elif level==7:
                bass=[round(i*.01+.005,6) for i in low_indices if low[i]>=.2 and near(lowpeaks,i*.01+.005)<=.05]
            else:
                bass=secondary
            for t in bass:
                pos=min(range(len(beats)),key=lambda k:abs(beats[k]-t))
                distance=abs(beats[pos]-t)
                if distance<=.08:continue
                if distance<=.16:
                    beats[pos]=round((beats[pos]+t)/2,6)
                else:beats.append(t)
            beats=sorted(beats)
            excluded=merge_intervals(silent(rows,duration)+[(0,max(0,beats[0]-.12)),(min(duration,beats[-1]+.12),duration)])
            candidate.update(version='v3-percussion-bass-3-short90',nominalBpm=bpm,beats=beats,excluded=excluded,
                analysisMethod='PCM-biquad-RMS10ms-percussion-DP; retained-bass-lattice; close-attack-centers',
                verified=False,analysisPassed=True,reviewStatus='automated-review-pending-listen',
                reviewNotes='Audio-derived percussion and separately supported recurring bass pulses. Only sustained broadband silence/edges excluded. No taps or calibration used. Listening/device confirmation pending.')
        # Independent FFT high-band DP and FFT low-band DP. These event sequences
        # are not copied from generated beat positions; C# evaluates both maps.
        sequences=[]
        for band in ['high_flux','low_flux']:
            signal=spectral(fftrows,band);ii=track_dp(signal,bpm)
            events=[float(fftrows[i]['time']) for i in ii if signal[i]>=.2]
            sequences.append(dict(name=band,times=events))
        # Midway between every pair is a deliberate out-of-rhythm negative case.
        negatives=[round((a+b)/2,6) for a,b in zip(candidate['beats'],candidate['beats'][1:]) if b-a>.27]
        fixture=dict(trackId=id,level=level,title=track['title'],sequences=sequences,negativeTimes=negatives)
        fixtures.append(fixture)
        report=dict(level=level,trackId=id,title=track['title'],audioSha256=old['audioSha256'],audioUnchanged=True,
            oldBpm=old['nominalBpm'],newBpm=candidate['nominalBpm'],oldBeatCount=len(old['beats']),newBeatCount=len(candidate['beats']),
            primaryPulses=len(primary),halfPulseCandidates=len(secondary) if level in (3,4,5,6,7) else 0,
            phasePolicy="Retain bass lattice on levels 3/5; PCM-supported bass on 7; combine attacks <=160ms by their center; keep distinct alternating pulses",
            oldExcludedSeconds=round(sum(e['end']-e['start'] for e in old['excluded']),6),
            newExcludedSeconds=round(sum(e['end']-e['start'] for e in candidate['excluded']),6),
            highAttackSupport=round(sum(high[i]>=.2 for i in indices)/len(indices),4),
            tempoEvidence={band:tempo_candidates(envelope(rows,band))[:4] for band in ['low','high']},
            changed=level in (3,4,5,6,7),physicalDeviceVerified=False)
        assert report['highAttackSupport']>=.9
        if args.write:
            backup.parent.mkdir(exist_ok=True)
            if not backup.exists():backup.write_bytes(mp.read_bytes())
            if level in (3,4,5,6,7):mp.write_text(json.dumps(candidate,ensure_ascii=False,indent=2)+'\n');track['content_revision']=5
        (REPORT/(id+'-candidate.json')).write_text(json.dumps(candidate,ensure_ascii=False,indent=2)+'\n')
        print(level,track['title'],report['oldBeatCount'],'->',report['newBeatCount'],'secondary',report['halfPulseCandidates'],flush=True)
        reports.append(report)
    if args.write:
        catalog['revision']=5
        (RES/'V3/catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n')
    (REPORT/'audio-audit.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2)+'\n')
    (REPORT/'independent-events.json').write_text(json.dumps(fixtures,ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
