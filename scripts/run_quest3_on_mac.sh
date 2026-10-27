#!/bin/zsh
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APK="$PROJECT_ROOT/unity/Builds/Quest/BCSignVR-Quest3.apk"
PACKAGE="za.ac.belgiumcampus.bcsignvr"
ADB_BIN="${ADB_BIN:-$(command -v adb || true)}"

if [[ ! -f "$APK" ]]; then
  echo "Quest APK not found. Run scripts/build_quest3_apk.sh first." >&2
  exit 1
fi
if [[ -z "$ADB_BIN" ]]; then
  echo "adb is required. Install Android platform-tools and reconnect the Quest 3." >&2
  exit 1
fi

device_list="$("$ADB_BIN" devices | awk '$2 == "device" { print $1 }')"
DEVICES=("${(@f)device_list}")
if (( ${#DEVICES[@]} == 0 )); then
  echo "No Quest 3 found. Connect it by USB, enable Developer Mode, and accept USB debugging in the headset." >&2
  exit 1
fi
if (( ${#DEVICES[@]} > 1 )); then
  echo "Multiple Android devices found; using ${DEVICES[1]}. Set ANDROID_SERIAL to choose another." >&2
fi
SERIAL="${ANDROID_SERIAL:-${DEVICES[1]}}"

camera_pid=""
cleanup() {
  [[ -z "$camera_pid" ]] || kill "$camera_pid" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

echo "Installing signzzZ on Quest 3 ($SERIAL)..."
"$ADB_BIN" -s "$SERIAL" reverse tcp:5055 tcp:5055 >/dev/null
"$ADB_BIN" -s "$SERIAL" install -r "$APK" >/dev/null
if [[ -x "$PROJECT_ROOT/.venv/bin/python" ]]; then
  (
    while true; do
      if "$PROJECT_ROOT/.venv/bin/python" "$PROJECT_ROOT/webcam/hand_bridge.py" \
          --camera mac --no-preview --transport tcp >>/tmp/bcsignvr-webcam-quest.log 2>&1; then
        break
      else
        print "Quest webcam tracker stopped; retrying in 2 seconds." >>/tmp/bcsignvr-webcam-quest.log
        sleep 2
      fi
    done
  ) &
  camera_pid=$!
else
  echo "Webcam runtime not found; VR hands will still work, but Webcam stays unavailable." >&2
fi
"$ADB_BIN" -s "$SERIAL" shell monkey -p "$PACKAGE" 1 >/dev/null

if command -v scrcpy >/dev/null 2>&1; then
  echo "Mirroring Quest 3 to this Mac. Close the mirror window to stop it."
  scrcpy --serial "$SERIAL" --window-title "signzzZ Quest 3 mirror" \
    --max-size 1600 --video-bit-rate 8M
  exit $?
fi

if command -v ffplay >/dev/null 2>&1; then
  echo "scrcpy is not installed; using the built-in ADB video fallback."
  echo "For a smoother mirror, install scrcpy with: brew install scrcpy"
  "$ADB_BIN" -s "$SERIAL" exec-out screenrecord --output-format=h264 --bit-rate 8000000 - \
    | ffplay -fflags nobuffer -flags low_delay -framerate 60 -probesize 32 -sync video -
  exit $?
fi

echo "The Quest app is running, but no Mac mirror tool was found." >&2
echo "Install scrcpy with: brew install scrcpy" >&2
echo "Keeping the USB webcam bridge alive. Press Ctrl-C to stop signzzZ." >&2
if [[ -n "$camera_pid" ]]; then
  wait "$camera_pid"
else
  while true; do sleep 60; done
fi
