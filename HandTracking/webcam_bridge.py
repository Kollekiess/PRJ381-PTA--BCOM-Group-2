#!/usr/bin/env python3
"""Local webcam classifier bridge for the BCSignVR Unity prototype.

Raw camera frames stay in this process. Unity receives the trained letter
classifier's result and confidence, plus hand landmarks for debugging.
"""

from __future__ import annotations

import argparse
import http.server
import ipaddress
import json
import math
import secrets
import socket
import threading
import time
from pathlib import Path

import numpy as np

import cv2
import mediapipe as mp


PORT = 8765
MIN_CONFIDENCE = 0.7
HOLD_SECONDS = 0.8
MODEL_DIR = Path(__file__).resolve().parent / "models"


class LetterClassifier:
    """Run the group's saved 63-128-64-26 dense model using NumPy."""

    def __init__(self) -> None:
        with np.load(MODEL_DIR / "sasl_gesture_weights.npz") as weights:
            self.weights = [
                (weights[f"dense_{i}_kernel"], weights[f"dense_{i}_bias"])
                for i in range(3)
            ]
        self.labels = json.loads((MODEL_DIR / "sasl_labels.json").read_text())

    def predict(self, hand_landmarks) -> tuple[str, float]:
        points = np.asarray(
            [(point.x, point.y, point.z) for point in hand_landmarks.landmark],
            dtype=np.float32,
        )
        points -= points[0]
        scale = float(np.linalg.norm(points[9, :2]))
        if scale < 1e-6:
            scale = 1.0
        values = (points / scale).reshape(1, 63)
        for index, (kernel, bias) in enumerate(self.weights):
            values = values @ kernel + bias
            if index < 2:
                values = np.maximum(values, 0)
        logits = values[0] - np.max(values[0])
        probabilities = np.exp(logits)
        probabilities /= np.sum(probabilities)
        best = int(np.argmax(probabilities))
        return self.labels[best], float(probabilities[best])


def local_addresses() -> list[str]:
    addresses = set()
    try:
        for info in socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET):
            address = info[4][0]
            if not address.startswith("127."):
                addresses.add(address)
    except OSError:
        pass
    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock.connect(("192.0.2.1", 80))
        addresses.add(sock.getsockname()[0])
        sock.close()
    except OSError:
        pass
    return sorted(addresses)


def normalized_landmarks(hand_landmarks, handedness: str) -> list[float]:
    points = [(point.x, point.y, point.z) for point in hand_landmarks.landmark]
    wrist = points[0]
    span = math.dist(points[5][:2], points[17][:2])
    span = max(span, 1e-5)
    mirror = -1.0 if handedness.lower() == "left" else 1.0
    # Make the palm scale-independent, wrist-centered, and consistent across hands.
    return [
        value
        for x, y, z in points
        for value in (
            mirror * (x - wrist[0]) / span,
            (y - wrist[1]) / span,
            (z - wrist[2]) / span,
        )
    ]


class BridgeState:
    def __init__(self) -> None:
        self.lock = threading.RLock()
        self.latest: dict = {
            "handDetected": False,
            "handedness": "",
            "landmarks": [],
            "targetLabel": "",
            "predictedLabel": "",
            "confidence": 0.0,
            "holdProgress": 0.0,
            "matched": False,
            "feedback": "Show one hand to the webcam",
            "timestamp": 0.0,
        }
        self.target_label = ""
        self.candidate = ""
        self.candidate_since = 0.0
        self.accepted = ""

    def update(self, landmarks: list[float], handedness: str, prediction: str, confidence: float) -> None:
        with self.lock:
            now = time.monotonic()
            if confidence >= MIN_CONFIDENCE:
                if prediction != self.candidate:
                    self.candidate = prediction
                    self.candidate_since = now
                    self.accepted = ""
                elif now - self.candidate_since >= HOLD_SECONDS:
                    self.accepted = prediction
            else:
                self.candidate = ""
                self.candidate_since = 0.0
                self.accepted = ""
            progress = (
                min(1.0, (now - self.candidate_since) / HOLD_SECONDS)
                if self.candidate and confidence >= MIN_CONFIDENCE
                else 0.0
            )
            matched = bool(self.target_label and self.accepted == self.target_label)
            self.latest = {
                "handDetected": True,
                "handedness": handedness,
                "landmarks": landmarks,
                "targetLabel": self.target_label,
                "predictedLabel": prediction,
                "confidence": round(confidence, 3),
                "holdProgress": round(progress, 3),
                "matched": matched,
                "feedback": (
                    "Correct!"
                    if matched
                    else (
                        "Set a target letter on this laptop"
                        if not self.target_label
                        else ("Hold the sign steady" if confidence >= MIN_CONFIDENCE else "Show a clear letter sign")
                    )
                ),
                "timestamp": time.time(),
            }

    def lost(self) -> None:
        with self.lock:
            self.candidate = ""
            self.candidate_since = 0.0
            self.accepted = ""
            current = dict(self.latest)
            current.update({
                "handDetected": False,
                "landmarks": [],
                "confidence": 0.0,
                "holdProgress": 0.0,
                "matched": False,
                "predictedLabel": "",
                "feedback": "Show one hand to the webcam",
                "timestamp": time.time(),
            })
            self.latest = current

    def set_target(self, label: str) -> tuple[bool, str]:
        with self.lock:
            label = label.strip().upper()
            if label not in "ABCDEFGHIJKLMNOPQRSTUVWXYZ" or len(label) != 1:
                return False, "Choose one trained letter from A to Z."
            self.target_label = label
            self.candidate = ""
            self.candidate_since = 0.0
            self.accepted = ""
            current = dict(self.latest)
            current.update({
                "targetLabel": self.target_label,
                "matched": False,
                "feedback": "Show one hand to the webcam" if not current["handDetected"] else "Show the target letter",
            })
            self.latest = current
            return True, f"Target letter set to {self.target_label}."


HTML = r"""<!doctype html>
<html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>BCSignVR Webcam Hand Bridge</title>
<style>
body{font:16px system-ui,sans-serif;max-width:740px;margin:40px auto;padding:0 20px;background:#101820;color:#edf2f7}
main{background:#1e2a35;padding:24px;border-radius:16px}h1{margin-top:0}p{line-height:1.5;color:#c4d0da}
input,button{font:inherit;padding:12px;border-radius:8px;border:1px solid #667784}input{background:#101820;color:#fff;width:min(100%,360px)}button{background:#38bdf8;color:#082331;border:0;font-weight:700;cursor:pointer;margin:8px 4px 8px 0}
code{color:#7dd3fc}.state{font-size:1.2em;font-weight:700}.hint{font-size:.9em}
</style><main>
<h1>BCSignVR hand tracking</h1>
<p>The camera preview stays in its local window. Unity receives letter predictions and confidence from the group's trained model.</p>
<label>Short-lived bridge token<br><input id="token" type="password" autocomplete="off" placeholder="Paste token from the bridge window"></label>
<p><label>Target letter<br><input id="label" maxlength="1" placeholder="A" autocapitalize="characters"></label></p>
<button id="capture">Set lesson target</button>
<p id="message" class="state">Connecting…</p><p id="status"></p>
<p class="hint">The group's classifier recognizes static letters A–Z. It does not recognize words, movement, facial expression, or two-handed signs. Confirm the signs and teaching references with a SASL competent source.</p>
</main><script>
const token=document.querySelector('#token'),label=document.querySelector('#label'),message=document.querySelector('#message'),status=document.querySelector('#status');
token.value=sessionStorage.getItem('bridgeToken')||'';
async function api(path,options={}){return fetch(path,{...options,headers:{'Content-Type':'application/json','X-Bridge-Token':token.value,...options.headers}})}
token.oninput=()=>sessionStorage.setItem('bridgeToken',token.value);
document.querySelector('#capture').onclick=async()=>{try{const r=await api('/api/target',{method:'POST',body:JSON.stringify({label:label.value})});const d=await r.json();message.textContent=d.message||'Could not set target';}catch(e){message.textContent='Bridge token or connection problem';}};
async function refresh(){try{const r=await api('/api/state');if(!r.ok)throw Error();const d=await r.json();message.textContent=d.feedback;status.textContent=d.handDetected?`Target: ${d.targetLabel||'not set'} · Recognized: ${d.predictedLabel||'?'} · Confidence: ${Math.round(d.confidence*100)}% · Hold: ${Math.round(d.holdProgress*100)}%`:'No hand detected';}catch(e){message.textContent='Enter the current bridge token to connect';status.textContent='The bridge is local to this computer.';}setTimeout(refresh,350)}refresh();
</script></html>"""


def make_handler(state: BridgeState, token: str):
    class Handler(http.server.BaseHTTPRequestHandler):
        server_version = "BCSignVRBridge/1.0"

        def log_message(self, fmt, *args):
            return

        def _json(self, status: int, payload: dict) -> None:
            body = json.dumps(payload).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Cache-Control", "no-store")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self):
            if self.path == "/" or self.path.startswith("/?"):
                body = HTML.encode("utf-8")
                self.send_response(200)
                self.send_header("Content-Type", "text/html; charset=utf-8")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
                return
            if self.path == "/healthz":
                self._json(200, {"ok": True})
                return
            if self.path == "/api/bootstrap":
                try:
                    loopback = ipaddress.ip_address(self.client_address[0]).is_loopback
                except ValueError:
                    loopback = False
                if not loopback:
                    self._json(403, {"error": "bootstrap is available only over a local connection"})
                    return
                self._json(200, {"token": token})
                return
            if self.path != "/api/state":
                self._json(404, {"error": "not found"})
                return
            if not secrets.compare_digest(self.headers.get("X-Bridge-Token", ""), token):
                self._json(401, {"error": "invalid bridge token"})
                return
            with state.lock:
                self._json(200, dict(state.latest))

        def do_POST(self):
            if self.path != "/api/target":
                self._json(404, {"error": "not found"})
                return
            if not secrets.compare_digest(self.headers.get("X-Bridge-Token", ""), token):
                self._json(401, {"error": "invalid bridge token"})
                return
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if length > 4096:
                    self._json(413, {"error": "request too large"})
                    return
                payload = json.loads(self.rfile.read(length) or b"{}")
                label = str(payload.get("label", ""))
            except (ValueError, json.JSONDecodeError):
                self._json(400, {"error": "invalid JSON"})
                return
            ok, message = state.set_target(label)
            self._json(200 if ok else 422, {"ok": ok, "message": message})

    return Handler


def run() -> int:
    parser = argparse.ArgumentParser(description="Track a hand locally and share landmarks with BCSignVR.")
    parser.add_argument("--camera", type=int, default=0, help="webcam device index (default: 0)")
    parser.add_argument("--host", default="127.0.0.1", help="bind address; use 0.0.0.0 only on a trusted private network")
    parser.add_argument("--port", type=int, default=PORT)
    parser.add_argument("--token", default="", help="temporary bridge token; generated when omitted")
    args = parser.parse_args()

    token = args.token or secrets.token_urlsafe(18)
    classifier = LetterClassifier()
    state = BridgeState()
    server = http.server.ThreadingHTTPServer((args.host, args.port), make_handler(state, token))
    server.daemon_threads = True
    server_thread = threading.Thread(target=server.serve_forever, daemon=True)
    server_thread.start()

    camera = cv2.VideoCapture(args.camera)
    camera.set(cv2.CAP_PROP_FRAME_WIDTH, 960)
    camera.set(cv2.CAP_PROP_FRAME_HEIGHT, 540)
    if not camera.isOpened():
        print(f"Could not open webcam {args.camera}. Check macOS camera permission and --camera index.")
        server.shutdown()
        return 2

    print("BCSignVR webcam bridge is running. Press Q or Esc in the preview to stop.")
    print(f"Token (keep private; Unity must send it as X-Bridge-Token): {token}")
    print(f"Local lesson page: http://127.0.0.1:{args.port}/")
    if args.host == "0.0.0.0":
        addresses = local_addresses()
        for address in addresses:
            print(f"Quest URL: http://{address}:{args.port}/api/state")
        print("For headset use, connect only over a trusted private hotspot/network; requests use plain HTTP.")

    hands = mp.solutions.hands.Hands(
        static_image_mode=False,
        max_num_hands=1,
        model_complexity=0,
        min_detection_confidence=0.65,
        min_tracking_confidence=0.55,
    )
    drawing = mp.solutions.drawing_utils
    try:
        while True:
            ok, frame = camera.read()
            if not ok:
                state.lost()
                continue
            frame = cv2.flip(frame, 1)
            result = hands.process(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
            if result.multi_hand_landmarks:
                hand = result.multi_hand_landmarks[0]
                handedness = "Right"
                if result.multi_handedness:
                    handedness = result.multi_handedness[0].classification[0].label
                values = normalized_landmarks(hand, handedness)
                prediction, confidence = classifier.predict(hand)
                state.update(values, handedness, prediction, confidence)
                drawing.draw_landmarks(frame, hand, mp.solutions.hands.HAND_CONNECTIONS)
                with state.lock:
                    label = state.latest["targetLabel"] or "Reference not set"
                    feedback = state.latest["feedback"]
                    predicted = state.latest["predictedLabel"]
                    score = state.latest["confidence"]
                    progress = state.latest["holdProgress"]
                cv2.putText(frame, f"Target: {label or '-'}  Seen: {predicted} {score:.0%}", (20, 35), cv2.FONT_HERSHEY_SIMPLEX, .75, (50, 230, 180), 2)
                cv2.putText(frame, f"{feedback}  Hold: {progress:.0%}", (20, 70), cv2.FONT_HERSHEY_SIMPLEX, .6, (255, 255, 255), 2)
            else:
                state.lost()
                cv2.putText(frame, "Show one hand to the webcam", (20, 40), cv2.FONT_HERSHEY_SIMPLEX, .8, (255, 255, 255), 2)
            cv2.imshow("BCSignVR | webcam hand tracking", frame)
            key = cv2.waitKey(1) & 0xFF
            if key in (27, ord("q")):
                break
    except KeyboardInterrupt:
        pass
    finally:
        camera.release()
        hands.close()
        cv2.destroyAllWindows()
        server.shutdown()
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(run())
