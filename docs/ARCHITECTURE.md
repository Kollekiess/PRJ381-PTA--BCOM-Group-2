# Architecture

```text
Meta XR Simulator / Quest 3    Mac webcam / MediaPipe    Desktop fallback
            |                           |                       |
            +------------- ordered hand joints ----------------+
                              |
                       Pose normalizer
                              |
                    Gesture confidence engine
                              |
          Classroom feedback + lesson state machine
                              |
               authenticated session result only
                              |
                  Python API -> SQLite database
```

Recognition runs locally in Unity so network latency cannot disrupt feedback. The backend never receives live hand geometry by default; it stores only lesson, accuracy, duration, mistake count, completion state, and timestamp.

The `IHandPoseProvider` boundary deliberately separates tracking from teaching logic. OpenXR, the webcam UDP bridge, and the desktop simulator produce the same `HandPoseFrame`, so the lesson engine is testable without a headset. Webcam packets stay on `127.0.0.1:5055`.

## Content states

- `prototype`: proves software behavior; not a teaching claim.
- `reviewed`: sign demonstration and label reviewed by a qualified SASL practitioner.
- `validated`: reviewed lesson tested with multiple SASL users and a signer-independent evaluation set.

Only reviewed or validated content should appear in a submission advertised as teaching SASL.

