"""
collect.py  -  record training samples for one letter.

Usage (run from the project root, next to main.py):
    python collect.py A

Keys (click the camera window first):
    SPACE  capture one sample
    A      toggle AUTO-capture (a sample every 0.4 s while a hand is visible)
    Q/ESC  quit

Samples are appended to data/landmarks_data.csv, one row per sample:
    label, x0, y0, z0, x1, y1, z1, ... x20, y20, z20
Each row is written and flushed immediately, so nothing is lost if you
close the window or the script crashes.
"""

import csv
import os
import sys
import time

import cv2

from tracking.hand_tracker import HandTracker

DATA_FILE = os.path.join("data", "landmarks_data.csv")
AUTO_INTERVAL = 0.4  # seconds between auto captures


def header_row():
    cols = ["label"]
    for i in range(21):
        cols += [f"x{i}", f"y{i}", f"z{i}"]
    return cols


def main():
    if len(sys.argv) < 2:
        print("Usage: python collect.py <LETTER>   e.g. python collect.py A")
        return
    label = sys.argv[1].upper()

    os.makedirs("data", exist_ok=True)
    needs_header = not os.path.isfile(DATA_FILE) or os.path.getsize(DATA_FILE) == 0

    tracker = HandTracker()
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("Could not open webcam. Is another app using it?")
        return

    csv_file = open(DATA_FILE, "a", newline="")
    writer = csv.writer(csv_file)
    if needs_header:
        writer.writerow(header_row())
        csv_file.flush()

    count = 0
    auto = False
    last_auto = 0.0
    timestamp = 0
    print(f"Recording letter '{label}'. SPACE = capture, A = auto, Q = quit.")

    while True:
        ok, frame = cap.read()
        if not ok:
            break
        frame = cv2.flip(frame, 1)
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        poses = tracker.detect(rgb, timestamp)
        timestamp += 33

        h, w, _ = frame.shape
        pose = poses[0] if poses else None
        if pose:
            for lm in pose.landmarks:
                cv2.circle(frame, (int(lm.x * w), int(lm.y * h)), 4, (0, 255, 0), -1)

        key = cv2.waitKey(1) & 0xFF
        capture = False
        if key == ord(" "):
            capture = True
        elif key == ord("a"):
            auto = not auto
            print("Auto-capture ON" if auto else "Auto-capture OFF")
        elif key in (ord("q"), 27):
            break

        if auto and pose and time.time() - last_auto >= AUTO_INTERVAL:
            capture = True
            last_auto = time.time()

        if capture and pose:
            writer.writerow([label] + pose.flattened_landmarks())
            csv_file.flush()
            count += 1
            print(f"Captured sample {count} for '{label}'")
        elif capture and not pose:
            print("No hand detected - sample skipped")

        status = f"Letter: {label}  Samples: {count}" + ("  [AUTO]" if auto else "")
        cv2.putText(frame, status, (10, 35), cv2.FONT_HERSHEY_SIMPLEX, 0.9, (0, 255, 0), 2)
        cv2.putText(frame, "SPACE = capture, A = auto, Q = quit", (10, 65),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 1)
        cv2.imshow("BCSignVR - collecting", frame)

    csv_file.close()
    cap.release()
    cv2.destroyAllWindows()
    print(f"Done. Saved {count} samples for '{label}' to {DATA_FILE}")


if __name__ == "__main__":
    main()
