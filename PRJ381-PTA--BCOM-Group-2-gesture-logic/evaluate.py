"""
evaluate.py  -  produce the EVIDENCE for your report.

Usage (from the project root, after train.py):
    python evaluate.py

Uses the same held-out test samples as train.py (the last 20% of every
letter) and writes to the reports/ folder:
    reports/summary.txt            overall accuracy + per-letter precision/recall
    reports/per_letter.csv         the same per-letter numbers as a table
    reports/confusion_matrix.csv   which letters get mixed up
    reports/confusion_matrix.png   the same as a picture (needs matplotlib)
    reports/most_confused.txt      the letter pairs the model mixes up most
"""

import json
import os

import numpy as np
import pandas as pd
import tensorflow as tf
from sklearn.metrics import accuracy_score, classification_report, confusion_matrix

from ml.features import normalize_landmarks

DATA_FILE = os.path.join("data", "landmarks_data.csv")
MODEL_FILE = os.path.join("models", "sasl_gesture_model.keras")
LABELS_FILE = os.path.join("models", "sasl_labels.json")
TEST_FRACTION = 0.2


def main():
    model = tf.keras.models.load_model(MODEL_FILE)
    with open(LABELS_FILE) as f:
        letters = json.load(f)

    df = pd.read_csv(DATA_FILE)
    to_id = {l: i for i, l in enumerate(letters)}
    y = np.array([to_id[l] for l in df["label"]])
    X = np.array([normalize_landmarks(r) for r in df.drop("label", axis=1).values])

    test_idx = []
    for cls in range(len(letters)):
        idx = np.where(y == cls)[0]
        test_idx.extend(idx[int(len(idx) * (1 - TEST_FRACTION)):])
    X_test, y_test = X[test_idx], y[test_idx]

    preds = np.argmax(model.predict(X_test, verbose=0), axis=1)
    acc = accuracy_score(y_test, preds)

    os.makedirs("reports", exist_ok=True)
    report_txt = classification_report(y_test, preds, target_names=letters, zero_division=0)
    with open(os.path.join("reports", "summary.txt"), "w") as f:
        f.write(f"Samples in dataset: {len(df)}   Test samples: {len(y_test)}\n")
        f.write(f"Overall test accuracy: {acc:.2%}\n\n{report_txt}")

    report = classification_report(y_test, preds, target_names=letters,
                                   zero_division=0, output_dict=True)
    pd.DataFrame(report).T.round(3).to_csv(os.path.join("reports", "per_letter.csv"))

    cm = confusion_matrix(y_test, preds, labels=range(len(letters)))
    pd.DataFrame(cm, index=letters, columns=letters).to_csv(
        os.path.join("reports", "confusion_matrix.csv"))

    pairs = [(cm[i, j], letters[i], letters[j])
             for i in range(len(letters)) for j in range(len(letters))
             if i != j and cm[i, j] > 0]
    pairs.sort(reverse=True)
    with open(os.path.join("reports", "most_confused.txt"), "w") as f:
        f.write("true letter -> predicted letter : times\n")
        for n, t, p in pairs[:15]:
            f.write(f"{t} -> {p} : {n}\n")

    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
        fig, ax = plt.subplots(figsize=(10, 9))
        ax.imshow(cm, cmap="Blues")
        ax.set_xticks(range(len(letters)), letters)
        ax.set_yticks(range(len(letters)), letters)
        ax.set_xlabel("Predicted letter")
        ax.set_ylabel("True letter")
        ax.set_title(f"SASL alphabet confusion matrix (accuracy {acc:.1%})")
        for i in range(len(letters)):
            for j in range(len(letters)):
                if cm[i, j]:
                    ax.text(j, i, cm[i, j], ha="center", va="center",
                            color="white" if cm[i, j] > cm.max() / 2 else "black", fontsize=8)
        fig.tight_layout()
        fig.savefig(os.path.join("reports", "confusion_matrix.png"), dpi=150)
        print("Saved reports/confusion_matrix.png")
    except ImportError:
        print("matplotlib not installed - skipped the picture (pip install matplotlib to get it)")

    print(f"Overall test accuracy: {acc:.2%}")
    print("Reports saved in the reports/ folder.")


if __name__ == "__main__":
    main()
