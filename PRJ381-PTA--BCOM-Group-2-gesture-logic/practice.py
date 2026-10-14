"""
practice.py  -  a mini lesson: the app names a letter, you sign it, it checks you.

Usage (from the project root):
    python practice.py              # 10 random letters from everything trained
    python practice.py A B C D E    # practise only these letters

How it works:
  - A target letter is shown. Sign it and HOLD it for about 1 second.
  - Correct  -> tick, score goes up, next letter.
  - Wrong    -> it tells you what it saw and which finger to fix.
  - 20 seconds with no success -> the letter is skipped.
At the end the session is saved to data/sessions.csv (accuracy, mistakes,
time), which matches the 'Session Performance' entity in the project plan.

Keys:  S = skip letter,  Q / ESC = finish
"""

import csv
import os
import random
import sys
import time
from datetime import datetime

import cv2

from ml.accumulator import LetterAccumulator
from ml.feedback import FeedbackEngine
from tracking.hand_tracker import HandTracker

ROUNDS = 10
ROUND_TIMEOUT = 20
SESSIONS_FILE = os.path.join("data", "sessions.csv")


def save_session(rounds_done, correct, skipped, mistakes, seconds):
    os.makedirs("data", exist_ok=True)
    new = not os.path.isfile(SESSIONS_FILE) or os.path.getsize(SESSIONS_FILE) == 0
    with open(SESSIONS_FILE, "a", newline="") as f:
        w = csv.writer(f)
        if new:
            w.writerow(["date", "rounds", "correct", "skipped", "mistakes",
                        "accuracy_percent", "duration_seconds"])
        acc = round(100 * correct / rounds_done, 1) if rounds_done else 0
        w.writerow([datetime.now().strftime("%Y-%m-%d %H:%M"), rounds_done, correct,
                    skipped, mistakes, acc, round(seconds)])


def main():
    engine = FeedbackEngine()
    pool = [a.upper() for a in sys.argv[1:]] or engine.labels
    pool = [a for a in pool if a in engine.labels]
    if not pool:
        print("None of those letters are in the trained model.")
        return

    tracker = HandTracker()
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("Could not open webcam.")
        return

    def new_target(previous=None):
        choices = [a for a in pool if a != previous] or pool
        return random.choice(choices)

    acc = LetterAccumulator(hold_seconds=1.0, min_conf=engine.min_confidence)
    target = new_target()
    round_start = session_start = time.time()
    rounds_done = correct = skipped = mistakes = 0
    flash_text, flash_color, flash_until = "", (255, 255, 255), 0.0
    timestamp = 0
    finished = False

    def next_round():
        nonlocal target, round_start, rounds_done, acc
        rounds_done += 1
        target = new_target(target)
        round_start = time.time()
        acc = LetterAccumulator(hold_seconds=1.0, min_conf=engine.min_confidence)

    while True:
        ok, frame = cap.read()
        if not ok:
            break
        frame = cv2.flip(frame, 1)
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        poses = tracker.detect(rgb, timestamp)
        timestamp += 33
        h, w, _ = frame.shape
        now = time.time()

        if not finished:
            result, letter, conf = None, None, 0.0
            if poses:
                pose = poses[0]
                for lm in pose.landmarks:
                    cv2.circle(frame, (int(lm.x * w), int(lm.y * h)), 4, (0, 255, 0), -1)
                result = engine.evaluate(target, pose.flattened_landmarks())
                letter, conf = result.predicted, result.confidence

            accepted, progress = acc.update(letter, conf, now)
            if accepted == target:
                correct += 1
                flash_text, flash_color, flash_until = "Correct!", (0, 255, 0), now + 1.2
                next_round()
            elif accepted:
                mistakes += 1
                flash_text, flash_color, flash_until = result.message, (0, 165, 255), now + 2.5
            elif now - round_start > ROUND_TIMEOUT:
                skipped += 1
                flash_text, flash_color, flash_until = f"Skipped {target}", (0, 0, 255), now + 1.5
                next_round()

            key = cv2.waitKey(1) & 0xFF
            if key in (ord("q"), 27):
                finished = True
            elif key == ord("s"):
                skipped += 1
                next_round()

            if rounds_done >= ROUNDS:
                finished = True

            # --- drawing ---
            cv2.putText(frame, f"Sign the letter:  {target}", (10, 50),
                        cv2.FONT_HERSHEY_SIMPLEX, 1.4, (0, 255, 255), 3)
            cv2.putText(frame, f"Round {min(rounds_done + 1, ROUNDS)}/{ROUNDS}   "
                               f"Correct: {correct}   Mistakes: {mistakes}", (10, 85),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 1)
            cv2.rectangle(frame, (10, 95), (210, 109), (255, 255, 255), 1)
            cv2.rectangle(frame, (10, 95), (10 + int(200 * progress), 109), (0, 255, 0), -1)
            if result and not result.correct:
                cv2.putText(frame, result.hint, (10, h - 20),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)
            if now < flash_until:
                cv2.putText(frame, flash_text, (10, h - 60),
                            cv2.FONT_HERSHEY_SIMPLEX, 1.0, flash_color, 3)
        else:
            seconds = time.time() - session_start
            pct = round(100 * correct / rounds_done) if rounds_done else 0
            frame[:] = (30, 30, 30)
            lines = ["Session finished", f"Correct: {correct} / {rounds_done}  ({pct}%)",
                     f"Mistakes: {mistakes}   Skipped: {skipped}",
                     f"Time: {int(seconds)} s", "", "Press any key to close"]
            for i, line in enumerate(lines):
                cv2.putText(frame, line, (30, 80 + i * 45),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.9, (255, 255, 255), 2)
            if cv2.waitKey(1) & 0xFF != 255:
                break

        cv2.imshow("BCSignVR - practice", frame)

    cap.release()
    cv2.destroyAllWindows()
    if rounds_done:
        save_session(rounds_done, correct, skipped, mistakes, time.time() - session_start)
        print(f"Session saved to {SESSIONS_FILE}")


if __name__ == "__main__":
    main()
