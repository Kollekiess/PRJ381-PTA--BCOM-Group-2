using System;
using System.Collections.Generic;
using UnityEngine;

namespace BCSignVR
{
    public readonly struct GestureScore
    {
        public readonly string label;
        public readonly float confidence;
        public readonly float distance;

        public GestureScore(string label, float confidence, float distance)
        {
            this.label = label;
            this.confidence = confidence;
            this.distance = distance;
        }
    }

    public sealed class GestureEvaluator
    {
        readonly Dictionary<string, float[]> templates = new();

        public void AddTemplate(string label, HandPoseFrame frame) => templates[label] = frame.NormalizedFeatures();

        public GestureScore Evaluate(HandPoseFrame frame)
        {
            if (!frame.tracked || templates.Count == 0)
                return new GestureScore("NO_HAND", 0f, float.PositiveInfinity);

            var features = frame.NormalizedFeatures();
            var bestLabel = "UNKNOWN";
            var bestDistance = float.PositiveInfinity;
            foreach (var entry in templates)
            {
                var sum = 0f;
                for (var i = 0; i < features.Length; i++)
                {
                    var delta = features[i] - entry.Value[i];
                    sum += delta * delta;
                }
                var distance = Mathf.Sqrt(sum / features.Length);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestLabel = entry.Key;
                }
            }
            var confidence = Mathf.Clamp01(Mathf.Exp(-bestDistance * 10f));
            return new GestureScore(bestLabel, confidence, bestDistance);
        }
    }
}

