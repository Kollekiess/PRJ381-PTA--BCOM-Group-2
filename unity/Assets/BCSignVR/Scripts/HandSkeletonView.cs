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
                sphere.transform.localScale = Vector3.one * (solid ? .074f : .035f);
                sphere.GetComponent<Renderer>().sharedMaterial = material;
                joints[i] = sphere.transform;
            }
            for (var i = 0; i < bones.Length; i++)
            {
                var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cylinder.name = $"Bone {i}";
                Object.Destroy(cylinder.GetComponent<Collider>());
                cylinder.transform.SetParent(root, false);
                cylinder.GetComponent<Renderer>().sharedMaterial = material;
                bones[i] = cylinder.transform;
            }
            if (solid)
            {
                var shape = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shape.name = "Soft palm"; Object.Destroy(shape.GetComponent<Collider>());
                shape.transform.SetParent(root, false);
                shape.GetComponent<Renderer>().sharedMaterial = material;
                palm = shape.transform;
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
                bones[i].localScale = new Vector3(solid ? .070f : .018f, delta.magnitude * 0.5f, solid ? .070f : .018f);
            }
            if (palm != null)
            {
                var baseOfFingers = (joints[7].localPosition + joints[12].localPosition + joints[17].localPosition + joints[22].localPosition) / 4;
                var wrist = joints[0].localPosition;
                var up = baseOfFingers - wrist;
                palm.localPosition = wrist + up * .53f;
                palm.localRotation = Quaternion.FromToRotation(Vector3.up, up);
                palm.localScale = new Vector3(Vector3.Distance(joints[7].localPosition, joints[22].localPosition) + .045f,
                    up.magnitude * 1.15f, .10f);
            }
        }
    }
}

