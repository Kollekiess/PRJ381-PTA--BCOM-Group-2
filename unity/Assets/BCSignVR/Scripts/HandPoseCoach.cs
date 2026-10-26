using UnityEngine;

namespace BCSignVR
{
    /// <summary>
    /// Geometry checks for the three tracking warm-ups. Scores describe finger
    /// extension, not SASL recognition or a learned probability of correctness.
    /// </summary>
    public static class HandPoseCoach
    {
        static readonly int[] FingerStarts = { 6, 11, 16, 21 };
        static readonly string[] FingerNames = { "index", "middle", "ring", "little" };

        public static string Hint(HandPoseFrame current, SimulatedHandPoseProvider.DemoPose target)
        {
            if (current == null || !current.tracked) return "Raise your full hand so all fingers can be tracked.";
            var weakest = 1f;
            var hint = "Keep your whole hand visible and hold steady.";
            var thumbShouldExtend = target != SimulatedHandPoseProvider.DemoPose.Fist;
            if (TryThumbExtension(current, out var thumbExtension))
            {
                var thumbMatch = thumbShouldExtend ? thumbExtension : 1f - thumbExtension;
                if (thumbMatch < weakest)
                {
                    weakest = thumbMatch;
                    hint = thumbShouldExtend
                        ? "Move your thumb away from your palm and keep it open."
                        : "Lower your thumb and tuck it across your palm.";
                }
            }
            for (var i = 0; i < FingerStarts.Length; i++)
            {
                if (!TryExtension(current, FingerStarts[i], out var extension))
                    return "Move back a little so all your fingers are visible.";
                var extend = target == SimulatedHandPoseProvider.DemoPose.Open ||
                    target == SimulatedHandPoseProvider.DemoPose.Point && i == 0;
                var match = extend ? extension : 1 - extension;
                if (match >= weakest) continue;
                weakest = match;
                hint = (extend ? "Straighten your " : "Gently curl your ") + FingerNames[i] +
                    (FingerNames[i] == "little" ? " (pinky)" : "") + " finger.";
            }
            return hint;
        }

        public static float Match(HandPoseFrame current, SimulatedHandPoseProvider.DemoPose target)
        {
            if (current == null || !current.tracked ||
                target < SimulatedHandPoseProvider.DemoPose.Open ||
                target > SimulatedHandPoseProvider.DemoPose.Point)
                return 0f;
            foreach (var joint in current.joints)
                if (!Finite(joint.x) || !Finite(joint.y) || !Finite(joint.z))
                    return 0f;

            var sum = 0f;
            var weakest = 1f;
            for (var finger = 0; finger < FingerStarts.Length; finger++)
            {
                if (!TryExtension(current, FingerStarts[finger], out var extension))
                    return 0f;
                var shouldExtend = target == SimulatedHandPoseProvider.DemoPose.Open ||
                    target == SimulatedHandPoseProvider.DemoPose.Point && finger == 0;
                var match = shouldExtend ? extension : 1f - extension;
                sum += match;
                weakest = Mathf.Min(weakest, match);
            }

            // Requiring every finger prevents a pointing hand from passing open
            // hand merely because three other joints happen to be similar.
            // The thumb is left comfortable during these tracking warm-ups;
            // sign-specific thumb placement belongs to reviewed SASL content.
            return Mathf.Clamp01(.6f * (sum / FingerStarts.Length) + .4f * weakest);
        }

        static bool TryExtension(HandPoseFrame frame, int start, out float extension)
        {
            extension = 0f;
            var metacarpal = frame.joints[start];
            var knuckle = frame.joints[start + 1];
            var proximal = frame.joints[start + 2] - knuckle;
            var middle = frame.joints[start + 3] - frame.joints[start + 2];
            var distal = frame.joints[start + 4] - frame.joints[start + 3];
            var palm = knuckle - metacarpal;
            var length = proximal.magnitude + middle.magnitude + distal.magnitude;
            if (!Finite(length) || palm.sqrMagnitude < 1e-12f ||
                proximal.sqrMagnitude < 1e-12f || middle.sqrMagnitude < 1e-12f || distal.sqrMagnitude < 1e-12f)
                return false;

            var bend = Vector3.Angle(palm, proximal) + Vector3.Angle(proximal, middle) +
                Vector3.Angle(middle, distal);
            var reach = Vector3.Distance(knuckle, frame.joints[start + 4]) / length;
            // Joint angles detect curl at the knuckle as well as the two finger
            // joints. Reach adds tolerance for tracking noise and hand anatomy.
            var straightFromBend = 1f - Mathf.InverseLerp(50f, 170f, bend);
            var straightFromReach = Mathf.InverseLerp(.45f, .90f, reach);
            extension = Mathf.Clamp01(.7f * straightFromBend + .3f * straightFromReach);
            return Finite(extension);
        }

        static bool TryThumbExtension(HandPoseFrame frame, out float extension)
        {
            extension = 0f;
            var first = frame.joints[3] - frame.joints[2];
            var second = frame.joints[4] - frame.joints[3];
            var third = frame.joints[5] - frame.joints[4];
            var length = first.magnitude + second.magnitude + third.magnitude;
            if (!Finite(length) || first.sqrMagnitude < 1e-12f ||
                second.sqrMagnitude < 1e-12f || third.sqrMagnitude < 1e-12f)
                return false;

            var bend = Vector3.Angle(first, second) + Vector3.Angle(second, third);
            var straightFromBend = 1f - Mathf.InverseLerp(35f, 155f, bend);
            var straightFromReach = Mathf.InverseLerp(.42f, .88f,
                Vector3.Distance(frame.joints[2], frame.joints[5]) / length);
            extension = Mathf.Clamp01(.7f * straightFromBend + .3f * straightFromReach);
            return Finite(extension);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
