using NUnit.Framework;
using UnityEngine;

namespace BCSignVR.Tests
{
    public sealed class HandPoseCoachTests
    {
        const float Accept = .78f;

        [TestCase(SimulatedHandPoseProvider.DemoPose.Open)]
        [TestCase(SimulatedHandPoseProvider.DemoPose.Fist)]
        [TestCase(SimulatedHandPoseProvider.DemoPose.Point)]
        public void EachDemoPoseMatchesOnlyItsOwnWarmup(SimulatedHandPoseProvider.DemoPose pose)
        {
            var frame = SimulatedHandPoseProvider.Template(pose);
            for (var target = 0; target < 3; target++)
            {
                var score = HandPoseCoach.Match(frame, (SimulatedHandPoseProvider.DemoPose)target);
                if (target == (int)pose) Assert.GreaterOrEqual(score, Accept);
                else Assert.Less(score, Accept, $"{pose} must not pass target {target}");
            }
        }

        [TestCase(SimulatedHandPoseProvider.DemoPose.Open, .65f)]
        [TestCase(SimulatedHandPoseProvider.DemoPose.Open, 1.4f)]
        [TestCase(SimulatedHandPoseProvider.DemoPose.Fist, .65f)]
        [TestCase(SimulatedHandPoseProvider.DemoPose.Fist, 1.4f)]
        [TestCase(SimulatedHandPoseProvider.DemoPose.Point, .65f)]
        [TestCase(SimulatedHandPoseProvider.DemoPose.Point, 1.4f)]
        public void ArticulatedHandsWithDifferentProportionsPassTheirWarmup(
            SimulatedHandPoseProvider.DemoPose pose, float width)
        {
            var frame = ArticulatedHand(pose, width);
            Assert.GreaterOrEqual(HandPoseCoach.Match(frame, pose), Accept);
            for (var target = 0; target < 3; target++)
                if (target != (int)pose)
                    Assert.Less(HandPoseCoach.Match(frame, (SimulatedHandPoseProvider.DemoPose)target), Accept);
        }

        [TestCase(SimulatedHandPoseProvider.DemoPose.Open, .03f)]
        [TestCase(SimulatedHandPoseProvider.DemoPose.Fist, 4f)]
        [TestCase(SimulatedHandPoseProvider.DemoPose.Point, .4f)]
        public void TranslationRotationAndHandSizeDoNotChangeMatch(
            SimulatedHandPoseProvider.DemoPose pose, float scale)
        {
            var frame = ArticulatedHand(pose, 1.2f);
            var original = HandPoseCoach.Match(frame, pose);
            var rotation = Quaternion.Euler(123f, -71f, 48f);
            for (var joint = 0; joint < frame.joints.Length; joint++)
                frame.joints[joint] = rotation * (frame.joints[joint] * scale) + new Vector3(.4f, -.7f, 1.2f);
            // A webcam does not provide a wrist quaternion. Geometry must suffice.
            frame.wristRotation = Quaternion.identity;
            Assert.AreEqual(original, HandPoseCoach.Match(frame, pose), .001f);
            Assert.GreaterOrEqual(HandPoseCoach.Match(frame, pose), Accept);
        }

        [Test]
        public void OneIncorrectFingerPreventsOpenHandSuccess()
        {
            var open = ArticulatedHand(SimulatedHandPoseProvider.DemoPose.Open, 1f);
            var fist = ArticulatedHand(SimulatedHandPoseProvider.DemoPose.Fist, 1f);
            for (var joint = 16; joint <= 20; joint++) open.joints[joint] = fist.joints[joint];
            Assert.Less(HandPoseCoach.Match(open, SimulatedHandPoseProvider.DemoPose.Open), Accept);
        }

        [Test]
        public void TrackingLossAndInvalidGeometryNeverMatch()
        {
            Assert.Zero(HandPoseCoach.Match(null, SimulatedHandPoseProvider.DemoPose.Open));
            var frame = ArticulatedHand(SimulatedHandPoseProvider.DemoPose.Open, 1f);
            frame.tracked = false;
            Assert.Zero(HandPoseCoach.Match(frame, SimulatedHandPoseProvider.DemoPose.Open));
            frame.tracked = true;
            frame.joints[8] = frame.joints[7];
            Assert.Zero(HandPoseCoach.Match(frame, SimulatedHandPoseProvider.DemoPose.Open));
            frame.joints[8] = new Vector3(float.NaN, 0, 0);
            Assert.Zero(HandPoseCoach.Match(frame, SimulatedHandPoseProvider.DemoPose.Open));
        }

        static HandPoseFrame ArticulatedHand(SimulatedHandPoseProvider.DemoPose pose, float width)
        {
            var frame = SimulatedHandPoseProvider.Template(pose);
            var x = new[] { -.039f, -.013f, .013f, .036f };
            var y = new[] { .073f, .080f, .077f, .065f };
            for (var finger = 0; finger < 4; finger++)
            {
                var start = 6 + 5 * finger;
                var knuckle = new Vector3(x[finger] * width, y[finger], .004f * finger);
                frame.joints[start] = knuckle * .55f;
                frame.joints[start + 1] = knuckle;
                var extends = pose == SimulatedHandPoseProvider.DemoPose.Open ||
                    pose == SimulatedHandPoseProvider.DemoPose.Point && finger == 0;
                var bend = extends ? new[] { 10f, 9f, 7f } : new[] { 60f, 100f, 60f };
                var lengths = new[] { .035f, .025f, .018f };
                var direction = (knuckle - frame.joints[start]).normalized;
                var curlAxis = Vector3.Cross(direction, Vector3.back).normalized;
                var angle = 0f;
                for (var segment = 0; segment < 3; segment++)
                {
                    angle += bend[segment];
                    var segmentDirection = Quaternion.AngleAxis(angle, curlAxis) * direction;
                    frame.joints[start + segment + 2] = frame.joints[start + segment + 1] +
                        segmentDirection * lengths[segment] * (1.12f - .08f * finger);
                }
            }
            return frame;
        }
    }
}
