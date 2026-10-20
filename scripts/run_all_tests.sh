#!/bin/zsh
set -euo pipefail

PROJECT_ROOT="${0:A:h:h}"
UNITY="/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app/Contents/MacOS/Unity"
RESULTS="${TMPDIR:-/tmp}/bcsignvr-unity-tests.xml"

cd "$PROJECT_ROOT"
print "[1/4] Backend tests"
PYTHONPATH=backend .venv/bin/python -m unittest discover -s backend/tests -q
print "[2/4] Webcam bridge tests"
.venv/bin/python -m unittest discover -s webcam -q
print "[3/4] ML tests"
.venv/bin/python -m unittest discover -s ml -q
print "[4/4] Unity EditMode tests"
if [[ ! -x "$UNITY" ]]; then
  print -u2 "Unity 6000.6.5f1 was not found at $UNITY"
  exit 1
fi
# Do not pass -quit: the Unity Test Framework owns shutdown and writes the XML
# result after the test run finishes.
"$UNITY" -batchmode -projectPath unity -runTests -testPlatform editmode \
  -testResults "$RESULTS" -logFile "${RESULTS%.xml}.log"
grep -q 'result="Passed"' "$RESULTS"
print "[5/5] Rendered classroom UI smoke"
./scripts/render_ui_smoke.sh
print "All BCSignVR test suites passed. Results: $RESULTS"
