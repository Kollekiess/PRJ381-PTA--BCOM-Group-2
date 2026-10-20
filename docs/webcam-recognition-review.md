# Webcam recognition review — 20 October 2026

## Current implementation

The local Python bridge uses pinned MediaPipe Hands to extract 21 landmarks from a Mac camera. It sends them to Unity on loopback UDP 5055 without uploading images. Unity maps these into 26 joints and draws the learner's hand next to the example.

Warm-ups check finger bending and extension independently of hand size, position and rotation. Feedback identifies the finger furthest from the exercise target. These checks cover open hand, fist and pointing, not SASL vocabulary or nonmanual expression.

SASL exploration plays extracted reference sequences for Cape Town, Bloemfontein and Pretoria. The recognizer compares rolling sequences with six training clips. Weak or ambiguous matches return unknown. Tracking loss resets memory; Unity rejects packets older than 450 ms. Similarity is not a probability of correct signing. Research exercises do not award proficiency.

## Research and decisions

- The [National Institute for the Deaf](https://nid.org.za/resources/) publishes free SASL alphabet/counting and commonly used sign resources. Its free fingerspelling font supplies the in-class A–Z reference glyphs; the greeting card comes from its free poster. The app displays attribution beside these references.
- The [Department of Basic Education Foundation Phase SASL curriculum](https://www.education.gov.za/Portals/0/Documents/Publications/SASignLanguage%20HL%20FP%20Gr%20R-3.pdf) defines five distinguishing parameters: handshape, location, movement, palm orientation and non-manual features. Webcam validation therefore cannot be reduced to a single still handshape score.
- [Google Hand Landmarker](https://developers.google.com/edge/mediapipe/solutions/vision/hand_landmarker/python) provides image coordinates, estimated world landmarks and handedness. Video/live modes require timestamps and use tracking to reduce latency. The pinned legacy implementation remains compatible with the extracted dataset. A migration must regenerate and revalidate the features.
- [Google Gesture Recognizer](https://developers.google.com/edge/mediapipe/solutions/vision/gesture_recognizer) detects generic gestures, not a SASL vocabulary. Movement-based signs need temporal recognition.
- [UFS source dataset, version 2](https://doi.org/10.38140/ufs.29376494.v2), CC BY-NC-SA 4.0. Attribution is displayed in the collection. Six clips cannot establish reliable generalisation.

## Required before validated SASL teaching

1. A fluent SASL reviewer must approve full demonstrations, regional variants and teaching cues. Full signer footage is needed for body location, two-handed signs and facial expression.
2. Capture consented recordings across multiple signers and conditions. Preserve hand identity and support both hands plus pose/face cues where required. The current bridge tracks one hand.
3. Use identical feature extraction during training and inference. Correct image aspect ratio or use world landmarks for geometry; preserve body-relative position and orientation. The existing image-coordinate baseline is camera-dependent.
4. Segment by timestamps and handle different signing speeds. The current fixed-frame rolling comparison is speed-sensitive.
5. Split evaluation by signer and recording session. Include unknown gestures, idle hands, occlusion and lighting changes. Report per-sign precision/recall, false acceptance and latency; calibrate thresholds on held-out data.
6. Award success only after reviewed shape, movement, location and orientation checks. Test with humans on the Mac camera and a physical Quest.

## Lesson path now represented in the app

1. A–Z fingerspelling, presented one letter at a time using the NID SASL reference font. The camera must see a hand before practice is recorded. These are visual-practice records and do not claim an automatic correctness score.
2. Beginner greeting movement, using the NID commonly used signs reference and live webcam hand display. It is also stored as visual practice.
3. Tracked hand warm-ups, which are automatically scored for open hand, fist and pointing to validate the camera/feedback loop. They are explicitly described as tracking basics rather than SASL words.
4. UFS place-name research examples. They demonstrate recorded movement sequences and conservative similarity rejection, but do not award proficiency.

The next teaching content should cover introductions and conversational questions such as HOW and YOU only after complete, fluent-SASL demonstrations are licensed and reviewed. The project deliberately does not construct an English phrase by guessing at isolated poster images.

The lecturer is original procedural geometry inspired by cartoon boss proportions. Nunito is bundled under its included SIL Open Font License. Real input never silently falls back to demo; demo results remain separate.
