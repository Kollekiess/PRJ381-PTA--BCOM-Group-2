# PRJ381-PTA--BCOM-Group-2

## BCSignVR Quest 2 demonstration

The Unity project is in `PRJ_VR`. The project plan named Quest 3 in some sections; this demonstration is configured for a **Meta Quest 2** (Android ARM64, OpenXR). The Quest build reads headset pose through OpenXR. The Oculus Touch profile is enabled, but controller-driven game interactions are not implemented. The learner's sign is tracked by the Mac's webcam, not the headset's hand-tracking cameras.

### Build the installable Android app

1. Open `PRJ_VR` from Unity Hub using Unity editor version 6000.3.9f1 with Android Build Support, Android SDK & NDK Tools, and OpenJDK.
2. In the Unity editor, choose **BCSignVR → 1. Configure Quest 2 Android XR**. If Unity switches platforms or imports packages, run the menu item again after it finishes.
3. The default build uses localhost. Build the APK with **BCSignVR → 2. Build Quest 2 APK**. For a USB-connected Quest, start the webcam bridge and run `adb reverse tcp:8765 tcp:8765` from the Mac. The app obtains the temporary token over that forwarded local connection.
4. The APK is written to `Builds/BCSignVR-Quest2.apk`. A Wi-Fi build is also possible; see `HandTracking/README.md` for the LAN configuration.

The Mac webcam bridge setup and its current limits are documented in [`HandTracking/README.md`](HandTracking/README.md). The model and letter labels came from the group's draft [PR #6](https://github.com/Kollekiess/PRJ381-PTA--BCOM-Group-2/pull/6); verify the dataset represents SASL and test recognition with a SASL fluent reviewer before presenting it as an accurate SASL teacher. Words, moving signs, facial expression, body position and score persistence are not implemented in this demo.

The Quest 2 APK has built successfully on the Mac and is prepared for installation. It has not been run on the physical headset, which is currently at campus. The webcam bridge has detected a hand locally; letter recognition accuracy remains unverified.
