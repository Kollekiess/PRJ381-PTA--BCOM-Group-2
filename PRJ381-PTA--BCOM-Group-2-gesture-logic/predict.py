"""
predict.py  -  live sign recognition using your HandTracker + the trained model.

Usage (from the project root):
    python predict.py

Q or ESC to quit. Shows "?" when the model isn't confident, and smooths the
answer over the last few frames so the letter doesn't flicker.
"""

import json
import os
from collections import Counter, deque

import cv2
import numpy as np
import tensorflow as tf

from ml.features import normalize_landmarks
from tracking.hand_tracker import HandTracker

MODEL_FILE = os.path.join("models", "sasl_gesture_model.keras")
LABELS_FILE = os.path.join("models", "sasl_labels.json")
CONFIDENCE_THRESHOLD = 0.6
SMOOTHING_FRAMES = 7


def main():
    if not os.path.isfile(MODEL_FILE):
        print(f"{MODEL_FILE} not found. Run train.py first.")
        return
    model = tf.keras.models.load_model(MODEL_FILE)
    with open(LABELS_FILE) as f:
        letters = json.load(f)

    tracker = HandTracker()
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("Could not open webcam.")
        return

    recent = deque(maxlen=SMOOTHING_FRAMES)
    timestamp = 0

    while True:
        ok, frame = cap.read()
        if not ok:
            break
        frame = cv2.flip(frame, 1)
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        poses = tracker.detect(rgb, timestamp)
        timestamp += 33

        h, w, _ = frame.shape
        text, conf_text = "No hand detected", ""

        if poses:
            pose = poses[0]
            for lm in pose.landmarks:
                cv2.circle(frame, (int(lm.x * w), int(lm.y * h)), 4, (0, 255, 0), -1)

            feats = normalize_landmarks(pose.flattened_landmarks()).reshape(1, -1)
            probs = model(feats.astype("float32"), training=False).numpy()[0]
            best = int(np.argmax(probs))
            conf = probs[best]
            recent.append(letters[best] if conf >= CONFIDENCE_THRESHOLD else "?")
            text = f"Predicted: {Counter(recent).most_common(1)[0][0]}"
            conf_text = f"Confidence: {conf:.0%}"
        else:
            recent.clear()

        cv2.putText(frame, text, (10, 40), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (0, 255, 0), 2)
        cv2.putText(frame, conf_text, (10, 75), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 0), 2)
        cv2.imshow("BCSignVR - live", frame)

        if cv2.waitKey(1) & 0xFF in (ord("q"), 27):
            break

    cap.release()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
