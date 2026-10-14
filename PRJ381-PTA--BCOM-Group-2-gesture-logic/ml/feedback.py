"""
ml/feedback.py  -  the FEEDBACK ENGINE (plan deliverable FR-6 / FR-7).

Given a TARGET sign and the learner's hand landmarks, it returns:
    - correct / not correct
    - what the model thought it saw, and how confident it is
    - a hint about which finger to fix

It compares the learner's hand with:
    1. the trained TensorFlow model (is it recognised as the target?), and
    2. a stored reference TEMPLATE per letter (the average hand shape of all
       your recorded samples) to work out which finger is off.

Templates are saved to models/sasl_templates.json and rebuilt automatically
whenever data/landmarks_data.csv changes. They are the "stored SASL patterns"
(they can fill the Gesture entity's HandLandmarks field in the database).
"""

import json
import os
from dataclasses import dataclass

import numpy as np
import pandas as pd
import tensorflow as tf

from ml.features import normalize_landmarks

MODEL_FILE = os.path.join("models", "sasl_gesture_model.keras")
LABELS_FILE = os.path.join("models", "sasl_labels.json")
TEMPLATE_FILE = os.path.join("models", "sasl_templates.json")
DATA_FILE = os.path.join("data", "landmarks_data.csv")

# landmark indices of each finger, base -> tip
FINGERS = {
    "thumb": (1, 2, 3, 4),
    "index": (5, 6, 7, 8),
    "middle": (9, 10, 11, 12),
    "ring": (13, 14, 15, 16),
    "pinky": (17, 18, 19, 20),
}
CLOSE_ENOUGH = 0.2   # finger position error (in hand-size units) below which we don't nag


@dataclass
class FeedbackResult:
    target: str
    predicted: str
    confidence: float      # model confidence in what it predicted (0..1)
    target_score: float    # probability the model gives the TARGET letter (0..1)
    correct: bool
    message: str           # short text to show the learner
    hint: str              # which finger to fix ("" when correct)


class FeedbackEngine:
    def __init__(self, min_confidence=0.7):
        self.min_confidence = min_confidence
        self.model = tf.keras.models.load_model(MODEL_FILE)
        with open(LABELS_FILE) as f:
            self.labels = json.load(f)
        self.templates = self._load_or_build_templates()

    # ---------- templates ----------
    def _load_or_build_templates(self):
        fresh = (os.path.isfile(TEMPLATE_FILE) and
                 (not os.path.isfile(DATA_FILE) or
                  os.path.getmtime(TEMPLATE_FILE) >= os.path.getmtime(DATA_FILE)))
        if fresh:
            with open(TEMPLATE_FILE) as f:
                return {k: np.array(v).reshape(21, 3) for k, v in json.load(f).items()}

        df = pd.read_csv(DATA_FILE)
        templates = {}
        for letter, group in df.groupby("label"):
            feats = np.array([normalize_landmarks(r) for r in group.drop("label", axis=1).values])
            templates[letter] = feats.mean(axis=0).reshape(21, 3)
        os.makedirs("models", exist_ok=True)
        with open(TEMPLATE_FILE, "w") as f:
            json.dump({k: v.flatten().tolist() for k, v in templates.items()}, f)
        return templates

    # ---------- hint ----------
    def _hint(self, target, feats):
        template = self.templates.get(target)
        if template is None:
            return ""
        pts = feats.reshape(21, 3)
        worst, worst_err = None, 0.0
        for name, joints in FINGERS.items():
            err = float(np.mean([np.linalg.norm(pts[j, :2] - template[j, :2]) for j in joints]))
            if err > worst_err:
                worst, worst_err = name, err
        if worst is None or worst_err < CLOSE_ENOUGH:
            return "Close! Hold your hand steady and match the picture."

        tip = FINGERS[worst][-1]
        mine = np.linalg.norm(pts[tip, :2])          # fingertip distance from wrist
        theirs = np.linalg.norm(template[tip, :2])
        ratio = mine / theirs if theirs > 1e-6 else 1.0
        part = "thumb" if worst == "thumb" else f"{worst} finger"
        if ratio < 0.85:
            return f"Straighten your {part} more."
        if ratio > 1.15:
            return f"Curl your {part} more."
        return f"Move your {part} closer to the target position."

    # ---------- main call ----------
    def evaluate(self, target, flat_landmarks):
        """target: letter the learner should sign. flat_landmarks: 63 numbers
        from HandPose.flattened_landmarks()."""
        target = target.upper()
        feats = normalize_landmarks(flat_landmarks)
        probs = self.model(feats.reshape(1, -1).astype("float32"), training=False).numpy()[0]
        best = int(np.argmax(probs))
        predicted, confidence = self.labels[best], float(probs[best])
        target_score = float(probs[self.labels.index(target)]) if target in self.labels else 0.0

        correct = predicted == target and confidence >= self.min_confidence
        if correct:
            return FeedbackResult(target, predicted, confidence, target_score, True, "Correct!", "")
        hint = self._hint(target, feats)
        if predicted != target and confidence >= self.min_confidence:
            message = f"That looks like {predicted}, not {target}."
        else:
            message = f"Not quite {target} yet."
        return FeedbackResult(target, predicted, confidence, target_score, False, message, hint)
