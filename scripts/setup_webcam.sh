#!/bin/zsh
set -euo pipefail

PROJECT_ROOT="${0:A:h:h}"
"${PYTHON_BIN:-python3}" -m venv "$PROJECT_ROOT/.venv"
"$PROJECT_ROOT/.venv/bin/python" -m pip install --upgrade pip
"$PROJECT_ROOT/.venv/bin/pip" install -r "$PROJECT_ROOT/webcam/requirements.txt"

echo "Webcam tracking is ready. macOS may ask for Camera permission on first launch."

