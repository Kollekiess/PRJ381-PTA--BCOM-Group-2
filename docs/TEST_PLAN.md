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

