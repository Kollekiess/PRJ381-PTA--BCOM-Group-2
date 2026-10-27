# Laptop webcam letter bridge

This bridge runs the group's trained static-letter model from PR #6 using NumPy. MediaPipe tracks one hand on the laptop webcam; the model predicts a letter from A to Z. Unity polls the prediction and confidence. The camera image remains on the laptop, and only landmarks and classifier results are sent to Unity.

The model asset and labels are from [PR #6](https://github.com/Kollekiess/PRJ381-PTA--BCOM-Group-2/pull/6). The model has not been independently validated here for accuracy or SASL instruction. It recognizes static letters only; it does not recognize words, movement, facial expression, or two-handed signs. Confirm its dataset labels and lesson examples with a SASL competent source.

## Set up and run on the Mac

On this Apple Silicon Mac, use Homebrew Python 3.12 (the Xcode-provided Python 3.9 runtime does not initialize this MediaPipe build reliably). From the project root, install the local Python environment once:

```sh
/opt/homebrew/bin/python3.12 -m venv HandTracking/.venv-mac
HandTracking/.venv-mac/bin/python -m pip install --no-deps mediapipe==0.10.21
HandTracking/.venv-mac/bin/python -m pip install -r HandTracking/requirements.txt
```

Start the bridge on the Mac. The default binds only to localhost:

```sh
HandTracking/.venv-mac/bin/python HandTracking/webcam_bridge.py
```

Allow camera access if macOS asks. If camera access is denied, run the bridge from Terminal and allow Terminal under **System Settings → Privacy & Security → Camera**. The preview shows the detected hand, current prediction, confidence and hold progress. The terminal prints a temporary access token.

On the Mac, open `http://127.0.0.1:8765/`, enter the temporary token, enter a target letter from A to Z and choose **Set lesson target**. Hold the sign steady until the progress reaches 100%. The page and Quest app show the recognized letter and feedback.

The prepared Quest 2 APK uses localhost. Connect the headset to the Mac with USB, enable Quest Developer Mode, accept the headset's USB debugging prompt, and forward the bridge port:

```sh
/Applications/Unity/Hub/Editor/6000.6.5f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb reverse tcp:8765 tcp:8765
```

The app retrieves its temporary token only over this local forwarded connection. Install the APK with `adb install -r Builds/BCSignVR-Quest2.apk` if it is not already installed. Set the lesson letter in the Mac browser page as described above. Stop the bridge with **Q** or **Esc** in the preview when finished. The bridge does not upload video or landmark data to a cloud service.

## Connect the Unity app

For a wireless demonstration, use a trusted private network and rebuild the APK after setting **Bridge Base Url** to `http://<laptop-ip>:8765` and **Bridge Token** to the token printed by the bridge. Run the bridge with `--host 0.0.0.0`. The token bootstrap endpoint refuses requests from non-local addresses.

The app manifest enables internet access and local cleartext HTTP. The Unity Android build targets Quest 2 (ARM64); Quest 3 hardware is not required. The headset uses OpenXR head pose and controllers; hand detection runs on the Mac webcam.

## Current limits

- The model is an existing group artifact for static letters A–Z. Accuracy and SASL linguistic validity are not independently verified by this bridge.
- Lesson selection is a single letter entered on the laptop page; the bridge does not yet load lessons or save scores to the backend.
- Dynamic signs, words, facial expression, body position and two-handed signs are not supported.
- The Unity project has not yet completed a Quest Android build or been tested on the physical Quest 2.
