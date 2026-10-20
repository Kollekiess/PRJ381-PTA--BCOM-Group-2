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
            var subsystem = HandTracking.subsystem;
            if (subsystem == null || !subsystem.running || !subsystem.rightHand.isTracked)
                return false;

            var hand = subsystem.rightHand;
            for (var index = 0; index < HandPoseFrame.JointCount; index++)
            {
                var jointId = XRHandJointIDUtility.FromIndex(index);
                if (!hand.GetJoint(jointId).TryGetPose(out var pose))
                    return false;
                destination.joints[index] = pose.position;
                if (index == 0)
                    destination.wristRotation = pose.rotation;
            }
            destination.tracked = true;
            destination.source = DisplayName;
            return true;
        }
    }
}

