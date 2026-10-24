#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app/Contents/MacOS/Unity}"

if [[ ! -x "$UNITY" ]]; then
  echo "Unity 6000.6.5f1 was not found at: $UNITY" >&2
  exit 1
fi

echo "Building Quest 3 APK..."
"$UNITY" -batchmode -projectPath "$PROJECT_ROOT/unity" \
  -executeMethod BCSignVR.Editor.ProjectSetup.BuildQuest -quit -logFile -
echo "APK: $PROJECT_ROOT/unity/Builds/Quest/BCSignVR-Quest3.apk"
