# SASL reference and licensing notes

This project uses external SASL references as research leads, not as a claim
that every image or sign has been validated for teaching. The lesson content
must be reviewed by a fluent SASL signer or educator before it is labelled
`REVIEWED_SASL_*` and used for model training.

## Useful authoritative starting points

- [National Institute for the Deaf resources](https://nid.org.za/resources/)
  provides SASL learning resources, including alphabet/counting material,
  commonly used signs, and a SASL video dictionary/course material.
- [WCED ePortal SASL teaching plan](https://wcedeportal.westerncape.gov.za/eresource/2020-national-revised-teaching-plans-gr-7-south-african-sign-language-sasl-home-policy)
  is an education-sector source for curriculum and teaching context. The
  portal also publishes individual SASL terminology videos under the same
  Creative Commons Attribution-NonCommercial-NoDerivatives terms; verify the
  exact asset licence before reusing any media.

- [Maswao dataset construction repository](https://github.com/dsfsi/maswo-south-african-sign-language-dataset)
  is a useful SASL research lead, but its repository distributes video
  identifiers and annotations rather than the interpreter footage itself. The
  current metadata is weakly aligned sentence-level material, not a reviewed
  webcam landmark set for individual words. It therefore has not been copied
  into the app or used to claim phrase-recognition accuracy.

The included greeting card is kept as a private prototype reference with NID
attribution; it is not a trained recognition target. NID states that its site
content is owned by NID, so posters, video frames, and dictionary media need
permission/licence review before this project is published or redistributed.
The standalone app's imported model is only the PRJ team's A–Z landmark
baseline, whose provenance and limitations are recorded in
`ml/team_sasl/SOURCE.md`.

## What is and is not trained today

- A–Z: the imported team landmark model is runnable locally and is gated as a
  research baseline (`TEAM_SASL_*`). Its published holdout is reproduced at
  77.45%; it is not a community-validated SASL dictionary.
- Words and phrases: the app has lesson slots and progression plumbing, but
  there are no reviewer-approved “hello” or “how are you” landmark sequences
  checked into this project yet. Do not present the phrase lessons as trained
  recognition until consented recordings have been reviewed and labelled.

The distinction matters here: a spoken-language transcript or a sign-language
video identifier can help locate candidate teaching material, but it does not
provide the hand, body, face, timing and signer metadata required to train a
reliable learner-facing recognizer.

## Safe path to phrase recognition

Capture landmark-only recordings with consent, have a fluent SASL reviewer
verify each sign and movement, label the rows with `REVIEWED_SASL_*`, then run
the workflow in `ml/README.md`. Keep identities outside the repository and
retain the review/source record alongside the dataset release.
