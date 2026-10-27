#!/usr/bin/env python3
"""Check the local BC Sign VR stack without changing account data."""

from __future__ import annotations

import json
import plistlib
import sqlite3
import subprocess
import sys
import urllib.request
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DB = ROOT / "backend" / "data" / "bcsignvr.sqlite3"
APP_CONTENTS = ROOT / "unity" / "Builds" / "macOS" / "BCSignVR.app" / "Contents"


def check(name: str, condition: bool, detail: str) -> None:
    print(f"{'PASS' if condition else 'FAIL'}  {name}: {detail}")
    if not condition:
        raise SystemExit(1)


def main() -> None:
    try:
        with urllib.request.urlopen("http://127.0.0.1:8787/health", timeout=2) as response:
            health = json.loads(response.read())
        check("local API", health.get("status") == "ok", str(health))
    except Exception as error:
        check("local API", False, f"start the classroom launcher first ({error})")

    with (APP_CONTENTS / "Info.plist").open("rb") as plist_file:
        executable = plistlib.load(plist_file)["CFBundleExecutable"]
    app = APP_CONTENTS / "MacOS" / executable
    check("Mac build", app.is_file() and app.stat().st_mode & 0o111, str(app))
    check("progress database", DB.is_file(), str(DB))
    with sqlite3.connect(DB) as connection:
        lesson_count = connection.execute("SELECT COUNT(*) FROM lessons").fetchone()[0]
        alphabet_count = connection.execute(
            "SELECT COUNT(*) FROM lessons WHERE id LIKE 'alphabet-%'"
        ).fetchone()[0]
        columns = {row[1] for row in connection.execute("PRAGMA table_info(sessions)")}
    check("seeded lesson path", lesson_count >= 30 and alphabet_count == 26,
          f"{lesson_count} lessons, {alphabet_count} alphabet lessons")
    check("input provenance", "input_source" in columns,
          "sessions keep webcam, VR, demo, and reference provenance")

    probe = [
        sys.executable,
        "-c",
        "import cv2; c=cv2.VideoCapture(0); ok, frame=c.read(); print(ok, None if frame is None else frame.shape[:2]); c.release(); raise SystemExit(0 if ok and frame is not None else 1)",
    ]
    camera = subprocess.run(probe, cwd=ROOT, capture_output=True, text=True, timeout=15)
    check("camera frame", camera.returncode == 0, camera.stdout.strip() or camera.stderr.strip())
    print("Local stack smoke test passed. No account rows were written.")


if __name__ == "__main__":
    main()
