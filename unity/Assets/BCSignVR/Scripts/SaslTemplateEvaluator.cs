using System;
using System.Collections.Generic;
using UnityEngine;

namespace BCSignVR
{
    /// <summary>
    /// Conservative static A-Z matcher for the tracked XR hand. The checked-in
    /// templates are the same 21-landmark baseline used by the Mac webcam
    /// bridge; this lets Quest practice use the very same target vocabulary
    /// without pretending that a nearest template is a certified classifier.
    /// </summary>
    public sealed class SaslTemplateEvaluator
    {
        const float MaxDistance = .34f;
        const float MinMargin = .035f;
        readonly Dictionary<string, float[]> templates = new();

        [Serializable] sealed class TemplateCollection { public Template[] templates; }
        [Serializable] sealed class Template
        {
            public string label;
            public float[] features;
        }

        public bool IsReady => templates.Count == 26;

        public SaslTemplateEvaluator(TextAsset asset)
        {
            if (asset == null) return;
            try
            {
                var collection = JsonUtility.FromJson<TemplateCollection>(asset.text);
                if (collection?.templates == null) return;
                foreach (var item in collection.templates)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.label) ||
                        item.features == null || item.features.Length != 63) continue;
                    var normalized = NormalizeTemplate(item.features);
                    if (normalized != null) templates[item.label.ToUpperInvariant()] = normalized;
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning($"SASL A-Z templates could not be loaded: {error.Message}");
            }
        }

        public GestureScore Evaluate(HandPoseFrame frame)
        {
            if (frame == null || !frame.tracked || templates.Count == 0)
                return new GestureScore("NO_HAND", 0f, float.PositiveInfinity);

            var features = Features(frame);
            if (features == null)
                return new GestureScore("UNKNOWN", 0f, float.PositiveInfinity);

            var bestLabel = "UNKNOWN";
            var bestDistance = float.PositiveInfinity;
            var secondDistance = float.PositiveInfinity;
            foreach (var item in templates)
            {
                var distance = Distance(features, item.Value);
                if (distance < bestDistance)
                {
                    secondDistance = bestDistance;
                    bestDistance = distance;
                    bestLabel = item.Key;
                }
                else if (distance < secondDistance)
                {
                    secondDistance = distance;
                }
            }

            // A nearest neighbour is only useful when it is clearly separated
            // from the next letter and sits inside the reviewed baseline's
            // acceptance range.
            if (bestDistance > MaxDistance || secondDistance - bestDistance < MinMargin)
                return new GestureScore("UNKNOWN", 0f, bestDistance);
            var confidence = Mathf.Clamp01(Mathf.Exp(-bestDistance / .35f));
            return new GestureScore(bestLabel, confidence, bestDistance);
        }

        static float[] Features(HandPoseFrame frame)
        {
            var points = new Vector3[21];
            points[0] = Local(frame, 0);
            points[1] = Local(frame, 2);
            points[2] = Local(frame, 3);
            points[3] = Local(frame, 4);
            points[4] = Local(frame, 5);
            // HandPoseFrame keeps an extra palm-to-metacarpal anchor for each
            // finger, matching WebcamHandPoseProvider.MapFinger. Skip that
            // synthetic anchor here: MediaPipe's 21-point template expects
            // the actual MCP through tip sequence.
            points[5] = Local(frame, 7);
            points[6] = Local(frame, 8);
            points[7] = Local(frame, 9);
            points[8] = Local(frame, 10);
            points[9] = Local(frame, 12);
            points[10] = Local(frame, 13);
            points[11] = Local(frame, 14);
            points[12] = Local(frame, 15);
            points[13] = Local(frame, 17);
            points[14] = Local(frame, 18);
            points[15] = Local(frame, 19);
            points[16] = Local(frame, 20);
            points[17] = Local(frame, 22);
            points[18] = Local(frame, 23);
            points[19] = Local(frame, 24);
            points[20] = Local(frame, 25);

            var scale = Mathf.Sqrt(points[9].x * points[9].x + points[9].y * points[9].y);
            if (scale < .0001f || float.IsNaN(scale) || float.IsInfinity(scale)) return null;
            var result = new float[63];
            for (var i = 0; i < points.Length; i++)
            {
                result[i * 3] = points[i].x / scale;
                result[i * 3 + 1] = points[i].y / scale;
                result[i * 3 + 2] = points[i].z / scale;
            }
            return result;
        }

        static float[] NormalizeTemplate(float[] source)
        {
            var scale = Mathf.Sqrt(source[27] * source[27] + source[28] * source[28]);
            if (scale < .0001f || float.IsNaN(scale) || float.IsInfinity(scale)) return null;
            var result = new float[source.Length];
            for (var i = 0; i < source.Length; i++) result[i] = source[i] / scale;
            return result;
        }

        static Vector3 Local(HandPoseFrame frame, int index)
        {
            var wrist = frame.joints[0];
            return Quaternion.Inverse(frame.wristRotation) * (frame.joints[index] - wrist);
        }

        static float Distance(float[] actual, float[] expected)
        {
            if (expected == null || expected.Length != actual.Length) return float.PositiveInfinity;
            var sum = 0f;
            for (var i = 0; i < actual.Length; i++)
            {
                var delta = actual[i] - expected[i];
                sum += delta * delta;
            }
            return Mathf.Sqrt(sum / actual.Length);
        }
    }
}
