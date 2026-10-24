using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.OpenXR;

namespace BCSignVR
{
    public sealed class OpenXRHandPoseProvider : IHandPoseProvider
    {
        public string DisplayName => "Quest/OpenXR right hand";

        public bool TryGetRightHand(HandPoseFrame destination)
        {
            destination.tracked = false;
            destination.source = DisplayName;
            var subsystem = HandTracking.subsystem;
            if (subsystem == null || !subsystem.running || !subsystem.rightHand.isTracked)
                return false;

            var hand = subsystem.rightHand;
            for (var index = 0; index < HandPoseFrame.JointCount; index++)
            {
                var jointId = XRHandJointIDUtility.FromIndex(index);
                if (!hand.GetJoint(jointId).TryGetPose(out var pose))
                    return false;
                if (!Finite(pose.position) || !Finite(pose.rotation) || QuaternionLengthSquared(pose.rotation) < .0001f)
                    return false;
                destination.joints[index] = pose.position;
                if (index == 0)
                    destination.wristRotation = pose.rotation;
            }
            destination.tracked = true;
            destination.source = DisplayName;
            return true;
        }

        static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        static bool Finite(Quaternion value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z) &&
            !float.IsNaN(value.w) && !float.IsInfinity(value.w);

        static float QuaternionLengthSquared(Quaternion value) =>
            value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
    }
}

