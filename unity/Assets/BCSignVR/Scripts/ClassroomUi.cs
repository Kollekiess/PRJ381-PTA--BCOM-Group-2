using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR;

namespace BCSignVR
{
    public static class ClassroomUi
    {
        public static readonly Color Blue = new(.14f, .58f, .81f);
        public static readonly Color DeepBlue = new(.08f, .39f, .60f);
        public static readonly Color Ink = new(.18f, .26f, .33f);
        public static readonly Color Muted = new(.40f, .49f, .55f);
        public static readonly Color Line = new(.77f, .86f, .90f);
        public static readonly Color Paper = new(.97f, .99f, 1f);
        public static readonly Color Green = new(.10f, .59f, .43f);

        public static GameObject Panel(string name, Transform parent, Vector3 center, Vector2 size,
            Color color, float radius = .15f, bool border = false)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = center;
            if (border)
            {
                Surface("Soft shadow", root.transform, new Vector3(0, -.035f, .025f), size + Vector2.one * .045f,
                    new Color(.72f, .80f, .84f), radius);
                Surface("Border", root.transform, Vector3.zero, size, Line, radius);
                Surface("Face", root.transform, new Vector3(0, 0, -.008f), size - Vector2.one * .025f, color, radius - .01f);
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
            Action action, bool primary = false, float height = .45f, bool disabled = false)
        {
            var root = Panel(label, parent, new Vector3(x, y, -.03f), new Vector2(width, height),
                disabled ? new Color(.90f, .93f, .94f) : primary ? Blue : Color.white, .12f, true);
            var button = root.AddComponent<BoardButton>();
            var text = Label(label, root.transform, 0, 0, .17f,
                disabled ? Muted : primary ? Color.white : DeepBlue,
                TextAnchor.MiddleCenter, width - .14f);
            button.Initialize(action, primary, text, disabled);
            var collider = root.AddComponent<BoxCollider>(); collider.size = new Vector3(width, height, .08f);
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
        Renderer face;
        bool selected;
        bool disabled;
        float lastPressedAt = -10f;
        public bool Interactable => !disabled;
        public void Initialize(Action callback, bool primary, TextMesh label, bool isDisabled = false)
        {
            disabled = isDisabled;
            action = callback; normal = isDisabled ? new Color(.90f, .93f, .94f) : primary ? ClassroomUi.Blue : Color.white;
            face = transform.Find("Face").GetComponent<Renderer>();
            if (face != null) face.material.color = normal;
            if (label != null) label.color = (isDisabled ? ClassroomUi.Muted : primary ? Color.white : ClassroomUi.DeepBlue).linear;
        }
        public void Focus(bool value)
        {
            if (!this || !gameObject || !transform) return;
            if (disabled) { selected = false; return; }
            selected = value;
            if (face != null) face.material.color = value ? Color.Lerp(normal, ClassroomUi.Blue, .22f) : normal;
            transform.localScale = value ? new Vector3(1.025f, 1.025f, 1) : Vector3.one;
        }
        public void Press()
        {
            if (disabled || !isActiveAndEnabled || action == null || Time.unscaledTime - lastPressedAt < .12f) return;
            lastPressedAt = Time.unscaledTime;
            action.Invoke();
        }
        // Desktop builds also receive Unity's native collider click. Keeping this
        // path alongside BoardPointer makes the classroom usable when the cursor
        // is over a modal button, while Press's debounce prevents double actions.
        void OnMouseDown() => Press();
        void OnMouseUpAsButton() => Press();
        void OnDisable() { if (selected) Focus(false); }
    }

    public sealed class BoardPointer : MonoBehaviour
    {
        const float GazeSelectSeconds = 5f;
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
        int selectedIndex = -1;
        Vector3 guiMousePosition;
        bool hasGuiMousePosition;
        float gazeStartedAt = -1f;
        GazeSelectionRing gazeRing;
        void Awake() => gazeRing = new GazeSelectionRing();
        void Update()
        {
            var camera = GetComponent<Camera>();
            var vr = XRSettings.isDeviceActive;
            // IMGUI reports the cursor in window-local coordinates on macOS,
            // including multi-display setups. Reuse that position for hover
            // and clicks instead of trying to reconstruct it from a global
            // Quartz cursor location.
            var mousePosition = hasGuiMousePosition ? guiMousePosition : DesktopMousePosition();
            var ray = vr ? new Ray(transform.position, transform.forward) : camera.ScreenPointToRay(mousePosition);
            BoardButton next = null;
            // Decorative card meshes can be in front of a button. Search all
            // hits and pick the nearest button collider instead of assuming the
            // first hit is interactive.
            var hits = Physics.RaycastAll(ray, 20f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            var nextHit = default(RaycastHit);
            foreach (var hit in hits)
            {
                var candidate = hit.collider.GetComponentInParent<BoardButton>();
                if (candidate && candidate.Interactable) { next = candidate; nextHit = hit; break; }
            }
            if (next != focused)
            {
                if (focused) focused.Focus(false);
                focused = next;
                if (focused) focused.Focus(true);
                gazeStartedAt = focused ? Time.unscaledTime : -1f;
            }
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                var buttons = new List<BoardButton>();
                foreach (var button in FindObjectsByType<BoardButton>(FindObjectsSortMode.InstanceID))
                    if (button && button.Interactable) buttons.Add(button);
                if (buttons.Count > 0)
                {
                    buttons.Sort((a, b) =>
                    {
                        var aPoint = camera.WorldToScreenPoint(a.transform.position);
                        var bPoint = camera.WorldToScreenPoint(b.transform.position);
                        var row = Mathf.Abs(aPoint.y - bPoint.y) > 24f ? bPoint.y.CompareTo(aPoint.y) : 0;
                        return row != 0 ? row : aPoint.x.CompareTo(bPoint.x);
                    });
                    if (keyboardFocus) keyboardFocus.Focus(false); selectedIndex = (selectedIndex + 1) % buttons.Count;
                    keyboardFocus = buttons[selectedIndex]; if (keyboardFocus) keyboardFocus.Focus(true);
                }
            }
            if (vr)
            {
                if (focused && gazeStartedAt >= 0)
                {
                    var progress = Mathf.Clamp01((Time.unscaledTime - gazeStartedAt) / GazeSelectSeconds);
                    gazeRing.Show(nextHit.point, nextHit.normal, camera.transform.up, camera.transform.forward, progress);
                    if (progress >= 1f)
                    {
                        focused.Press();
                        gazeStartedAt = Time.unscaledTime;
                    }
                }
                else gazeRing.Hide();
            }
            else
            {
                gazeStartedAt = -1f;
                gazeRing.Hide();
            }
            if (Input.GetMouseButtonDown(0))
            {
                var target = focused ?? ButtonAtScreenPoint(camera, mousePosition);
                if (target) target.Press();
            }
            if (Input.GetKeyDown(KeyCode.Return)) { var target = keyboardFocus ? keyboardFocus : focused; if (target) target.Press(); }
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
                if (!button || !button.isActiveAndEnabled || !button.Interactable) continue;
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

    sealed class GazeSelectionRing
    {
        readonly GameObject root;
        readonly LineRenderer track;
        readonly LineRenderer progress;
        readonly Transform progressTip;
        readonly Transform focusDot;
        const int TrackPoints = 64;
        const float Radius = .105f;

        public GazeSelectionRing()
        {
            root = new GameObject("VR gaze countdown");
            track = CreateLine("Gaze countdown track", new Color(.72f, .84f, .88f));
            progress = CreateLine("Gaze countdown progress", ClassroomUi.Blue);
            progressTip = CreateDot("Gaze countdown tip", ClassroomUi.Blue, .034f);
            focusDot = CreateDot("Gaze countdown centre", new Color(.72f, .84f, .88f), .020f);
            track.loop = true;
            root.SetActive(false);
        }

        LineRenderer CreateLine(string name, Color color)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(root.transform, false);
            line.useWorldSpace = false;
            // Keep the stroke in the ring's own XY plane. View alignment can
            // billboard each segment independently on a tilted spatial board,
            // which makes the countdown look like it has fallen flat.
            line.alignment = LineAlignment.TransformZ;
            line.textureMode = LineTextureMode.Stretch;
            line.widthMultiplier = name.EndsWith("progress") ? .022f : .016f;
            line.numCapVertices = 6;
            line.numCornerVertices = 4;
            line.sharedMaterial = ClassroomFactory.Material(color);
            return line;
        }

        Transform CreateDot(string name, Color color, float diameter)
        {
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = name;
            dot.transform.SetParent(root.transform, false);
            dot.transform.localScale = Vector3.one * diameter;
            dot.GetComponent<Renderer>().sharedMaterial = ClassroomFactory.Material(color);
            UnityEngine.Object.Destroy(dot.GetComponent<Collider>());
            return dot.transform;
        }

        public void Show(Vector3 position, Vector3 normal, Vector3 up, Vector3 cameraForward, float amount)
        {
            root.SetActive(true);
            root.transform.position = position + normal * .014f;
            // Face the learner instead of inheriting a potentially horizontal
            // surface basis from a tilted spatial board. Project the headset's
            // up vector onto the ring plane so the countdown stays upright and
            // legible throughout the five-second gaze selection.
            var faceNormal = -cameraForward;
            if (faceNormal.sqrMagnitude < .0001f) faceNormal = normal;
            faceNormal.Normalize();
            var ringUp = Vector3.ProjectOnPlane(up, faceNormal);
            if (ringUp.sqrMagnitude < .0001f)
                ringUp = Vector3.ProjectOnPlane(Vector3.up, faceNormal);
            if (ringUp.sqrMagnitude < .0001f)
                ringUp = Vector3.Cross(faceNormal, Vector3.right);
            root.transform.rotation = Quaternion.LookRotation(faceNormal, ringUp.normalized);
            track.positionCount = TrackPoints;
            for (var i = 0; i < TrackPoints; i++)
                track.SetPosition(i, Point(i / (float)TrackPoints * Mathf.PI * 2f));

            amount = Mathf.Clamp01(amount);
            var points = Mathf.Max(2, Mathf.CeilToInt(TrackPoints * amount) + 1);
            progress.positionCount = points;
            progress.gameObject.SetActive(amount > 0f);
            for (var i = 0; i < points; i++)
            {
                var angle = -Mathf.PI * .5f + Mathf.Clamp01(i / (float)(points - 1)) * Mathf.PI * 2f * amount;
                progress.SetPosition(i, Point(angle));
            }
            var tipAngle = -Mathf.PI * .5f + Mathf.PI * 2f * amount;
            progressTip.localPosition = Point(tipAngle);
            progressTip.gameObject.SetActive(amount > 0f);
            var pulse = 1f + Mathf.Sin(Time.unscaledTime * 5.5f) * .08f;
            focusDot.localScale = Vector3.one * (.020f * pulse);
            focusDot.gameObject.SetActive(true);
        }

        static Vector3 Point(float angle) => new(Mathf.Cos(angle) * Radius, Mathf.Sin(angle) * Radius, 0);
        public void Hide()
        {
            root.SetActive(false);
            progressTip.gameObject.SetActive(false);
            focusDot.gameObject.SetActive(false);
        }
    }
}
