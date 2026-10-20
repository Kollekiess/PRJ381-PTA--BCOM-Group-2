#!/bin/zsh
set -euo pipefail

PROJECT_ROOT="${0:A:h:h}"
if [[ $# -ne 1 || -z "$1" ]]; then
  print -u2 "Usage: ./scripts/record_sasl_sample.sh LABEL"
  print -u2 "Example: ./scripts/record_sasl_sample.sh hello"
  exit 2
fi

label="${1:l}"
if [[ ! "$label" =~ '^[a-z0-9][a-z0-9-]*$' ]]; then
  print -u2 "LABEL may contain only letters, numbers and hyphens."
  exit 2
fi
recordings="$PROJECT_ROOT/webcam/recordings"
mkdir -p "$recordings"
output="$recordings/${label}-$(date +%Y%m%d-%H%M%S).jsonl"
print "Recording landmarks for '$label' to $output"
print "Press Ctrl-C when the complete demonstration is finished."
exec "$PROJECT_ROOT/.venv/bin/python" "$PROJECT_ROOT/webcam/hand_bridge.py" \
  --no-preview --record "$output"
