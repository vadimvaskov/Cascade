#!/usr/bin/env python3
"""Onset-assisted beat maps for Cascade's supplied PCM recordings.

No third-party Python packages are required. Compile onsets.cpp, extract CSVs,
then run this script. Exported maps stay provisional until listening review.
The first two tracks follow spectral attacks with a tempo continuity prior.
Dark uses its separately validated continuous percussion pulse; the old
bass-weighted tracker changed phase and must not regenerate that map.
"""
import argparse
import csv
import hashlib
import json
import math
import statistics
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TRACKS = [
    ("midnight-floor", "Midnight Floor", 110.0),
    ("drop-bass-princess", "Drop Bass Princess", 130.0),
    ("dark-bass-drop", "Dark Bass Drop", 88.0),
]
STEP = .01
METHOD = "spectral-flux-dp-v1"


def percentile(values, p):
    a = sorted(values)
    return a[min(len(a)-1, int((len(a)-1)*p))] if a else 0


def features(path):
    with path.open() as f:
        rows = list(csv.DictReader(f))
    times = [float(r["time"]) for r in rows]
    cols = [[float(r[k]) for r in rows] for k in ["low_flux", "mid_flux", "high_flux"]]
    normalized = []
    for col in cols:
        scale = statistics.mean(col)
        normalized.append([min(v/max(scale, 1e-9), 8) for v in col])
    signal = [.65*normalized[0][i] + .25*normalized[1][i] + .1*normalized[2][i] for i in range(len(rows))]
    baseline = statistics.mean(signal)
    novelty = [max(0, a-.65*baseline) for a in signal]
    scale = percentile(novelty, .95) or 1
    novelty = [v/scale for v in novelty]
    return times, novelty, [float(r["rms"]) for r in rows]


def tempo_candidates(x, min_bpm=70, max_bpm=180):
    mean = statistics.mean(x)
    v = [a-mean for a in x]
    lo, hi = round(6000/max_bpm), round(6000/min_bpm)
    ac = {lag: sum(v[i]*v[i-lag] for i in range(lag, len(v)))/(len(v)-lag) for lag in range(lo-1, hi+2)}
    candidates = []
    for lag in range(lo, hi+1):
        if ac[lag] >= ac[lag-1] and ac[lag] >= ac[lag+1]:
            a, b, c = ac[lag-1], ac[lag], ac[lag+1]
            delta = .5*(a-c)/(a-2*b+c) if a-2*b+c else 0
            candidates.append({"bpm": round(6000/(lag+delta), 3), "autocorrelation": round(ac[lag], 5)})
    return sorted(candidates, key=lambda v: v["autocorrelation"], reverse=True)


def track_dp(onset, bpm):
    period = 6000/bpm
    n = len(onset)
    # A narrow tempo prior prevents half/double-time switching during fills.
    penalties = {lag: 60.0*math.log(lag/period)**2 for lag in range(int(period*.85), int(period*1.16)+1)}
    scores = [0.0]*n
    back = [-1]*n
    for i in range(n):
        best, prev = 0, -1
        for lag, penalty in penalties.items():
            j = i-lag
            if j < 0:
                continue
            score = scores[j]-.12-penalty
            if score > best:
                best, prev = score, j
        scores[i] = onset[i]+best
        back[i] = prev
    tail = max(range(max(0, n-int(period*2)), n), key=lambda i: scores[i])
    indices = []
    while tail >= 0:
        indices.append(tail)
        tail = back[tail]
    indices.reverse()
    return indices


def onset_evidence(i, onset, rms):
    candidates = [j for j in range(max(1, i-8), min(len(onset)-1, i+9))
                  if onset[j] >= onset[j-1] and onset[j] >= onset[j+1] and onset[j] >= .20]
    distance = min(abs(j-i)*STEP for j in candidates) if candidates else .09
    maximum = max(onset[max(0, i-8):min(len(onset), i+9)])
    supported = onset[i] >= .20 and onset[i] >= .45*maximum and distance <= .05 and rms[i] >= .006
    return supported, distance, onset[i]


def merge_intervals(intervals):
    result = []
    for start, end in sorted(intervals):
        if end <= start:
            continue
        if result and start <= result[-1][1]+.001:
            result[-1][1] = max(result[-1][1], end)
        else:
            result.append([start, end])
    return [{"start": round(a, 6), "end": round(b, 6)} for a, b in result]


def review(times, indices, onset, rms, bpm, duration):
    beats = [times[i] for i in indices]
    evidence = [onset_evidence(i, onset, rms) for i in indices]
    intervals = [b-a for a,b in zip(beats, beats[1:])]
    excluded = []
    if beats:
        # Untracked file edges cannot grant accidental early/late rewards.
        excluded.append((0, max(0, beats[0]-.12)))
        excluded.append((min(duration, beats[-1]+.12), duration))
    for k, (supported, distance, strength) in enumerate(evidence):
        if not supported:
            lo = (beats[k-1]+beats[k])/2 if k else 0
            hi = (beats[k]+beats[k+1])/2 if k+1 < len(beats) else duration
            excluded.append((lo, hi))
    excluded = merge_intervals(excluded)
    def excluded_at(t):
        return any(x["start"] <= t < x["end"] for x in excluded)
    blocks = []
    for start in range(0, math.ceil(duration), 8):
        ks = [k for k,t in enumerate(beats) if start <= t < start+8]
        vals = [evidence[k] for k in ks]
        active_vals = [evidence[k] for k in ks if not excluded_at(beats[k])]
        local_intervals = [beats[k+1]-beats[k] for k in ks if k+1 < len(beats)]
        blocks.append({
            "start": start, "end": round(min(start+8,duration),6), "beats": len(ks),
            "scorableBeats": len(active_vals),
            "supportedFraction": round(sum(v[0] for v in vals)/max(1,len(vals)),3),
            "medianStrength": round(statistics.median(v[2] for v in vals),3) if vals else 0,
            "medianOnsetDistanceMs": round(statistics.median(v[1] for v in active_vals)*1000,1) if active_vals else None,
            "localMedianBpm": round(60/statistics.median(local_intervals),3) if local_intervals else None,
        })
    ratio = sum(e["end"]-e["start"] for e in excluded)/duration
    valid = [v for v in evidence if v[0]]
    metrics = {
        "beatCount": len(beats), "scorableBeatCount": len(valid),
        "supportedFraction": round(len(valid)/max(1,len(beats)),4),
        "excludedFraction": round(ratio,4),
        "medianBpm": round(60/statistics.median(intervals),3) if intervals else 0,
        "intervalP05Seconds": round(percentile(intervals,.05),4),
        "intervalP95Seconds": round(percentile(intervals,.95),4),
        "medianScorableOnsetDistanceMs": round(statistics.median(v[1] for v in valid)*1000,2) if valid else None,
        "onsetDistanceP95Ms": round(percentile([v[1] for v in valid],.95)*1000,2),
    }
    passed = len(beats) > 100 and metrics["supportedFraction"] >= .70 and ratio <= .30 and metrics["onsetDistanceP95Ms"] <= 50
    return beats, excluded, metrics, blocks, passed


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--features", default="/tmp/cascade-audio-analysis")
    parser.add_argument("--ffmpeg", default="/tmp/cascade-ffmpeg")
    parser.add_argument("--write", action="store_true", help="Write provisional Resources maps and reports")
    args = parser.parse_args()
    reports = []
    manifest = {"version": "audio-manifest-1", "tracks": []}
    for track_id, name, guess in TRACKS:
        source = ROOT/"Assets/Cascade/Resources/Audio"/(name+".wav")
        with wave.open(str(source)) as clip:
            duration = clip.getnframes()/clip.getframerate()
            samples, rate = clip.getnframes(), clip.getframerate()
        checksum = hashlib.sha256(source.read_bytes()).hexdigest()
        manifest["tracks"].append({"trackId":track_id,"resourceName":name,"audioSha256":checksum,"duration":duration,"sampleRate":rate,"samples":samples})
        if track_id == "dark-bass-drop":
            from dark_pulse_map import build_map
            data, report = build_map(args.features, args.ffmpeg)
            reports.append(report)
            print(name,json.dumps(report["metrics"]),"PASS")
            if args.write:
                (ROOT/"Assets/Cascade/Resources/Data"/(name+".json")).write_text(json.dumps(data,indent=2)+"\n")
            continue
        times, novelty, rms = features(Path(args.features)/(name+".csv"))
        candidates = tempo_candidates(novelty)
        chosen = min(candidates,key=lambda a:abs(a["bpm"]-guess))["bpm"]
        indices = track_dp(novelty,chosen)
        beats, excluded, metrics, blocks, passed = review(times,indices,novelty,rms,chosen,duration)
        note = "The listening review is pending. Detected onsets and continuity prior were checked automatically."
        data = {
            "trackId": track_id, "version": "onsets-1", "audioSha256": checksum,
            "verified": False, "analysisPassed": passed,
            "reviewStatus": "automated-review-pending-listen", "method": METHOD,
            "duration": duration, "nominalBpm": chosen, "beats": beats, "excluded": excluded,
            "analysis": metrics, "reviewNotes": note,
        }
        report = {"trackId":track_id,"name":name,"audioSha256":checksum,"duration":duration,
                  "method":METHOD,"trackingBpm":chosen,"tempoCandidates":candidates,
                  "analysisPassed":passed,"metrics":metrics,"blocks":blocks,"excluded":excluded,"reviewNotes":note}
        reports.append(report)
        print(name,json.dumps(metrics),"PASS" if passed else "FAIL")
        if args.write:
            (ROOT/"Assets/Cascade/Resources/Data"/(name+".json")).write_text(json.dumps(data,indent=2)+"\n")
    if args.write:
        (ROOT/"Assets/Cascade/Resources/Data/audio-manifest.json").write_text(json.dumps(manifest,indent=2)+"\n")
        (ROOT/"Documentation/AudioAnalysis.json").write_text(json.dumps({"method":"per-track; see each track's method","tracks":reports},indent=2)+"\n")
        with (ROOT/"Documentation/AudioAnalysis.csv").open("w") as f:
            writer=csv.DictWriter(f,fieldnames=["track","start","end","beats","scorableBeats","supportedFraction","medianStrength","medianOnsetDistanceMs","localMedianBpm"])
            writer.writeheader()
            for report in reports:
                for block in report["blocks"]:writer.writerow({"track":report["name"],**block})

if __name__ == "__main__":
    main()
