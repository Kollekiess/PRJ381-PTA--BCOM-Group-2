using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.OpenXR;

namespace BCSignVR
{
    /// <summary>
    /// A friendly, solid representation of the learner's tracked hands. The
    /// geometry stays at the OpenXR joint poses, so it appears where the user
    /// is actually holding their hands rather than inside the lesson board.
    /// </summary>
    public sealed class FloatingHandsView
    {
        static readonly int[,] Bones =
        {
            {0,1},{0,2},{2,3},{3,4},{4,5},
            {1,6},{6,7},{7,8},{8,9},{9,10},
            {1,11},{11,12},{12,13},{13,14},{14,15},
            {1,16},{16,17},{17,18},{18,19},{19,20},
            {1,21},{21,22},{22,23},{23,24},{24,25}
        };

        sealed class HandSide
        {
            public readonly Transform root;
            public readonly Transform[] fillJoints = new Transform[HandPoseFrame.JointCount];
            public readonly Transform[] outlineJoints = new Transform[HandPoseFrame.JointCount];
            public readonly Transform[] fillBones = new Transform[Bones.GetLength(0)];
            public readonly Transform[] outlineBones = new Transform[Bones.GetLength(0)];
            public readonly Pose[] poses = new Pose[HandPoseFrame.JointCount];
            public readonly Transform fillPalm;
            public readonly Transform outlinePalm;
            public readonly Transform fillWrist;
            public readonly Transform outlineWrist;
            public bool initialized;

            public HandSide(string name, Color fillColor)
            {
                root = new GameObject(name).transform;
                var fillMaterial = ClassroomFactory.Material(fillColor);
                // Keep the glove silhouette friendly and readable in the
                // headset. The outline follows the glove colour instead of
                // turning the tracked hand into a skin-toned debug object.
                var outlineMaterial = ClassroomFactory.Material(Color.Lerp(
                    fillColor, Color.black, .34f));

                for (var index = 0; index < fillJoints.Length; index++)
                {
                    outlineJoints[index] = Sphere($"{name} outline joint {index}", root, outlineMaterial);
                    fillJoints[index] = Sphere($"{name} joint {index}", root, fillMaterial);
                    // Keep the tracked hand soft and readable instead of
                    // exposing a debugging bead at every landmark. Fingertips
                    // remain rounded; the palm and finger volumes carry the
                    // rest of the silhouette.
                    var fingertip = index == 5 || index == 10 || index == 15 || index == 20 || index == 25;
                    outlineJoints[index].gameObject.SetActive(fingertip);
                    fillJoints[index].gameObject.SetActive(fingertip);
                }
                for (var index = 0; index < fillBones.Length; index++)
                {
                    outlineBones[index] = Capsule($"{name} outline finger {index}", root, outlineMaterial);
                    fillBones[index] = Capsule($"{name} finger {index}", root, fillMaterial);
                }

                outlinePalm = Sphere($"{name} outline palm", root, outlineMaterial);
                fillPalm = Sphere($"{name} palm", root, fillMaterial);
                outlineWrist = Sphere($"{name} outline wrist", root, outlineMaterial);
                fillWrist = Sphere($"{name} wrist", root, fillMaterial);
                root.gameObject.SetActive(false);
            }

            static Transform Sphere(string name, Transform parent, Material material)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = name;
                Object.Destroy(sphere.GetComponent<Collider>());
                sphere.transform.SetParent(parent, false);
                sphere.GetComponent<Renderer>().sharedMaterial = material;
                return sphere.transform;
            }

            static Transform Capsule(string name, Transform parent, Material material)
            {
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = name;
                Object.Destroy(capsule.GetComponent<Collider>());
                capsule.transform.SetParent(parent, false);
                capsule.GetComponent<Renderer>().sharedMaterial = material;
                return capsule.transform;
            }
        }

        // The hands are intentionally colour-coded so the learner can tell
        // left from right at a glance while practising in mixed reality.
        readonly HandSide left = new("Your left hand glove", new Color(.62f, .67f, .73f));
        readonly HandSide right = new("Your right hand glove", new Color(.20f, .52f, .90f));

        public FloatingHandsView()
        {
            left.root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            right.root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        public void Update()
        {
            var subsystem = HandTracking.subsystem;
            if (subsystem == null || !subsystem.running)
            {
                Hide(left); Hide(right);
                return;
            }
            UpdateHand(left, subsystem.leftHand);
            UpdateHand(right, subsystem.rightHand);
        }

        static void UpdateHand(HandSide visual, XRHand hand)
        {
            if (!hand.isTracked)
            {
                Hide(visual);
                return;
            }

            for (var index = 0; index < visual.poses.Length; index++)
            {
                if (!hand.GetJoint(XRHandJointIDUtility.FromIndex(index)).TryGetPose(out visual.poses[index]))
                {
                    Hide(visual);
                    return;
                }
            }

            visual.root.gameObject.SetActive(true);
            var blend = visual.initialized ? 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime) : 1f;
            for (var index = 0; index < visual.poses.Length; index++)
            {
                var target = visual.poses[index].position;
                var fill = visual.fillJoints[index];
                var outline = visual.outlineJoints[index];
                if (!visual.initialized)
                {
                    fill.position = target;
                    outline.position = target;
                }
                else
                {
                    fill.position = Vector3.Lerp(fill.position, target, blend);
                    outline.position = fill.position;
                }
                fill.localScale = Vector3.one * (index == 0 || index == 1 ? .026f : .020f);
                outline.localScale = fill.localScale * 1.32f;
            }

            for (var index = 0; index < Bones.GetLength(0); index++)
            {
                var start = visual.fillJoints[Bones[index, 0]].position;
                var end = visual.fillJoints[Bones[index, 1]].position;
                SetSegment(visual.outlineBones[index], start, end, .017f);
                SetSegment(visual.fillBones[index], start, end, .012f);
            }

            var wrist = visual.fillJoints[0].position;
            var knuckles = (visual.fillJoints[6].position + visual.fillJoints[11].position +
                visual.fillJoints[16].position + visual.fillJoints[21].position) * .25f;
            var palmDirection = knuckles - wrist;
            if (palmDirection.sqrMagnitude < .0001f) palmDirection = Vector3.up;
            var palmCenter = wrist + palmDirection * .52f;
            SetPalm(visual.outlinePalm, palmCenter, palmDirection, .115f, .075f, .040f);
            SetPalm(visual.fillPalm, palmCenter, palmDirection, .098f, .062f, .028f);
            SetPalm(visual.outlineWrist, wrist, palmDirection, .060f, .045f, .040f);
            SetPalm(visual.fillWrist, wrist, palmDirection, .050f, .036f, .028f);
            visual.initialized = true;
        }

        static void SetSegment(Transform segment, Vector3 start, Vector3 end, float radius)
        {
            var delta = end - start;
            segment.position = (start + end) * .5f;
            segment.rotation = Quaternion.FromToRotation(Vector3.up, delta);
            // Unity's capsule is two units tall with a .5-unit radius. This
            // keeps each finger a smooth rounded tube instead of an elongated
            // sphere or a chain of visible landmark balls.
            segment.localScale = new Vector3(radius * 2f,
                Mathf.Max(radius, delta.magnitude * .5f + radius), radius * 2f);
        }

        static void SetPalm(Transform palm, Vector3 center, Vector3 direction, float width, float height, float depth)
        {
            palm.position = center;
            palm.rotation = Quaternion.FromToRotation(Vector3.up, direction);
            palm.localScale = new Vector3(width, height, depth);
        }

        static void Hide(HandSide visual)
        {
            visual.root.gameObject.SetActive(false);
            visual.initialized = false;
        }
    }
}
