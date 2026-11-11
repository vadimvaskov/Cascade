#!/bin/bash
# Exports an Xcode project only. Archiving and uploading remain explicit Xcode steps.
set -euo pipefail
project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
build_number="${1:-15}"
[[ "$build_number" =~ ^[1-9][0-9]*$ ]] || { echo "Use a positive build number, e.g. bash Tools/build-ios-mac.sh 15" >&2; exit 2; }
[[ "$(uname -s)" == Darwin ]] || { echo "Run this export on the Mac." >&2; exit 2; }
unity_version="$(sed -n 's/^m_EditorVersion: //p' "$project_dir/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
unity_app="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/$unity_version/Unity.app/Contents/MacOS/Unity}"
[[ -x "$unity_app" ]] || { echo "Install Unity $unity_version with iOS Build Support in Unity Hub." >&2; exit 2; }
xcodebuild -version
xcrun --sdk iphoneos --show-sdk-version
python3 "$project_dir/Tools/verify_source_checkout.py"
output="$project_dir/Builds/iOS/1.0-$build_number"
if [[ -e "$output" ]]; then
    echo "Output already exists: $output. Keep it and choose another build number." >&2
    exit 2
fi
mkdir -p "$output"
echo "Exporting Cascade 1.0 ($build_number). Close this project in Unity before continuing."
# No -quit: the entry point waits through settings reload, exports, then exits with its result.
"$unity_app" -batchmode -buildTarget iOS -projectPath "$project_dir" \
    -executeMethod CascadeIOSBatchBuild.Run -cascadeBuildNumber "$build_number" \
    -cascadeOutputPath "$output/Xcode" -logFile "$output/unity-export.log"
[[ -d "$output/Xcode/Unity-iPhone.xcodeproj" ]] || { echo "Xcode export missing; see $output/unity-export.log" >&2; exit 1; }
echo "Xcode project ready: $output/Xcode/Unity-iPhone.xcodeproj"
open "$output/Xcode/Unity-iPhone.xcodeproj"
