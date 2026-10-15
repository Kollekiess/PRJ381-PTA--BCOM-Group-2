"""
train.py  -  train the TensorFlow/Keras sign classifier.

Usage (from the project root):
    python train.py

Reads : data/landmarks_data.csv
Saves : models/sasl_gesture_model.keras
        models/sasl_labels.json

The LAST 20% of each letter's samples are held out for testing (back-to-back
captures are near-duplicates, so a random split would overstate accuracy).
"""

import json
import os

import numpy as np
import pandas as pd
import tensorflow as tf
from sklearn.metrics import classification_report, confusion_matrix
from tensorflow import keras

from ml.features import normalize_landmarks

DATA_FILE = os.path.join("data", "landmarks_data.csv")
MODEL_FILE = os.path.join("models", "sasl_gesture_model.keras")
LABELS_FILE = os.path.join("models", "sasl_labels.json")
TEST_FRACTION = 0.2
EPOCHS = 60

tf.random.set_seed(42)
np.random.seed(42)


def build_model(num_classes):
    return keras.Sequential([
        keras.layers.Input(shape=(63,)),
        keras.layers.Dense(128, activation="relu"),
        keras.layers.Dropout(0.3),
        keras.layers.Dense(64, activation="relu"),
        keras.layers.Dropout(0.3),
        keras.layers.Dense(num_classes, activation="softmax"),
    ])


def main():
    if not os.path.isfile(DATA_FILE):
        print(f"{DATA_FILE} not found. Run collect.py first (or move your CSV into data/).")
        return
    df = pd.read_csv(DATA_FILE)
    if df.empty:
        print("No samples yet.")
        return
    print(f"Loaded {len(df)} samples: {df['label'].value_counts().sort_index().to_dict()}")

    letters = sorted(df["label"].unique())
    to_id = {l: i for i, l in enumerate(letters)}
    y = np.array([to_id[l] for l in df["label"]])
    X = np.array([normalize_landmarks(r) for r in df.drop("label", axis=1).values])

    train_idx, test_idx = [], []
    for cls in range(len(letters)):
        idx = np.where(y == cls)[0]
        cut = int(len(idx) * (1 - TEST_FRACTION))
        train_idx.extend(idx[:cut])
        test_idx.extend(idx[cut:])
    X_train, y_train = X[train_idx], y[train_idx]
    X_test, y_test = X[test_idx], y[test_idx]

    model = build_model(len(letters))
    model.compile(optimizer=keras.optimizers.Adam(1e-3),
                  loss="sparse_categorical_crossentropy", metrics=["accuracy"])
    print(f"\nTraining on {len(X_train)}, testing on {len(X_test)}...\n")
    model.fit(X_train, y_train, validation_data=(X_test, y_test),
              epochs=EPOCHS, batch_size=32, verbose=2,
              callbacks=[keras.callbacks.EarlyStopping(
                  monitor="val_loss", patience=10, restore_best_weights=True)])

    preds = np.argmax(model.predict(X_test, verbose=0), axis=1)
    print(f"\nTest accuracy: {np.mean(preds == y_test):.2%}\n")
    print(classification_report(y_test, preds, target_names=letters, zero_division=0))
    print("Confusion matrix (rows = true, columns = predicted):")
    print(pd.DataFrame(confusion_matrix(y_test, preds, labels=range(len(letters))),
                       index=letters, columns=letters))

    os.makedirs("models", exist_ok=True)
    model.save(MODEL_FILE)
    with open(LABELS_FILE, "w") as f:
        json.dump(letters, f)
    print(f"\nSaved {MODEL_FILE} and {LABELS_FILE}")


if __name__ == "__main__":
    main()
