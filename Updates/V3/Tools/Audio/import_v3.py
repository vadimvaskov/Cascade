#!/usr/bin/env python3
"""Lossless PCM decode, source authentication and whole-file channel/peak audit."""
import hashlib,json,subprocess,unicodedata,wave
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
PACKAGE=ROOT/'Documentation/Inputs/V3/Package'
OUT=ROOT/'Assets/Cascade/Resources/V3'
FFMPEG='/private/tmp/cascade-ffmpeg'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
catalog=json.loads((PACKAGE/'Data/Content_Catalog_v3.json').read_text())
(OUT/'Audio').mkdir(exist_ok=True)
(ROOT/'Documentation/V3/Audio').mkdir(exist_ok=True)
for record in catalog['records']:
 for track in record['tracks']:
  relative=Path(track['source_audio_path'])
  source=next(p for p in (PACKAGE/relative.parent).iterdir() if unicodedata.normalize('NFC',p.name)==relative.name)
  assert sha(source)==track['source_audio_sha256']
  target=OUT/'Audio'/track['recommended_import_filename']
  subprocess.run([FFMPEG,'-v','error','-i',str(source),'-vn','-ar','48000','-ac','2','-c:a','pcm_s16le','-y',str(target)],check=True)
  with wave.open(str(target)) as w:duration=w.getnframes()/w.getframerate();assert w.getnchannels()==2 and w.getsampwidth()==2
  assert abs(duration-track['container_duration_seconds'])<.06
  audit=subprocess.run([FFMPEG,'-hide_banner','-i',str(target),'-af','astats=metadata=0:reset=0','-f','null','-'],capture_output=True,text=True,check=True)
  (ROOT/'Documentation/V3/Audio'/(track['track_id']+'-pcm-audit.txt')).write_text(audit.stderr)
  track.update(runtime_audio_path='V3/Audio/'+target.stem,runtime_audio_sha256=sha(target),runtime_duration_seconds=duration,beatmap_path='V3/Beatmaps/'+target.stem,beatmap_status='pending_analysis_and_listening',level_settings_path='V3/balance')
  print(track['track_id'],duration,flush=True)
(OUT/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n')
