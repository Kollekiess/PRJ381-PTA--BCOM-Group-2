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
            // The outline is deliberately a darker peach rather than the old
            // debug brown/green palette. Some Mac GPUs depth-sort the paired
            // capsules as one layer, so keeping both layers in Bob's skin
            // palette guarantees a friendly hand even when the outline wins.
            outlineMaterial = ClassroomFactory.Material(new Color(.97f, .73f, .57f));
            // Bob's generated hand uses a warm peach base with a soft apricot
            // highlight. Keep the live learner hand in the same family.
            fillMaterial = ClassroomFactory.Material(new Color(.97f, .73f, .57f));
            highlightMaterial = ClassroomFactory.Material(new Color(1.0f, .84f, .68f));

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
            root.gameObject.SetActive(frame.tracked);
            if (!frame.tracked) return;

            root.position = position;
            root.rotation = rotation;
            var normalized = frame.NormalizedFeatures();
            for (var i = 0; i < joints.Length; i++)
            {
                var point = new Vector3(normalized[i * 3], normalized[i * 3 + 1], 0f) * scale;
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
                SetCapsule(boneOutlines[i], a, b, SegmentRadius(i) * .86f, .018f);
                SetCapsule(bones[i], a, b, SegmentRadius(i), -.05f);
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
            SetOval(palmOutline, palmCenter + Vector3.forward * .018f, horizontal, direction,
                fingerSpan * .92f + .20f, up.magnitude * .84f + .15f);
            SetOval(palm, palmCenter - Vector3.forward * .05f, horizontal, direction,
                fingerSpan * .76f + .12f, up.magnitude * .68f + .08f);
            SetOval(wristOutline, wristPoint - direction * .06f + Vector3.forward * .018f,
                horizontal, direction, .31f, .28f);
            SetOval(wrist, wristPoint - direction * .06f - Vector3.forward * .05f, horizontal, direction, .27f, .23f);
            SetOval(highlight, palmCenter + horizontal * (fingerSpan * -.16f) + direction * (up.magnitude * .20f),
                horizontal, direction, .14f, .07f);
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
            // Spheres are intentional: they render consistently in the
            // standalone Mac build and keep the hand soft and toy-like.
            return Ball(name, material);
        }

        static void SetCapsule(Transform capsule, Vector3 start, Vector3 end, float radius, float zOffset)
        {
            var delta = end - start;
            capsule.localPosition = (start + end) * .5f + Vector3.forward * zOffset;
            capsule.localRotation = Quaternion.FromToRotation(Vector3.up, delta);
            capsule.localScale = new Vector3(radius, Mathf.Max(radius, delta.magnitude * .5f + radius), radius);
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
            return segment == 0 ? .24f : segment == 1 ? .20f : segment == 2 ? .17f : segment == 3 ? .145f : .12f;
        }

        static float JointRadius(int index) => index == 0 || index == 1 ? .18f : .14f;
    }
}
