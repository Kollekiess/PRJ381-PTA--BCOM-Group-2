# BCSignVR Standalone

Independent, from-scratch rescue prototype for a Meta Quest 3 South African Sign Language learning experience. This repository does not modify or depend on the group repository.

## What works

- A generated low-poly classroom with an instructor, lesson board, feedback panel, and progress display.
- Meta Quest 3-compatible OpenXR hand-joint input through Unity XR Hands.
- Explicit demo mode with deterministic gesture poses for testing the lesson loop without a headset.
- Live Mac webcam tracking via MediaPipe and a localhost-only hand-landmark bridge.
- One-at-a-time SASL alphabet reference lessons and a beginner greeting preview, with local practice records.
- Hand-pose normalization and confidence scoring that is independent of hand size and world position.
- A local API with password hashing, signed sessions, role checks, lessons, progress history, and SQLite persistence.
- A capture-and-train pipeline for reviewed SASL hand-pose samples.

## Important language-model status

The software pipeline is functional, but no unverified American Sign Language data is presented as SASL. The included `DEMO_OPEN`, `DEMO_FIST`, and `DEMO_POINT` classes validate the recognition loop only. Before calling a lesson pedagogical SASL, a SASL subject-matter expert must review the source demonstrations and the team must capture consented SASL samples using the provided pipeline.

The A–Z reference glyphs use the free NID SASL Fingerspelling font, and the greeting reference is cropped from NID's freely downloadable commonly used signs poster. Both are attributed in the lesson. Visual practice is saved separately from automatically scored sessions.

## Run the local backend

Requires Python 3.11+ and no third-party packages.

```bash
cd backend
python3 -m bcsignvr.server
```

The API starts at `http://127.0.0.1:8787`. On first run it creates `backend/data/bcsignvr.sqlite3` and seeds three prototype lessons.

Run its tests with:

```bash
cd backend
python3 -m unittest discover -s tests -v
```

## Run the Unity classroom on this Mac

1. Start the backend.
2. Open the `unity` folder in Unity Hub with Unity `6000.6.5f1`.
3. Open `Assets/Scenes/Classroom.unity`.
4. Press Play.
5. Click the board controls, or use Tab and Enter. Choose your input on the welcome screen. Demo supports `1`, `2`, `3` and Space; Continue advances after completing an exercise.

For the built app, double-click **Open Classroom.command**. It starts the local API and webcam bridge and stops the helper processes it owns when the classroom closes. Enable Camera access for Terminal if macOS requests it. See `docs/webcam-recognition-review.md` for current recognition limitations and the validation plan.

## Enable the Mac webcam

Run `./scripts/setup_webcam.sh` once. After that, `./scripts/run_mac_simulator.sh` starts the camera tracker automatically. Allow Camera access when macOS asks. The board status changes to `LIVE MAC CAMERA HAND` while a hand is visible; no camera frames are uploaded or saved unless recording is explicitly requested.

To diagnose the camera without opening Unity, run:

```bash
.venv/bin/python -c 'import cv2; c=cv2.VideoCapture(0); ok,f=c.read(); print({"camera_open":c.isOpened(),"frame_received":ok,"size":None if f is None else [f.shape[1],f.shape[0]]}); c.release()'
```

The classroom launcher keeps retrying the MediaPipe bridge if the camera temporarily stops delivering frames. In the lesson, `CAMERA / WAITING` means the bridge has not sent a packet yet; `YOUR HAND / NOT VISIBLE` means the camera is connected but no hand is currently detected.

## Run as a simulated Meta Quest 3

Run `./scripts/run_mac_simulator.sh`. It starts the local API when needed, opens Meta XR Simulator, and launches the Mac build against that OpenXR runtime without changing the Mac's global runtime setting.

In Meta XR Simulator choose the Quest 3 device profile and use its input panel to drive both hands. Select **VR hands** on the classroom input screen. There is no automatic fallback to demo when real tracking disappears.

The installed Meta XR Simulator `201.0` supports keyboard/mouse simulated hands. The app's MediaPipe bridge provides webcam hands independently of that version. Meta's own built-in webcam camera tracking requires Meta XR Simulator `207` or newer.

## Build for Quest later

Install Unity's Android Build Support module, switch the build target to Android, enable OpenXR for Android, and make a development APK. A physical Quest 3 is still required for final optical-hand-tracking and performance validation.

## Repository layout

- `unity/` - VR classroom and hand tracking client.
- `backend/` - local API and SQLite database.
- `ml/` - dataset format, capture validation, and lightweight pose-model trainer.
- `webcam/` - MediaPipe camera tracking and the localhost Unity bridge.
- `docs/` - architecture, SASL data ethics, testing, and simulator setup.

