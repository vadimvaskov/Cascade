#!/usr/bin/env python3
"""Check WAV identity, mapping invariants and an independent onset detector.

The low-pass RMS detector differs from the spectral-flux tracker used to export
maps. Agreement is timing evidence; it is not listening or device validation.
"""
import argparse
import array
import bisect
import hashlib
import json
import math
import statistics
import subprocess
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def p95(values):
    return sorted(values)[min(len(values)-1,int(len(values)*.95))] if values else None


def envelope_peaks(source, ffmpeg):
    raw = subprocess.check_output([ffmpeg, '-v', 'error', '-i', str(source),
        '-af', 'lowpass=f=180', '-ar', '6000', '-ac', '1', '-f', 'f32le', 'pipe:1'])
    samples = array.array('f')
    samples.frombytes(raw)
    energy = [math.sqrt(sum(v*v for v in samples[i:i+60])/60)
        for i in range(0,len(samples)-60,60)]
    delta = [0]+[max(0,energy[i]-energy[i-1]) for i in range(1,len(energy))]
    return [i*.01+.005 for i in range(1,len(delta)-1)
        if delta[i]>=delta[i-1] and delta[i]>=delta[i+1] and delta[i]>.005]


def check_peaks(beats, excluded, peaks, duration):
    scorable = [t for t in beats if not any(e['start']<=t<e['end'] for e in excluded)]
    matched = []
    for t in scorable:
        i = bisect.bisect_left(peaks,t)
        near = peaks[max(0,i-1):i+1]
        nearest = min(near,key=lambda x:abs(t-x)) if near else None
        if nearest is not None and abs(nearest-t)<=.12:
            matched.append((t,nearest-t))
    windows = []
    for start in range(0,math.ceil(duration),30):
        errors = [e for t,e in matched if start<=t<start+30]
        windows.append({'start':start,'end':min(duration,start+30),
            'matchedBeats':len(errors),
            'medianErrorMs':round(statistics.median(errors)*1000,3) if errors else None})
    errors = [e for t,e in matched]
    return {'method':'independent-180Hz-RMS-attack','matchedBeats':len(matched),
        'scorableBeats':len(scorable),'matchedFraction':round(len(matched)/max(1,len(scorable)),4),
        'medianErrorMs':round(statistics.median(errors)*1000,3) if errors else None,
        'absoluteErrorP95Ms':round(p95([abs(e) for e in errors])*1000,3) if errors else None,
        'windows':windows}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--ffmpeg',default='/tmp/cascade-ffmpeg')
    parser.add_argument('--write',action='store_true')
    parser.add_argument('--features',default='/tmp/cascade-audio-analysis')
    args = parser.parse_args()
    manifest = json.loads((ROOT/'Assets/Cascade/Resources/Data/audio-manifest.json').read_text())
    report = json.loads((ROOT/'Documentation/AudioAnalysis.json').read_text())
    for track in manifest['tracks']:
        name = track['resourceName']
        source = ROOT/'Assets/Cascade/Resources/Audio'/(name+'.wav')
        data = json.loads((ROOT/'Assets/Cascade/Resources/Data'/(name+'.json')).read_text())
        actual_hash = hashlib.sha256(source.read_bytes()).hexdigest()
        assert actual_hash==track['audioSha256']==data['audioSha256'], name+' audio hash'
        with wave.open(str(source)) as w:
            duration = w.getnframes()/w.getframerate()
            assert track['samples']==w.getnframes() and track['sampleRate']==w.getframerate()
        assert abs(duration-data['duration'])<1e-6 and abs(duration-track['duration'])<1e-6
        beats, excluded = data['beats'], data['excluded']
        assert data['verified'] is False and data['reviewStatus']=='automated-review-pending-listen'
        assert all(0<=t<duration for t in beats) and all(a<b for a,b in zip(beats,beats[1:]))
        assert all(0<=e['start']<e['end']<=duration for e in excluded)
        assert all(a['end']<=b['start'] for a,b in zip(excluded,excluded[1:]))
        matching = next(r for r in report['tracks'] if r['trackId']==track['trackId'])
        if track['trackId']=='dark-bass-drop':
            from dark_pulse_map import validate_map
            assert data['version']=='pulse-2', 'Dark must not regress to the phase-changing bass tracker'
            metrics = validate_map(data,args.ffmpeg)
            assert metrics['passed'], name+' independent percussion phase continuity'
            matching['independentPulseCheck'] = metrics
            print(name,'PASS',json.dumps({k:v for k,v in metrics.items() if k!='phaseWindows'}))
        else:
            metrics = check_peaks(beats,excluded,envelope_peaks(source,args.ffmpeg),duration)
            assert metrics['matchedFraction']>=.90, name+' independent attack support'
            assert metrics['absoluteErrorP95Ms']<=50, name+' independent onset residual'
            matching['independentEnvelopeCheck'] = metrics
            print(name,'PASS',json.dumps({k:v for k,v in metrics.items() if k!='windows'}))
    report['validationNote'] = ('Both detectors are automatic. Their agreement tests timing support, '
        'not the musical choice of metrical level or perceptual latency. A listening and device '
        'calibration review remains required.')
    if args.write:
        (ROOT/'Documentation/AudioAnalysis.json').write_text(json.dumps(report,indent=2)+'\n')

if __name__=='__main__':
    main()
