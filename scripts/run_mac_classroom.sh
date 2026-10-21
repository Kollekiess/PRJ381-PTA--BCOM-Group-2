#!/bin/zsh
set -euo pipefail
PROJECT_ROOT="${0:A:h:h}"
APP_BINARY="$PROJECT_ROOT/unity/Builds/macOS/BCSignVR.app/Contents/MacOS/BCSignVR Standalone"
if [[ ! -x "$APP_BINARY" ]]; then
  print "The classroom needs a Mac build first. See README.md."
  exit 1
fi
camera_pid=""
backend_pid=""
cleanup() {
  [[ -z "$camera_pid" ]] || kill "$camera_pid" 2>/dev/null || true
  [[ -z "$backend_pid" ]] || kill "$backend_pid" 2>/dev/null || true
}
trap cleanup EXIT INT TERM
if ! curl --silent --fail http://127.0.0.1:8787/health >/dev/null 2>&1; then
  (cd "$PROJECT_ROOT/backend" && exec python3 -m bcsignvr.server) >/tmp/bcsignvr-backend.log 2>&1 &
  backend_pid=$!
fi
if [[ -x "$PROJECT_ROOT/.venv/bin/python" ]] && ! pgrep -f "webcam/hand_bridge.py" >/dev/null 2>&1; then
  (
    while true; do
      if "$PROJECT_ROOT/.venv/bin/python" "$PROJECT_ROOT/webcam/hand_bridge.py" --camera mac --no-preview >>/tmp/bcsignvr-webcam.log 2>&1; then
        break
      else
        status=$?
        print "Webcam tracker stopped ($status); retrying in 2 seconds." >>/tmp/bcsignvr-webcam.log
        sleep 2
      fi
    done
  ) &
  camera_pid=$!
fi
BCSIGNVR_XR=0 "$APP_BINARY" -screen-fullscreen 0 -screen-width 1440 -screen-height 900
