#!/bin/zsh
set -euo pipefail

PROJECT_ROOT="${0:A:h:h}"
APP="$PROJECT_ROOT/unity/Builds/macOS/BCSignVR.app/Contents/MacOS/signzzZ"
if [[ ! -x "$APP" ]]; then
  print -u2 "Mac build not found: $APP"
  exit 1
fi

output_dir="$(mktemp -d /tmp/bcsignvr-ui.XXXXXX)"
pages=(profiles add-profile greeting fingerspell alphabet alphabet-vr tracked-hand-vr practice success progress help input sasl reference sasl-capetown sasl-bloemfontein sasl-pretoria)
for letter in {a..z}; do pages+=("alphabet-$letter"); done
for page in $pages; do
  output="$output_dir/$page.png"
  print "Rendering $page"
  BCSIGNVR_SCREENSHOT="$output" BCSIGNVR_PREVIEW_PAGE="$page" \
    "$APP" -screen-fullscreen 0 -screen-width 1440 -screen-height 900 \
    >"$output_dir/$page.log" 2>&1
  [[ -s "$output" ]] || { print -u2 "No screenshot produced for $page"; exit 1; }
  file "$output" | grep -q 'PNG image data' || { print -u2 "Invalid screenshot for $page"; exit 1; }
done
print "UI smoke passed. Screenshots: $output_dir"
