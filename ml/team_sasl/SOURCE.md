# Team SASL alphabet baseline

These templates came from the PRJ381 repository's `AI-models-trained` branch,
commit `245c2bdcabbc536fad44e8810d2f26d2d55b6b46`:

https://github.com/Kollekies/PRJ381-PTA--BCOM-Group-2/tree/AI-models-trained/PRJ381-PTA--BCOM-Group-2-gesture-logic

The source branch reports 2,146 labelled A–Z landmark samples and 77.45%
held-out test accuracy for its Keras model. Its per-letter report shows weak
classes, including F, M, N and Y. This standalone app uses the checked-in
landmark templates as a conservative fallback because the Mac webcam
environment does not need TensorFlow: the dense model weights are exported to
NumPy for inference. Predictions are labelled `TEAM_SASL_*`
and are feedback, not a certified proficiency result. The scene, UI, Bob
avatar and lesson flow remain this standalone project's own work.

The exported NumPy weights were checked against the source CSV using the
source feature normalization and contiguous per-letter holdout: 439 test rows
reproduce the reported 77.45% accuracy exactly. This verifies the conversion;
it does not improve the source dataset's generalisation.

Before importing it, I audited the repository data: the CSV has 2,146 rows and
all 26 A–Z labels, but the accompanying session record contains only one
session and the rows do not identify signers or a qualified SASL reviewer. The
reported holdout is therefore useful engineering evidence, not community or
signer-held-out validation. The app keeps the model behind a conservative
distance/margin gate and never records its prediction as certified accuracy.
