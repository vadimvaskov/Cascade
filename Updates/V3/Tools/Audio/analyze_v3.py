#!/usr/bin/env python3
"""Candidate beat maps: local spectral tracking, section diagnostics, no listening claim."""
import json,subprocess,statistics,wave
from pathlib import Path
from build_maps import features,tempo_candidates,track_dp,review
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Cascade/Resources/V3'
REPORT=ROOT/'Documentation/V3/Audio'
catalog=json.loads((OUT/'catalog.json').read_text());reports=[]
(OUT/'Beatmaps').mkdir(exist_ok=True)
for record in catalog['records']:
 for track in record['tracks']:
  id=track['track_id'];wav=ROOT/'Assets/Cascade/Resources'/(track['runtime_audio_path']+'.wav');csv=REPORT/(id+'-features.csv')
  subprocess.run(['/private/tmp/cascade-v3-onsets',str(wav),str(csv)],check=True,capture_output=True)
  times,novelty,rms=features(csv)
  candidates=tempo_candidates(novelty)
  # Consider several metrically related hypotheses, assess the whole file and every 8s block.
  alternatives=[]
  for candidate in candidates[:5]:
   bpm=candidate['bpm'];indices=track_dp(novelty,bpm)
   beats,excluded,metrics,blocks,passed=review(times,indices,novelty,rms,bpm,track['runtime_duration_seconds'])
   quality=metrics['supportedFraction']-.25*metrics['excludedFraction']
   alternatives.append((quality,bpm,beats,excluded,metrics,blocks,passed))
  best=max(alternatives,key=lambda a:a[0]);_,bpm,beats,excluded,metrics,blocks,passed=best
  version='v3-spectral-candidate-1'
  data=dict(trackId=id,version=version,audioSha256=track['runtime_audio_sha256'],verified=False,analysisPassed=passed,reviewStatus='automated-review-pending-listen',analysisMethod='spectral-flux-local-dp-multi-tempo-v3',duration=track['runtime_duration_seconds'],nominalBpm=bpm,beats=beats,excluded=excluded)
  (OUT/'Beatmaps'/(id+'.json')).write_text(json.dumps(data,indent=2)+'\n')
  track['beatmap_status']='automated-review-pending-listen' if passed else 'analysis_failed_not_playable'
  report=dict(trackId=id,title=track['title'],version=version,audioSha256=track['runtime_audio_sha256'],trackingBpm=bpm,analysisPassed=passed,listeningVerified=False,metrics=metrics,blocks=blocks,excluded=excluded,tempoCandidates=candidates,alternatives=[dict(bpm=a[1],quality=a[0],metrics=a[4],passed=a[6]) for a in alternatives])
  reports.append(report)
  print(id,round(bpm,2),metrics,'PASS' if passed else 'FAIL',flush=True)
(OUT/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n')
(REPORT/'map-analysis.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2)+'\n')
