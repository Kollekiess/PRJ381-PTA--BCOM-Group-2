using UnityEngine;

namespace BCSignVR
{
    public sealed class SimulatedHandPoseProvider : IHandPoseProvider
    {
        public enum DemoPose { Open, Fist, Point }

        public DemoPose CurrentPose { get; set; } = DemoPose.Open;
        public string DisplayName => "Desktop hand simulator";

        public bool TryGetRightHand(HandPoseFrame destination)
        {
            BuildPose(CurrentPose, destination);
            destination.tracked = true;
            destination.source = DisplayName;
            return true;
        }

        public static HandPoseFrame Template(DemoPose pose)
        {
            var frame = new HandPoseFrame();
            BuildPose(pose, frame);
            frame.tracked = true;
            frame.source = "Prototype template";
            return frame;
        }

        static void BuildPose(DemoPose pose, HandPoseFrame frame)
        {
            frame.wristRotation = Quaternion.identity;
            frame.joints[0] = Vector3.zero;
            frame.joints[1] = new Vector3(0f, 0.055f, 0f);

            BuildThumb(frame.joints, pose == DemoPose.Fist);
            BuildFinger(frame.joints, 6, -0.032f, 0.058f, pose != DemoPose.Fist, pose == DemoPose.Point);
            BuildFinger(frame.joints, 11, -0.011f, 0.064f, pose == DemoPose.Open, false);
            BuildFinger(frame.joints, 16, 0.012f, 0.061f, pose == DemoPose.Open, false);
            BuildFinger(frame.joints, 21, 0.032f, 0.052f, pose == DemoPose.Open, false);
        }

        static void BuildThumb(Vector3[] joints, bool curled)
        {
            joints[2] = new Vector3(-0.038f, 0.028f, 0f);
            if (!curled)
            {
                joints[3] = new Vector3(-0.056f, 0.046f, 0f);
                joints[4] = new Vector3(-0.069f, 0.061f, 0f);
                joints[5] = new Vector3(-0.079f, 0.072f, 0f);
            }
            else
            {
                joints[3] = new Vector3(-0.046f, 0.045f, -0.006f);
                joints[4] = new Vector3(-0.026f, 0.055f, -0.012f);
                joints[5] = new Vector3(-0.004f, 0.057f, -0.014f);
            }
        }

        static void BuildFinger(Vector3[] joints, int start, float x, float y, bool extended, bool forcedPoint)
        {
            joints[start] = new Vector3(x, y, 0f);
            if (extended || forcedPoint)
            {
                joints[start + 1] = new Vector3(x, y + 0.030f, 0f);
                joints[start + 2] = new Vector3(x, y + 0.055f, 0f);
                joints[start + 3] = new Vector3(x, y + 0.077f, 0f);
                joints[start + 4] = new Vector3(x, y + 0.096f, 0f);
            }
            else
            {
                joints[start + 1] = new Vector3(x, y + 0.022f, -0.005f);
                joints[start + 2] = new Vector3(x, y + 0.029f, -0.023f);
                joints[start + 3] = new Vector3(x, y + 0.014f, -0.037f);
                joints[start + 4] = new Vector3(x, y - 0.005f, -0.031f);
            }
        }
    }
}

