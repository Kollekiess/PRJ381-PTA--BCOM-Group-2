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

## Latest verification — 25 October 2026

- Passed: 56 Unity EditMode tests, including scaled-board hand geometry, source-independent cartoon anatomy, the Bob greeting copy, five-second one-shot dwell, controller-trigger/dwell interaction, and delayed network responses during profile switching.
- Passed: 8 backend, 29 webcam bridge, 5 ML, and 11 Quest helper tests. The helper tests cover reboot identity changes, reconnects, inactive apps, and cleanup.
- Built current Mac sources and rendered all 45 smoke-test pages (19 named pages including open/fist/point hand cases plus A–Z). Screenshot production is not proof that every physical controller/hand interaction is correct.
- Built Windows x64 and checked both the executable and UnityPlayer.dll are x86-64, not ARM64. Windows runtime testing is still required on a Windows PC.
- Built and installed the updated Quest APK without clearing learner data.
- Observed the previous app remain awake and render on the connected Quest after the off-face override. Installed a Mac login helper to reapply it after reboot/reconnect while signzzZ is resumed.
- Restarted the Quest and confirmed that the helper reconnects automatically. After the normal user unlock, opened signzzZ, observed the automatic override reapply without another manual power command, and captured the rendered profile screen with the display awake. The helper does not bypass the post-restart lock.
- Follow-up anatomy correction: all input sources now use proximal knuckles, not palm metacarpals, for the visible finger bases. The greeting now says “one hand sign at a time.” All 56 Unity tests passed, the updated Mac tracked-hand preview rendered successfully, and the fist/point cases passed in the expanded sweep. Regional reference hands receive a small fit adjustment so narrow Cape Town/Bloemfontein/Pretoria signs remain legible.
- Rebuilt and installed the latest Quest APK after the anatomy correction; the app launched to the learner picker and the headset stayed awake. Rebuilt Windows and verified both `BCSignVR Standalone.exe` and `UnityPlayer.dll` are x86-64.
- Still required: human live-hand/controller checks on the latest Quest build, visual comparison of all moving tracked poses with Bob's artwork, and the SASL expert/signer-held-out language validation above. Automated geometry/template checks do not establish signing accuracy.

