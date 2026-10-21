# Reviewed SASL capture checklist

The app can already teach the alphabet and run the webcam lesson loop. A
word-level SASL model must only be enabled after a fluent SASL reviewer has
checked the examples. This checklist keeps the training set traceable and
prevents the demo poses from being mistaken for real SASL labels.

## Before recording

- [ ] Obtain informed consent from each signer and record only a pseudonym in
      the metadata; do not put names, faces, or contact details in the dataset.
- [ ] Agree the exact label and gloss with a fluent SASL reviewer. Record the
      source (for example, a NID reference or an approved teaching resource).
- [ ] Decide whether the lesson is a static handshape (such as a letter) or a
      movement sequence (such as a greeting). Do not mix these in one label.
- [ ] Use a plain, uncluttered background, even front lighting, and a fixed
      webcam position. Keep the signing hand fully visible from wrist to
      fingertips.
- [ ] Capture several repetitions from more than one consenting signer. Keep
      the raw landmark recordings local and outside Git.

## Capture

From the project root, start one label at a time:

```bash
./scripts/record_sasl_sample.sh hello
```

The recorder stores hand landmarks only in `webcam/recordings/`. Repeat the
command for each approved label. For a moving phrase, make sure each sample
contains the complete motion from the agreed start pose to the agreed end
pose.

## Review before training

- [ ] A fluent reviewer has watched every sample and marked it `approved`,
      `needs_retake`, or `reject`.
- [ ] The reviewer confirmed hand orientation, location, timing, and the
      intended SASL meaning—not only visual similarity to an ASL example.
- [ ] Labels, signer pseudonyms, reviewer ID, source, and consent status are
      present in the review metadata.
- [ ] Retakes and rejected samples are excluded from the training manifest.

Only the approved manifest should be passed to the preparation and training
scripts in [`ml/README.md`](../ml/README.md). After training, verify the model
on held-out recordings from a signer who was not used for training, then run
the full test suite and a human lesson review before enabling the model in a
classroom build.

Until those boxes are checked, the app deliberately keeps word-level webcam
recognition in research/reference mode and does not claim that demo poses are
SASL.
