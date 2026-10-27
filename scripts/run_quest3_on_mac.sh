#!/bin/zsh
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APK="$PROJECT_ROOT/unity/Builds/Quest/BCSignVR-Quest3.apk"
PACKAGE="za.ac.belgiumcampus.bcsignvr"
ADB_BIN="${ADB_BIN:-$(command -v adb || true)}"
PYTHON_BIN="${PYTHON_BIN:-python3}"
PRESENTATION_MODE=0

while (( $# > 0 )); do
  case "$1" in
    --presentation)
      PRESENTATION_MODE=1
      ;;
    *)
      echo "Unknown option: $1" >&2
      echo "Usage: $0 [--presentation]" >&2
      exit 2
      ;;
  esac
  shift
done

if [[ ! -f "$APK" ]]; then
  echo "Quest APK not found. Run scripts/build_quest3_apk.sh first." >&2
  exit 1
fi
if [[ -z "$ADB_BIN" ]]; then
  echo "adb is required. Install Android platform-tools and reconnect the Quest 3." >&2
  exit 1
fi

device_list="$("$ADB_BIN" devices | awk '$2 == "device" { print $1 }')"
if [[ -z "$device_list" ]]; then
  echo "No authorized Quest found. Connect USB and accept USB debugging in the headset." >&2
  exit 1
fi
DEVICES=("${(@f)device_list}")
if (( ${#DEVICES[@]} == 0 )); then
  echo "No Quest 3 found. Connect it by USB, enable Developer Mode, and accept USB debugging in the headset." >&2
  exit 1
fi
if (( ${#DEVICES[@]} > 1 )); then
  if [[ -z "${ANDROID_SERIAL:-}" ]]; then
    echo "Multiple Android devices found. Set ANDROID_SERIAL to your Quest serial." >&2
    exit 1
  fi
fi
SERIAL="${ANDROID_SERIAL:-${DEVICES[1]}}"

awake_pid=""
cleanup() {
  if [[ -n "$awake_pid" ]]; then
    kill "$awake_pid" 2>/dev/null || true
    wait "$awake_pid" 2>/dev/null || true
  fi
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

echo "Installing signzzZ on Quest 3 ($SERIAL)..."
"$ADB_BIN" -s "$SERIAL" install -r "$APK" >/dev/null
"$ADB_BIN" -s "$SERIAL" shell monkey -p "$PACKAGE" 1 >/dev/null
if [[ "${QUEST_KEEP_AWAKE:-1}" == 1 ]]; then
  if launchctl print "gui/$(id -u)/za.ac.belgiumcampus.signzzz.quest-awake.$SERIAL" >/dev/null 2>&1; then
    echo "Automatic Quest stay-awake is already managed by your Mac login helper."
  else
    "$PYTHON_BIN" "$PROJECT_ROOT/scripts/quest_keep_awake.py" --adb "$ADB_BIN" --serial "$SERIAL" &
    awake_pid=$!
  fi
fi

if command -v scrcpy >/dev/null 2>&1; then
  if (( PRESENTATION_MODE )); then
    echo "Showing the Quest 3 view full-screen for the audience. Close the mirror window to stop it."
    scrcpy --serial "$SERIAL" --window-title "signzzZ — live Quest view" \
      --max-size 1600 --video-bit-rate 8M --crop 2064:2208:0:0 \
      --fullscreen --no-control
  else
    echo "Mirroring Quest 3 to this Mac. Close the mirror window to stop it."
    scrcpy --serial "$SERIAL" --window-title "signzzZ Quest 3 mirror" \
      --max-size 1600 --video-bit-rate 8M --crop 2064:2208:0:0 --always-on-top
  fi
  exit $?
fi

if command -v ffplay >/dev/null 2>&1; then
  echo "scrcpy is not installed; using the built-in ADB video fallback."
  echo "For a smoother mirror, install scrcpy with: brew install scrcpy"
  # Quest's raw Android display is side-by-side stereo (left and right eyes).
  # Crop to the left eye so the audience sees one normal classroom view.
  ffplay_args=(-fflags nobuffer -flags low_delay -framerate 60 -probesize 32 -sync video \
    -vf 'crop=iw/2:ih:0:0')
  if (( PRESENTATION_MODE )); then
    ffplay_args+=(-fs)
  fi
  "$ADB_BIN" -s "$SERIAL" exec-out screenrecord --output-format=h264 --bit-rate 8000000 - \
    | ffplay "${ffplay_args[@]}" -
  exit $?
fi

echo "signzzZ is running. Install scrcpy for a Mac mirror."
if [[ -n "$awake_pid" ]]; then
  echo "Keep this terminal open for off-face testing; Ctrl+C restores normal sleep."
  wait "$awake_pid"
fi
