#!/usr/bin/env python3
from __future__ import annotations

import json
import math
import statistics
import sys
from collections import defaultdict
from pathlib import Path


def flatten_normalized(joints):
    if len(joints) < 6 or any(len(point) != 3 for point in joints):
        raise ValueError("Each sample needs an ordered list of at least six XYZ joints")
    wrist = joints[0]
    centered = [[float(v) - float(wrist[i]) for i, v in enumerate(point)] for point in joints]
    scale = max(math.dist(centered[0], point) for point in centered)
    if scale < 1e-6:
        raise ValueError("Degenerate hand sample")
    return [coordinate / scale for point in centered for coordinate in point]


def train(input_path: Path, output_path: Path):
    grouped = defaultdict(list)
    with input_path.open() as stream:
        for line_number, line in enumerate(stream, 1):
            if not line.strip():
                continue
            sample = json.loads(line)
            label = str(sample.get("label", ""))
            if not label.startswith("REVIEWED_SASL_"):
                raise ValueError(f"Line {line_number}: label must start with REVIEWED_SASL_")
            if not sample.get("reviewedBy") or not sample.get("source"):
                raise ValueError(f"Line {line_number}: reviewedBy and source are required")
            grouped[label].append(flatten_normalized(sample["joints"]))
    if not grouped:
        raise ValueError("No reviewed samples found")
    feature_sizes = {len(row) for rows in grouped.values() for row in rows}
    if len(feature_sizes) != 1:
        raise ValueError("All samples must use the same joint order and count")
    model = {"format": "BCSignVRPoseCentroidsV1", "classes": []}
    for label, rows in sorted(grouped.items()):
        centroid = [statistics.fmean(column) for column in zip(*rows)]
        distances = [math.sqrt(statistics.fmean((a - b) ** 2 for a, b in zip(row, centroid))) for row in rows]
        model["classes"].append({"label": label, "sampleCount": len(rows), "centroid": centroid, "acceptanceDistance": max(0.05, statistics.fmean(distances) * 2.5 if distances else 0.05)})
    output_path.write_text(json.dumps(model, indent=2) + "\n")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("Usage: train_pose_model.py INPUT.jsonl OUTPUT.json")
    train(Path(sys.argv[1]), Path(sys.argv[2]))

