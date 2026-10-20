#!/usr/bin/env python3
"""Export real SASL prototype recordings into Unity's array-friendly JSON format.

This deliberately selects the FIRST recorded prototype for each class. Averaged
classification centroids can combine incompatible handshapes and must never be
presented as a signing demonstration. Attribution travels with the derived data.
"""
from __future__ import annotations

import argparse
import json
import math
from pathlib import Path


def export_playback(model_path: Path, output_path: Path) -> dict:
    model = json.loads(model_path.read_text())
    lessons = []
    for entry in model.get("classes", []):
        prototypes = entry.get("prototypeSequences", [])
        if not prototypes or not prototypes[0]:
            raise ValueError(f"{entry.get('label', 'Unnamed class')} has no recorded prototype")
        frames = []
        for row in prototypes[0]:
            if len(row) != 65 or not all(math.isfinite(value) for value in row):
                raise ValueError(f"{entry['label']} contains an invalid 65-feature recording frame")
            joints = [dict(zip(("x", "y", "z"), row[2 + i * 3:5 + i * 3])) for i in range(21)]
            frames.append({"wristX": row[0], "wristY": row[1], "joints": joints})
        sources = entry.get("sourceFiles", [])
        first_source = sources[0] if sources else {}
        lessons.append({
            "label": entry["label"],
            "title": entry.get("displayName", entry["label"]),
            "sourceFile": first_source.get("file", ""),
            "sourceMd5": first_source.get("md5", ""),
            "frames": frames,
        })
    if not lessons:
        raise ValueError("The SASL model contains no recorded lessons")
    playback = {
        "format": "BCSignVRSASLPlaybackV1",
        "source": model.get("source", ""),
        "license": model.get("license", ""),
        "notice": model.get("notice", ""),
        "status": model.get("status", ""),
        "derivation": "First recorded landmark prototype per class; no centroid averaging.",
        "durationSeconds": 4.0,
        "lessons": lessons,
    }
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(playback, separators=(",", ":")) + "\n")
    return playback


if __name__ == "__main__":
    repo = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model", type=Path, default=repo / "unity/Assets/Resources/sasl_sequence_model.json")
    parser.add_argument("--output", type=Path, default=repo / "unity/Assets/Resources/sasl_playback.json")
    args = parser.parse_args()
    result = export_playback(args.model, args.output)
    print(f"Exported {len(result['lessons'])} recorded SASL demonstrations to {args.output}")
