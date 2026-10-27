using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.OpenXR;
using UnityEngine.XR.Management;

namespace BCSignVR
{
    public sealed class OpenXRHandPoseProvider : IHandPoseProvider
    {
        const float JointDropoutGraceSeconds = .12f;
        readonly Vector3[] lastJoints = new Vector3[HandPoseFrame.JointCount];
        Quaternion lastWristRotation = Quaternion.identity;
        float lastCompleteTrackingAt = -Mathf.Infinity;
        bool hasCachedPose;

        public string DisplayName => "Quest/OpenXR right hand";
        // A running OpenXR hand subsystem means the headset/simulator session is
        // actually available. A headset being physically plugged in is not
        // enough by itself: the runtime must have accepted the connection.
        public bool IsConnected
        {
            get
            {
                var subsystem = HandTracking.subsystem;
                // XRSettings becomes active as soon as OpenXR has accepted the
                // headset/simulator. Hand tracking can take a moment longer to
                // start, but Webcam must unlock as soon as the VR connection is
                // real rather than waiting for the first visible hand.
                var loaderActive = XRGeneralSettings.Instance?.Manager?.activeLoader != null;
                return loaderActive || XRSettings.isDeviceActive || subsystem != null && subsystem.running;
            }
        }

        public bool TryGetRightHand(HandPoseFrame destination)
        {
            var subsystem = HandTracking.subsystem;
            if (subsystem == null || !subsystem.running)
                return false;

            var hand = subsystem.rightHand;
            if (!hand.isTracked)
                return TryUseRecentPose(destination);

            var allJointsAvailable = true;
            var anyJointAvailable = false;
            for (var index = 0; index < HandPoseFrame.JointCount; index++)
            {
                var jointId = XRHandJointIDUtility.FromIndex(index);
                if (hand.GetJoint(jointId).TryGetPose(out var pose))
                {
                    destination.joints[index] = pose.position;
                    lastJoints[index] = pose.position;
                    anyJointAvailable = true;
                    if (index == 0)
                    {
                        destination.wristRotation = pose.rotation;
                        lastWristRotation = pose.rotation;
                    }
                }
                else if (hasCachedPose)
                {
                    destination.joints[index] = lastJoints[index];
                    if (index == 0) destination.wristRotation = lastWristRotation;
                    allJointsAvailable = false;
                }
                else
                {
                    allJointsAvailable = false;
                }
            }
            if (!anyJointAvailable || !allJointsAvailable && !hasCachedPose)
                return false;
            if (allJointsAvailable) lastCompleteTrackingAt = Time.unscaledTime;
            hasCachedPose = true;
            destination.tracked = true;
            destination.source = DisplayName;
            return true;
        }

        bool TryUseRecentPose(HandPoseFrame destination)
        {
            if (!hasCachedPose || Time.unscaledTime - lastCompleteTrackingAt > JointDropoutGraceSeconds)
                return false;
            System.Array.Copy(lastJoints, destination.joints, HandPoseFrame.JointCount);
            destination.wristRotation = lastWristRotation;
            destination.tracked = true;
            destination.source = DisplayName;
            return true;
        }
    }
}

