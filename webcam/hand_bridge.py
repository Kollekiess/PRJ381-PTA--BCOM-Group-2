#!/usr/bin/env python3
"""Track one hand with the Mac webcam and stream landmarks to Unity over localhost UDP."""
from __future__ import annotations

import argparse
import base64
from collections import deque
import json
import math
import re
import socket
import subprocess
import time
from pathlib import Path

import cv2
import mediapipe as mp
try:
    import numpy as np
except ImportError:  # The template fallback still works without NumPy.
    np = None


class SequenceRecognizer:
    MIN_FRAMES = 12
    MAX_FRAME_GAP = .45
    MIN_CONFIDENCE = .55
    MIN_CONFIDENCE_MARGIN = .12

    def __init__(self, model_path: Path | None):
        self.frames = deque(maxlen=120)
        self.classes = []
        self.last_frame_at = None
        if model_path and model_path.exists():
            model = json.loads(model_path.read_text())
            self.classes = model.get("classes", [])

    def reset(self):
        """A new visible hand starts a new movement, never the previous sign's tail."""
        self.frames.clear()
        self.last_frame_at = None

    @staticmethod
    def features(landmarks):
        points = landmark_values(landmarks)
        wrist = points[0]
        centered = [[value - origin for value, origin in zip(point, wrist)]
                    for point in points]
        scale = max(math.dist((0, 0, 0), point) for point in centered)
        if scale <= 1e-6:
            raise ValueError("Hand landmarks have no measurable shape")
        return wrist[:2] + [value / scale for point in centered for value in point]

    def update(self, landmarks):
        observed_at = time.monotonic()
        if self.last_frame_at is not None and observed_at - self.last_frame_at > self.MAX_FRAME_GAP:
            self.reset()
        try:
            features = self.features(landmarks)
        except (AttributeError, TypeError, ValueError, OverflowError):
            self.reset()
            return "UNKNOWN", 0.0
        self.last_frame_at = observed_at
        self.frames.append(features)
        if not self.classes:
            return "MODEL_UNAVAILABLE", 0.0
        if len(self.frames) < self.MIN_FRAMES:
            return "COLLECTING_MOVEMENT", min(1.0, len(self.frames) / self.MIN_FRAMES)
        sampled = [self.frames[round(i * (len(self.frames) - 1) / 31)] for i in range(32)]
        candidates = []
        for item in self.classes:
            distances = []
            for sequence in item.get("prototypeSequences") or [item.get("centroidSequence")]:
                if not valid_sequence(sequence, len(features)):
                    continue
                distances.append(math.sqrt(sum(
                    (actual - expected) ** 2
                    for frame, expected_frame in zip(sampled, sequence)
                    for actual, expected in zip(frame, expected_frame)
                ) / (32 * len(self.frames[0]))))
            if not distances:
                continue
            distance = min(distances)
            threshold = item.get("acceptanceDistance", .2)
            if not isinstance(threshold, (float, int)) or not math.isfinite(threshold) or threshold <= 0:
                continue
            confidence = math.exp(-distance / threshold)
            candidates.append((confidence, distance, threshold, item.get("label", "UNKNOWN")))
        if not candidates:
            return "UNKNOWN", 0.0
        candidates.sort(reverse=True)
        confidence, distance, threshold, label = candidates[0]
        # This is a conservative similarity gate, not a calibrated accuracy estimate.
        # A nearest neighbour alone must never turn arbitrary movement into a sign.
        if distance > threshold or confidence < self.MIN_CONFIDENCE:
            return "UNKNOWN", 0.0
        if len(candidates) > 1 and confidence - candidates[1][0] < self.MIN_CONFIDENCE_MARGIN:
            return "UNKNOWN", 0.0
        return label, min(1.0, confidence)


class AlphabetTemplateRecognizer:
    """Conservative A-Z baseline using the team's reviewed templates."""
    MIN_DISTANCE = .32
    MIN_MARGIN = .045

    def __init__(self, model_path: Path | None):
        self.templates = {}
        self.history = deque(maxlen=7)
        if model_path and model_path.exists():
            try:
                raw = json.loads(model_path.read_text())
                self.templates = {str(label): [float(value) for value in values]
                                  for label, values in raw.items()
                                  if isinstance(values, list) and len(values) == 63}
            except (OSError, ValueError, TypeError):
                self.templates = {}

    @staticmethod
    def features(landmarks):
        points = landmark_values(landmarks)
        wrist = points[0]
        centered = [[value - origin for value, origin in zip(point, wrist)]
                    for point in points]
        scale = math.hypot(centered[9][0], centered[9][1]) or 1.0
        return [value / scale for point in centered for value in point]

    def update(self, landmarks):
        if not self.templates:
            return "MODEL_UNAVAILABLE", 0.0
        try:
            features = self.features(landmarks)
        except (AttributeError, TypeError, ValueError, OverflowError):
            self.history.clear()
            return "UNKNOWN", 0.0
        ranked = sorted(
            (math.sqrt(sum((actual - expected) ** 2 for actual, expected in zip(features, template)) / 63), label)
            for label, template in self.templates.items())
        best_distance, best_label = ranked[0]
        second_distance = ranked[1][0] if len(ranked) > 1 else float("inf")
        if best_distance > self.MIN_DISTANCE or second_distance - best_distance < self.MIN_MARGIN:
            self.history.clear()
            return "UNKNOWN", 0.0
        self.history.append(best_label)
        stable_label = max(set(self.history), key=self.history.count)
        confidence = math.exp(-best_distance / .35) * (self.history.count(stable_label) / len(self.history))
        return "TEAM_SASL_" + stable_label, min(1.0, confidence)


class DenseAlphabetRecognizer:
    """NumPy inference for the team's exported 63-128-64-26 Keras model."""
    MIN_CONFIDENCE = .60
    MIN_MARGIN = .12

    def __init__(self, weights_path: Path | None, labels_path: Path | None):
        self.weights = None
        self.labels = []
        self.history = deque(maxlen=7)
        if np is None or not weights_path or not weights_path.exists() or not labels_path or not labels_path.exists():
            return
        try:
            archive = np.load(weights_path)
            self.weights = {key: archive[key] for key in archive.files}
            archive.close()
            self.labels = json.loads(labels_path.read_text())
            if len(self.labels) != 26 or any(self.weights[key].shape != shape for key, shape in {
                    "w1": (63, 128), "b1": (128,), "w2": (128, 64), "b2": (64,),
                    "w3": (64, 26), "b3": (26,)}.items()):
                self.weights = None
        except (OSError, ValueError, KeyError, TypeError):
            self.weights = None

    def update(self, landmarks):
        if self.weights is None:
            return "MODEL_UNAVAILABLE", 0.0
        try:
            x = np.asarray(AlphabetTemplateRecognizer.features(landmarks), dtype=np.float32)
            w = self.weights
            x = np.maximum(0, x @ w["w1"] + w["b1"])
            x = np.maximum(0, x @ w["w2"] + w["b2"])
            logits = x @ w["w3"] + w["b3"]
            logits -= np.max(logits)
            probabilities = np.exp(logits)
            probabilities /= np.sum(probabilities)
        except (AttributeError, TypeError, ValueError, OverflowError):
            self.history.clear()
            return "UNKNOWN", 0.0
        order = np.argsort(probabilities)[::-1]
        best, second = int(order[0]), int(order[1])
        confidence = float(probabilities[best])
        if confidence < self.MIN_CONFIDENCE or confidence - float(probabilities[second]) < self.MIN_MARGIN:
            self.history.clear()
            return "UNKNOWN", 0.0
        self.history.append(self.labels[best])
        stable = max(set(self.history), key=self.history.count)
        stability = self.history.count(stable) / len(self.history)
        return "TEAM_SASL_" + stable, min(1.0, confidence * stability)


def landmark_values(landmarks):
    points = [[float(point.x), float(point.y), float(point.z)]
              for point in landmarks.landmark]
    if len(points) != 21 or not all(math.isfinite(value) for point in points for value in point):
        raise ValueError("Expected 21 finite hand landmarks")
    return points


def valid_sequence(sequence, feature_count):
    return (isinstance(sequence, list) and len(sequence) == 32 and
            all(isinstance(frame, list) and len(frame) == feature_count and
                all(isinstance(value, (int, float)) and math.isfinite(value) for value in frame)
                for frame in sequence))


def read_camera_frame(camera, on_missing, *, video=False, max_failures=30, retry_delay=.05):
    """Bound live-camera retries and send tracking loss as soon as a read fails."""
    for attempt in range(max_failures):
        ok, frame = camera.read()
        if ok and frame is not None:
            return frame
        on_missing()
        if video:
            return None
        if attempt < max_failures - 1:
            time.sleep(retry_delay)
    raise RuntimeError("Camera stopped providing frames. Reconnect it and restart webcam tracking.")


def discover_mac_camera_index() -> int:
    """Prefer the built-in FaceTime camera over Continuity Camera/iPhone devices."""
    try:
        result = subprocess.run(
            ["ffmpeg", "-f", "avfoundation", "-list_devices", "true", "-i", ""],
            capture_output=True, text=True, timeout=5, check=False,
        )
    except (OSError, subprocess.SubprocessError):
        return 0
    candidates = []
    for line in (result.stdout + "\n" + result.stderr).splitlines():
        match = re.search(r"\[(\d+)\]\s*(.+)$", line)
        if not match:
            continue
        name = match.group(2).strip().lower()
        if any(word in name for word in ("iphone", "ipad", "continuity", "desk view")):
            continue
        score = 0
        if "facetime" in name:
            score += 4
        if "built-in" in name or "mac" in name or "isight" in name:
            score += 3
        candidates.append((score, int(match.group(1))))
    return max(candidates, default=(0, 0))[1]


def open_camera(preferred: int, *, fallback: bool = True):
    """Open the preferred camera, optionally falling back to another index."""
    indices = [preferred] if not fallback else [preferred] + [index for index in range(4) if index != preferred]
    for index in indices:
        camera = cv2.VideoCapture(index)
        if camera.isOpened():
            return camera
        camera.release()
    return None


def packet_for(hand_landmarks, handedness: str, frame_width: int, frame_height: int,
               recognized="", confidence=0.0, preview_jpeg="") -> dict:
    points = landmark_values(hand_landmarks)
    confidence = float(confidence)
    confidence = min(1.0, max(0.0, confidence)) if math.isfinite(confidence) else 0.0
    return {
        "version": 1,
        "timestamp": time.time(),
        "tracked": True,
        "handedness": handedness,
        "imageWidth": frame_width,
        "imageHeight": frame_height,
        "recognized": recognized,
        "confidence": confidence,
        "previewJpeg": preview_jpeg if isinstance(preview_jpeg, str) else "",
        "landmarks": [
            {"x": point[0], "y": point[1], "z": point[2]}
            for point in points
        ],
    }


def select_primary_hand(hand_landmarks, handedness):
    """Prefer the right hand while retaining a deterministic fallback."""
    if not hand_landmarks:
        raise ValueError("At least one hand is required")
    for index, label in enumerate(handedness):
        if str(label).lower() == "right":
            return index
    return 0


def packet_hands(hand_landmarks, handedness):
    """Serialize every detected hand while keeping MediaPipe objects out of JSON."""
    if len(hand_landmarks) != len(handedness):
        raise ValueError("Hand and handedness counts must match")
    return [
        {"handedness": str(label),
         "landmarks": [{"x": point[0], "y": point[1], "z": point[2]}
                       for point in landmark_values(item)]}
        for item, label in zip(hand_landmarks, handedness)
    ]


def choose_recognition(sequence_result, alphabet_result):
    """Keep reviewed/recorded sequence labels ahead of static A–Z fallback."""
    sequence_label, sequence_confidence = sequence_result
    if sequence_label.startswith(("RESEARCH_SASL_", "REVIEWED_SASL_")):
        return sequence_result
    return alphabet_result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--camera", default="mac",
                        help="Camera index, or 'mac' to prefer the built-in FaceTime camera")
    parser.add_argument("--video", type=Path, help="Use a video file instead of the live camera")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5055)
    parser.add_argument("--no-preview", action="store_true")
    parser.add_argument("--record", type=Path, help="Optional raw landmark JSONL recording")
    parser.add_argument("--model", type=Path, default=Path(__file__).resolve().parents[1] /
                        "unity/Assets/Resources/sasl_sequence_model.json")
    parser.add_argument("--alphabet-model", type=Path, default=Path(__file__).resolve().parents[1] /
                        "ml/team_sasl/sasl_templates.json")
    parser.add_argument("--alphabet-weights", type=Path, default=Path(__file__).resolve().parents[1] /
                        "ml/team_sasl/sasl_dense_weights.npz")
    parser.add_argument("--alphabet-labels", type=Path, default=Path(__file__).resolve().parents[1] /
                        "ml/team_sasl/sasl_labels.json")
    args = parser.parse_args()

    mac_camera = not args.video and str(args.camera).lower() in {"mac", "builtin", "built-in", "auto"}
    if args.video:
        camera_index = None
    elif mac_camera:
        camera_index = discover_mac_camera_index()
    else:
        camera_index = int(args.camera)
    camera = cv2.VideoCapture(str(args.video)) if args.video else open_camera(camera_index, fallback=not mac_camera)
    if not args.video:
        print(f"Using Mac camera index {camera_index} (built-in preference)", flush=True)
    if camera is None or not camera.isOpened():
        raise SystemExit("Could not open the webcam. Check macOS Camera permission for Terminal/Codex.")

    def configure_camera(device):
        device.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
        device.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)

    configure_camera(camera)
    sender = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    drawing = mp.solutions.drawing_utils
    hands_api = mp.solutions.hands
    recording = args.record.open("a", encoding="utf-8") if args.record else None
    preview_frame = 0
    recognizer = SequenceRecognizer(args.model)
    alphabet_recognizer = DenseAlphabetRecognizer(args.alphabet_weights, args.alphabet_labels)
    if alphabet_recognizer.weights is None:
        alphabet_recognizer = AlphabetTemplateRecognizer(args.alphabet_model)

    def preview_jpeg(frame):
        """Return a small local-only preview that is safe for a UDP packet."""
        nonlocal preview_frame
        if frame is None:
            return ""
        preview_frame += 1
        if preview_frame % 3 != 0:
            return ""
        thumbnail = cv2.resize(frame, (160, 90), interpolation=cv2.INTER_AREA)
        encoded, buffer = cv2.imencode(".jpg", thumbnail,
                                       [int(cv2.IMWRITE_JPEG_QUALITY), 35])
        return base64.b64encode(buffer.tobytes()).decode("ascii") if encoded and len(buffer) <= 6000 else ""

    def tracking_lost(frame=None):
        recognizer.reset()
        packet = {"version": 1, "tracked": False, "recognized": "UNKNOWN", "confidence": 0,
                  "previewJpeg": preview_jpeg(frame)}
        sender.sendto(json.dumps(packet, separators=(",", ":")).encode("utf-8"),
                      (args.host, args.port))

    try:
        with hands_api.Hands(
            static_image_mode=False,
            max_num_hands=2,
            model_complexity=1,
            min_detection_confidence=0.55,
            min_tracking_confidence=0.55,
        ) as hands:
            while True:
                try:
                    frame = read_camera_frame(camera, tracking_lost, video=bool(args.video))
                except RuntimeError:
                    # macOS cameras can briefly disappear when another app claims
                    # them. Re-open the device here so Unity keeps its session and
                    # receives a clean tracking-loss packet while we recover.
                    tracking_lost()
                    if args.video:
                        raise
                    camera.release()
                    time.sleep(1.0)
                    camera = open_camera(camera_index, fallback=not mac_camera)
                    if camera is None:
                        continue
                    configure_camera(camera)
                    continue
                if frame is None:
                    break
                rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                result = hands.process(rgb)
                if result.multi_hand_landmarks:
                    labels = [item.classification[0].label for item in result.multi_handedness]
                    primary_index = select_primary_hand(result.multi_hand_landmarks, labels)
                    landmarks = result.multi_hand_landmarks[primary_index]
                    handedness = labels[primary_index]
                    recognized, confidence = recognizer.update(landmarks)
                    alphabet_label, alphabet_confidence = alphabet_recognizer.update(landmarks)
                    recognized, confidence = choose_recognition(
                        (recognized, confidence), (alphabet_label, alphabet_confidence))
                    packet = packet_for(landmarks, handedness, frame.shape[1], frame.shape[0],
                                        recognized, confidence)
                    # A small, local-only preview lets the learner see the real
                    # webcam movement in the board's second card. It is not
                    # written to recordings and stays on 127.0.0.1.
                    packet["previewJpeg"] = preview_jpeg(frame)
                    # Keep legacy primary-hand fields for Unity, while retaining
                    # every detected hand in recordings for two-handed SASL signs.
                    packet["hands"] = packet_hands(result.multi_hand_landmarks, labels)
                    payload = json.dumps(packet, separators=(",", ":"))
                    sender.sendto(payload.encode("utf-8"), (args.host, args.port))
                    if recording:
                        recording.write(payload + "\n")
                        recording.flush()
                    drawing.draw_landmarks(frame, landmarks, hands_api.HAND_CONNECTIONS)
                    cv2.putText(frame, f"{recognized.replace('_', ' ')}  {confidence:.0%}",
                                (20, 76), cv2.FONT_HERSHEY_SIMPLEX, .65, (255, 255, 255), 2)
                else:
                    tracking_lost(frame)

                if not args.no_preview:
                    cv2.putText(frame, "BC Sign VR - press Q to close camera", (20, 38),
                                cv2.FONT_HERSHEY_SIMPLEX, .8, (55, 230, 180), 2)
                    cv2.imshow("BC Sign VR Webcam Hands", frame)
                    if cv2.waitKey(1) & 0xFF in (ord("q"), 27):
                        break
    finally:
        if recording:
            recording.close()
        camera.release()
        cv2.destroyAllWindows()
        sender.close()


if __name__ == "__main__":
    main()

