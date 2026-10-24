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

        readonly struct Vote
        {
            public readonly string label;
            public Vote(string label) { this.label = label; }
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

        const float MinimumProbability = .60f;
        const float MinimumMargin = .12f;
        const float MinimumConfidence = .60f;
        const int HistoryLength = 7;
        const int StableVotesRequired = 4;

        readonly Queue<Vote> history = new();
        readonly float[] hidden1 = new float[128];
        readonly float[] hidden2 = new float[64];
        readonly float[] probabilities = new float[26];
        DenseModel model;
        float lastFrameAt = float.NegativeInfinity;

        public bool IsReady
        {
            get { LoadModel(); return model != null; }
        }

        public void Reset()
        {
            history.Clear();
            lastFrameAt = float.NegativeInfinity;
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
            if (model == null) return false;
            var now = Time.unscaledTime;
            if (now - lastFrameAt > .35f) history.Clear();
            lastFrameAt = now;
            Infer(features, out var bestIndex, out var secondProbability);
            var frameConfidence = probabilities[bestIndex];
            if (frameConfidence < MinimumProbability || frameConfidence - secondProbability < MinimumMargin)
            {
                history.Clear();
                return false;
            }

            history.Enqueue(new Vote(model.labels[bestIndex]));
            while (history.Count > HistoryLength) history.Dequeue();

            var voteCounts = new Dictionary<string, int>();
            foreach (var vote in history)
            {
                voteCounts.TryGetValue(vote.label, out var count);
                voteCounts[vote.label] = count + 1;
            }

            var stableLabel = model.labels[bestIndex];
            var stableCount = 0;
            foreach (var vote in voteCounts)
            {
                if (vote.Value <= stableCount) continue;
                stableLabel = vote.Key;
                stableCount = vote.Value;
            }
            confidence = frameConfidence * stableCount / history.Count;
            if (stableCount < StableVotesRequired || confidence < MinimumConfidence) return false;

            label = "TEAM_SASL_" + stableLabel;
            return true;
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
            if (model != null) return;
            var asset = Resources.Load<TextAsset>("sasl_dense_quest");
            if (asset == null)
            {
                Debug.LogWarning("Quest A–Z handshape model is unavailable.");
                return;
            }

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

        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
