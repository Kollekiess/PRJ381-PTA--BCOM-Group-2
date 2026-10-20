#!/usr/bin/env python3
"""Build a reviewed SASL movement model from one labelled landmark recording.

The input is JSONL written by ``webcam/hand_bridge.py --record``.  A fluent
SASL reviewer must supply the label and provenance; this tool never infers a
word from English text or from the bridge's provisional recognition field.
The output is the ``SequenceRecognizer`` format used by the live bridge.
"""
from __future__ import annotations

import argparse
import json
import math
from pathlib import Path


def _features(packet: dict, line_number: int) -> list[float] | None:
    if not packet.get("tracked"):
        return None
    points = packet.get("landmarks")
    if not isinstance(points, list) or len(points) != 21:
        raise ValueError(f"Line {line_number}: expected 21 landmarks")
    values = []
    for point in points:
        if not isinstance(point, dict) or any(key not in point for key in ("x", "y", "z")):
            raise ValueError(f"Line {line_number}: malformed landmark")
        row = [float(point[key]) for key in ("x", "y", "z")]
        if not all(math.isfinite(value) for value in row):
            raise ValueError(f"Line {line_number}: landmark values must be finite")
        values.append(row)
    wrist = values[0]
    centered = [[value - origin for value, origin in zip(point, wrist)] for point in values]
    scale = max(math.dist((0, 0, 0), point) for point in centered)
    if scale <= 1e-6:
        raise ValueError(f"Line {line_number}: degenerate hand shape")
    return wrist[:2] + [value / scale for point in centered for value in point]


def _sample(frames: list[list[float]], count: int = 32) -> list[list[float]]:
    if len(frames) < count:
        raise ValueError(f"recording contains {len(frames)} tracked frames; need at least {count}")
    return [frames[round(index * (len(frames) - 1) / (count - 1))] for index in range(count)]


def train(input_path: Path, output_path: Path, *, label: str, signer: str,
          reviewed_by: str, source: str, min_frames: int = 32,
          acceptance_distance: float = .20) -> int:
    if not label.startswith("REVIEWED_SASL_"):
        raise ValueError("label must start with REVIEWED_SASL_")
    if not signer.strip() or not reviewed_by.strip() or not source.strip():
        raise ValueError("signer, reviewedBy and source are required")
    if not math.isfinite(acceptance_distance) or acceptance_distance <= 0:
        raise ValueError("acceptance distance must be positive and finite")
    frames = []
    with input_path.open(encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, 1):
            if line.strip():
                features = _features(json.loads(line), line_number)
                if features is not None:
                    frames.append(features)
    if len(frames) < min_frames:
        raise ValueError(f"recording contains {len(frames)} tracked frames; need {min_frames}")
    sequence = _sample(frames)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps({
        "format": "BCSignVRSequenceModelV1",
        "status": "reviewed-sasl-recording",
        "classes": [{
            "label": label,
            "signer": signer,
            "reviewedBy": reviewed_by,
            "source": source,
            "sampleCount": len(frames),
            "prototypeSequences": [sequence],
            "acceptanceDistance": acceptance_distance,
        }],
    }, indent=2) + "\n", encoding="utf-8")
    return len(frames)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--label", required=True, help="For example REVIEWED_SASL_HELLO")
    parser.add_argument("--signer", required=True)
    parser.add_argument("--reviewed-by", required=True, dest="reviewed_by")
    parser.add_argument("--source", required=True)
    parser.add_argument("--min-frames", type=int, default=32)
    parser.add_argument("--acceptance-distance", type=float, default=.20)
    args = parser.parse_args()
    count = train(args.input, args.output, label=args.label, signer=args.signer,
                   reviewed_by=args.reviewed_by, source=args.source,
                   min_frames=args.min_frames, acceptance_distance=args.acceptance_distance)
    print(f"Prepared {count} reviewed frames for {args.label}")


if __name__ == "__main__":
    main()
