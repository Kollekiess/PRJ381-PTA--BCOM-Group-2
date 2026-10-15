# Laptop webcam letter bridge

This bridge runs the group's trained static-letter model from PR #6 using NumPy. MediaPipe tracks one hand on the laptop webcam; the model predicts a letter from A to Z. Unity polls the prediction and confidence. The camera image remains on the laptop, and only landmarks and classifier results are sent to Unity.

The model asset and labels are from [PR #6](https://github.com/Kollekiess/PRJ381-PTA--BCOM-Group-2/pull/6). The model has not been independently validated here for accuracy or SASL instruction. It recognizes static letters only; it does not recognize words, movement, facial expression, or two-handed signs. Confirm its dataset labels and lesson examples with a SASL competent source.

## Set up and run on the Mac

From the project root, install the local Python environment once:

```sh
python3 -m venv HandTracking/.venv
HandTracking/.venv/bin/python -m pip install --no-deps mediapipe==0.10.21
HandTracking/.venv/bin/python -m pip install -r HandTracking/requirements.txt
```

Start the bridge on the Mac. The default binds only to localhost:

```sh
HandTracking/.venv/bin/python HandTracking/webcam_bridge.py
```

Allow camera access if macOS asks. If camera access is denied, run the bridge from Terminal and allow Terminal under **System Settings → Privacy & Security → Camera**. The preview shows the detected hand, current prediction, confidence and hold progress. The terminal prints a temporary access token.

On the Mac, open `http://127.0.0.1:8765/`, enter the temporary token, enter a target letter from A to Z and choose **Set lesson target**. Hold the sign steady until the progress reaches 100%. The page and Quest app show the recognized letter and feedback.

The prepared Quest 2 APK uses localhost. Connect the headset to the Mac with USB, enable Quest Developer Mode, and accept the headset's USB debugging prompt. With the APK built in the repository root, install it and forward the bridge port from the Mac:

```sh
/Applications/Unity/Hub/Editor/6000.3.9f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb install -r Builds/BCSignVR-Quest2.apk
/Applications/Unity/Hub/Editor/6000.3.9f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb reverse tcp:8765 tcp:8765
```

The app retrieves its temporary token only over this local forwarded connection. Set the lesson letter in the Mac browser page as described above. Stop the bridge with **Q** or **Esc** in the preview when finished. The bridge does not upload video or landmark data to a cloud service.

## Connect the Unity app

For a wireless demonstration, use a trusted private network and rebuild the APK after setting **Bridge Base Url** to `http://<laptop-ip>:8765` and **Bridge Token** to the token printed by the bridge. Run the bridge with `--host 0.0.0.0`. The token bootstrap endpoint refuses requests from non-local addresses.

The app manifest enables internet access and local cleartext HTTP. The Unity Android build targets Quest 2 (ARM64); Quest 3 hardware is not required. The headset uses OpenXR head pose. The Oculus Touch profile is enabled, but controller-driven gameplay is not implemented; hand detection runs on the Mac webcam.

## Build and verification status

The Quest 2 Android APK has been built successfully with Unity 6.3 LTS (6000.3.9f1). Unity's player build and Android Gradle release build both completed, and the generated APK passed an archive integrity check. It targets Android ARM64 and uses OpenXR. The APK is at `Builds/BCSignVR-Quest2.apk` after building.

The physical Quest 2 is currently at campus and was not available for installation or runtime testing. Treat this as a prepared build, not a headset-verified release. The webcam bridge was tested on the Mac and detected a hand, but the observed letter prediction was low confidence.

## Current limits

- The model is an existing group artifact for static letters A–Z. Accuracy and SASL linguistic validity are not independently verified by this bridge.
- Lesson selection is a single letter entered on the laptop page; the bridge does not yet load lessons or save scores to the backend.
- Dynamic signs, words, facial expression, body position and two-handed signs are not supported.
- The physical Quest 2 has not been used to verify installation, headset tracking, comfort, performance, or the webcam-to-headset connection.
