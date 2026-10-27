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

        static string[] AlphabetLetters() => Enumerable.Range('A', 26)
            .Select(letter => ((char)letter).ToString()).ToArray();

        public static string[] VerifiedLetters() => new[] { "A", "B", "C", "E", "T" };

        [TestCaseSource(nameof(VerifiedLetters))]
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
                        label == "TEAM_SASL_" + expectedLabel && confidence >= .68f;
                Assert.IsTrue(matched, $"A stable tracked {expectedLabel} handshape must pass the Quest model at the stricter confidence threshold.");

                hand.tracked = false;
                Assert.IsFalse(recognizer.TryRecognize(hand, camera, out _, out _),
                    "The model must not report a match after hand tracking is lost.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void UpdatedQuestTemplateLibraryRecognizesEveryAlphabetSign()
        {
            var templateAsset = Resources.Load<TextAsset>("sasl_templates_quest");
            Assert.IsNotNull(templateAsset, "Chantel's reviewed Quest template library must load.");
            var library = JsonUtility.FromJson<TemplateLibrary>(templateAsset.text);
            var cameraObject = new GameObject("Quest complete alphabet viewpoint");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                var recognizer = new SaslAlphabetPoseRecognizer();
                Assert.IsTrue(recognizer.IsReady, "The dense Quest model must load beside the reviewed templates.");
                foreach (var template in library.templates)
                {
                    recognizer.Reset();
                    var hand = new HandPoseFrame { tracked = true, source = "updated Quest template" };
                    SetTemplatePose(hand, template.features, Vector2.zero);
                    var matched = false;
                    var observedLabel = "UNKNOWN";
                    var observedConfidence = 0f;
                    for (var frame = 0; frame < 7; frame++)
                    {
                        matched = recognizer.TryRecognize(hand, camera, out observedLabel, out observedConfidence) &&
                            observedLabel == "TEAM_SASL_" + template.label && observedConfidence >= .60f;
                    }
                    Assert.IsTrue(matched, $"The reviewed {template.label} handshape must be recognized on Quest (saw {observedLabel}, {observedConfidence:P0}).");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [TestCase("J")]
        [TestCase("Z")]
        public void MovementLettersRecognizeTheirFullTeachingStroke(string expectedLabel)
        {
            var templateAsset = Resources.Load<TextAsset>("sasl_templates_quest");
            var library = JsonUtility.FromJson<TemplateLibrary>(templateAsset.text);
            var template = library.templates.Single(item => item.label == expectedLabel);
            var cameraObject = new GameObject("Quest movement-letter viewpoint");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                var recognizer = new SaslAlphabetPoseRecognizer();
                var hand = new HandPoseFrame { tracked = true, source = "Quest movement test" };
                var matched = false;
                foreach (var point in TeachingStroke(expectedLabel))
                {
                    SetTemplatePose(hand, template.features, point);
                    matched = recognizer.TryRecognize(hand, camera, out var label, out var confidence) &&
                        label == "TEAM_SASL_" + expectedLabel && confidence >= .80f;
                }
                Assert.IsTrue(matched, $"Quest must recognize the complete moving {expectedLabel} stroke.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void OpenPracticePoseDoesNotFalseConfirmLetterB()
        {
            var cameraObject = new GameObject("Quest open-hand validation viewpoint");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(new Vector3(0, 1.85f, -2.6f), Quaternion.identity);
                var recognizer = new SaslAlphabetPoseRecognizer();
                var openHand = SimulatedHandPoseProvider.Template(SimulatedHandPoseProvider.DemoPose.Open);
                var confirmed = false;
                var label = "UNKNOWN";
                var confidence = 0f;
                for (var frame = 0; frame < 7; frame++)
                    confirmed = recognizer.TryRecognize(openHand, camera, out label, out confidence);

                Assert.IsFalse(confirmed, $"A spread open hand must not be confirmed as {label} ({confidence:P0}).");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void EveryAlphabetAndPracticeHandArtworkIsAvailable()
        {
            foreach (var letter in AlphabetLetters())
            {
                var texture = Resources.Load<Texture2D>("CartoonHand" + letter);
                Assert.IsNotNull(texture, $"Bob's cartoon hand artwork for {letter} must load.");
                Assert.Greater(texture.width, 0);
                Assert.Greater(texture.height, 0);
            }

            foreach (var pose in new[] { "Open", "Fist", "Point", "Greeting" })
                Assert.IsNotNull(Resources.Load<Texture2D>("CartoonHand" + pose),
                    $"Bob's {pose} practice artwork must load.");
        }

        [Test]
        public void EveryQuestAlphabetTemplateIsPresentAndWellFormed()
        {
            var templateAsset = Resources.Load<TextAsset>("sasl_templates_quest");
            Assert.IsNotNull(templateAsset, "Quest A–Z templates must be included.");
            var library = JsonUtility.FromJson<TemplateLibrary>(templateAsset.text);
            Assert.AreEqual(26, library.templates.Length);

            foreach (var letter in AlphabetLetters())
            {
                var template = library.templates.Single(item => item.label == letter);
                Assert.AreEqual(63, template.features.Length, $"{letter} needs 21 3D landmarks.");
                foreach (var feature in template.features)
                    Assert.IsFalse(float.IsNaN(feature) || float.IsInfinity(feature),
                        $"{letter} contains an invalid landmark.");
            }
        }

        static void SetTemplatePose(HandPoseFrame hand, float[] features, Vector2 wristImageOffset)
        {
            var worldOffset = new Vector3(-wristImageOffset.x, -wristImageOffset.y, 0f);
            for (var landmark = 0; landmark < QuestJointForMediaPipe.Length; landmark++)
            {
                var offset = landmark * 3;
                hand.joints[QuestJointForMediaPipe[landmark]] = new Vector3(
                    -features[offset], -features[offset + 1], -features[offset + 2]) + worldOffset;
            }
        }

        static Vector2[] TeachingStroke(string label)
        {
            var output = new Vector2[28];
            for (var index = 0; index < output.Length; index++)
            {
                var phase = index / (output.Length - 1f);
                if (label == "Z")
                {
                    if (phase < .34f)
                        output[index] = Vector2.Lerp(new Vector2(-.25f, .24f), new Vector2(.25f, .24f), Ease(phase / .34f));
                    else if (phase < .67f)
                        output[index] = Vector2.Lerp(new Vector2(.25f, .24f), new Vector2(-.25f, -.28f), Ease((phase - .34f) / .33f));
                    else
                        output[index] = Vector2.Lerp(new Vector2(-.25f, -.28f), new Vector2(.25f, -.28f), Ease((phase - .67f) / .33f));
                }
                else if (phase < .62f)
                    output[index] = Vector2.Lerp(new Vector2(.15f, .24f), new Vector2(.15f, -.28f), Ease(phase / .62f));
                else
                {
                    var curl = Ease((phase - .62f) / .38f);
                    output[index] = new Vector2(Mathf.Lerp(.15f, -.23f, curl),
                        -.28f + Mathf.Sin(curl * Mathf.PI) * .14f);
                }
            }
            return output;
        }

        static float Ease(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        [Test]
        public void EveryRegionalReferenceRendersItsRecordedAndInterpolatedFrames()
        {
            var library = new SaslReferenceLibrary();
            var labels = new[]
            {
                "RESEARCH_SASL_CAPE_TOWN",
                "RESEARCH_SASL_BLOEMFONTEIN",
                "RESEARCH_SASL_PRETORIA"
            };
            var titles = new[] { "Cape Town", "Bloemfontein", "Pretoria" };
            var sources = new[] { "cape_town_01.mp4", "bloemfontein_01.mp4", "pretoria_01.mp4" };
            Assert.AreEqual(3, library.Count, "Cape Town and the other regional practice signs must load.");

            var output = new HandPoseFrame();
            for (var sign = 0; sign < labels.Length; sign++)
            {
                Assert.AreEqual(labels[sign], library.Label(sign));
                Assert.AreEqual(titles[sign], library.Title(sign));
                Assert.AreEqual(sources[sign], library.SourceFile(sign));

                // Each source contains 32 frames. Sample all recorded frames and
                // each in-between pose that the animated board displays.
                for (var frame = 0; frame < 32; frame++)
                {
                    AssertReferencePose(library, output, sign, frame / 31f, sources[sign]);
                    if (frame < 31)
                        AssertReferencePose(library, output, sign, (frame + .5f) / 31f, sources[sign]);
                }
            }

            library.GetPose(-1, 0f, output);
            Assert.IsFalse(output.tracked, "An invalid reference must never appear as a tracked hand.");
        }

        static void AssertReferencePose(SaslReferenceLibrary library, HandPoseFrame output,
            int sign, float phase, string sourceFile)
        {
            var movement = library.GetPose(sign, phase, output);
            Assert.IsTrue(output.tracked, $"{sourceFile} at phase {phase} must produce a visible hand.");
            Assert.AreEqual("SASL reference: " + sourceFile, output.source);
            Assert.LessOrEqual(Mathf.Abs(movement.x), .18f + .0001f);
            Assert.LessOrEqual(Mathf.Abs(movement.y), .12f + .0001f);
            Assert.AreEqual(HandPoseFrame.JointCount, output.joints.Length);
            foreach (var joint in output.joints)
            {
                Assert.IsFalse(float.IsNaN(joint.x) || float.IsInfinity(joint.x), sourceFile + " has invalid X.");
                Assert.IsFalse(float.IsNaN(joint.y) || float.IsInfinity(joint.y), sourceFile + " has invalid Y.");
                Assert.IsFalse(float.IsNaN(joint.z) || float.IsInfinity(joint.z), sourceFile + " has invalid Z.");
            }
        }
    }
}
