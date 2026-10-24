using System.Collections.Generic;
using UnityEngine;

namespace BCSignVR
{
    /// <summary>
    /// Draws tracked joints as a flat, friendly Bob-style hand. The view stays
    /// driven by the real landmarks, but uses outlined rounded fingers, a soft
    /// palm, small highlights and crease strokes instead of debug beads.
    /// </summary>
    public sealed class HandSkeletonView
    {
        sealed class FingerStroke
        {
            public int start;
            public int count;
            public LineRenderer outline;
            public LineRenderer fill;
            public float outlineZ;
            public float fillZ;
            public float width;
        }

        readonly Transform root;
        readonly Vector3[] smoothedPoints = new Vector3[HandPoseFrame.JointCount];
        readonly Vector3[] displayPoints = new Vector3[HandPoseFrame.JointCount];
        readonly List<FingerStroke> fingers = new();
        readonly LineRenderer[,] fingerCreases = new LineRenderer[4, 2];
        readonly LineRenderer[] palmCreases = new LineRenderer[2];
        readonly Transform palmOutline;
        readonly Transform palmFill;
        readonly Transform wristOutline;
        readonly Transform wristFill;
        readonly Transform palmHighlight;
        readonly Transform[] nailHighlights = new Transform[5];
        readonly Material outlineMaterial;
        readonly Material fillMaterial;
        readonly Material creaseMaterial;
        readonly Material highlightMaterial;
        bool poseInitialized;

        static readonly int[] FingerStarts = { 2, 6, 11, 16, 21 };
        static readonly int[] FingerPointCounts = { 4, 5, 5, 5, 5 };
        // Broad, softly joined fingers read as Bob's illustrated hand on the
        // board instead of a thin landmark skeleton at headset distance.
        static readonly float[] FingerWidths = { .132f, .112f, .118f, .104f, .092f };

        public HandSkeletonView(string name, Transform parent, Color color, bool solid = false)
        {
            root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.gameObject.SetActive(false);

            // Bob's source art uses a warm peach fill, a soft umber contour,
            // and restrained crease/highlight accents. Keeping these surfaces
            // flat and front-facing avoids the old lumpy 3-D capsule look.
            outlineMaterial = ClassroomFactory.Material(new Color(.57f, .27f, .18f));
            fillMaterial = ClassroomFactory.Material(color);
            creaseMaterial = ClassroomFactory.Material(new Color(.70f, .38f, .28f));
            highlightMaterial = ClassroomFactory.Material(new Color(.99f, .82f, .67f));

            palmOutline = CreateDisc("Bob hand palm contour", outlineMaterial);
            palmFill = CreateDisc("Bob hand palm", fillMaterial);
            wristOutline = CreateDisc("Bob hand wrist contour", outlineMaterial);
            wristFill = CreateDisc("Bob hand wrist", fillMaterial);
            palmHighlight = CreateDisc("Bob hand palm highlight", highlightMaterial);

            for (var finger = 0; finger < FingerStarts.Length; finger++)
            {
                var width = FingerWidths[finger];
                fingers.Add(new FingerStroke
                {
                    start = FingerStarts[finger],
                    count = FingerPointCounts[finger],
                    outline = CreateStroke(name + " hand outline " + finger, outlineMaterial,
                        width * 1.22f, width * .92f, FingerPointCounts[finger]),
                    fill = CreateStroke(name + " hand fill " + finger, fillMaterial,
                        width, width * .76f, FingerPointCounts[finger]),
                    outlineZ = .022f,
                    fillZ = .029f,
                    width = width
                });
            }

            for (var finger = 0; finger < 4; finger++)
            for (var crease = 0; crease < 2; crease++)
                fingerCreases[finger, crease] = CreateStroke(name + " hand finger crease", creaseMaterial,
                    .009f, .007f, 3);

            for (var crease = 0; crease < palmCreases.Length; crease++)
                palmCreases[crease] = CreateStroke(name + " hand palm crease", creaseMaterial,
                    .010f, .008f, 4);

            for (var finger = 0; finger < nailHighlights.Length; finger++)
                nailHighlights[finger] = CreateDisc("Bob hand fingertip highlight", highlightMaterial);
        }

        public void SetPose(HandPoseFrame frame, Vector3 position, Quaternion rotation, float scale = 3f)
        {
            SetPoseInternal(frame, position, rotation, scale, false);
        }

        public void SetLearnerPose(HandPoseFrame frame, Vector3 position, Quaternion rotation, float scale = 3f)
        {
            SetPoseInternal(frame, position, rotation, scale, true);
        }

        void SetPoseInternal(HandPoseFrame frame, Vector3 position, Quaternion rotation, float scale,
            bool boardLocalRotation)
        {
            if (frame == null || !frame.tracked)
            {
                root.gameObject.SetActive(false);
                poseInitialized = false;
                return;
            }

            root.gameObject.SetActive(true);
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
            var blend = poseInitialized
                ? 1f - Mathf.Exp(-24f * Mathf.Max(Time.unscaledDeltaTime, .001f))
                : 1f;
            for (var index = 0; index < displayPoints.Length; index++)
            {
                var target = displayPoints[index] * scale;
                smoothedPoints[index] = poseInitialized
                    ? Vector3.Lerp(smoothedPoints[index], target, blend)
                    : target;
            }

            var wristPoint = smoothedPoints[0];
            var indexBase = smoothedPoints[6];
            var middleBase = smoothedPoints[11];
            var ringBase = smoothedPoints[16];
            var littleBase = smoothedPoints[21];
            var knuckles = (indexBase + middleBase + ringBase + littleBase) * .25f;
            var up = knuckles - wristPoint;
            if (up.sqrMagnitude < .0001f) up = Vector3.up * .15f;
            var vertical = up.normalized;
            var horizontal = Vector3.ProjectOnPlane(littleBase - indexBase, vertical);
            if (horizontal.sqrMagnitude < .0001f) horizontal = new Vector3(vertical.y, -vertical.x, 0f);
            if (horizontal.sqrMagnitude < .0001f) horizontal = Vector3.right;
            horizontal.Normalize();

            var fingerSpan = Vector3.Distance(indexBase, littleBase);
            var palmCenter = wristPoint + up * .54f;
            var palmWidth = fingerSpan * 1.10f + .10f;
            var palmHeight = up.magnitude * .80f + .08f;
            SetDiscPair(palmOutline, palmFill, palmCenter, vertical,
                palmWidth + .035f, palmHeight + .035f, .010f, .018f);

            var wristCenter = wristPoint - vertical * .055f;
            SetDiscPair(wristOutline, wristFill, wristCenter, vertical,
                fingerSpan * .82f + .10f, .17f, .011f, .019f);

            for (var index = 0; index < fingers.Count; index++)
            {
                var stroke = fingers[index];
                for (var point = 0; point < stroke.count; point++)
                {
                    var positionOnFinger = smoothedPoints[stroke.start + point];
                    stroke.outline.SetPosition(point, positionOnFinger + Vector3.forward * stroke.outlineZ);
                    stroke.fill.SetPosition(point, positionOnFinger + Vector3.forward * stroke.fillZ);
                }
            }

            for (var finger = 0; finger < 4; finger++)
            for (var crease = 0; crease < 2; crease++)
            {
                var start = FingerStarts[finger + 1];
                var joint = start + (crease == 0 ? 2 : 3);
                var before = smoothedPoints[joint] - smoothedPoints[joint - 1];
                var after = smoothedPoints[joint + 1] - smoothedPoints[joint];
                var tangent = before + after;
                if (tangent.sqrMagnitude < .0001f) tangent = before;
                var across = new Vector3(-tangent.y, tangent.x, 0f).normalized;
                var radius = strokeWidth(finger + 1) * .28f;
                var center = smoothedPoints[joint] + Vector3.forward * .037f;
                var creaseLine = fingerCreases[finger, crease];
                creaseLine.SetPosition(0, center - across * radius);
                creaseLine.SetPosition(1, center + across * (radius * .08f) + Vector3.up * .004f);
                creaseLine.SetPosition(2, center + across * radius);
            }

            SetPalmCrease(palmCreases[0], wristPoint, horizontal, vertical, fingerSpan, up.magnitude,
                new Vector2(-.40f, .42f), new Vector2(-.04f, .50f), new Vector2(.34f, .35f), new Vector2(.43f, .27f));
            SetPalmCrease(palmCreases[1], wristPoint, horizontal, vertical, fingerSpan, up.magnitude,
                new Vector2(-.08f, .23f), new Vector2(.10f, .40f), new Vector2(.29f, .68f), new Vector2(.36f, .76f));

            for (var finger = 0; finger < nailHighlights.Length; finger++)
            {
                var start = FingerStarts[finger];
                var tip = smoothedPoints[start + FingerPointCounts[finger] - 1];
                var previous = smoothedPoints[start + FingerPointCounts[finger] - 2];
                var direction = tip - previous;
                var nail = nailHighlights[finger];
                nail.localPosition = Vector3.Lerp(previous, tip, .80f) + Vector3.forward * .040f;
                nail.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
                var width = strokeWidth(finger) * .58f;
                nail.localScale = new Vector3(width, width * .38f, 1f);
            }

            var highlightCenter = palmCenter - horizontal * (fingerSpan * .17f) + vertical * (up.magnitude * .17f);
            palmHighlight.localPosition = highlightCenter + Vector3.forward * .036f;
            palmHighlight.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(vertical.y, vertical.x) * Mathf.Rad2Deg - 90f);
            palmHighlight.localScale = new Vector3(Mathf.Max(.06f, fingerSpan * .31f),
                Mathf.Max(.035f, up.magnitude * .12f), 1f);
            poseInitialized = true;
        }

        static float strokeWidth(int finger) => FingerWidths[Mathf.Clamp(finger, 0, FingerWidths.Length - 1)];

        LineRenderer CreateStroke(string name, Material material, float startWidth, float endWidth, int pointCount)
        {
            var item = new GameObject(name);
            item.transform.SetParent(root, false);
            var line = item.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = pointCount;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.widthMultiplier = 1f;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 8;
            line.numCornerVertices = 8;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = material;
            return line;
        }

        Transform CreateDisc(string name, Material material)
        {
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(root, false);
            item.GetComponent<MeshFilter>().sharedMesh = UnitDisc();
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            return item.transform;
        }

        static Mesh UnitDisc()
        {
            const int segments = 32;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            for (var index = 0; index < segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                vertices[index + 1] = new Vector3(Mathf.Cos(angle) * .5f, Mathf.Sin(angle) * .5f, 0f);
                var next = (index + 1) % segments;
                triangles[index * 3] = 0;
                triangles[index * 3 + 1] = index + 1;
                triangles[index * 3 + 2] = next + 1;
            }
            var mesh = new Mesh { name = "Bob hand soft ellipse" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void SetDiscPair(Transform outline, Transform fill, Vector3 center, Vector3 vertical,
            float width, float height, float outlineZ, float fillZ)
        {
            var rotation = Quaternion.Euler(0, 0,
                Mathf.Atan2(vertical.y, vertical.x) * Mathf.Rad2Deg - 90f);
            outline.localPosition = center + Vector3.forward * outlineZ;
            fill.localPosition = center + Vector3.forward * fillZ;
            outline.localRotation = fill.localRotation = rotation;
            outline.localScale = new Vector3(width, height, 1f);
            fill.localScale = new Vector3(Mathf.Max(.02f, width - .035f), Mathf.Max(.02f, height - .035f), 1f);
        }

        void SetPalmCrease(LineRenderer line, Vector3 wrist, Vector3 horizontal, Vector3 vertical,
            float fingerSpan, float palmHeight, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            line.SetPosition(0, PalmPoint(wrist, horizontal, vertical, fingerSpan, palmHeight, a));
            line.SetPosition(1, PalmPoint(wrist, horizontal, vertical, fingerSpan, palmHeight, b));
            line.SetPosition(2, PalmPoint(wrist, horizontal, vertical, fingerSpan, palmHeight, c));
            line.SetPosition(3, PalmPoint(wrist, horizontal, vertical, fingerSpan, palmHeight, d));
        }

        static Vector3 PalmPoint(Vector3 wrist, Vector3 horizontal, Vector3 vertical, float fingerSpan,
            float palmHeight, Vector2 point) => wrist + horizontal * (point.x * fingerSpan) +
            vertical * (point.y * palmHeight) + Vector3.forward * .037f;

        static void BuildDisplayPoints(HandPoseFrame frame, Vector3[] output)
        {
            var wrist = frame.joints[0];
            var knuckles = (frame.joints[6] + frame.joints[11] + frame.joints[16] + frame.joints[21]) * .25f;
            var vertical = knuckles - wrist;
            if (vertical.sqrMagnitude < .0001f) vertical = Vector3.up;
            vertical.Normalize();

            var horizontal = Vector3.ProjectOnPlane(frame.joints[21] - frame.joints[6], vertical);
            if (horizontal.sqrMagnitude < .0001f) horizontal = Vector3.Cross(vertical, Vector3.forward);
            if (horizontal.sqrMagnitude < .0001f) horizontal = Vector3.right;
            horizontal.Normalize();

            var handScale = .0001f;
            for (var index = 1; index < HandPoseFrame.JointCount; index++)
                handScale = Mathf.Max(handScale, Vector3.Distance(wrist, frame.joints[index]));
            for (var index = 0; index < HandPoseFrame.JointCount; index++)
            {
                var delta = frame.joints[index] - wrist;
                output[index] = new Vector3(Vector3.Dot(delta, horizontal), Vector3.Dot(delta, vertical), 0f) / handScale;
            }
        }
    }
}
