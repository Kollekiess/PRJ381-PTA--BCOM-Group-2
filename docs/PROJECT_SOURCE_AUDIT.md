# PRJ GitHub source audit

Checked the PRJ381 repository branches before importing assets into the
standalone project.

## Reused

- `AI-models-trained/PRJ381-PTA--BCOM-Group-2-gesture-logic/models/` contains
  explicit A–Z labels, 2,146 landmark rows, templates, and a Keras model.
- Its contiguous 439-row holdout reproduces the published 77.45% result after
  converting the dense weights to NumPy. Those weights are used by the webcam
  bridge as a conservative `TEAM_SASL_*` research baseline.

## Excluded

- `devs/PRJ_VR/Assets/V_Work/Textures/asl_hello.jpg` and the similarly named
  `asl_*` assets are explicitly labelled **ASL**, not SASL. They are not used
  as teaching references.
- The PRJ branches did not contain reviewer-labelled SASL word/phrase
  landmark sequences for “hello” or “how are you”. Existing phrase-looking
  art therefore cannot be treated as a validated SASL lesson.

The standalone classroom, Bob tutor, UI, database, webcam transport, and Quest
simulation remain this project's own implementation.
