using UnityEngine;

namespace BCSignVR
{
    /// <summary>
    /// Presents webcam/VR landmarks as a friendly Bob-style hand. The source
    /// remains the real landmark stream; this layer only changes presentation
    /// so the learner never sees a blue/green debugging skeleton.
    /// </summary>
    public sealed class HandSkeletonView
    {
        static readonly int[,] Bones =
        {
            {0,1},{0,2},{2,3},{3,4},{4,5},
            {1,6},{6,7},{7,8},{8,9},{9,10},
            {1,11},{11,12},{12,13},{13,14},{14,15},
            {1,16},{16,17},{17,18},{18,19},{19,20},
            {1,21},{21,22},{22,23},{23,24},{24,25}
        };

        readonly Transform root;
        readonly Transform[] joints = new Transform[HandPoseFrame.JointCount];
        readonly Transform[] jointOutlines = new Transform[HandPoseFrame.JointCount];
        readonly Transform[] bones = new Transform[Bones.GetLength(0)];
        readonly Transform[] boneOutlines = new Transform[Bones.GetLength(0)];
        readonly Vector3[] smoothedPoints = new Vector3[HandPoseFrame.JointCount];
        readonly Vector3[] displayPoints = new Vector3[HandPoseFrame.JointCount];
        bool poseInitialized;
        readonly Material outlineMaterial;
        readonly Material fillMaterial;
        readonly Material highlightMaterial;
        readonly Transform palm;
        readonly Transform palmOutline;
        readonly Transform wrist;
        readonly Transform wristOutline;
        readonly Transform highlight;

        public HandSkeletonView(string name, Transform parent, Color color, bool solid = false)
        {
            root = new GameObject(name).transform;
            root.SetParent(parent, false);
            // Match Bob's original hand asset: bright peach fill, a restrained
            // orange-brown contour, and a soft highlight. Keep the fill
            // unlit so a reversed camera-facing hand never becomes muddy.
            var bobPeach = color;
            outlineMaterial = ClassroomFactory.Material(new Color(
                bobPeach.r * .66f, bobPeach.g * .34f, bobPeach.b * .25f));
            fillMaterial = ClassroomFactory.Material(bobPeach);
            highlightMaterial = ClassroomFactory.Material(new Color(
                Mathf.Min(1f, bobPeach.r + .07f),
                Mathf.Min(1f, bobPeach.g + .12f),
                Mathf.Min(1f, bobPeach.b + .10f)));

            for (var i = 0; i < joints.Length; i++)
            {
                jointOutlines[i] = Ball($"Bob hand outline joint {i}", outlineMaterial);
                joints[i] = Ball($"Bob hand joint {i}", fillMaterial);
                // Do not show landmark beads. Keeping the transforms hidden
                // lets the rounded capsules use them as anchors without
                // exposing a technical skeleton to the learner.
                jointOutlines[i].gameObject.SetActive(false);
                joints[i].gameObject.SetActive(false);
            }
            for (var i = 0; i < bones.Length; i++)
            {
                boneOutlines[i] = Capsule($"Bob hand outline finger {i}", outlineMaterial);
                bones[i] = Capsule($"Bob hand finger {i}", fillMaterial);
            }

            palmOutline = Ball("Bob hand outline palm", outlineMaterial);
            palm = Ball("Bob hand palm", fillMaterial);
            wristOutline = Ball("Bob hand outline wrist", outlineMaterial);
            wrist = Ball("Bob hand wrist", fillMaterial);
            highlight = Ball("Bob hand soft highlight", highlightMaterial);
        }

        public void SetPose(HandPoseFrame frame, Vector3 position, Quaternion rotation, float scale = 3f)
        {
            SetPoseInternal(frame, position, rotation, scale, false);
        }

        public void SetLearnerPose(HandPoseFrame frame, Vector3 position, Quaternion rotation, float scale = 3f)
        {
            SetPoseInternal(frame, position, rotation, scale, true);
        }

        void SetPoseInternal(HandPoseFrame frame, Vector3 position, Quaternion rotation, float scale, bool boardLocalRotation)
        {
            root.gameObject.SetActive(frame.tracked);
            if (!frame.tracked) { poseInitialized = false; return; }

            // Keep the learner hand just in front of the board card. The
            // board is rotated in the room, so a fixed local-z offset can put
            // the fill behind the card on one Quest eye and leave only the
            // dark contour visible.
            var camera = Camera.main;
            if (camera != null)
            {
                var towardCamera = camera.transform.position - position;
                root.position = towardCamera.sqrMagnitude > .0001f
                    ? position + towardCamera.normalized * .035f
                    : position;
            }
            else root.position = position;
            if (boardLocalRotation) root.localRotation = rotation;
            else root.rotation = rotation;
            BuildDisplayPoints(frame, displayPoints);
            var blend = poseInitialized ? 1f - Mathf.Exp(-24f * Mathf.Max(Time.unscaledDeltaTime, .001f)) : 1f;
            for (var i = 0; i < joints.Length; i++)
            {
                var targetPoint = displayPoints[i] * scale;
                var point = poseInitialized ? Vector3.Lerp(smoothedPoints[i], targetPoint, blend) : targetPoint;
                smoothedPoints[i] = point;
                joints[i].localPosition = point;
                jointOutlines[i].localPosition = point + Vector3.forward * .018f;
                joints[i].localPosition = point - Vector3.forward * .05f;
                var radius = JointRadius(i);
                joints[i].localScale = Vector3.one * radius;
                jointOutlines[i].localScale = Vector3.one * radius * .86f;
            }

            for (var i = 0; i < bones.Length; i++)
            {
                var a = joints[Bones[i, 0]].localPosition;
                var b = joints[Bones[i, 1]].localPosition;
                SetCapsule(boneOutlines[i], a, b, SegmentRadius(i) * 1.07f, DepthOffset(.016f));
                SetCapsule(bones[i], a, b, SegmentRadius(i), DepthOffset(.032f));
            }

            var wristPoint = joints[0].localPosition;
            var indexBase = joints[6].localPosition;
            var middleBase = joints[11].localPosition;
            var ringBase = joints[16].localPosition;
            var littleBase = joints[21].localPosition;
            var knuckles = (indexBase + middleBase + ringBase + littleBase) * .25f;
            var up = knuckles - wristPoint;
            if (up.sqrMagnitude < .0001f) up = Vector3.up * .15f;
            var direction = up.normalized;
            var horizontal = new Vector3(-direction.y, direction.x, 0f);
            var fingerSpan = Vector3.Distance(indexBase, littleBase);
            var palmCenter = wristPoint + up * .54f;
            SetOval(palmOutline, palmCenter, horizontal, direction, DepthOffset(.016f),
                fingerSpan * .84f + .13f, up.magnitude * .76f + .12f);
            SetOval(palm, palmCenter, horizontal, direction, DepthOffset(.032f),
                fingerSpan * .79f + .10f, up.magnitude * .71f + .07f);
            SetOval(wristOutline, wristPoint - direction * .06f, horizontal, direction,
                DepthOffset(.016f), .30f, .27f);
            SetOval(wrist, wristPoint - direction * .06f, horizontal, direction,
                DepthOffset(.032f), .27f, .23f);
            SetOval(highlight, palmCenter + horizontal * (fingerSpan * -.16f) + direction * (up.magnitude * .20f),
                horizontal, direction, DepthOffset(.038f), .14f, .07f);
            poseInitialized = true;
        }

        static void BuildDisplayPoints(HandPoseFrame frame, Vector3[] output)
        {
            var wrist = frame.joints[0];
            var knuckles = (frame.joints[6] + frame.joints[11] + frame.joints[16] + frame.joints[21]) * .25f;
            var vertical = knuckles - wrist;
            if (vertical.sqrMagnitude < .0001f) vertical = Vector3.up;
            vertical.Normalize();

            // Project the real 3D landmarks into a stable front-facing hand
            // plane. Using wristRotation here made a sideways hand collapse
            // into a blob when it was rendered on the flat lesson card.
            var horizontal = frame.joints[21] - frame.joints[6];
            horizontal = Vector3.ProjectOnPlane(horizontal, vertical);
            if (horizontal.sqrMagnitude < .0001f)
                horizontal = Vector3.Cross(vertical, Vector3.forward);
            if (horizontal.sqrMagnitude < .0001f)
                horizontal = Vector3.right;
            horizontal.Normalize();

            var handScale = .0001f;
            for (var i = 1; i < HandPoseFrame.JointCount; i++)
                handScale = Mathf.Max(handScale, Vector3.Distance(wrist, frame.joints[i]));
            for (var i = 0; i < HandPoseFrame.JointCount; i++)
            {
                var delta = frame.joints[i] - wrist;
                output[i] = new Vector3(Vector3.Dot(delta, horizontal), Vector3.Dot(delta, vertical), 0f) / handScale;
            }
        }

        Transform Ball(string name, Material material)
        {
            var shape = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shape.name = name;
            Object.Destroy(shape.GetComponent<Collider>());
            shape.transform.SetParent(root, false);
            shape.GetComponent<Renderer>().sharedMaterial = material;
            return shape.transform;
        }

        Transform Capsule(string name, Material material)
        {
            var shape = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            shape.name = name;
            Object.Destroy(shape.GetComponent<Collider>());
            shape.transform.SetParent(root, false);
            shape.GetComponent<Renderer>().sharedMaterial = material;
            return shape.transform;
        }

        static void SetCapsule(Transform capsule, Vector3 start, Vector3 end, float radius, Vector3 depthOffset)
        {
            var delta = end - start;
            capsule.localPosition = (start + end) * .5f + depthOffset;
            capsule.localRotation = Quaternion.FromToRotation(Vector3.up, delta);
            capsule.localScale = new Vector3(radius * 2f,
                Mathf.Max(radius, delta.magnitude * .5f + radius), radius * 2f);
        }

        static void SetOval(Transform shape, Vector3 position, Vector3 horizontal, Vector3 vertical,
            Vector3 depthOffset, float width, float height)
        {
            shape.localPosition = position + depthOffset;
            shape.localRotation = Quaternion.FromToRotation(Vector3.up, vertical);
            shape.localScale = new Vector3(Mathf.Max(.02f, width), Mathf.Max(.02f, height), .14f);
        }

        Vector3 DepthOffset(float distance)
        {
            var camera = Camera.main;
            var towardCamera = camera != null
                ? camera.transform.position - root.position
                : root.TransformDirection(Vector3.forward);
            if (towardCamera.sqrMagnitude < .0001f) towardCamera = root.TransformDirection(Vector3.forward);
            return root.InverseTransformDirection(towardCamera.normalized) * distance;
        }

        static float SegmentRadius(int index)
        {
            var segment = index % 5;
            return segment == 0 ? .24f : segment == 1 ? .20f : segment == 2 ? .17f : segment == 3 ? .145f : .12f;
        }

        static float JointRadius(int index) => index == 0 || index == 1 ? .18f : .14f;
    }
}
