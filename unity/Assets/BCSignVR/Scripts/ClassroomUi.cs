using System;
using System.Collections.Generic;
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
            Action action, bool primary = false, float height = .45f)
        {
            var root = Panel(label, parent, new Vector3(x, y, -.03f), new Vector2(width, height),
                primary ? Blue : Color.white, .12f, true);
            var button = root.AddComponent<BoardButton>();
            var text = Label(label, root.transform, 0, 0, .17f, primary ? Color.white : DeepBlue,
                TextAnchor.MiddleCenter, width - .14f);
            button.Initialize(action, primary, text);
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
        public void Initialize(Action callback, bool primary, TextMesh label)
        {
            action = callback; normal = primary ? ClassroomUi.Blue : Color.white;
            face = transform.Find("Face").GetComponent<Renderer>();
        }
        public void Focus(bool value)
        {
            if (!this || !gameObject || !transform) return;
            selected = value;
            if (face != null) face.material.color = value ? Color.Lerp(normal, ClassroomUi.Blue, .22f) : normal;
            transform.localScale = value ? new Vector3(1.025f, 1.025f, 1) : Vector3.one;
        }
        public void Press() { if (isActiveAndEnabled) action?.Invoke(); }
        void OnDisable() { if (selected) Focus(false); }
    }

    public sealed class BoardPointer : MonoBehaviour
    {
        BoardButton focused;
        BoardButton keyboardFocus;
        int selectedIndex = -1;
        bool wasPressed;
        readonly List<InputDevice> devices = new();
        void Update()
        {
            var camera = GetComponent<Camera>();
            var vr = XRSettings.isDeviceActive;
            var ray = vr ? new Ray(transform.position, transform.forward) : camera.ScreenPointToRay(Input.mousePosition);
            BoardButton next = null;
            if (Physics.Raycast(ray, out var hit, 20f)) next = hit.collider.GetComponent<BoardButton>();
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
            var pressed = false;
            if (vr)
            {
                devices.Clear(); InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);
                foreach (var device in devices)
                    if (device.TryGetFeatureValue(CommonUsages.triggerButton, out var trigger) && trigger) pressed = true;
                var hand = UnityEngine.XR.Hands.OpenXR.HandTracking.subsystem;
                if (hand != null && hand.running && hand.rightHand.isTracked &&
                    hand.rightHand.GetJoint(UnityEngine.XR.Hands.XRHandJointID.ThumbTip).TryGetPose(out var thumb) &&
                    hand.rightHand.GetJoint(UnityEngine.XR.Hands.XRHandJointID.IndexTip).TryGetPose(out var index))
                    pressed |= Vector3.Distance(thumb.position, index.position) < .023f;
            }
            if (Input.GetMouseButtonDown(0) || pressed && !wasPressed) { if (focused) focused.Press(); }
            if (Input.GetKeyDown(KeyCode.Return)) { var target = keyboardFocus ? keyboardFocus : focused; if (target) target.Press(); }
            wasPressed = pressed;
        }
    }
}
