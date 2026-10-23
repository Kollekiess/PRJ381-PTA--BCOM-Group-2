using UnityEngine;

namespace BCSignVR
{
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
        readonly Transform[] bones = new Transform[Bones.GetLength(0)];
        readonly Material material;
        readonly Transform palm;
        readonly Transform knuckles;
        readonly Transform wrist;
        readonly bool solid;

        public HandSkeletonView(string name, Transform parent, Color color, bool solid = false)
        {
            this.solid = solid;
            root = new GameObject(name).transform;
            root.SetParent(parent, false);
            material = solid ? ClassroomFactory.LitMaterial(color) : ClassroomFactory.Material(color);
            for (var i = 0; i < joints.Length; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Joint {i}";
                Object.Destroy(sphere.GetComponent<Collider>());
                sphere.transform.SetParent(root, false);
                // Keep landmarks subtle. The silhouette is built from the
                // rounded finger segments below, so joints should not look
                // like oversized robot ball bearings.
                sphere.transform.localScale = Vector3.one * (solid ? .030f : .022f);
                sphere.GetComponent<Renderer>().sharedMaterial = material;
                joints[i] = sphere.transform;
            }
            for (var i = 0; i < bones.Length; i++)
            {
                // A scaled sphere gives each phalanx a soft capsule with a
                // rounded end. That reads as an illustrated human hand much
                // better than thin, uniform cylinders.
                var segment = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                segment.name = $"Rounded finger segment {i}";
                Object.Destroy(segment.GetComponent<Collider>());
                segment.transform.SetParent(root, false);
                segment.GetComponent<Renderer>().sharedMaterial = material;
                bones[i] = segment.transform;
            }
            if (solid)
            {
                var shape = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shape.name = "Soft palm"; Object.Destroy(shape.GetComponent<Collider>());
                shape.transform.SetParent(root, false);
                shape.GetComponent<Renderer>().sharedMaterial = material;
                palm = shape.transform;

                var bridge = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bridge.name = "Rounded finger webbing"; Object.Destroy(bridge.GetComponent<Collider>());
                bridge.transform.SetParent(root, false);
                bridge.GetComponent<Renderer>().sharedMaterial = material;
                knuckles = bridge.transform;

                var wristShape = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                wristShape.name = "Soft wrist"; Object.Destroy(wristShape.GetComponent<Collider>());
                wristShape.transform.SetParent(root, false);
                wristShape.GetComponent<Renderer>().sharedMaterial = material;
                wrist = wristShape.transform;
            }
        }

        public void SetPose(HandPoseFrame frame, Vector3 position, Quaternion rotation, float scale = 3f)
        {
            root.gameObject.SetActive(frame.tracked);
            if (!frame.tracked) return;
            root.position = position;
            root.rotation = rotation;
            var normalized = frame.NormalizedFeatures();
            for (var i = 0; i < joints.Length; i++)
                joints[i].localPosition = new Vector3(normalized[i * 3], normalized[i * 3 + 1], normalized[i * 3 + 2]) * scale;
            for (var i = 0; i < bones.Length; i++)
            {
                var a = joints[Bones[i, 0]].localPosition;
                var b = joints[Bones[i, 1]].localPosition;
                var delta = b - a;
                bones[i].localPosition = (a + b) * 0.5f;
                bones[i].localRotation = Quaternion.FromToRotation(Vector3.up, delta);
                if (solid)
                {
                    var radius = SegmentRadius(i);
                    // A Unity sphere has diameter 2, so its local Z scale is
                    // the half-length plus one radius at each rounded end.
                    // The segment is aligned from local Y (the same axis used
                    // by the old cylinder), so elongate Y and keep X/Z round.
                    bones[i].localScale = new Vector3(radius,
                        delta.magnitude * .5f + radius, radius);
                }
                else
                    bones[i].localScale = new Vector3(.018f, delta.magnitude * .5f, .018f);
            }
            if (palm != null)
            {
                var baseOfFingers = (joints[7].localPosition + joints[12].localPosition + joints[17].localPosition + joints[22].localPosition) / 4;
                var wristPoint = joints[0].localPosition;
                var up = baseOfFingers - wristPoint;
                // Lift the palm slightly into the finger bases so the hand
                // reads as one continuous illustrated form rather than a
                // circle with pieces floating above it.
                palm.localPosition = wristPoint + up * .56f;
                palm.localRotation = Quaternion.FromToRotation(Vector3.up, up);
                palm.localScale = new Vector3(Vector3.Distance(joints[7].localPosition, joints[22].localPosition) + .28f,
                    up.magnitude * 1.05f, .16f);
                var fingerWidth = Vector3.Distance(joints[7].localPosition, joints[22].localPosition);
                knuckles.localPosition = wristPoint + up * .57f;
                knuckles.localRotation = Quaternion.FromToRotation(Vector3.up, up);
                knuckles.localScale = new Vector3(fingerWidth + .25f, .13f, .17f);
                wrist.localPosition = wristPoint - up.normalized * .06f;
                wrist.localScale = new Vector3(.24f, .23f, .16f);
            }
        }

        static float SegmentRadius(int index)
        {
            // The wrist/base is fuller and the fingertips taper gently.
            var segment = index % 5;
            return segment == 0 ? .105f : segment == 1 ? .092f : segment == 2 ? .080f : segment == 3 ? .068f : .058f;
        }
    }
}

