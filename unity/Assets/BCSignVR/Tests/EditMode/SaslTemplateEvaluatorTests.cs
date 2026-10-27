using System;
using NUnit.Framework;
using UnityEngine;

namespace BCSignVR.Tests
{
    public sealed class SaslTemplateEvaluatorTests
    {
        [Serializable] sealed class Collection { public Item[] templates; }
        [Serializable] sealed class Item { public string label; public float[] features; }

        [Test]
        public void BundledAlphabetTemplatesLoadAsACompleteSet()
        {
            var asset = Resources.Load<TextAsset>("SaslTemplates");
            var evaluator = new SaslTemplateEvaluator(asset);
            Assert.That(asset, Is.Not.Null);
            Assert.That(evaluator.IsReady, Is.True);
        }

        [Test]
        public void EveryBundledLetterRoundTripsThroughTheQuestJointMap()
        {
            var asset = Resources.Load<TextAsset>("SaslTemplates");
            var collection = JsonUtility.FromJson<Collection>(asset.text);
            var evaluator = new SaslTemplateEvaluator(asset);

            foreach (var item in collection.templates)
            {
                var frame = FrameFromWebcamFeatures(item.features);
                var score = evaluator.Evaluate(frame);
                Assert.That(score.label, Is.EqualTo(item.label), item.label);
                Assert.That(score.confidence, Is.GreaterThan(.80f), item.label);
            }
        }

        static HandPoseFrame FrameFromWebcamFeatures(float[] features)
        {
            Assert.That(features, Has.Length.EqualTo(63));
            var points = new Vector3[21];
            for (var i = 0; i < points.Length; i++)
                points[i] = new Vector3(features[i * 3], features[i * 3 + 1], features[i * 3 + 2]);

            var frame = new HandPoseFrame { tracked = true, wristRotation = Quaternion.identity };
            frame.joints[0] = points[0];
            frame.joints[2] = points[1];
            frame.joints[3] = points[2];
            frame.joints[4] = points[3];
            frame.joints[5] = points[4];
            CopyFinger(frame, points, 5, 6);
            CopyFinger(frame, points, 9, 11);
            CopyFinger(frame, points, 13, 16);
            CopyFinger(frame, points, 17, 21);
            return frame;
        }

        static void CopyFinger(HandPoseFrame frame, Vector3[] points, int sourceStart, int targetStart)
        {
            frame.joints[targetStart] = Vector3.Lerp(points[0], points[sourceStart], .55f);
            for (var i = 0; i < 4; i++) frame.joints[targetStart + 1 + i] = points[sourceStart + i];
        }
    }
}
