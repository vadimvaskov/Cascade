#!/bin/bash
# Экспорт iOS из Unity (V4) и открытие Xcode. Архив и загрузка — в Xcode.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_dir="$(cd "$script_dir/.." && pwd)"
output="${project_dir}/Builds/iOS/Xcode"
log="${project_dir}/Builds/iOS/unity-export.log"
development=0
open_xcode=1

for arg in "$@"; do
  case "$arg" in
    --dev) development=1 ;;
    --no-open) open_xcode=0 ;;
    -h|--help)
      echo "Usage: $(basename "$0") [--dev] [--no-open]" >&2
      echo "  --dev      Unity Development build (-cascadeDevelopment)" >&2
      echo "  --no-open  Do not open Unity-iPhone.xcodeproj after export" >&2
      exit 0
      ;;
    *)
      echo "Unknown option: $arg (try --help)" >&2
      exit 2
      ;;
  esac
done

[[ "$(uname -s)" == Darwin ]] || { echo "Run this script on macOS." >&2; exit 2; }

unity_version="$(sed -n 's/^m_EditorVersion: //p' "$project_dir/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
unity_app="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/$unity_version/Unity.app/Contents/MacOS/Unity}"
[[ -x "$unity_app" ]] || {
  echo "Install Unity $unity_version with iOS Build Support (UNITY_EDITOR to override path)." >&2
  exit 2
}

if ! command -v xcodebuild >/dev/null 2>&1; then
  echo "Xcode command-line tools not found. Install Xcode from the App Store." >&2
  exit 2
fi

xcodebuild -version
mkdir -p "$(dirname "$log")"
rm -rf "$output"
mkdir -p "$output"

echo "Project: $project_dir"
echo "Export:  $output"
echo "Log:     $log"
echo "Close this project in the Unity Editor before continuing."

unity_args=(
  -batchmode
  -buildTarget
  iOS
  -projectPath
  "$project_dir"
  -executeMethod
  CascadeV4Build.Run
  -cascadeOutputPath
  "$output"
  -logFile
  "$log"
)
if (( development )); then
  unity_args+=( -cascadeDevelopment )
  echo "Build type: Development"
else
  echo "Build type: Release"
fi

# Без -quit: CascadeV4Build ждёт компиляцию и сам выходит с кодом 0 или 1.
"$unity_app" "${unity_args[@]}"

xcodeproj="$output/Unity-iPhone.xcodeproj"
[[ -d "$xcodeproj" ]] || {
  echo "Xcode project missing. See log: $log" >&2
  exit 1
}

echo "Xcode project ready: $xcodeproj"
if (( open_xcode )); then
  open "$xcodeproj"
fi