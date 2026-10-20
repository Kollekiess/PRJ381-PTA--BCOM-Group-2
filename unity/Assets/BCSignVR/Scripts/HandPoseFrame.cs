using System;
using UnityEngine;

namespace BCSignVR
{
    [Serializable]
    public sealed class HandPoseFrame
    {
        public const int JointCount = 26;
        public readonly Vector3[] joints = new Vector3[JointCount];
        public Quaternion wristRotation = Quaternion.identity;
        public bool tracked;
        public string source = "None";

        public HandPoseFrame Clone()
        {
            var copy = new HandPoseFrame
            {
                wristRotation = wristRotation,
                tracked = tracked,
                source = source
            };
            Array.Copy(joints, copy.joints, JointCount);
            return copy;
        }

        public float[] NormalizedFeatures()
        {
            var features = new float[JointCount * 3];
            var wrist = joints[0];
            var inverseWrist = Quaternion.Inverse(wristRotation);
            var scale = 0f;
            for (var i = 1; i < JointCount; i++)
                scale = Mathf.Max(scale, Vector3.Distance(wrist, joints[i]));
            scale = Mathf.Max(scale, 0.0001f);
            for (var i = 0; i < JointCount; i++)
            {
                var local = inverseWrist * (joints[i] - wrist) / scale;
                features[i * 3] = local.x;
                features[i * 3 + 1] = local.y;
                features[i * 3 + 2] = local.z;
            }
            return features;
        }
    }

    public interface IHandPoseProvider
    {
        bool TryGetRightHand(HandPoseFrame destination);
        string DisplayName { get; }
    }
}

