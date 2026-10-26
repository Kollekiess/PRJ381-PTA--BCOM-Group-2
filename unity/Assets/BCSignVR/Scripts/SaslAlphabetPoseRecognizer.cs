using System;
using System.Collections.Generic;
using UnityEngine;

namespace BCSignVR
{
    /// <summary>
    /// On-device A–Z inference for Quest/OpenXR using the team's exported
    /// dense model. A match is practice feedback, not certified assessment.
    /// </summary>
    public sealed class SaslAlphabetPoseRecognizer
    {
        [Serializable]
        sealed class DenseModel
        {
            public string[] labels;
            public float[] w1, b1, w2, b2, w3, b3;
        }

        [Serializable]
        sealed class TemplateLibrary
        {
            public Template[] templates;
        }

        [Serializable]
        sealed class Template
        {
            public string label;
            public float[] features;
        }

        readonly struct Vote
        {
            public readonly string label;
            public readonly float confidence;
            public Vote(string label, float confidence)
            {
                this.label = label;
                this.confidence = confidence;
            }
        }

        readonly struct MotionSample
        {
            public readonly float time;
            public readonly Vector2 wrist;
            public MotionSample(float time, Vector2 wrist)
            {
                this.time = time;
                this.wrist = wrist;
            }
        }

        // MediaPipe's 21-point order, represented by the corresponding Quest
        // OpenXR joint. Palm and wrist positions are in camera space so the
        // model sees the same handed, screen-aligned basis as the webcam path.
        static readonly int[] XrJointForMediaPipe =
        {
            0, 2, 3, 4, 5,
            7, 8, 9, 10,
            12, 13, 14, 15,
            17, 18, 19, 20,
            22, 23, 24, 25
        };

        // The original Quest path required seven consecutive high-confidence
        // dense-model frames. That was safe but made a correct handshape look
        // invisible whenever tracking dropped for one frame. Keep the model's
        // runner-up guard, but let temporal voting do the smoothing.
        const float MinimumProbability = .66f;
        const float MinimumMargin = .10f;
        const float MinimumConfidence = .60f;
        const int HistoryLength = 7;
        const int StableVotesRequired = 4;

        const float TemplateMaximumDistance = .34f;
        const float TemplateMinimumMargin = .035f;
        const float MotionWindowSeconds = 1.75f;
        const float MinimumMotionSpan = .10f;
        const float MinimumMotionScore = .72f;
        const int MotionSampleLimit = 96;

        readonly Queue<Vote> history = new();
        readonly Queue<MotionSample> motionHistory = new();
        readonly List<MotionSample> motionWindow = new();
        readonly float[] hidden1 = new float[128];
        readonly float[] hidden2 = new float[64];
        readonly float[] probabilities = new float[26];
        DenseModel model;
        Template[] templates;
        bool loadAttempted;
        float lastFrameAt = float.NegativeInfinity;
        float lastAcceptedAt = float.NegativeInfinity;
        string lastStaticLabel = "";

        public bool IsReady
        {
            get { LoadModel(); return model != null; }
        }

        public void Reset()
        {
            history.Clear();
            motionHistory.Clear();
            motionWindow.Clear();
            lastFrameAt = float.NegativeInfinity;
            lastAcceptedAt = float.NegativeInfinity;
            lastStaticLabel = "";
        }

        public bool TryRecognize(HandPoseFrame hand, Camera viewpoint, out string label, out float confidence)
        {
            label = "UNKNOWN";
            confidence = 0f;
            if (hand == null || !hand.tracked || viewpoint == null || !TryBuildFeatures(hand, viewpoint, out var features))
            {
                Reset();
                return false;
            }

            LoadModel();
            if (model == null && (templates == null || templates.Length == 0)) return false;
            var now = Time.unscaledTime;
            if (now - lastFrameAt > .35f)
            {
                history.Clear();
                motionHistory.Clear();
                lastStaticLabel = "";
            }
            lastFrameAt = now;
            AddMotionSample(hand, viewpoint, now);

            var candidate = "";
            var candidateConfidence = 0f;
            if (model != null)
            {
                Infer(features, out var bestIndex, out var secondProbability);
                var frameConfidence = probabilities[bestIndex];
                var frameMargin = frameConfidence - secondProbability;
                // Remember J/Z's static handshape while the learner traces the
                // movement. The model can become uncertain mid-stroke even
                // though the hand is still the correct sign.
                if ((model.labels[bestIndex] == "J" || model.labels[bestIndex] == "Z") &&
                    frameConfidence >= .30f)
                    lastStaticLabel = model.labels[bestIndex];
                if (frameConfidence >= MinimumProbability && frameMargin >= MinimumMargin)
                {
                    candidate = model.labels[bestIndex];
                    candidateConfidence = frameConfidence;
                }
            }

            // Chantel's reviewed template library is deliberately kept beside
            // the dense model. It rescues frames where Quest landmark jitter
            // pushes the neural output just below its gate, while the dense
            // model remains authoritative whenever it is decisive.
            var hasTemplateCandidate = TryTemplateCandidate(features, out var templateLabel,
                out var templateConfidence);
            if (hasTemplateCandidate && (string.IsNullOrEmpty(candidate) ||
                    // An exact reviewed template is a stronger signal than a
                    // contradictory dense prediction. The high threshold
                    // prevents a merely similar template from overriding the
                    // trained model on ordinary tracked frames.
                    templateConfidence >= .80f && templateLabel != candidate))
            {
                candidate = templateLabel;
                candidateConfidence = templateConfidence;
                if (templateLabel == "J" || templateLabel == "Z") lastStaticLabel = templateLabel;
            }

            if (!string.IsNullOrEmpty(candidate))
            {
                history.Enqueue(new Vote(candidate, candidateConfidence));
                lastAcceptedAt = now;
                while (history.Count > HistoryLength) history.Dequeue();
            }
            else if (now - lastAcceptedAt > .34f)
            {
                history.Clear();
            }

            if (TryRecognizeMovement(out var movingLabel, out var movingConfidence))
            {
                label = "TEAM_SASL_" + movingLabel;
                confidence = movingConfidence;
                return true;
            }

            if (history.Count == 0 || now - lastAcceptedAt > .45f) return false;
            var voteCounts = new Dictionary<string, int>();
            var confidenceTotals = new Dictionary<string, float>();
            foreach (var vote in history)
            {
                voteCounts.TryGetValue(vote.label, out var count);
                voteCounts[vote.label] = count + 1;
                confidenceTotals.TryGetValue(vote.label, out var total);
                confidenceTotals[vote.label] = total + vote.confidence;
            }

            var stableLabel = "";
            var stableCount = 0;
            foreach (var vote in voteCounts)
            {
                if (vote.Value <= stableCount) continue;
                stableLabel = vote.Key;
                stableCount = vote.Value;
            }
            var averageConfidence = confidenceTotals[stableLabel] / stableCount;
            var stability = Mathf.Lerp(.70f, 1f, stableCount / (float)HistoryLength);
            confidence = averageConfidence * stability;
            if (stableCount < StableVotesRequired || confidence < MinimumConfidence) return false;

            label = "TEAM_SASL_" + stableLabel;
            return true;
        }

        bool TryTemplateCandidate(float[] features, out string label, out float confidence)
        {
            label = "";
            confidence = 0f;
            if (templates == null || templates.Length == 0) return false;
            var bestDistance = float.PositiveInfinity;
            var secondDistance = float.PositiveInfinity;
            var bestLabel = "";
            foreach (var template in templates)
            {
                if (template?.features == null || template.features.Length != features.Length) continue;
                var distance = 0f;
                for (var index = 0; index < features.Length; index++)
                {
                    var delta = features[index] - template.features[index];
                    distance += delta * delta;
                }
                distance = Mathf.Sqrt(distance / features.Length);
                if (distance < bestDistance)
                {
                    secondDistance = bestDistance;
                    bestDistance = distance;
                    bestLabel = template.label;
                }
                else if (distance < secondDistance)
                {
                    secondDistance = distance;
                }
            }
            if (string.IsNullOrEmpty(bestLabel) || bestDistance > TemplateMaximumDistance ||
                secondDistance - bestDistance < TemplateMinimumMargin) return false;
            label = bestLabel;
            confidence = Mathf.Clamp01(Mathf.Exp(-bestDistance / .35f));
            return confidence >= MinimumConfidence;
        }

        void AddMotionSample(HandPoseFrame hand, Camera viewpoint, float now)
        {
            var wrist = viewpoint.transform.InverseTransformPoint(hand.joints[0]);
            if (!Finite(wrist)) return;
            // The wrist is intentional: J and Z move the whole hand through
            // space, so a fingertip-minus-wrist path would erase the gesture.
            motionHistory.Enqueue(new MotionSample(now, new Vector2(-wrist.x, -wrist.y)));
            while (motionHistory.Count > MotionSampleLimit) motionHistory.Dequeue();
        }

        bool TryRecognizeMovement(out string label, out float confidence)
        {
            label = "";
            confidence = 0f;
            if (lastStaticLabel != "J" && lastStaticLabel != "Z") return false;
            var now = Time.unscaledTime;
            motionWindow.Clear();
            foreach (var sample in motionHistory)
                if (now - sample.time <= MotionWindowSeconds) motionWindow.Add(sample);
            if (motionWindow.Count < 10) return false;

            // Ignore the quiet hold while the learner gets ready, so the
            // comparison begins at the first real stroke instead of spending
            // most of its samples on the starting pose.
            var quietPrefix = 0;
            while (quietPrefix < motionWindow.Count - 1 &&
                   Vector2.Distance(motionWindow[quietPrefix].wrist, motionWindow[quietPrefix + 1].wrist) < .002f)
                quietPrefix++;
            // A learner often holds the starting pose before moving. Trim
            // that genuinely quiet prefix, but leave the first moving sample
            // in place so the stroke keeps its correct phase.
            if (quietPrefix >= 6)
                motionWindow.RemoveRange(0, Mathf.Max(0, quietPrefix - 1));
            if (motionWindow.Count < 10) return false;

            var score = MotionScore(lastStaticLabel, motionWindow);
            if (score < MinimumMotionScore) return false;
            label = lastStaticLabel;
            // Motion is a deliberately high-confidence completion signal only
            // after the path has matched the sign's reviewed teaching stroke.
            confidence = Mathf.Clamp(.80f + (score - MinimumMotionScore) * .70f, .80f, .97f);
            return true;
        }

        static float MotionScore(string label, List<MotionSample> samples)
        {
            var minimumX = float.PositiveInfinity;
            var maximumX = float.NegativeInfinity;
            var minimumY = float.PositiveInfinity;
            var maximumY = float.NegativeInfinity;
            foreach (var sample in samples)
            {
                minimumX = Mathf.Min(minimumX, sample.wrist.x);
                maximumX = Mathf.Max(maximumX, sample.wrist.x);
                minimumY = Mathf.Min(minimumY, sample.wrist.y);
                maximumY = Mathf.Max(maximumY, sample.wrist.y);
            }
            var span = Mathf.Max(maximumX - minimumX, maximumY - minimumY);
            if (!Finite(span) || span < MinimumMotionSpan) return 0f;

            var start = samples[0].wrist;
            var end = samples[samples.Count - 1].wrist;
            var expectedEnd = label == "J" ? new Vector2(-.38f, -.52f) / .52f : new Vector2(.50f, -.52f) / .52f;
            var actualEnd = (end - start) / span;
            var endpointError = Vector2.Distance(actualEnd, expectedEnd);
            var pathError = 0f;
            const int samplesToCompare = 24;
            for (var index = 0; index < samplesToCompare; index++)
            {
                var point = Resample(samples, index / (samplesToCompare - 1f));
                var target = MotionPoint(label, index / (samplesToCompare - 1f));
                target /= .52f;
                var actual = (point - start) / span;
                pathError += Vector2.SqrMagnitude(actual - target);
            }
            pathError = Mathf.Sqrt(pathError / samplesToCompare);
            return Mathf.Clamp01(Mathf.Exp(-(pathError * 2.8f + endpointError * .75f)));
        }

        static Vector2 Resample(List<MotionSample> samples, float phase)
        {
            var position = phase * (samples.Count - 1);
            var lower = Mathf.Clamp(Mathf.FloorToInt(position), 0, samples.Count - 1);
            var upper = Mathf.Clamp(lower + 1, 0, samples.Count - 1);
            return Vector2.Lerp(samples[lower].wrist, samples[upper].wrist, position - lower);
        }

        static Vector2 MotionPoint(string label, float phase)
        {
            if (label == "Z")
            {
                if (phase < .34f)
                    return Vector2.Lerp(new Vector2(-.25f, .24f), new Vector2(.25f, .24f), Ease(phase / .34f));
                if (phase < .67f)
                    return Vector2.Lerp(new Vector2(.25f, .24f), new Vector2(-.25f, -.28f), Ease((phase - .34f) / .33f));
                return Vector2.Lerp(new Vector2(-.25f, -.28f), new Vector2(.25f, -.28f), Ease((phase - .67f) / .33f));
            }
            if (phase < .62f)
                return Vector2.Lerp(new Vector2(.15f, .24f), new Vector2(.15f, -.28f), Ease(phase / .62f));
            var curl = Ease((phase - .62f) / .38f);
            return new Vector2(Mathf.Lerp(.15f, -.23f, curl), -.28f + Mathf.Sin(curl * Mathf.PI) * .14f);
        }

        static float Ease(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        static bool TryBuildFeatures(HandPoseFrame hand, Camera viewpoint, out float[] features)
        {
            features = null;
            var points = new Vector3[XrJointForMediaPipe.Length];
            for (var index = 0; index < points.Length; index++)
            {
                var world = hand.joints[XrJointForMediaPipe[index]];
                if (!Finite(world)) return false;
                var cameraPoint = viewpoint.transform.InverseTransformPoint(world);
                // The trained templates use image-right, image-down and
                // MediaPipe's toward-camera depth convention.
                points[index] = new Vector3(-cameraPoint.x, -cameraPoint.y, -cameraPoint.z);
            }

            var wrist = points[0];
            var middleMcp = points[9] - wrist;
            var scale = Mathf.Sqrt(middleMcp.x * middleMcp.x + middleMcp.y * middleMcp.y);
            if (!Finite(scale) || scale < .0001f) return false;

            features = new float[points.Length * 3];
            for (var point = 0; point < points.Length; point++)
            {
                var normalized = (points[point] - wrist) / scale;
                var offset = point * 3;
                features[offset] = normalized.x;
                features[offset + 1] = normalized.y;
                features[offset + 2] = normalized.z;
                if (!Finite(normalized)) { features = null; return false; }
            }
            return true;
        }

        void Infer(float[] features, out int bestIndex, out float secondProbability)
        {
            for (var output = 0; output < hidden1.Length; output++)
            {
                var sum = model.b1[output];
                for (var input = 0; input < features.Length; input++)
                    sum += features[input] * model.w1[input * hidden1.Length + output];
                hidden1[output] = Mathf.Max(0f, sum);
            }
            for (var output = 0; output < hidden2.Length; output++)
            {
                var sum = model.b2[output];
                for (var input = 0; input < hidden1.Length; input++)
                    sum += hidden1[input] * model.w2[input * hidden2.Length + output];
                hidden2[output] = Mathf.Max(0f, sum);
            }

            var maximumLogit = float.NegativeInfinity;
            for (var output = 0; output < probabilities.Length; output++)
            {
                var logit = model.b3[output];
                for (var input = 0; input < hidden2.Length; input++)
                    logit += hidden2[input] * model.w3[input * probabilities.Length + output];
                probabilities[output] = logit;
                maximumLogit = Mathf.Max(maximumLogit, logit);
            }

            var total = 0f;
            for (var output = 0; output < probabilities.Length; output++)
            {
                probabilities[output] = Mathf.Exp(probabilities[output] - maximumLogit);
                total += probabilities[output];
            }
            bestIndex = 0;
            secondProbability = 0f;
            for (var output = 0; output < probabilities.Length; output++)
            {
                probabilities[output] /= total;
                if (probabilities[output] > probabilities[bestIndex]) bestIndex = output;
            }
            for (var output = 0; output < probabilities.Length; output++)
                if (output != bestIndex) secondProbability = Mathf.Max(secondProbability, probabilities[output]);
        }

        void LoadModel()
        {
            if (loadAttempted) return;
            loadAttempted = true;
            var asset = Resources.Load<TextAsset>("sasl_dense_quest");
            if (asset != null)
            {
                try
                {
                    var loaded = JsonUtility.FromJson<DenseModel>(asset.text);
                    if (loaded?.labels == null || loaded.labels.Length != 26 ||
                        loaded.w1?.Length != 63 * 128 || loaded.b1?.Length != 128 ||
                        loaded.w2?.Length != 128 * 64 || loaded.b2?.Length != 64 ||
                        loaded.w3?.Length != 64 * 26 || loaded.b3?.Length != 26)
                        throw new InvalidOperationException("Quest A–Z dense model is incomplete.");
                    model = loaded;
                }
                catch (Exception error)
                {
                    Debug.LogWarning("Quest A–Z handshape model could not be read: " + error.Message);
                }
            }

            try
            {
                var templateAsset = Resources.Load<TextAsset>("sasl_templates_quest");
                var library = templateAsset == null ? null : JsonUtility.FromJson<TemplateLibrary>(templateAsset.text);
                if (library?.templates == null || library.templates.Length != 26)
                    throw new InvalidOperationException("Quest A–Z template library is incomplete.");
                foreach (var template in library.templates)
                {
                    if (template?.features == null || template.features.Length != 63)
                        throw new InvalidOperationException("Quest A–Z template contains an invalid feature vector.");
                    // Centroids are averaged after per-sample normalization,
                    // so their middle-MCP length is not guaranteed to remain
                    // exactly one. Normalize the imported Chantel centroids
                    // once to match the live Quest feature transform.
                    var scale = Mathf.Sqrt(template.features[27] * template.features[27] +
                        template.features[28] * template.features[28]);
                    if (!Finite(scale) || scale < .0001f)
                        throw new InvalidOperationException("Quest A–Z template has no usable scale.");
                    for (var index = 0; index < template.features.Length; index++)
                        template.features[index] /= scale;
                }
                templates = library.templates;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Quest A–Z template fallback could not be read: " + error.Message);
            }
            if (model == null && (templates == null || templates.Length == 0))
                Debug.LogWarning("Quest A–Z handshape model and template fallback are unavailable.");
        }

        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
