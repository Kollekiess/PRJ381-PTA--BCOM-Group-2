# SASL gesture dataset and training

The recognizer uses normalized 3D hand-joint vectors rather than camera pixels. This matches the joint data exposed by Quest/OpenXR and avoids storing identifiable hand images. The webcam bridge records up to two hands; the current Unity lesson displays the right hand while recordings retain both hands for future two-handed SASL lessons.

The checked-in `team_sasl/` templates are the team's A–Z research baseline. They are used conservatively by the webcam bridge and are identified as `TEAM_SASL_*`; see `team_sasl/SOURCE.md` for provenance and limitations.

Each JSON Lines sample must contain:

```json
{"label":"REVIEWED_SASL_A","signer":"anonymous-001","reviewedBy":"SASL expert name or reference","source":"consented capture","joints":[[0,0,0],[...]]}
```

`joints` must contain the same ordered joints used by Unity's `HandPoseFrame`: wrist, palm, and each finger's metacarpal/proximal/intermediate/distal/tip joints. Capture multiple signers, dominant hands, hand sizes, and comfortable pose variations. Keep signer identities outside the repository.

Run `python3 train_pose_model.py samples.jsonl model.json` to create a nearest-centroid model. The trainer rejects unreviewed samples and labels that are not explicitly prefixed `REVIEWED_SASL_`.

To create reviewed rows from a consented webcam recording, first capture landmarks without images:

```sh
.venv/bin/python webcam/hand_bridge.py --no-preview --record recordings/anon-001-a.jsonl
```

Have a fluent SASL reviewer verify the recording, then label it explicitly:

```sh
python3 ml/prepare_reviewed_samples.py recordings/anon-001-a.jsonl samples/anon-001-a.jsonl \
  --label REVIEWED_SASL_A --signer anonymous-001 \
  --reviewed-by "reviewer-id" --source "consented capture; review form 2026-01"
```

Repeat across signers and variants, combine the reviewed JSONL files, and run the trainer. Do not label a recording from the English letter name alone; the label is an assertion that a SASL reviewer approved the actual handshape and source.

