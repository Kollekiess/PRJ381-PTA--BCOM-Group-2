#!/usr/bin/env python3
"""Export one attributed Chantel sequence clip per word for the Unity preview.

The Quest app does not run the uploaded Keras word model yet.  It does, however,
need to show learners what the uploaded sequences look like.  This exporter
keeps the original 30-frame, two-hand landmark format and writes a small,
read-only Unity resource that the Bob-style renderer can replay.
"""

from __future__ import annotations

import argparse
import io
import json
import zipfile
from pathlib import Path

import numpy as np


WORDS = {
    "GOODBYE": 25,
    "HELLO": 24,
    "NICE_TO_MEET_YOU": 14,
    "PLEASE": 30,
    "THANK_YOU": 26,
}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("archive", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()

    with zipfile.ZipFile(args.archive) as archive:
        names = archive.namelist()
        words = []
        for label, clip_count in WORDS.items():
            suffix = f"data/sequences/{label}/clip_000.npy"
            matches = [name for name in names if name.endswith(suffix)]
            if len(matches) != 1:
                raise RuntimeError(f"Could not find exactly one first clip for {label}: {matches}")
            clip = np.load(io.BytesIO(archive.read(matches[0])), allow_pickle=False)
            if clip.shape != (30, 126):
                raise RuntimeError(f"Unexpected {label} shape: {clip.shape}")
            words.append({
                "label": label,
                "clipCount": clip_count,
                "frames": [{"features": frame} for frame in clip.astype(float).round(6).tolist()],
            })

    payload = {
        "format": "chantel-word-playback-v1",
        "source": "Chantel / Updated-Hand-signs upload",
        "notice": "One recorded 30-frame clip is replayed as a visual reference. The uploaded word classifier is not live Quest recognition.",
        "frameCount": 30,
        "featureCount": 126,
        "words": words,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, separators=(",", ":")) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
