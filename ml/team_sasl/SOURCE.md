# Team SASL alphabet baseline

These templates came from the PRJ381 repository's `AI-models-trained` branch,
commit `245c2bdcabbc536fad44e8810d2f26d2d55b6b46`:

https://github.com/Kollekies/PRJ381-PTA--BCOM-Group-2/tree/AI-models-trained/PRJ381-PTA--BCOM-Group-2-gesture-logic

The source branch reports 2,146 labelled A–Z landmark samples and 77.45%
held-out test accuracy for its Keras model. Its per-letter report shows weak
classes, including F, M, N and Y. This standalone app uses the checked-in
landmark templates as a conservative fallback because the Mac webcam
environment does not ship TensorFlow. Predictions are labelled `TEAM_SASL_*`
and are feedback, not a certified proficiency result. The scene, UI, Bob
avatar and lesson flow remain this standalone project's own work.
