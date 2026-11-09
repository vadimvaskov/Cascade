#!/usr/bin/env python3
"""Dark Bass Drop: continuous audible pulse, independently checked from PCM.

The generator reads only the WAV and spectral features. No device journals or
touch offsets participate in tempo/phase selection. --write publishes ONLY Dark;
--candidate-dir writes review artifacts; the default/--check never publishes.
"""
import argparse
import array
import bisect
import copy
import csv
import hashlib
import json
import math
import statistics
import subprocess
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
NAME = "Dark Bass Drop"
WAV = ROOT / "Assets/Cascade/Resources/Audio" / (NAME + ".wav")
MAP = ROOT / "Assets/Cascade/Resources/Data" / (NAME + ".json")
STEP = .01
PULSE_BPM = 176.0
VERSION = "pulse-2"
METHOD = "continuous-highband-pulse-v2"


def quantile(values, fraction):
    ordered = sorted(values)
    position = (len(ordered)-1)*fraction
    low = int(position)
    return ordered[low] + (ordered[min(low+1,len(ordered)-1)]-ordered[low])*(position-low)


def duration_and_hash():
    with wave.open(str(WAV)) as audio:
        duration = audio.getnframes()/audio.getframerate()
    digest = hashlib.sha256()
    with WAV.open("rb") as source:
        for block in iter(lambda: source.read(1024*1024), b""):
            digest.update(block)
    return duration, digest.hexdigest()


def spectral_features(directory):
    with (Path(directory)/(NAME+".csv")).open() as source:
        rows = list(csv.DictReader(source))
    return [(float(r["time"]),float(r["high_flux"]),float(r["rms"])) for r in rows]


def coherence(samples, bpm):
    frequency = bpm/60
    z = sum(v*complex(math.cos(2*math.pi*frequency*t),math.sin(2*math.pi*frequency*t)) for t,v in samples)
    return abs(z)/max(1e-12,sum(v for _,v in samples))


def folded_peak(samples, bpm, bins=136):
    period = 60/bpm
    histogram = [0.0]*bins
    for time,weight in samples:
        histogram[int((time % period)/period*bins) % bins] += weight
    smoothed = [sum(histogram[(i+j) % bins] for j in range(-3,4))/7 for i in range(bins)]
    peak = max(range(bins),key=lambda i:smoothed[i])
    return (peak+.5)*period/bins, smoothed[peak]


def independent_pcm(ffmpeg):
    # This detector does not use the FFT CSV. A direct PCM high-pass attack
    # envelope provides separate timing evidence for the fitted spectral grid.
    raw = subprocess.check_output([str(ffmpeg),"-v","error","-i",str(WAV),
        "-af","highpass=f=1500","-ar","12000","-ac","1","-f","f32le","pipe:1"])
    samples = array.array("f")
    samples.frombytes(raw)
    rms = [math.sqrt(sum(v*v for v in samples[i:i+120])/120) for i in range(0,len(samples)-120,120)]
    attacks = [0.0]+[max(0,rms[i]-rms[i-1]) for i in range(1,len(rms))]
    return [(i*STEP+.005,attacks[i]) for i in range(1,len(attacks)-1)
        if attacks[i]>=attacks[i-1] and attacks[i]>=attacks[i+1] and attacks[i]>.002]


def signed_phase_error(phase, expected, period):
    return (phase-expected+period/2) % period-period/2


def validate_map(data, ffmpeg, peaks=None):
    duration,checksum = duration_and_hash()
    if peaks is None:
        peaks = independent_pcm(ffmpeg)
    period = 60/PULSE_BPM
    phase = data["beats"][0] % period
    windows = []
    for start in range(0,math.ceil(duration),16):
        local = [(t,v) for t,v in peaks if start <= t < start+16]
        measured,_ = folded_peak(local,PULSE_BPM)
        error = signed_phase_error(measured,phase,period)
        windows.append({"start":start,"end":min(start+16,duration),
            "phaseSeconds":round(measured,6),"errorMs":round(error*1000,3),"attackCount":len(local)})
    beats = data["beats"]
    gaps = [b-a for a,b in zip(beats,beats[1:])]
    expected_gap = 60/data["nominalBpm"]
    max_grid_error = max(abs(t-beats[0]-i*expected_gap) for i,t in enumerate(beats))
    phase_errors = [abs(w["errorMs"]) for w in windows]
    passed = (checksum == data["audioSha256"] and abs(duration-data["duration"])<1e-6
        and all(0<=t<duration for t in beats) and all(gap>0 for gap in gaps)
        and max_grid_error<.000002 and max(phase_errors)<35
        and data["nominalBpm"] in (88.0,176.0))
    # A +/- one subdivision shift must fail the independent phase test. This
    # guards against a detector that would approve any dense collection of hits.
    shifted_errors = [abs(signed_phase_error(w["phaseSeconds"],phase+period/2,period))*1000 for w in windows]
    passed = passed and min(shifted_errors)>120
    return {"passed":passed,"method":"independent-PCM-highpass1500Hz-12kHz-RMS10ms",
        "phaseWindows":windows,"maximumWindowPhaseErrorMs":round(max(phase_errors),3),
        "medianWindowPhaseErrorMs":round(statistics.median(w["errorMs"] for w in windows),3),
        "incorrectHalfPulsePhaseMinimumErrorMs":round(min(shifted_errors),3),
        "maximumGridErrorMicroseconds":round(max_grid_error*1e6,3),
        "audioSha256Matches":checksum==data["audioSha256"],
        "note":"Separate PCM detector validates phase continuity, not listening approval or device latency."}


def quiet_intervals(features, duration):
    # Missing individual bass attacks are not silence. Require at least one
    # second of very low broadband RMS before excluding any time.
    intervals=[]
    start=None
    for time,_,rms in features:
        if rms < .006 and start is None:
            start=time-STEP/2
        if rms >= .006 and start is not None:
            if time-start >= 1.0:
                intervals.append({"start":round(max(0,start),6),"end":round(time-STEP/2,6)})
            start=None
    if start is not None and duration-start >= 1.0:
        intervals.append({"start":round(max(0,start),6),"end":duration})
    return intervals


def build_map(features_dir="/tmp/cascade-audio-analysis", ffmpeg="/tmp/cascade-ffmpeg"):
    features = spectral_features(features_dir)
    duration,checksum = duration_and_hash()
    training = [(t,v) for t,v,_ in features if 16<=t<224]
    average = statistics.mean(v for _,v in training)
    normalized = [(t,max(0,min(v/average,8)-.6)) for t,v in training]
    fits = [(coherence(normalized,175.8+i*.002),175.8+i*.002) for i in range(201)]
    _,fitted_bpm = max(fits)
    # The exact 176 BPM lattice passes the independent full-song phase test;
    # its discrepancy from the spectral fit is only ~11 ms over this recording.
    spectral_phase,_ = folded_peak(training,PULSE_BPM)
    phase = round(spectral_phase/.005)*.005
    beats=[]
    index=0
    while phase+index*60/PULSE_BPM < duration:
        beats.append(round(phase+index*60/PULSE_BPM,6))
        index+=1
    excluded = quiet_intervals(features,duration)
    data={"trackId":"dark-bass-drop","version":VERSION,"audioSha256":checksum,
        "verified":False,"analysisPassed":False,"reviewStatus":"automated-review-pending-listen",
        "method":METHOD,"duration":duration,"nominalBpm":PULSE_BPM,"beats":beats,"excluded":excluded,
        "reviewNotes":"Audio-derived continuous 176 BPM carrier. Both alternating pulses are present in the percussion. No device taps or calibration offsets were used to construct the grid. Listening/device confirmation remains pending."}
    peaks=independent_pcm(ffmpeg)
    validation=validate_map(data,ffmpeg,peaks)
    assert abs(fitted_bpm-PULSE_BPM)<.04,"Spectral pulse tempo no longer supports 176 BPM"
    assert validation["passed"],"Independent PCM phase validation failed"
    data["analysisPassed"]=True
    scorable=[t for t in beats if not any(e["start"]<=t<e["end"] for e in excluded)]
    peak_times=[t for t,_ in peaks]
    errors=[]
    for beat in scorable:
        position=bisect.bisect_left(peak_times,beat)
        near=peak_times[max(0,position-1):position+1]
        errors.append(min(abs(t-beat) for t in near))
    metrics={"beatCount":len(beats),"scorableBeatCount":len(scorable),
        "supportedFraction":round(sum(error<=.04 for error in errors)/len(errors),4),
        "excludedFraction":round(sum(e["end"]-e["start"] for e in excluded)/duration,4),
        "medianBpm":PULSE_BPM,"intervalP05Seconds":round(60/PULSE_BPM,6),
        "intervalP95Seconds":round(60/PULSE_BPM,6),
        "medianScorableOnsetDistanceMs":round(statistics.median(errors)*1000,3),
        "onsetDistanceP95Ms":round(quantile(errors,.95)*1000,3),
        "supportDetector":"independent high-pass RMS; phase-window validation is the acceptance criterion"}
    data["analysis"]=metrics
    family_samples=[(t,min(v/average,8)) for t,v in training]
    family=[{"bpm":bpm,"highBandCoherence":round(coherence(family_samples,bpm),6)} for bpm in [88.0,117.333333333,176.0]]
    anchors=[]
    for number in range(59,83):
        expected=phase+number*60/PULSE_BPM
        nearby=[(t,v) for t,v in peaks if abs(t-expected)<=.04]
        actual,strength=max(nearby,key=lambda item:item[1])
        anchors.append({"time":round(actual,6),"rmsAttack":round(strength,6)})
    blocks=[]
    for start in range(0,math.ceil(duration),8):
        local=[(t,error) for t,error in zip(scorable,errors) if start<=t<start+8]
        blocks.append({"start":start,"end":min(start+8,duration),"beats":len(local),
            "scorableBeats":len(local),"supportedFraction":round(sum(e<=.04 for _,e in local)/max(1,len(local)),3),
            "medianStrength":None,"medianOnsetDistanceMs":round(statistics.median(e for _,e in local)*1000,3) if local else None,
            "localMedianBpm":PULSE_BPM})
    report={"trackId":data["trackId"],"name":NAME,"audioSha256":checksum,"duration":duration,
        "method":METHOD,"trackingBpm":PULSE_BPM,"analysisPassed":True,"metrics":metrics,"blocks":blocks,
        "excluded":excluded,"reviewNotes":data["reviewNotes"],
        "fit":{"input":"high_flux FFT features only; 16 <= time < 224 seconds",
            "fittedBpm":round(fitted_bpm,6),"chosenBpm":PULSE_BPM,
            "wholeSongDifferenceFromFittedTempoMs":round(abs(duration*(fitted_bpm/PULSE_BPM-1))*1000,3),
            "foldedAttackPhaseSeconds":round(spectral_phase,6),"chosenPhaseSeconds":round(phase,6),
            "phaseSelection":"spectral folded peak rounded to nearest 5 ms; PCM validator not used to fit phase",
            "tempoCandidates":family},
        "independentPulseCheck":validation,
        "independentPcmAnchors":anchors,
        "metricalDecision":{"choice":"176 BPM continuous percussion carrier",
            "evidence":"Both 88 BPM parities have independently visible high-frequency PCM attacks; 117 BPM lacks stable high-band phase. An 88 BPM subset discards alternate audible pulses.",
            "noDeviceFit":True,"noCalibrationOffsetInMap":True,
            "requiredReview":"Listen and play the complete track; automated phase evidence is not subjective meter approval."},
        "exclusionPolicy":{"minimumBroadbandRms":.006,"minimumQuietDurationSeconds":1.0,
            "reason":"Exclude only sustained low-energy sections, never a single weak/missing bass onset."}}
    return data,report


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--features",default="/tmp/cascade-audio-analysis")
    parser.add_argument("--ffmpeg",default="/tmp/cascade-ffmpeg")
    parser.add_argument("--check",action="store_true",help="Analyze without publishing the production map")
    parser.add_argument("--write",action="store_true",help="Publish ONLY Dark Bass Drop.json; explicit opt-in")
    parser.add_argument("--candidate-dir",type=Path,help="Write review candidates/report outside Resources")
    args=parser.parse_args()
    if args.check and args.write:
        parser.error("--check and --write are mutually exclusive")
    data,report=build_map(args.features,args.ffmpeg)
    if args.candidate_dir:
        target=args.candidate_dir.resolve()
        if target==MAP.parent.resolve() or MAP.parent.resolve() in target.parents:
            parser.error("Candidate output must not be inside production Resources/Data")
        target.mkdir(parents=True,exist_ok=True)
        (target/"Dark Bass Drop-176.json").write_text(json.dumps(data,indent=2)+"\n")
        half=copy.deepcopy(data)
        half["nominalBpm"]=88.0
        half["beats"]=data["beats"][::2]
        half["version"]="pulse-2-half-time-review"
        half["reviewNotes"]="Review-only 88 BPM subset; drops alternate audible 176 BPM pulses and keeps a parity dependency."
        half["analysis"]={"beatCount":len(half["beats"]),"independentPulseCheck":validate_map(half,args.ffmpeg)}
        (target/"Dark Bass Drop-88.json").write_text(json.dumps(half,indent=2)+"\n")
        (target/"report.json").write_text(json.dumps(report,indent=2)+"\n")
    if args.write:
        MAP.write_text(json.dumps(data,indent=2)+"\n")
    print(json.dumps({"track":NAME,"bpm":data["nominalBpm"],"phase":report["fit"]["chosenPhaseSeconds"],
        "beats":len(data["beats"]),"excluded":data["excluded"],"validation":report["independentPulseCheck"]["passed"],
        "maxIndependentPhaseErrorMs":report["independentPulseCheck"]["maximumWindowPhaseErrorMs"],
        "published":args.write},indent=2))


if __name__=="__main__":
    main()
