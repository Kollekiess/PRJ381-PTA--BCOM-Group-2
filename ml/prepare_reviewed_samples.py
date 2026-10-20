#!/usr/bin/env python3
"""Convert a consented webcam landmark recording into reviewed training rows.

The webcam bridge records packets, not labels. This small, explicit step keeps
labelling and SASL review separate: a reviewer must choose the label and cite
the source before the trainer will accept the rows.
"""
from __future__ import annotations

import argparse
import json
import math
from pathlib import Path


def _landmarks(packet: dict, line_number: int) -> list[list[float]] | None:
    if not packet.get("tracked"):
        return None
    points = packet.get("landmarks")
    if not isinstance(points, list) or len(points) != 21:
        raise ValueError(f"Line {line_number}: expected 21 landmarks")
    joints: list[list[float]] = []
    for point in points:
        if not isinstance(point, dict) or any(key not in point for key in ("x", "y", "z")):
            raise ValueError(f"Line {line_number}: malformed landmark")
        row = [float(point[key]) for key in ("x", "y", "z")]
        if not all(math.isfinite(value) for value in row):
            raise ValueError(f"Line {line_number}: landmark values must be finite")
        joints.append(row)
    return joints


def prepare(input_path: Path, output_path: Path, *, label: str, signer: str,
            reviewed_by: str, source: str, min_frames: int = 1) -> int:
    if not label.startswith("REVIEWED_SASL_"):
        raise ValueError("label must start with REVIEWED_SASL_")
    if not signer.strip() or not reviewed_by.strip() or not source.strip():
        raise ValueError("signer, reviewedBy and source are required")
    rows = []
    with input_path.open(encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, 1):
            if not line.strip():
                continue
            try:
                packet = json.loads(line)
            except json.JSONDecodeError as error:
                raise ValueError(f"Line {line_number}: invalid JSON") from error
            joints = _landmarks(packet, line_number)
            if joints is not None:
                rows.append({"label": label, "signer": signer,
                             "reviewedBy": reviewed_by, "source": source,
                             "joints": joints})
    if len(rows) < min_frames:
        raise ValueError(f"recording contains {len(rows)} tracked frames; need {min_frames}")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("w", encoding="utf-8") as stream:
        for row in rows:
            stream.write(json.dumps(row, separators=(",", ":")) + "\n")
    return len(rows)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path, help="JSONL produced with hand_bridge.py --record")
    parser.add_argument("output", type=Path, help="reviewed JSONL consumed by train_pose_model.py")
    parser.add_argument("--label", required=True, help="For example REVIEWED_SASL_A")
    parser.add_argument("--signer", required=True, help="Pseudonymous signer identifier")
    parser.add_argument("--reviewed-by", required=True, dest="reviewed_by")
    parser.add_argument("--source", required=True, help="Consent/form/source reference")
    parser.add_argument("--min-frames", type=int, default=1)
    args = parser.parse_args()
    count = prepare(args.input, args.output, label=args.label, signer=args.signer,
                    reviewed_by=args.reviewed_by, source=args.source,
                    min_frames=args.min_frames)
    print(f"Prepared {count} reviewed landmark frames for {args.label}")


if __name__ == "__main__":
    main()
