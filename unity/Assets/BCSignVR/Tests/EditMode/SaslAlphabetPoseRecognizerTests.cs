using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BCSignVR.Tests
{
    public sealed class SaslAlphabetPoseRecognizerTests
    {
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

        // Quest's joint array maps its index finger's proximal-through-tip
        // points to MediaPipe's MCP-through-tip points; the palm-only point
        // and metacarpal bases are deliberately skipped.
        static readonly int[] QuestJointForMediaPipe =
        {
            0, 2, 3, 4, 5,
            7, 8, 9, 10,
            12, 13, 14, 15,
            17, 18, 19, 20,
            22, 23, 24, 25
        };

        [Test]
        [TestCase("A")]
        [TestCase("B")]
        [TestCase("C")]
        [TestCase("E")]
        [TestCase("T")]
        public void QuestDenseModelRecognizesTrackedLettersAndRejectsTrackingLoss(string expectedLabel)
        {
            var templateAsset = Resources.Load<TextAsset>("sasl_templates_quest");
            Assert.IsNotNull(templateAsset, "Quest alphabet template resource must be included.");
            var library = JsonUtility.FromJson<TemplateLibrary>(templateAsset.text);
            var template = library.templates.Single(item => item.label == expectedLabel);
            Assert.AreEqual(63, template.features.Length);

            var hand = new HandPoseFrame { tracked = true, source = "test Quest joints" };
            for (var landmark = 0; landmark < QuestJointForMediaPipe.Length; landmark++)
            {
                var offset = landmark * 3;
                hand.joints[QuestJointForMediaPipe[landmark]] = new Vector3(
                    -template.features[offset],
                    -template.features[offset + 1],
                    -template.features[offset + 2]);
            }

            var cameraObject = new GameObject("Quest alphabet test viewpoint");
            try
            {
                var recognizer = new SaslAlphabetPoseRecognizer();
                Assert.IsTrue(recognizer.IsReady, "Quest dense A–Z model resource must load.");
                var camera = cameraObject.AddComponent<Camera>();
                var matched = false;
                for (var frame = 0; frame < 7; frame++)
                    matched = recognizer.TryRecognize(hand, camera, out var label, out var confidence) &&
                        label == "TEAM_SASL_" + expectedLabel && confidence >= .60f;
                Assert.IsTrue(matched, $"A stable tracked {expectedLabel} handshape must pass the Quest model.");

                hand.tracked = false;
                Assert.IsFalse(recognizer.TryRecognize(hand, camera, out _, out _),
                    "The model must not report a match after hand tracking is lost.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
