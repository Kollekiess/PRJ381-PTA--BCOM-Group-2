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
        readonly Material outlineMaterial;
        readonly Material fillMaterial;
        readonly Material jointMaterial;
        readonly Material highlightMaterial;
        readonly Material[] fingerMaterials = new Material[5];
        readonly Transform palm;
        readonly Transform palmOutline;
        readonly Transform wrist;
        readonly Transform wristOutline;
        readonly Transform highlight;
        readonly Transform[] fingertipHighlights = new Transform[5];
        Vector3 trackingAnchor;
        bool hasTrackingAnchor;

        public HandSkeletonView(string name, Transform parent, Color color, bool solid = false)
        {
            root = new GameObject(name).transform;
            root.SetParent(parent, false);
            // Keep the light-grey Bob-style hand readable: articulated capsules,
            // dark definitions and small highlights. The landmarks stay as
            // hidden anchors, while the whole renderer follows the real hand's
            // wrist angle and movement inside the learner card.
            var grey = Mathf.Clamp01((color.r + color.g + color.b) / 3f);
            outlineMaterial = ClassroomFactory.Material(new Color(grey * .34f, grey * .34f, grey * .34f));
            fillMaterial = ClassroomFactory.Material(new Color(grey, grey, grey));
            jointMaterial = ClassroomFactory.Material(new Color(grey * .76f, grey * .76f, grey * .76f));
            highlightMaterial = ClassroomFactory.Material(new Color(
                Mathf.Min(1f, grey + .13f), Mathf.Min(1f, grey + .13f), Mathf.Min(1f, grey + .13f)));
            for (var i = 0; i < fingerMaterials.Length; i++)
            {
                var shade = 1f + (i - 2) * .035f;
                fingerMaterials[i] = ClassroomFactory.Material(new Color(
                    Mathf.Clamp01(grey * shade), Mathf.Clamp01(grey * shade), Mathf.Clamp01(grey * shade)));
            }

            for (var i = 0; i < joints.Length; i++)
            {
                jointOutlines[i] = Ball($"Bob hand outline joint {i}", outlineMaterial);
                joints[i] = Ball($"Bob hand joint {i}", jointMaterial);
                // Keep small wooden knuckles visible. They make each tracked
                // finger legible without returning to the old blue/grey debug
                // skeleton or hiding the pose inside one dark contour.
                jointOutlines[i].gameObject.SetActive(true);
                joints[i].gameObject.SetActive(true);
            }
            for (var i = 0; i < bones.Length; i++)
            {
                boneOutlines[i] = Capsule($"Bob hand outline finger {i}", outlineMaterial);
                bones[i] = Capsule($"Bob hand finger {i}", fingerMaterials[Mathf.Min(4, i / 5)]);
                boneOutlines[i].gameObject.SetActive(true);
            }

            for (var i = 0; i < fingertipHighlights.Length; i++)
                fingertipHighlights[i] = Ball($"Bob hand fingertip highlight {i}", highlightMaterial);

            palmOutline = Ball("Bob hand outline palm", outlineMaterial);
            palm = Ball("Bob hand palm", fillMaterial);
            wristOutline = Ball("Bob hand outline wrist", outlineMaterial);
            wrist = Ball("Bob hand wrist", fillMaterial);
            highlight = Ball("Bob hand soft highlight", highlightMaterial);
            palmOutline.gameObject.SetActive(true);
            wristOutline.gameObject.SetActive(true);
        }

        public void SetPose(HandPoseFrame frame, Vector3 position, Quaternion rotation, float scale = 3f)
        {
            root.gameObject.SetActive(frame.tracked);
            if (!frame.tracked)
            {
                hasTrackingAnchor = false;
                return;
            }

            root.rotation = rotation;
            var wristWorld = frame.joints[0];
            var palmUp = frame.joints[11] - wristWorld;
            var palmAcross = frame.joints[6] - frame.joints[21];
            palmAcross = Vector3.ProjectOnPlane(palmAcross, palmUp);
            if (palmUp.sqrMagnitude < .0001f || palmAcross.sqrMagnitude < .0001f)
            {
                root.gameObject.SetActive(false);
                hasTrackingAnchor = false;
                return;
            }
            palmUp.Normalize();
            palmAcross.Normalize();
            var palmNormal = Vector3.Cross(palmAcross, palmUp).normalized;

            // Keep the hand attached to the learner's real wrist. The card is
            // the safe presentation area, so clamp only the translation at its
            // edges; hand roll, pitch and yaw remain visible to the learner.
            if (!hasTrackingAnchor)
            {
                trackingAnchor = wristWorld;
                hasTrackingAnchor = true;
            }
            var boardNormal = rotation * Vector3.forward;
            var boardRight = rotation * Vector3.right;
            var boardUp = rotation * Vector3.up;
            var movement = wristWorld - trackingAnchor;
            var horizontalMotion = Mathf.Clamp(Vector3.Dot(movement, boardRight) * .78f, -.42f, .42f);
            var verticalMotion = Mathf.Clamp(Vector3.Dot(movement, boardUp) * .78f, -.30f, .30f);
            root.position = position + boardRight * horizontalMotion + boardUp * verticalMotion;

            // The old renderer forced a fixed upright board rotation. Build the
            // display orientation from the actual palm plane instead, retaining
            // the board-facing side so the light-grey hand stays readable while
            // still turning and moving with the user's wrist.
            if (Vector3.Dot(palmNormal, boardNormal) < 0f) palmNormal = -palmNormal;
            var trackedRotation = Quaternion.LookRotation(palmNormal, palmUp);
            root.rotation = Quaternion.Slerp(rotation, trackedRotation, .92f);

            var handSize = .0001f;
            for (var i = 1; i < frame.joints.Length; i++)
                handSize = Mathf.Max(handSize, Vector3.Distance(wristWorld, frame.joints[i]));
            for (var i = 0; i < joints.Length; i++)
            {
                // All 26 tracked joints contribute. Depth is retained lightly
                // so curled fingers read as wood joints without hiding the
                // handshape the learner is meant to copy.
                var relative = frame.joints[i] - wristWorld;
                var point = new Vector3(
                    Vector3.Dot(relative, palmAcross),
                    Vector3.Dot(relative, palmUp),
                    Vector3.Dot(relative, palmNormal) * .24f) / handSize * scale;
                joints[i].localPosition = point;
                jointOutlines[i].localPosition = point + Vector3.forward * .018f;
                joints[i].localPosition = point - Vector3.forward * .05f;
                var radius = JointRadius(i);
                joints[i].localScale = Vector3.one * radius;
                jointOutlines[i].localScale = Vector3.one * radius * .86f;
            }

            var tipIndices = new[] { 5, 10, 15, 20, 25 };
            for (var i = 0; i < fingertipHighlights.Length; i++)
            {
                var tip = joints[tipIndices[i]].localPosition + Vector3.forward * .045f;
                fingertipHighlights[i].localPosition = tip;
                fingertipHighlights[i].localScale = Vector3.one * .055f;
            }

            for (var i = 0; i < bones.Length; i++)
            {
                var a = joints[Bones[i, 0]].localPosition;
                var b = joints[Bones[i, 1]].localPosition;
                // The board faces the opposite local-Z direction from the
                // first prototype. Keep the contour behind the wood fill or
                // it completely occludes the articulated hand on Quest.
                SetCapsule(boneOutlines[i], a, b, SegmentRadius(i) * 1.16f, -.06f);
                SetCapsule(bones[i], a, b, SegmentRadius(i), .01f);
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
            SetOval(palmOutline, palmCenter - Vector3.forward * .06f, horizontal, direction,
                fingerSpan * .90f + .16f, up.magnitude * .80f + .13f);
            SetOval(palm, palmCenter + Vector3.forward * .01f, horizontal, direction,
                fingerSpan * .74f + .08f, up.magnitude * .64f + .05f);
            SetOval(wristOutline, wristPoint - direction * .06f - Vector3.forward * .06f,
                horizontal, direction, .28f, .27f);
            SetOval(wrist, wristPoint - direction * .06f + Vector3.forward * .01f, horizontal, direction, .22f, .20f);
            SetOval(highlight, palmCenter + horizontal * (fingerSpan * -.16f) + direction * (up.magnitude * .20f) + Vector3.forward * .025f,
                horizontal, direction, .10f, .05f);
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

        static void SetCapsule(Transform capsule, Vector3 start, Vector3 end, float radius, float zOffset)
        {
            var delta = end - start;
            capsule.localPosition = (start + end) * .5f + Vector3.forward * zOffset;
            capsule.localRotation = Quaternion.FromToRotation(Vector3.up, delta);
            // Unity's capsule has a default radius of .5, so X/Z need the
            // diameter scale while Y is half-length plus one radius.
            capsule.localScale = new Vector3(radius * 2f,
                Mathf.Max(radius, delta.magnitude * .5f + radius), radius * 2f);
        }

        static void SetOval(Transform shape, Vector3 position, Vector3 horizontal, Vector3 vertical,
            float width, float height)
        {
            shape.localPosition = position;
            shape.localRotation = Quaternion.FromToRotation(Vector3.up, vertical);
            shape.localScale = new Vector3(Mathf.Max(.02f, width), Mathf.Max(.02f, height), .14f);
        }

        static float SegmentRadius(int index)
        {
            var segment = index % 5;
            return segment == 0 ? .105f : segment == 1 ? .092f : segment == 2 ? .080f : segment == 3 ? .068f : .058f;
        }

        static float JointRadius(int index) => index == 0 ? .13f : index == 1 ? .10f : .065f;
    }
}
