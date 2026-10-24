#!/usr/bin/env python3
"""Wrap the reviewed A–Z template map in a JsonUtility-friendly Resources file."""

from __future__ import annotations

import ast
import json
import struct
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "ml" / "team_sasl" / "sasl_templates.json"
OUTPUT = ROOT / "unity" / "Assets" / "Resources" / "sasl_templates_quest.json"
WEIGHTS = ROOT / "ml" / "team_sasl" / "sasl_dense_weights.npz"
LABELS = ROOT / "ml" / "team_sasl" / "sasl_labels.json"
DENSE_OUTPUT = ROOT / "unity" / "Assets" / "Resources" / "sasl_dense_quest.json"

DENSE_SHAPES = {
    "w1": (63, 128), "b1": (128,),
    "w2": (128, 64), "b2": (64,),
    "w3": (64, 26), "b3": (26,),
}


def read_npy_float32(data: bytes) -> tuple[tuple[int, ...], list[float]]:
    if data[:6] != b"\x93NUMPY":
        raise ValueError("Dense model contains an invalid NumPy array")
    major, minor = data[6], data[7]
    if major == 1:
        header_start = 10
        header_length = struct.unpack("<H", data[8:10])[0]
    elif major in (2, 3):
        header_start = 12
        header_length = struct.unpack("<I", data[8:12])[0]
    else:
        raise ValueError(f"Unsupported NumPy array version {major}.{minor}")
    header_end = header_start + header_length
    header = ast.literal_eval(data[header_start:header_end].decode("latin1").strip())
    if header.get("descr") not in ("<f4", "=f4") or header.get("fortran_order"):
        raise ValueError("Dense model must contain little-endian, row-major float32 arrays")
    shape = tuple(header.get("shape", ()))
    count = 1
    for dimension in shape:
        count *= dimension
    values = struct.unpack("<" + "f" * count, data[header_end:])
    return shape, list(values)


def main() -> None:
    templates = json.loads(SOURCE.read_text(encoding="utf-8"))
    if len(templates) != 26 or any(len(values) != 63 for values in templates.values()):
        raise SystemExit("Expected the complete A–Z set of 63-feature templates")
    resource = {"templates": [
        {"label": label, "features": features}
        for label, features in sorted(templates.items())
    ]}
    OUTPUT.write_text(json.dumps(resource, separators=(",", ":")) + "\n", encoding="utf-8")

    labels = json.loads(LABELS.read_text(encoding="utf-8"))
    if len(labels) != 26 or len(set(labels)) != 26:
        raise SystemExit("Expected the complete A–Z dense-model label set")
    model = {"labels": labels}
    with zipfile.ZipFile(WEIGHTS) as archive:
        for name, expected_shape in DENSE_SHAPES.items():
            shape, values = read_npy_float32(archive.read(name + ".npy"))
            if shape != expected_shape or len(values) != _element_count(expected_shape):
                raise SystemExit(f"Unexpected dense-model shape for {name}: {shape}")
            model[name] = values
    DENSE_OUTPUT.write_text(
        json.dumps(model, separators=(",", ":"), allow_nan=False) + "\n", encoding="utf-8")
    print(f"Exported {len(resource['templates'])} Quest alphabet templates to {OUTPUT}")
    print(f"Exported A–Z dense model to {DENSE_OUTPUT}")


def _element_count(shape: tuple[int, ...]) -> int:
    count = 1
    for dimension in shape:
        count *= dimension
    return count


if __name__ == "__main__":
    main()
