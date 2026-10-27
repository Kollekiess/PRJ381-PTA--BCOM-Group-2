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

The repository's `Updated-Hand-signs` upload was also cross-checked before this
integration. Its A–Z template library and labels match the checked-in source,
while its additional word model is a separate sequence model that this
alphabet lesson does not consume. Quest now loads the reviewed A–Z templates
as a normalized fallback alongside the dense model, preserving the app's Bob
artwork and lesson styling.

The same upload contains a separate five-class sequence dataset and Keras word
model: HELLO (24 clips), GOODBYE (25), NICE_TO_MEET_YOU (14), PLEASE (30), and
THANK_YOU (26). The app's Words menu replays one original 30-frame clip per
class through the Bob-style renderer so learners can inspect the captured
movement. The uploaded word model is intentionally labelled as not-yet-live
Quest recognition; it is not silently treated as a validated on-device model.

Before importing it, I audited the repository data: the CSV has 2,146 rows and
all 26 A–Z labels, but the accompanying session record contains only one
session and the rows do not identify signers or a qualified SASL reviewer. The
reported holdout is therefore useful engineering evidence, not community or
signer-held-out validation. The app keeps the model behind a conservative
distance/margin gate and never records its prediction as certified accuracy.
