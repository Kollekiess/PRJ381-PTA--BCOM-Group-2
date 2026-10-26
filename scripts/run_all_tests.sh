#!/bin/zsh
set -euo pipefail

PROJECT_ROOT="${0:A:h:h}"
UNITY="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app/Contents/MacOS/Unity}"
PYTHON="${PYTHON_BIN:-$PROJECT_ROOT/.venv/bin/python}"
RESULTS_DIR="$(mktemp -d /tmp/signzzz-tests.XXXXXX)"
RESULTS="$RESULTS_DIR/unity.xml"

cd "$PROJECT_ROOT"
if [[ ! -x "$PYTHON" ]]; then
  print -u2 "Run scripts/setup_webcam.sh first, or set PYTHON_BIN to an environment with webcam/requirements.txt installed."
  exit 1
fi
print "[1/7] Backend tests"
PYTHONPATH=backend "$PYTHON" -m unittest discover -s backend/tests -q
print "[2/7] Webcam bridge tests"
"$PYTHON" -m unittest discover -s webcam -q
print "[3/7] ML tests"
"$PYTHON" -m unittest discover -s ml -q
print "[4/7] Launcher and Quest session tests"
"$PYTHON" -m unittest discover -s scripts/tests -q
print "[5/7] Unity EditMode tests"
if [[ ! -x "$UNITY" ]]; then
  print -u2 "Unity 6000.6.5f1 was not found at $UNITY"
  exit 1
fi
# Do not pass -quit: the Unity Test Framework owns shutdown and writes the XML
# result after the test run finishes.
"$UNITY" -batchmode -projectPath unity -runTests -testPlatform editmode \
  -testResults "$RESULTS" -logFile "${RESULTS%.xml}.log"
"$PYTHON" -c 'import sys, xml.etree.ElementTree as E; r=E.parse(sys.argv[1]).getroot(); assert r.get("result") == "Passed" and int(r.get("failed", "1")) == 0 and int(r.get("total", "0")) > 0, r.attrib' "$RESULTS"
print "[6/7] Build current Mac sources for rendered checks"
"$UNITY" -batchmode -projectPath unity -executeMethod BCSignVR.Editor.ProjectSetup.BuildMac -quit -logFile "$RESULTS_DIR/mac-build.log"
print "[7/7] Rendered classroom UI smoke"
./scripts/render_ui_smoke.sh
print "All BCSignVR test suites passed. Results: $RESULTS"
