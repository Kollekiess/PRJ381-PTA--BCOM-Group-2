using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.OpenXR;
using NewInputSystem = UnityEngine.InputSystem.InputSystem;
using NewXRController = UnityEngine.InputSystem.XR.XRController;
using NewVector3Control = UnityEngine.InputSystem.Controls.Vector3Control;
using NewQuaternionControl = UnityEngine.InputSystem.Controls.QuaternionControl;
using NewButtonControl = UnityEngine.InputSystem.Controls.ButtonControl;
using NewAxisControl = UnityEngine.InputSystem.Controls.AxisControl;

namespace BCSignVR
{
    public static class ClassroomUi
    {
        public static readonly Color Blue = new(.14f, .58f, .81f);
        public static readonly Color DeepBlue = new(.08f, .39f, .60f);
        public static readonly Color Ink = new(.18f, .26f, .33f);
        public static readonly Color Muted = new(.40f, .49f, .55f);
        public static readonly Color Line = new(.73f, .84f, .89f);
        public static readonly Color Paper = new(.97f, .99f, 1f);
        public static readonly Color Green = new(.10f, .59f, .43f);

        public static GameObject Panel(string name, Transform parent, Vector3 center, Vector2 size,
            Color color, float radius = .15f, bool border = false, Color? borderColor = null)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = center;
            if (border)
            {
                Surface("Soft shadow", root.transform, new Vector3(0, -.025f, .025f), size + Vector2.one * .045f,
                    new Color(.72f, .80f, .84f), radius);
                Surface("Border", root.transform, Vector3.zero, size, borderColor ?? Line, radius);
                Surface("Face", root.transform, new Vector3(0, 0, -.010f), size - Vector2.one * .032f,
                    color, Mathf.Max(.01f, radius - .012f));
                Surface("Top highlight", root.transform, new Vector3(0, size.y * .5f - .045f, -.014f),
                    new Vector2(Mathf.Max(.05f, size.x - .22f), .012f), new Color(.84f, .91f, .94f), .006f);
            }
            else Surface("Face", root.transform, Vector3.zero, size, color, radius);
            return root;
        }

        static GameObject Surface(string name, Transform parent, Vector3 center, Vector2 size, Color color, float radius)
        {
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(parent, false); item.transform.localPosition = center;
            radius = Mathf.Clamp(radius, .001f, Mathf.Min(size.x, size.y) * .5f);
            var vertices = new List<Vector3> { Vector3.zero };
            for (var corner = 0; corner < 4; corner++)
            {
                var cx = corner == 0 || corner == 3 ? size.x * .5f - radius : -size.x * .5f + radius;
                var cy = corner < 2 ? size.y * .5f - radius : -size.y * .5f + radius;
                for (var step = 0; step <= 10; step++)
                {
                    var angle = (corner * 90f + step * 9f) * Mathf.Deg2Rad;
                    vertices.Add(new Vector3(cx + Mathf.Cos(angle) * radius, cy + Mathf.Sin(angle) * radius));
                }
            }
            var triangles = new List<int>();
            for (var i = 1; i < vertices.Count; i++)
            { triangles.Add(0); triangles.Add(i == vertices.Count - 1 ? 1 : i + 1); triangles.Add(i); }
            var mesh = new Mesh { name = name + " rounded rectangle" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            item.GetComponent<MeshRenderer>().sharedMaterial = ClassroomFactory.Material(color);
            return item;
        }

        public static TextMesh Label(string value, Transform parent, float x, float y, float height,
            Color color, TextAnchor anchor = TextAnchor.MiddleLeft, float maxWidth = 0)
        {
            var text = ClassroomFactory.Text(value, parent, new Vector3(x, y, -.055f), height * .105f, color, anchor);
            text.fontSize = 96; text.lineSpacing = 1.12f;
            if (maxWidth > 0) text.gameObject.AddComponent<FitBoardText>().maxWidth = maxWidth;
            return text;
        }

        public static TextMesh FontLabel(string value, string resourceFont, Transform parent, float x, float y,
            float height, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var text = Label(value, parent, x, y, height, color, anchor);
            var font = Resources.Load<Font>(resourceFont);
            if (font != null)
            {
                text.font = font;
                text.GetComponent<Renderer>().sharedMaterial = font.material;
                text.color = color.linear;
            }
            return text;
        }

        public static GameObject Picture(string name, string resourceTexture, Transform parent, Vector3 center, Vector2 size)
        {
            // Keep sign art crisp while giving every still illustration a
            // consistent soft mounting card and fine border.
            Panel(name + " illustration mat", parent, center + new Vector3(0, 0, .035f),
                size * .90f, new Color(.92f, .97f, .995f), .10f, true);
            var item = GameObject.CreatePrimitive(PrimitiveType.Quad);
            item.name = name; item.transform.SetParent(parent, false); item.transform.localPosition = center;
            item.transform.localScale = new Vector3(size.x, size.y, 1);
            UnityEngine.Object.Destroy(item.GetComponent<Collider>());
            var shader = Resources.Load<Shader>("BCSignVRUnlitTexture");
            var texture = Resources.Load<Texture2D>(resourceTexture);
            var material = new Material(shader) { mainTexture = texture };
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        public static BoardButton Button(string label, Transform parent, float x, float y, float width,
            Action action, bool primary = false, float height = .45f)
        {
            var root = Panel(label, parent, new Vector3(x, y, -.03f), new Vector2(width, height),
                primary ? Blue : Color.white, .12f, true, primary ? DeepBlue : Line);
            var button = root.AddComponent<BoardButton>();
            var text = Label(label, root.transform, 0, 0, .17f, primary ? Color.white : DeepBlue,
                TextAnchor.MiddleCenter, width - .14f);
            button.Initialize(action, primary, text);
            // Give the 3D button a small comfort margin. Quest's system UI does
            // not require a laser to land on a one-pixel edge of a control.
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(width + .16f, height + .14f, .18f);
            return button;
        }
    }

    public sealed class FitBoardText : MonoBehaviour
    {
        public float maxWidth;
        string previous;
        void LateUpdate()
        {
            var text = GetComponent<TextMesh>();
            if (text.text == previous) return;
            previous = text.text; transform.localScale = Vector3.one;
            var width = GetComponent<Renderer>().bounds.size.x;
            if (width > maxWidth && maxWidth > 0) transform.localScale = Vector3.one * (maxWidth / width);
        }
    }

    public sealed class BoardButton : MonoBehaviour
    {
        Action action;
        Color normal;
        Color normalBorder;
        Renderer face;
        Renderer border;
        TextMesh label;
        bool selected;
        float lastPressedAt = -10f;
        public void Initialize(Action callback, bool primary, TextMesh label)
        {
            action = callback; normal = primary ? ClassroomUi.Blue : Color.white;
            face = transform.Find("Face").GetComponent<Renderer>();
            border = transform.Find("Border").GetComponent<Renderer>();
            this.label = label;
            normalBorder = primary ? ClassroomUi.DeepBlue : ClassroomUi.Line;
        }
        public void Focus(bool value)
        {
            if (!this || !gameObject || !transform) return;
            selected = value;
            if (face != null) face.material.color = value
                ? Color.Lerp(normal, primaryTint(normal), .16f) : normal;
            if (border != null) border.material.color = value ? ClassroomUi.Blue : normalBorder;
            if (label != null) label.color = normal == ClassroomUi.Blue ? Color.white : ClassroomUi.DeepBlue;
            transform.localScale = value ? new Vector3(1.015f, 1.015f, 1) : Vector3.one;
        }
        static Color primaryTint(Color color) => color == ClassroomUi.Blue
            ? new Color(.25f, .67f, .87f) : new Color(.89f, .96f, 1f);
        public void Press()
        {
            if (!isActiveAndEnabled || action == null || Time.unscaledTime - lastPressedAt < .12f) return;
            lastPressedAt = Time.unscaledTime;
            action.Invoke();
        }
        // Desktop builds also receive Unity's native collider click. Keeping this
        // path alongside BoardPointer makes the classroom usable when the cursor
        // is over a modal button. Do not invoke again on mouse-up: a long press
        // used to navigate twice after the debounce window expired.
        void OnMouseDown() => Press();
        void OnDisable() { if (selected) Focus(false); }
    }

    /// <summary>Five-second, source-aware dwell selection used by the VR pointer.</summary>
    public sealed class BoardDwellSelection
    {
        public const float DurationSeconds = 5f;
        BoardButton target;
        int sourceId = int.MinValue;
        float elapsed;
        bool fired;

        public float Advance(BoardButton nextTarget, int nextSourceId, float deltaTime,
            out BoardButton pressTarget)
        {
            pressTarget = null;
            if (!nextTarget || !nextTarget.isActiveAndEnabled)
            {
                Reset();
                return 0f;
            }

            if (nextTarget != target || nextSourceId != sourceId)
            {
                target = nextTarget;
                sourceId = nextSourceId;
                elapsed = 0f;
                fired = false;
            }
            else if (!fired)
            {
                elapsed += Mathf.Max(0f, deltaTime);
            }

            var progress = Mathf.Clamp01(elapsed / DurationSeconds);
            if (progress >= 1f && !fired)
            {
                fired = true;
                pressTarget = target;
            }
            return progress;
        }

        public void Press(BoardButton selected, int selectedSourceId)
        {
            if (!selected || !selected.isActiveAndEnabled) return;
            // Keep this target consumed until the pointer leaves it. Resetting
            // here used to start another countdown on Replay and toggle buttons.
            target = selected;
            sourceId = selectedSourceId;
            elapsed = DurationSeconds;
            fired = true;
            selected.Press();
        }

        public void Reset()
        {
            target = null;
            sourceId = int.MinValue;
            elapsed = 0f;
            fired = false;
        }
    }

    public sealed class BoardPointer : MonoBehaviour
    {
        struct PointerCandidate
        {
            public Ray ray;
            public BoardButton target;
            public RaycastHit targetHit;
            public bool pressed;
            public float maxLength;
            public int visualIndex;
            public int sourceId;
            public Vector3 visualPoint;
            public bool hasVisualPoint;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NativePoint { public double x, y; }
        [StructLayout(LayoutKind.Sequential)]
        struct NativeSize { public double width, height; }
        [StructLayout(LayoutKind.Sequential)]
        struct NativeRect { public NativePoint origin; public NativeSize size; }

#if UNITY_STANDALONE_OSX
        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        static extern IntPtr CGEventCreate(IntPtr source);
        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        static extern NativePoint CGEventGetLocation(IntPtr eventRef);
        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        static extern int CGGetDisplaysWithPoint(NativePoint point, uint maxDisplays,
            [Out] uint[] displays, out uint displayCount);
        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        static extern NativeRect CGDisplayBounds(uint display);
        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        static extern void CFRelease(IntPtr value);
#endif

        BoardButton focused;
        BoardButton keyboardFocus;
        readonly BoardDwellSelection dwellSelection = new();
        int selectedIndex = -1;
        readonly Dictionary<int, bool> previousPressState = new();
        int activeSourceId = int.MinValue;
        bool rightPinching;
        bool leftPinching;
        Vector3 guiMousePosition;
        bool hasGuiMousePosition;
        readonly List<InputDevice> devices = new();
        readonly List<PointerCandidate> candidates = new(5);
        readonly PointerVisual[] pointerVisuals = new PointerVisual[5];
        readonly Dictionary<int, Ray> smoothedRays = new();
        readonly HashSet<int> loggedControllerDevices = new();
        readonly HashSet<int> loggedLegacyControllerDevices = new();
        int lastInputControllerCount = -1;
        int lastLegacyControllerCount = -1;
        static readonly InputFeatureUsage<Vector3> PointerPosition = new("pointerPosition");
        static readonly InputFeatureUsage<Quaternion> PointerRotation = new("pointerRotation");
        Transform uiBoard;

        sealed class PointerVisual
        {
            public LineRenderer line;
            public LineRenderer dwellTrack;
            public LineRenderer dwellArc;
            public Transform reticle;
            public Renderer reticleRenderer;
            public Transform center;
            public Renderer centerRenderer;
            public Transform[] dwellDots;
            public Renderer[] dwellDotRenderers;
        }

        void Awake()
        {
            // Keep the visual independent of the camera/board hierarchy. A
            // world-space pointer must not inherit the camera pose a second
            // time or it becomes a giant, unstable wedge in stereo rendering.
            var pointerShader = Resources.Load<Shader>("BCSignVRPointer") ??
                Shader.Find("BCSignVR/Pointer");
            uiBoard = GameObject.Find("Learning board")?.transform;
            for (var index = 0; index < 5; index++)
            {
                var pointer = new GameObject($"VR UI pointer {index}");
                var line = pointer.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.startWidth = .008f;
                line.endWidth = .003f;
                line.widthMultiplier = 1f;
                line.alignment = LineAlignment.View;
                line.numCapVertices = 4;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sharedMaterial = pointerShader != null
                    ? new Material(pointerShader)
                    : ClassroomFactory.Material(ClassroomUi.Blue);
                line.enabled = false;

                var dwellTrackObject = new GameObject($"VR UI dwell track {index}");
                dwellTrackObject.transform.SetParent(pointer.transform, false);
                var dwellTrack = dwellTrackObject.AddComponent<LineRenderer>();
                dwellTrack.useWorldSpace = true;
                dwellTrack.positionCount = 41;
                dwellTrack.startWidth = .009f;
                dwellTrack.endWidth = .009f;
                dwellTrack.widthMultiplier = 1f;
                dwellTrack.alignment = LineAlignment.View;
                dwellTrack.numCapVertices = 6;
                dwellTrack.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dwellTrack.receiveShadows = false;
                dwellTrack.sharedMaterial = pointerShader != null
                    ? new Material(pointerShader)
                    : ClassroomFactory.Material(new Color(.70f, .85f, .91f));
                dwellTrack.enabled = false;

                var dwellArcObject = new GameObject($"VR UI dwell arc {index}");
                dwellArcObject.transform.SetParent(pointer.transform, false);
                var dwellArc = dwellArcObject.AddComponent<LineRenderer>();
                dwellArc.useWorldSpace = true;
                dwellArc.positionCount = 2;
                dwellArc.startWidth = .018f;
                dwellArc.endWidth = .018f;
                dwellArc.widthMultiplier = 1f;
                dwellArc.alignment = LineAlignment.View;
                dwellArc.numCapVertices = 6;
                dwellArc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dwellArc.receiveShadows = false;
                dwellArc.sharedMaterial = pointerShader != null
                    ? new Material(pointerShader)
                    : ClassroomFactory.Material(ClassroomUi.Green);
                dwellArc.enabled = false;

                var reticle = new GameObject($"VR UI reticle {index}", typeof(MeshFilter), typeof(MeshRenderer));
                var reticleMesh = BuildReticleMesh();
                reticleMesh.RecalculateBounds();
                reticle.GetComponent<MeshFilter>().sharedMesh = reticleMesh;
                reticle.GetComponent<MeshRenderer>().sharedMaterial = pointerShader != null
                    ? new Material(pointerShader)
                    : ClassroomFactory.Material(ClassroomUi.Blue);
                reticle.transform.localScale = Vector3.one * .03f;
                reticle.SetActive(false);

                var center = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                center.name = $"VR UI reticle center {index}";
                UnityEngine.Object.Destroy(center.GetComponent<Collider>());
                center.transform.SetParent(reticle.transform, false);
                center.transform.localScale = Vector3.one * .42f;
                center.GetComponent<Renderer>().sharedMaterial = pointerShader != null
                    ? new Material(pointerShader)
                    : ClassroomFactory.Material(ClassroomUi.Blue);

                // Match the reference cursor: twelve evenly spaced dots,
                // neutral grey while idle and charcoal as the dwell fills.
                const int dwellDotCount = 12;
                var dwellDots = new Transform[dwellDotCount];
                var dwellDotRenderers = new Renderer[dwellDotCount];
                for (var dotIndex = 0; dotIndex < dwellDotCount; dotIndex++)
                {
                    var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    dot.name = $"VR UI dwell dot {index} {dotIndex}";
                    UnityEngine.Object.Destroy(dot.GetComponent<Collider>());
                    dot.transform.SetParent(pointer.transform, false);
                    dot.transform.localScale = Vector3.one * .018f;
                    var dotRenderer = dot.GetComponent<Renderer>();
                    dotRenderer.sharedMaterial = pointerShader != null
                        ? new Material(pointerShader)
                        : ClassroomFactory.Material(new Color(.82f, .82f, .82f));
                    dot.SetActive(false);
                    dwellDots[dotIndex] = dot.transform;
                    dwellDotRenderers[dotIndex] = dotRenderer;
                }
                reticle.GetComponent<Renderer>().enabled = false;
                pointerVisuals[index] = new PointerVisual {
                    line = line,
                    dwellTrack = dwellTrack,
                    reticle = reticle.transform,
                    reticleRenderer = reticle.GetComponent<Renderer>(),
                    center = center.transform,
                    centerRenderer = center.GetComponent<Renderer>(),
                    dwellDots = dwellDots,
                    dwellDotRenderers = dwellDotRenderers
                };
                pointerVisuals[index].dwellArc = dwellArc;
            }
        }

        static Mesh BuildReticleMesh()
        {
            const int segments = 24;
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (var index = 0; index < segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                vertices[index * 2] = direction * .5f;
                vertices[index * 2 + 1] = direction * .30f;
                var next = (index + 1) % segments;
                var triangle = index * 6;
                triangles[triangle] = index * 2;
                triangles[triangle + 1] = next * 2;
                triangles[triangle + 2] = index * 2 + 1;
                triangles[triangle + 3] = index * 2 + 1;
                triangles[triangle + 4] = next * 2;
                triangles[triangle + 5] = next * 2 + 1;
            }
            var mesh = new Mesh { name = "VR UI reticle ring mesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        void HidePointers()
        {
            foreach (var visual in pointerVisuals)
            {
                if (visual == null) continue;
                visual.line.enabled = false;
                visual.dwellTrack.enabled = false;
                visual.dwellArc.enabled = false;
                visual.reticle.gameObject.SetActive(false);
                if (visual.dwellDots != null)
                    foreach (var dot in visual.dwellDots) if (dot != null) dot.gameObject.SetActive(false);
            }
        }

        void ShowPointer(int index, in PointerCandidate candidate, bool selected, float dwellProgress)
        {
            if (index < 0 || index >= pointerVisuals.Length) return;
            var visual = pointerVisuals[index];
            if (visual == null) return;
            // Keep the cursor quiet during sign practice, but let a controller
            // (or an intentional hand pinch) show its aim point anywhere on
            // the board. Requiring a button hit here hid the cursor precisely
            // when the learner needed it to aim at a button.
            var hasAimPoint = candidate.target != null || candidate.hasVisualPoint &&
                (IsControllerSource(candidate.sourceId) || candidate.pressed || candidate.sourceId == 0);
            if (!hasAimPoint) return;
            var ray = candidate.ray;
            var end = candidate.targetHit.collider != null ? candidate.targetHit.point :
                candidate.hasVisualPoint ? candidate.visualPoint : ray.origin + ray.direction * candidate.maxLength;
            // The cursor is intentionally neutral. Green belongs to lesson
            // feedback; it made the selection state look like a status light
            // instead of the calm Quest-style dwell indicator in the reference.
            var color = new Color(.08f, .08f, .08f, 1f);

            // Quest's own hand UI uses a compact cursor. A full beam here is
            // noisy in a classroom and is especially distracting when the
            // controller is resting on the floor, so keep the beam disabled.
            visual.line.enabled = false;

            visual.reticle.gameObject.SetActive(true);
            visual.reticle.position = end + ray.direction * .004f;
            visual.reticle.rotation = StableReticleRotation(ray.direction);
            visual.reticle.localScale = Vector3.one * .064f;
            visual.reticleRenderer.sharedMaterial.color = color;
            visual.centerRenderer.sharedMaterial.color = color;
            DrawDwellDots(visual, end + ray.direction * .008f, visual.reticle.rotation, dwellProgress);
        }

        static void DrawDwellDots(PointerVisual visual, Vector3 center, Quaternion rotation, float progress)
        {
            if (visual == null || visual.dwellDots == null) return;
            const int dotCount = 12;
            const float radius = .058f;
            var filled = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(progress) * dotCount), 0, dotCount);
            for (var index = 0; index < dotCount; index++)
            {
                var angle = Mathf.PI * .5f - index / (float)dotCount * Mathf.PI * 2f;
                var local = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                var dot = visual.dwellDots[index];
                dot.position = center + rotation * local;
                dot.rotation = rotation;
                dot.localScale = Vector3.one * .014f;
                visual.dwellDotRenderers[index].sharedMaterial.color = index < filled
                    ? new Color(.08f, .08f, .08f, 1f) : new Color(.82f, .82f, .82f, 1f);
                dot.gameObject.SetActive(true);
            }
        }

        Quaternion StableReticleRotation(Vector3 direction)
        {
            // Keep the spinner in a stable, front-facing view. Orienting it
            // to the pointer ray made the ring flip and become edge-on as a
            // hand/controller moved across the board.
            var camera = Camera.main;
            if (camera != null)
                return Quaternion.LookRotation(-camera.transform.forward, camera.transform.up);

            var up = Vector3.ProjectOnPlane(Vector3.up, direction);
            if (up.sqrMagnitude < .001f) up = Vector3.ProjectOnPlane(transform.up, direction);
            if (up.sqrMagnitude < .001f) up = Vector3.right;
            return Quaternion.LookRotation(-direction, up.normalized);
        }

        bool TryGetUiPoint(Ray ray, float maxLength, out Vector3 point)
        {
            point = default;
            if (!uiBoard) uiBoard = GameObject.Find("Learning board")?.transform;
            if (!uiBoard) return false;
            // Keep the cursor on the actual learning-board plane. Using the
            // nearest arbitrary collider made it jump onto the floor, tutor or
            // classroom props whenever a controller was a few degrees off.
            var plane = new Plane(-uiBoard.forward, uiBoard.TransformPoint(new Vector3(0, 0, -.045f)));
            if (!plane.Raycast(ray, out var distance) || distance <= .05f || distance >= maxLength)
                return false;

            var local = uiBoard.InverseTransformPoint(ray.GetPoint(distance));
            if (Mathf.Abs(local.x) > 3.6f || Mathf.Abs(local.y) > 2.35f)
                return false;
            local.z = -.045f;
            point = uiBoard.TransformPoint(local);
            return true;
        }

        void Update()
        {
            var camera = GetComponent<Camera>();
            if (!camera) return;
            var vr = XRSettings.isDeviceActive;
            HidePointers();
            // IMGUI reports the cursor in window-local coordinates on macOS,
            // including multi-display setups. Reuse that position for hover
            // and clicks instead of trying to reconstruct it from a global
            // Quartz cursor location.
            var mousePosition = hasGuiMousePosition ? guiMousePosition : DesktopMousePosition();
            var next = default(BoardButton);
            var pressTarget = default(BoardButton);
            var dwellPressTarget = default(BoardButton);
            var pressed = false;
            var justPressed = false;

            if (!vr)
            {
                var desktopCandidate = new PointerCandidate {
                    ray = camera.ScreenPointToRay(mousePosition),
                    maxLength = 20f,
                    visualIndex = -1,
                    sourceId = -1
                };
                ResolveCandidate(ref desktopCandidate);
                next = desktopCandidate.target;
                pressTarget = desktopCandidate.target;
            }
            else
            {
                BuildVrCandidates();
                var selectedCandidate = ChooseActiveCandidate();

                if (selectedCandidate >= 0)
                {
                    var candidate = candidates[selectedCandidate];
                    var dwellProgress = UpdateDwell(candidate.target, candidate.sourceId, out dwellPressTarget);
                    ShowPointer(candidate.visualIndex, in candidate, true, dwellProgress);
                }
                else ResetDwell();

                if (selectedCandidate >= 0)
                {
                    var selected = candidates[selectedCandidate];
                    next = selected.target;
                    pressTarget = selected.target;
                    pressed = selected.pressed;
                    previousPressState.TryGetValue(selected.sourceId, out var wasPressed);
                    justPressed = pressed && !wasPressed;
                    activeSourceId = selected.sourceId;
                }
                foreach (var candidate in candidates)
                    previousPressState[candidate.sourceId] = candidate.pressed;
            }
            if (next != focused) { if (focused) focused.Focus(false); focused = next; if (focused) focused.Focus(true); }
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                var buttons = FindObjectsByType<BoardButton>(FindObjectsSortMode.InstanceID);
                if (buttons.Length > 0)
                {
                    Array.Sort(buttons, (a, b) =>
                    {
                        var aPoint = camera.WorldToScreenPoint(a.transform.position);
                        var bPoint = camera.WorldToScreenPoint(b.transform.position);
                        var row = Mathf.Abs(aPoint.y - bPoint.y) > 24f ? bPoint.y.CompareTo(aPoint.y) : 0;
                        return row != 0 ? row : aPoint.x.CompareTo(bPoint.x);
                    });
                    if (keyboardFocus) keyboardFocus.Focus(false); selectedIndex = (selectedIndex + 1) % buttons.Length;
                    keyboardFocus = buttons[selectedIndex]; if (keyboardFocus) keyboardFocus.Focus(true);
                }
            }
            if (Input.GetMouseButtonDown(0))
            {
                var target = focused ?? ButtonAtScreenPoint(camera, mousePosition);
                if (target) { ResetDwell(); target.Press(); }
            }
            if (justPressed && pressTarget)
            {
                dwellSelection.Press(pressTarget, activeSourceId);
            }
            else if (dwellPressTarget)
            {
                dwellSelection.Press(dwellPressTarget, activeSourceId);
            }
            if (Input.GetKeyDown(KeyCode.Return)) { var target = keyboardFocus ? keyboardFocus : focused; if (target) target.Press(); }
        }

        float UpdateDwell(BoardButton target, int sourceId, out BoardButton pressTarget)
        {
            return dwellSelection.Advance(target, sourceId, Time.unscaledDeltaTime, out pressTarget);
        }

        void ResetDwell() => dwellSelection.Reset();

        void BuildVrCandidates()
        {
            candidates.Clear();

            // Prefer the tracked index finger when Quest is in hand mode. The
            // controller devices can remain listed by the runtime for a few
            // frames after the user puts them down, which previously made the
            // cursor appear to ignore the user's hand.
            var handSubsystem = HandTracking.subsystem;
            if (handSubsystem != null && handSubsystem.running)
            {
                if (TryGetHandRay(handSubsystem.rightHand, ref rightPinching, out var rightRay, out var rightPinch))
                {
                    var candidate = new PointerCandidate {
                        ray = StabilizeRay(4, rightRay), pressed = rightPinch, maxLength = 4.2f,
                        visualIndex = candidates.Count, sourceId = 4
                    };
                    ResolveCandidate(ref candidate); candidates.Add(candidate);
                }
                if (TryGetHandRay(handSubsystem.leftHand, ref leftPinching, out var leftRay, out var leftPinch))
                {
                    var candidate = new PointerCandidate {
                        ray = StabilizeRay(3, leftRay), pressed = leftPinch, maxLength = 4.2f,
                        visualIndex = candidates.Count, sourceId = 3
                    };
                    ResolveCandidate(ref candidate); candidates.Add(candidate);
                }
            }
            else
            {
                rightPinching = false;
                leftPinching = false;
            }

            // Read Quest Touch Plus directly through its OpenXR Input System
            // layout first. That layout exposes a real Pointer Pose and named
            // trigger/A/B controls; relying only on legacy XR device discovery
            // can leave a perfectly paired controller without a usable ray.
            var inputLeft = NewInputSystem.GetDevice<NewXRController>(
                UnityEngine.InputSystem.CommonUsages.LeftHand);
            var inputRight = NewInputSystem.GetDevice<NewXRController>(
                UnityEngine.InputSystem.CommonUsages.RightHand);
            var inputControllerCount = 0;
            foreach (var inputDevice in NewInputSystem.devices)
            {
                if (!(inputDevice is NewXRController inputController)) continue;
                inputControllerCount++;
                if (loggedControllerDevices.Add(inputController.deviceId))
                    Debug.Log($"BCSignVR Input System controller found: {inputController.displayName} ({inputController.name}).");
            }
            if (inputControllerCount != lastInputControllerCount)
            {
                lastInputControllerCount = inputControllerCount;
                Debug.Log($"BCSignVR Input System controller count: {inputControllerCount}.");
            }
            var inputLeftAdded = TryAddInputSystemController(inputLeft, 1);
            var inputRightAdded = TryAddInputSystemController(inputRight, 2);

            devices.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);
            if (devices.Count != lastLegacyControllerCount)
            {
                lastLegacyControllerCount = devices.Count;
                Debug.Log($"BCSignVR legacy XR controller count: {devices.Count}.");
            }
            devices.Sort((a, b) =>
            {
                var aLeft = (a.characteristics & InputDeviceCharacteristics.Left) != 0;
                var bLeft = (b.characteristics & InputDeviceCharacteristics.Left) != 0;
                // Prefer the right hand when neither pointer is over a
                // control; it is the usual dominant-hand cursor on Quest.
                return aLeft.CompareTo(bLeft);
            });
            foreach (var device in devices)
            {
                var sourceId = (device.characteristics & InputDeviceCharacteristics.Left) != 0 ? 1 : 2;
                var deviceKey = (device.name ?? string.Empty).GetHashCode() ^ (sourceId << 28);
                if (loggedLegacyControllerDevices.Add(deviceKey))
                    Debug.Log($"BCSignVR legacy XR controller found: {device.name}, source {sourceId}, valid={device.isValid}.");
                if (sourceId == 1 && inputLeftAdded || sourceId == 2 && inputRightAdded) continue;
                if (!TryGetControllerRay(device, out var ray)) continue;
                var candidate = new PointerCandidate {
                    ray = StabilizeRay(sourceId, ray),
                    pressed = IsControllerPressed(device),
                    maxLength = 4.2f,
                    visualIndex = candidates.Count,
                    sourceId = sourceId
                };
                ResolveCandidate(ref candidate);
                candidates.Add(candidate);
            }

            if (candidates.Count == 0)
            {
                var gazeCandidate = new PointerCandidate {
                    ray = new Ray(transform.position, transform.forward),
                    maxLength = 4.2f,
                    visualIndex = 0,
                    sourceId = 0
                };
                ResolveCandidate(ref gazeCandidate);
                candidates.Add(gazeCandidate);
            }
        }

        int ChooseActiveCandidate()
        {
            if (candidates.Count == 0) return -1;

            // A deliberate press can hand control to the other hand, even
            // while the dominant-hand cursor is still visible.
            for (var index = 0; index < candidates.Count; index++)
                if (candidates[index].pressed && candidates[index].target != null) return index;

            // A controller intentionally aimed at a control should take over
            // from idle hand tracking so the Quest remotes stay first-class
            // pointers when the learner picks them up. An idle controller
            // pointed elsewhere must not steal focus from a hand over a button.
            for (var index = 0; index < candidates.Count; index++)
                if ((candidates[index].sourceId == 2 || candidates[index].sourceId == 1) &&
                    candidates[index].target != null) return index;

            // The right hand is the default in hand mode, but cursor/dwell is
            // visible only if it really lands on a selectable control.
            for (var index = 0; index < candidates.Count; index++)
                if ((candidates[index].sourceId == 4 || candidates[index].sourceId == 3) &&
                    candidates[index].target != null) return index;

            // Once a controller ray reaches the board, keep it as the active
            // cursor even when it is between buttons. This makes the dot and
            // five-second countdown discoverable instead of requiring blind
            // button hunting.
            for (var index = 0; index < candidates.Count; index++)
                if (IsControllerSource(candidates[index].sourceId) && candidates[index].hasVisualPoint)
                    return index;

            // A pinched hand is also an explicit selection gesture; show its
            // cursor on the board even if the pinch starts between controls.
            for (var index = 0; index < candidates.Count; index++)
                if (candidates[index].pressed && candidates[index].hasVisualPoint) return index;

            // Preserve the last-used pointer only after giving any currently
            // aimed hand/controller target a chance to take over.
            for (var index = 0; index < candidates.Count; index++)
                if (candidates[index].sourceId == activeSourceId) return index;

            for (var index = 0; index < candidates.Count; index++)
                if (candidates[index].sourceId == 4 || candidates[index].sourceId == 3) return index;

            // Devices are sorted right-hand first, so a fresh controller
            // session starts with the expected dominant-hand pointer.
            return 0;
        }

        static bool IsControllerSource(int sourceId) => sourceId == 1 || sourceId == 2;

        bool TryAddInputSystemController(NewXRController controller, int sourceId)
        {
            if (!TryGetInputSystemControllerRay(controller, out var ray, out var pressed)) return false;
            var candidate = new PointerCandidate {
                ray = StabilizeRay(sourceId, ray),
                pressed = pressed,
                maxLength = 4.2f,
                visualIndex = candidates.Count,
                sourceId = sourceId
            };
            ResolveCandidate(ref candidate);
            candidates.Add(candidate);
            return true;
        }

        static bool TryGetInputSystemControllerRay(NewXRController controller, out Ray ray, out bool pressed)
        {
            ray = default;
            pressed = false;
            if (controller == null || !controller.enabled) return false;

            var tracked = controller.TryGetChildControl<NewButtonControl>("isTracked");
            if (tracked != null && !tracked.isPressed) return false;

            var positionControl = controller.TryGetChildControl<NewVector3Control>("pointerPosition") ??
                controller.TryGetChildControl<NewVector3Control>("devicePosition");
            var rotationControl = controller.TryGetChildControl<NewQuaternionControl>("pointerRotation") ??
                controller.TryGetChildControl<NewQuaternionControl>("deviceRotation");
            if (positionControl == null || rotationControl == null) return false;

            var position = positionControl.ReadValue();
            var rotation = rotationControl.ReadValue();
            var direction = rotation * Vector3.forward;
            if (!IsFinite(position) || !IsFinite(direction) || direction.sqrMagnitude < .5f ||
                !IsValidRotation(rotation) || position.sqrMagnitude < .0001f)
                return false;

            var triggerPressed = controller.TryGetChildControl<NewButtonControl>("triggerPressed");
            var triggerAxis = controller.TryGetChildControl<NewAxisControl>("trigger");
            var primary = controller.TryGetChildControl<NewButtonControl>("primaryButton");
            var secondary = controller.TryGetChildControl<NewButtonControl>("secondaryButton");
            pressed = triggerPressed != null && triggerPressed.isPressed ||
                triggerAxis != null && triggerAxis.ReadValue() > .55f ||
                primary != null && primary.isPressed || secondary != null && secondary.isPressed;
            ray = new Ray(position, direction.normalized);
            return true;
        }

        Ray StabilizeRay(int sourceId, Ray raw)
        {
            if (!smoothedRays.TryGetValue(sourceId, out var previous))
            {
                smoothedRays[sourceId] = raw;
                return raw;
            }
            var response = sourceId == 3 || sourceId == 4 ? 12f : 19f;
            var blend = 1f - Mathf.Exp(-response * Mathf.Max(Time.unscaledDeltaTime, .001f));
            var origin = Vector3.Lerp(previous.origin, raw.origin, blend);
            var direction = Vector3.Slerp(previous.direction, raw.direction, blend).normalized;
            var result = new Ray(origin, direction);
            smoothedRays[sourceId] = result;
            return result;
        }

        void ResolveCandidate(ref PointerCandidate candidate)
        {
            candidate.target = FindButton(candidate.ray, candidate.maxLength, out candidate.targetHit);
            if (candidate.target == null)
            {
                candidate.hasVisualPoint = TryGetUiPoint(candidate.ray, candidate.maxLength, out candidate.visualPoint);
                // Never silently turn a hand/controller ray into a head-gaze
                // ray: that displayed the dwell cursor while the learner was
                // signing and could auto-activate a control they were not aiming at.
            }
        }

        static bool IsControllerPressed(InputDevice device)
        {
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out var trigger) && trigger) return true;
            if (device.TryGetFeatureValue(CommonUsages.trigger, out var triggerAxis) && triggerAxis > .55f) return true;
            if (device.TryGetFeatureValue(CommonUsages.primaryButton, out var primary) && primary) return true;
            return device.TryGetFeatureValue(CommonUsages.secondaryButton, out var secondary) && secondary;
        }

        static BoardButton FindButton(Ray ray, float maxDistance, out RaycastHit buttonHit)
        {
            buttonHit = default;
            // Decorative card meshes can be in front of a button. Search all
            // hits and pick the nearest button collider instead of assuming the
            // first hit is interactive.
            var hits = Physics.RaycastAll(ray, maxDistance, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var button = hit.collider.GetComponentInParent<BoardButton>();
                if (button && button.isActiveAndEnabled)
                {
                    buttonHit = hit;
                    return button;
                }
            }
            return null;
        }

        static bool TryGetControllerRay(InputDevice device, out Ray ray)
        {
            ray = default;
            if (!device.isValid) return false;

            // Quest exposes a dedicated aim pose for the controller. The
            // generic device pose is the grip pose and points down along the
            // handle, which makes a ray appear to aim at the floor instead of
            // following the controller's laser pointer.
            var position = Vector3.zero;
            var rotation = Quaternion.identity;
            var hasAimPose = TryGetInputSystemAimPose(device, out position, out rotation) ||
                device.TryGetFeatureValue(PointerPosition, out position) &&
                device.TryGetFeatureValue(PointerRotation, out rotation) &&
                position.sqrMagnitude > .0001f;
            if (!hasAimPose &&
                (!device.TryGetFeatureValue(CommonUsages.devicePosition, out position) ||
                 !device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation)))
                return false;
            if (device.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) && !tracked) return false;
            var direction = rotation * Vector3.forward;
            if (!IsFinite(position) || !IsFinite(direction) || direction.sqrMagnitude < .5f) return false;
            ray = new Ray(position, direction.normalized);
            return true;
        }

        static bool TryGetInputSystemAimPose(InputDevice legacyDevice, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            var usage = (legacyDevice.characteristics & InputDeviceCharacteristics.Left) != 0
                ? UnityEngine.InputSystem.CommonUsages.LeftHand
                : UnityEngine.InputSystem.CommonUsages.RightHand;
            var controller = NewInputSystem.GetDevice<NewXRController>(usage);
            if (controller == null) return false;
            var pointerPosition = controller.TryGetChildControl<NewVector3Control>("pointerPosition");
            var pointerRotation = controller.TryGetChildControl<NewQuaternionControl>("pointerRotation");
            if (pointerPosition == null || pointerRotation == null) return false;
            position = pointerPosition.ReadValue();
            rotation = pointerRotation.ReadValue();
            return IsFinite(position) && IsValidRotation(rotation) && position.sqrMagnitude > .0001f;
        }

        static bool TryGetHandRay(XRHand hand, ref bool pinchState, out Ray ray, out bool pinching)
        {
            ray = default;
            pinching = false;
            if (!hand.isTracked) { pinchState = false; return false; }
            var hasIndexBase = hand.GetJoint(XRHandJointID.IndexMetacarpal).TryGetPose(out var indexBase);
            if (!hasIndexBase)
                hasIndexBase = hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out indexBase);
            if (!hasIndexBase || !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var tip))
                return false;
            var direction = tip.position - indexBase.position;
            if (direction.sqrMagnitude < .0001f) return false;
            // Keep the cursor tied to the visible index finger. Quest's
            // optional hand-aim pose can be reported in a different space on
            // some runtimes, which makes the cursor jump when pinching.
            ray = new Ray(tip.position + direction.normalized * .008f, direction.normalized);
            if (hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb))
            {
                var distance = Vector3.Distance(thumb.position, tip.position);
                // Hysteresis prevents a noisy frame at the pinch threshold
                // from making the pointer repeatedly press/release.
                if (!pinchState) pinchState = distance < .028f;
                else if (distance > .045f) pinchState = false;
                pinching = pinchState;
            }
            else pinchState = false;
            return true;
        }

        static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        static bool IsValidRotation(Quaternion value)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x) ||
                float.IsNaN(value.y) || float.IsInfinity(value.y) ||
                float.IsNaN(value.z) || float.IsInfinity(value.z) ||
                float.IsNaN(value.w) || float.IsInfinity(value.w)) return false;
            var length = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
            return length > .5f;
        }

        // Some macOS window configurations deliver the native mouse event to
        // Unity's IMGUI loop but do not populate Input.mousePosition. Mirror the
        // click through that path so desktop buttons remain usable in a window.
        void OnGUI()
        {
            var evt = Event.current;
            if (evt == null) return;
            guiMousePosition = new Vector3(evt.mousePosition.x, Screen.height - evt.mousePosition.y, 0);
            hasGuiMousePosition = true;
            if (evt.type != EventType.MouseDown || evt.button != 0) return;
            var camera = GetComponent<Camera>();
            if (!camera) return;
            var button = ButtonAtScreenPoint(camera, guiMousePosition);
            if (button)
            {
                button.Press();
                evt.Use();
            }
        }

        static Vector3 DesktopMousePosition()
        {
#if UNITY_STANDALONE_OSX
            var nativeEvent = CGEventCreate(IntPtr.Zero);
            if (nativeEvent != IntPtr.Zero)
            {
                var location = CGEventGetLocation(nativeEvent);
                CFRelease(nativeEvent);
                var displayIds = new uint[1];
                if (CGGetDisplaysWithPoint(location, 1, displayIds, out var displayCount) == 0 && displayCount > 0)
                {
                    var displayBounds = CGDisplayBounds(displayIds[0]);
                    location.x -= displayBounds.origin.x;
                    location.y -= displayBounds.origin.y;
                }
                var window = Screen.mainWindowPosition;
                return new Vector3((float)location.x - window.x,
                    Screen.height - ((float)location.y - window.y), 0);
            }
#endif
            var relative = Display.RelativeMouseAt(Input.mousePosition);
            return relative == Vector3.zero ? Input.mousePosition : relative;
        }

        static BoardButton ButtonAtScreenPoint(Camera camera, Vector3 point)
        {
            var buttons = FindObjectsByType<BoardButton>(FindObjectsSortMode.None);
            foreach (var button in buttons)
            {
                if (!button || !button.isActiveAndEnabled) continue;
                var collider = button.GetComponent<Collider>();
                if (!collider) continue;
                var bounds = collider.bounds;
                var min = camera.WorldToScreenPoint(bounds.min);
                var max = camera.WorldToScreenPoint(bounds.max);
                var rect = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                    Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
                if (rect.Contains(point)) return button;
            }
            return null;
        }
    }
}
