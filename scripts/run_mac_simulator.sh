#!/bin/zsh
set -euo pipefail

PROJECT_ROOT="${0:A:h:h}"
BACKEND_LOG="/tmp/bcsignvr-backend.log"
RUNTIME_JSON="/Applications/MetaXRSimulator.app/Contents/Resources/MetaXRSimulator/meta_openxr_simulator.json"
APP_BINARY="$PROJECT_ROOT/unity/Builds/macOS/BCSignVR.app/Contents/MacOS/BCSignVR Standalone"
OPENXR_LOADER_DIR="$PROJECT_ROOT/unity/Builds/macOS/BCSignVR.app/Contents/PlugIns/ARM64"
WEBCAM_LOG="/tmp/bcsignvr-webcam.log"

if ! curl --silent --fail http://127.0.0.1:8787/health >/dev/null 2>&1; then
  (cd "$PROJECT_ROOT/backend" && python3 -m bcsignvr.server >"$BACKEND_LOG" 2>&1 &)
fi

if [[ -x "$PROJECT_ROOT/.venv/bin/python" ]] && ! pgrep -f "webcam/hand_bridge.py" >/dev/null 2>&1; then
  (
    while true; do
      if "$PROJECT_ROOT/.venv/bin/python" "$PROJECT_ROOT/webcam/hand_bridge.py" >>"$WEBCAM_LOG" 2>&1; then
        break
      else
        status=$?
        print "Webcam tracker stopped ($status); retrying in 2 seconds." >>"$WEBCAM_LOG"
        sleep 2
      fi
    done
  ) &
fi

open -a MetaXRSimulator
BCSIGNVR_XR=1 XR_RUNTIME_JSON="$RUNTIME_JSON" DYLD_LIBRARY_PATH="$OPENXR_LOADER_DIR" \
  "$APP_BINARY" -screen-fullscreen 0 -screen-width 1440 -screen-height 900

