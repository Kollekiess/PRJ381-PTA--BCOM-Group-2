# PRJ381-PTA--BCOM-Group-2

## BCSignVR Quest 2 demonstration

The Unity project is in `PRJ_VR`. The project plan named Quest 3 in some sections; this demonstration is configured for a **Meta Quest 2** (Android ARM64, OpenXR). Quest 2 uses the same Unity OpenXR path for head tracking and controller input. The learner's sign is tracked by the Mac's webcam, not the headset's hand-tracking cameras.

### Build the installable Android app

1. Open `PRJ_VR` from Unity Hub using Unity **6000.6.5f1** with Android Build Support. Unity's license terms must be reviewed and accepted in Unity Hub before the editor can build.
2. In the Unity editor, choose **BCSignVR → 1. Configure Quest 2 Android XR**. If Unity switches platforms or imports packages, run the menu item again after it finishes.
3. Choose **BCSignVR → 2. Build Quest 2 APK**. The USB setup uses the default localhost bridge URL, so no laptop IP or token needs to be embedded in the build.
4. The APK is written to `Builds/BCSignVR-Quest2.apk`. On the headset, enable Developer Mode and accept the USB debugging prompt. Connect the USB cable, run `adb reverse tcp:8765 tcp:8765`, then install with `adb install -r Builds/BCSignVR-Quest2.apk`.

The Mac webcam bridge setup and its current limits are documented in [`HandTracking/README.md`](HandTracking/README.md). The model and letter labels came from the group's draft [PR #6](https://github.com/Kollekiess/PRJ381-PTA--BCOM-Group-2/pull/6); verify the dataset represents SASL and test recognition with a SASL fluent reviewer before presenting it as an accurate SASL teacher. Word-level recognition, moving signs, facial expression and body position are not implemented in this demo.

### Run the local sign-in and progress demo

The demo in `BackEnd` uses Express and a persistent SQLite database. Passwords are stored as salted scrypt hashes; sign-in returns a short-lived bearer token; lesson attempts and summary progress are saved in `BackEnd/data/bcsignvr.sqlite`.

1. Install Node.js 22.13 or newer.
2. In `BackEnd`, run `npm install` and `npm start`.
3. Open `http://127.0.0.1:3000` and create an account. Practice scores in this demo are sample lesson completions, not scores from the webcam classifier.

Use **View code** in the signed-in learner panel to inspect the live `auth.js` and `server.js` source. This is a local demonstration database. It does not connect to a hosted Supabase project, and the existing Supabase migration remains a separate schema draft. Keep the local SQLite file private and out of version control.
