# Test plan

## Automated

- Registration rejects weak input and hashes passwords.
- Login issues a signed, expiring token.
- Unauthenticated progress access is rejected.
- A completed lesson can be saved and retrieved.
- Pose normalization removes world translation and hand-size scale.
- Exact template matches score near 100%; clearly different poses score lower.

## Simulator acceptance

1. Meta XR Simulator reports the Quest 3 profile.
2. Unity enters Play mode without errors.
3. Head movement updates the camera.
4. Left and right hand joints become visible.
5. Open hand, fist, and point warm-ups produce distinct confidence scores.
6. Passing a warm-up saves a result and updates the progress board.
7. Stopping the backend does not freeze the VR scene; it reports local-only mode.

## Physical-device exit criteria

- Android APK installs and starts on Quest 3.
- Hand tracking reacquires cleanly after hands leave the field of view.
- Frame rate remains comfortable in the classroom.
- All text is legible at the intended viewing distance.
- At least one SASL expert reviews every teaching demonstration.
- Model evaluation holds out entire signers and documents per-class results.

## Latest verification — 26 October 2026

- Passed: 58 Unity EditMode tests, including scaled-board hand geometry, source-independent cartoon anatomy, the Bob greeting copy, five-second one-shot dwell, controller-trigger/dwell interaction, headset-gaze menu fallback, the 80% alphabet auto-advance gate, and delayed network responses during profile switching.
- Passed: 8 backend, 29 webcam bridge, 5 ML, and 11 Quest helper tests. The helper tests cover reboot identity changes, reconnects, inactive apps, and cleanup.
- Built current Mac sources and rendered all 46 smoke-test pages (20 named pages including the startup greeting, open/fist/point hand cases plus A–Z). Screenshot production is not proof that every physical controller/hand interaction is correct.
- Built Windows x64 and checked both the executable and UnityPlayer.dll are x86-64, not ARM64. Windows runtime testing is still required on a Windows PC.
- Built and installed the updated Quest APK without clearing learner data.
- Observed the previous app remain awake and render on the connected Quest after the off-face override. Installed a Mac login helper to reapply it after reboot/reconnect while signzzZ is resumed.
- Restarted the Quest and confirmed that the helper reconnects automatically. After the normal user unlock, opened signzzZ, observed the automatic override reapply without another manual power command, and captured the rendered profile screen with the display awake. The helper does not bypass the post-restart lock.
- Follow-up interaction correction: menu/profile pages restore a headset-gaze selector when controllers or hands are unavailable, while active signing pages keep the cursor hidden unless a tracked hand/controller is intentionally used. Confident alphabet matches at 80% or higher save once and open a five-second next-sign countdown. The rendered smoke sweep now includes the success overlay, for 47 UI pages total.
- Rebuilt and installed the latest Quest APK after the interaction correction; the app launched to the classroom and the headset stayed awake. Rebuilt Windows and verified both `BCSignVR Standalone.exe` and `UnityPlayer.dll` are x86-64.
- Latest pointer correction: the Quest build shows no idle gaze cursor and never starts dwell selection without a tracked hand or controller. A connected-headset stereo capture confirmed the learner picker renders cleanly with no stray cursor.
- Still required: human live-hand/controller checks on the latest Quest build, visual comparison of all moving tracked poses with Bob's artwork, and the SASL expert/signer-held-out language validation above. Automated geometry/template checks do not establish signing accuracy.

