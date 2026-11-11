"""Check a clone before importing it into Unity; uses only Python's standard library."""
import json
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[1]
errors = []
required = ["Assets/Scenes/CascadeGame.unity", "Packages/manifest.json", "Packages/packages-lock.json",
            "ProjectSettings/ProjectVersion.txt", "ProjectSettings/EditorBuildSettings.asset",
            "Assets/Cascade/Art/FaRodin/Fa_Rodin.fbx", "Assets/CascadeDream/Art/Mi/Mi_Rodin.fbx",
            "Assets/Plugins/iOS/CascadeHaptics.mm", "Assets/Plugins/iOS/CascadeAudioOutputTiming.mm",
            "Assets/Cascade/Resources/Approved/MusicEnergyVolume.shader"]
report = root / "Documentation/Release/dependencies.json"
if report.is_file():
    required += json.loads(report.read_text(encoding="utf-8"))["assets"]
else:
    errors.append("Missing Documentation/Release/dependencies.json")
required += [p + ".meta" for p in required if p.startswith("Assets/")]
for name in sorted(set(required)):
    path = root / name
    if not path.is_file():
        errors.append("Missing: " + name)
        continue
    with path.open("rb") as stream:
        if stream.read(100).startswith(b"version https://git-lfs.github.com/spec/v1"):
            errors.append("Unresolved Git LFS pointer: " + name)
    current = root
    for part in Path(name).parts:
        if part not in {item.name for item in current.iterdir()}:
            errors.append("Filename case mismatch: " + name)
            break
        current /= part

guids = {}
for meta in (root / "Assets").rglob("*.meta"):
    if "_Recovery" in meta.parts:
        continue
    found = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(encoding="utf-8-sig"), re.M)
    if found:
        guid = found[1]
        if guid in guids:
            errors.append(f"Duplicate GUID: {meta.relative_to(root)} and {guids[guid]}")
        guids[guid] = meta.relative_to(root)
    if not meta.with_suffix("").exists():
        errors.append("Orphan metadata: " + str(meta.relative_to(root)))

if errors:
    print("Source check failed:\n" + "\n".join(errors), file=sys.stderr)
    sys.exit(1)
print(f"PASS: {len(set(required))} required files, exact path case, no LFS placeholders; {len(guids)} unique asset GUIDs.")
