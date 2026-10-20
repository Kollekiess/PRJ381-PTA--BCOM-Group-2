#!/usr/bin/env python3
"""Build a small, attributed SASL movement baseline from the UFS place-name dataset.

This is a research/demo model, not an official terminology list or a production
translator. It downloads only selected CC BY-NC-SA 4.0 videos and stores no video
inside the Git repository.
"""
from __future__ import annotations

import argparse
import json
import math
import statistics
import urllib.request
from pathlib import Path

import cv2
import mediapipe as mp

ARTICLE_API = "https://api.figshare.com/v2/articles/29376494"
SOURCE = "https://doi.org/10.38140/ufs.29376494.v2"
LICENSE = "CC BY-NC-SA 4.0"
DEFAULT_CLASSES = ("cape_town", "bloemfontein", "pretoria")


def normalize(points):
    wrist = points[0]
    centered = [[p.x - wrist.x, p.y - wrist.y, p.z - wrist.z] for p in points]
    scale = max(math.dist((0, 0, 0), point) for point in centered) or 1.0
    # Wrist image location preserves movement/location while local joints preserve handshape.
    return [wrist.x, wrist.y] + [value / scale for point in centered for value in point]


def resample(sequence, length=32):
    if not sequence:
        return []
    if len(sequence) == 1:
        return sequence * length
    return [sequence[round(i * (len(sequence) - 1) / (length - 1))] for i in range(length)]


def extract(video_path: Path):
    frames = []
    capture = cv2.VideoCapture(str(video_path))
    hands_api = mp.solutions.hands
    with hands_api.Hands(static_image_mode=False, max_num_hands=1, model_complexity=1,
                         min_detection_confidence=.45, min_tracking_confidence=.45) as hands:
        while True:
            ok, frame = capture.read()
            if not ok:
                break
            result = hands.process(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
            if result.multi_hand_landmarks:
                frames.append(normalize(result.multi_hand_landmarks[0].landmark))
    capture.release()
    return resample(frames)


def train(data_dir: Path, output: Path, samples_per_class: int):
    article = json.load(urllib.request.urlopen(ARTICLE_API))
    video_files = [item for item in article["files"] if item["name"].endswith(".mp4")]
    classes = []
    data_dir.mkdir(parents=True, exist_ok=True)
    for class_name in DEFAULT_CLASSES:
        candidates = [item for item in video_files if item["name"].startswith(class_name + "_")]
        sequences = []
        sources = []
        for item in candidates[:samples_per_class]:
            path = data_dir / item["name"]
            if not path.exists():
                print(f"Downloading {item['name']}...")
                urllib.request.urlretrieve(item["download_url"], path)
            sequence = extract(path)
            if sequence:
                sequences.append(sequence)
                sources.append({"file": item["name"], "md5": item["supplied_md5"]})
        if not sequences:
            print(f"Skipping {class_name}: no hands detected")
            continue
        centroid = []
        for frame_index in range(32):
            centroid.append([
                statistics.fmean(sequence[frame_index][feature] for sequence in sequences)
                for feature in range(len(sequences[0][frame_index]))
            ])
        distances = [
            math.sqrt(statistics.fmean((a - b) ** 2 for frame, mean in zip(sequence, centroid)
                                       for a, b in zip(frame, mean)))
            for sequence in sequences
        ]
        classes.append({
            "label": "RESEARCH_SASL_" + class_name.upper(),
            "displayName": class_name.replace("_", " ").title(),
            "sampleCount": len(sequences),
            "acceptanceDistance": max(.08, statistics.fmean(distances) * 2.5),
            "centroidSequence": centroid,
            "prototypeSequences": sequences,
            "sourceFiles": sources,
        })
    if len(classes) < 2:
        raise SystemExit("Too few classes could be extracted to train a model")
    model = {
        "format": "BCSignVRSASLSequenceV1",
        "status": "research-baseline-not-production",
        "source": SOURCE,
        "license": LICENSE,
        "notice": "Place-name variants; not an official SASL terminology list.",
        "frameCount": 32,
        "featureCount": len(classes[0]["centroidSequence"][0]),
        "classes": classes,
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(model, separators=(",", ":")) + "\n")
    print(f"Wrote {output} with {len(classes)} SASL place-name classes")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-dir", type=Path, default=Path("ml/data/ufs_place_names"))
    parser.add_argument("--output", type=Path,
                        default=Path("unity/Assets/Resources/sasl_sequence_model.json"))
    parser.add_argument("--samples-per-class", type=int, default=3)
    args = parser.parse_args()
    train(args.data_dir, args.output, args.samples_per_class)

