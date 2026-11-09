#!/usr/bin/env python3
"""Check release sources, baseline project and exact JSON data in Unity export."""
import argparse,hashlib,json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--build',required=True);a=p.parse_args();root=Path.cwd();out=root/'Builds/iOS';export=out/'Xcode';build=a.build
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
release=json.loads((out/f'source-baseline-1.0-{build}.json').read_text())
changes=[n for n,h in release['hashes'].items() if not (root/n).is_file() or sha(root/n)!=h]
baseline=json.loads((root/'Documentation/Baseline/source-manifest.json').read_text());base=Path(baseline['sourceProject'])
basechanges=[n for n,h in baseline['sourceHashes'].items() if not (base/n).is_file() or sha(base/n)!=h]
assert not changes and not basechanges,(changes,basechanges)
blob=(export/'Data/resources.assets').read_bytes();checks=[]
for path in (root/'Assets/Cascade/Resources/V3').rglob('*.json'):
 assert path.read_bytes() in blob,'Export missing exact JSON '+str(path)
 checks.append(str(path.relative_to(root)))
native=root/'Assets/Plugins/iOS/CascadeAudioOutputTiming.mm'
if not native.exists():
 native=next((root/'Assets/Plugins/iOS').glob('*Timing*'))
assert (export/'Libraries/Plugins/iOS'/native.name).read_bytes()==native.read_bytes()
proof=dict(version='1.0',build=build,sourceFilesChecked=len(release['hashes']),sourceMismatches=changes,baselineFilesChecked=len(baseline['sourceHashes']),baselineMismatches=basechanges,exactJsonFiles=checks,nativeSourceIdentical=True,dataFileHashes={str(f.relative_to(export/'Data')):sha(f) for f in sorted((export/'Data').rglob('*')) if f.is_file()})
(out/f'export-proof-1.0-{build}.json').write_text(json.dumps(proof,indent=2)+'\n');print('PASS:',len(checks),'exact JSONs;',len(release['hashes']),'sources;',len(proof['dataFileHashes']),'Data files')
