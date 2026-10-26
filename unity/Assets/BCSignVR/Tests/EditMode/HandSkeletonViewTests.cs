using NUnit.Framework;
using UnityEngine;

namespace BCSignVR.Tests
{
    public sealed class HandSkeletonViewTests
    {
        GameObject parent;

        [TearDown]
        public void TearDown()
        {
            if (parent != null) Object.DestroyImmediate(parent);
        }

        [TestCase(SimulatedHandPoseProvider.DemoPose.Open)]
        [TestCase(SimulatedHandPoseProvider.DemoPose.Fist)]
        [TestCase(SimulatedHandPoseProvider.DemoPose.Point)]
        public void CartoonLearnerHandRendersEveryDigitForEachWarmupPose(
            SimulatedHandPoseProvider.DemoPose pose)
        {
            parent = new GameObject("Hand view test root");
            var view = new HandSkeletonView("Learner hand test", parent.transform,
                new Color(.97f, .73f, .57f), true);

            view.SetLearnerPose(SimulatedHandPoseProvider.Template(pose), Vector3.zero,
                Quaternion.Euler(0, 180, 0), 1.24f);

            var root = parent.transform.Find("Learner hand test");
            Assert.IsNotNull(root);
            Assert.IsTrue(root.gameObject.activeSelf);
            var strokes = root.GetComponentsInChildren<LineRenderer>(true);
            Assert.AreEqual(22, strokes.Length, "Five smoothed fingers, five crease pairs, and two palm creases are expected.");
            foreach (var stroke in strokes)
            for (var point = 0; point < stroke.positionCount; point++)
            {
                var position = stroke.GetPosition(point);
                Assert.IsFalse(float.IsNaN(position.x) || float.IsInfinity(position.x));
                Assert.IsFalse(float.IsNaN(position.y) || float.IsInfinity(position.y));
                Assert.IsFalse(float.IsNaN(position.z) || float.IsInfinity(position.z));
            }

            var missing = SimulatedHandPoseProvider.Template(pose);
            missing.tracked = false;
            view.SetLearnerPose(missing, Vector3.zero, Quaternion.identity, 1.24f);
            Assert.IsFalse(root.gameObject.activeSelf, "The board must hide the learner hand when tracking is lost.");
        }

        [Test]
        public void FingerGeometryShrinksWithTheQuestBoard()
        {
            parent = new GameObject("Hand scale test");
            var cameraObject = new GameObject("Hand scale camera", typeof(Camera));
            cameraObject.transform.SetParent(parent.transform, false);
            cameraObject.transform.position = new Vector3(0, .5f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            var board = new GameObject("Scaled Quest board").transform;
            board.SetParent(parent.transform, false);
            var view = new HandSkeletonView("Scale hand", board, Color.white, true);
            var frame = SimulatedHandPoseProvider.Template(SimulatedHandPoseProvider.DemoPose.Open);
            view.SetLearnerPose(frame, Vector3.zero, Quaternion.identity, 1f);
            var line = board.Find("Scale hand/Scale hand hand fill 1").GetComponent<LineRenderer>();
            var desktopWidth = BakedWorldWidth(line, camera);

            board.localScale = Vector3.one * .28f;
            view.SetLearnerPose(frame, Vector3.zero, Quaternion.identity, 1f);
            var questWidth = BakedWorldWidth(line, camera);

            Assert.That(questWidth / desktopWidth, Is.EqualTo(.28f).Within(.015f),
                "Finger strokes must shrink with the palm and board in VR, otherwise they merge into blobs.");
        }

        static float BakedWorldWidth(LineRenderer line, Camera camera)
        {
            var mesh = new Mesh();
            try
            {
                line.BakeMesh(mesh, camera, false);
                Assert.Greater(mesh.vertexCount, 0);
                var min = float.PositiveInfinity;
                var max = float.NegativeInfinity;
                foreach (var vertex in mesh.vertices)
                {
                    var x = line.transform.TransformPoint(vertex).x;
                    min = Mathf.Min(min, x);
                    max = Mathf.Max(max, x);
                }
                return max - min;
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void BothBoardHandsFollowTheBoardsRotation()
        {
            parent = new GameObject("Rotated board");
            parent.transform.rotation = Quaternion.Euler(12f, 57f, 0);
            var reference = new HandSkeletonView("Reference", parent.transform, Color.white);
            var learner = new HandSkeletonView("Learner", parent.transform, Color.white);
            var pose = SimulatedHandPoseProvider.Template(SimulatedHandPoseProvider.DemoPose.Open);
            var localRotation = Quaternion.Euler(0, 180, 0);
            reference.SetPose(pose, Vector3.zero, localRotation, 1f);
            learner.SetLearnerPose(pose, Vector3.zero, localRotation, 1f);
            var expected = parent.transform.rotation * localRotation;
            Assert.Less(Quaternion.Angle(expected, parent.transform.Find("Reference").rotation), .01f);
            Assert.Less(Quaternion.Angle(expected, parent.transform.Find("Learner").rotation), .01f);
        }

        [Test]
        public void RegionalReferenceFingerWidthsAdaptToRecordedKnuckleSpread()
        {
            parent = new GameObject("Regional hand view test root");
            var frame = new HandPoseFrame { tracked = true, source = "SASL reference: cape_town_01.mp4" };
            frame.joints[0] = Vector3.zero;
            var starts = new[] { 6, 11, 16, 21 };
            for (var finger = 0; finger < starts.Length; finger++)
            {
                var x = (finger - 1.5f) * .08f;
                var start = starts[finger];
                frame.joints[start] = new Vector3(x, .25f, 0);
                frame.joints[start + 1] = new Vector3(x, .38f, 0);
                frame.joints[start + 2] = new Vector3(x, .58f, 0);
                frame.joints[start + 3] = new Vector3(x, .78f, 0);
                frame.joints[start + 4] = new Vector3(x, .98f, 0);
            }
            frame.joints[2] = new Vector3(.12f, .15f, 0);
            frame.joints[3] = new Vector3(.22f, .25f, 0);
            frame.joints[4] = new Vector3(.30f, .34f, 0);
            frame.joints[5] = new Vector3(.37f, .43f, 0);

            var view = new HandSkeletonView("Regional hand test", parent.transform,
                new Color(.97f, .73f, .57f), true);
            view.SetPose(frame, Vector3.zero, Quaternion.identity, 1f);

            var root = parent.transform.Find("Regional hand test");
            var middleFinger = root.Find("Regional hand test hand fill 2").GetComponent<LineRenderer>();
            Assert.That(middleFinger.startWidth, Is.EqualTo(.0672f).Within(.003f),
                "The illustrated fingers should scale to the source hand's knuckle width, not merge into a blob.");
        }

        [Test]
        public void IdenticalPosesHaveIdenticalAnatomyAcrossTrackingSources()
        {
            parent = new GameObject("Source-independent anatomy");
            var pose = SimulatedHandPoseProvider.Template(SimulatedHandPoseProvider.DemoPose.Open);
            Vector3? palmScale = null;
            float? fingerWidth = null;
            foreach (var source in new[] { "Quest/OpenXR right hand", "Mac webcam", "SASL reference: cape_town_01.mp4" })
            {
                pose.source = source;
                var view = new HandSkeletonView(source, parent.transform, Color.white);
                view.SetLearnerPose(pose, Vector3.zero, Quaternion.identity, 1f);
                // Names contain slashes, so find the direct child by index.
                var root = parent.transform.GetChild(parent.transform.childCount - 1);
                var palm = root.Find("Bob hand palm");
                var finger = root.GetComponentsInChildren<LineRenderer>()[3];
                Assert.AreEqual(13, finger.positionCount, "Visible fingers have three segments, not the bone inside the palm.");
                if (palmScale.HasValue)
                {
                    Assert.That(Vector3.Distance(palmScale.Value, palm.localScale), Is.LessThan(.0001f));
                    Assert.That(finger.startWidth, Is.EqualTo(fingerWidth.Value).Within(.0001f));
                }
                palmScale = palm.localScale;
                fingerWidth = finger.startWidth;
            }
        }

        [Test]
        public void BobStartsWithTheRequestedSignGreeting()
        {
            parent = new GameObject("Welcome test");
            var bob = TutorAvatar.Build(parent.transform, Vector3.zero);
            var speech = bob.GetComponentInChildren<TextMesh>().text.Replace('\n', ' ');
            Assert.AreEqual("Welcome! Let's learn one hand sign at a time.", speech);
        }
    }
}
