using UnityEngine;

namespace BCSignVR
{
    public static class ClassroomFactory
    {
        public static readonly Color Ink = new(.18f, .26f, .33f);
        public static readonly Color Aqua = new(.16f, .64f, .76f);
        public static readonly Color Mint = new(.76f, .90f, .87f);
        public static readonly Color Coral = new(.95f, .57f, .43f);
        public static readonly Color Sunshine = new(.94f, .82f, .49f);
        public static readonly Color Cream = new(.91f, .93f, .94f);
        public static readonly Color Sky = new(.87f, .95f, .99f);

        public static Transform Build()
        {
            var root = new GameObject("Sign Club Classroom").transform;
            Cube("Warm classroom floor", root, new Vector3(0, -.08f, 2), new Vector3(15, .12f, 14), new Color(.77f, .66f, .52f));
            Cube("White back wall", root, new Vector3(0, 2.8f, 6), new Vector3(15, 5.6f, .12f), new Color(.95f, .94f, .88f));
            Cube("Sky band", root, new Vector3(0, 4.75f, 5.90f), new Vector3(15, .78f, .03f), Sky);
            Cube("Skirting", root, new Vector3(0, .20f, 5.87f), new Vector3(15, .16f, .05f), new Color(.72f, .83f, .87f));
            for (var x = -7; x <= 7; x++)
                    Cube("Floor seam", root, new Vector3(x, -.013f, 2), new Vector3(.008f, .004f, 14), new Color(.67f, .56f, .44f));
            for (var z = -4; z <= 8; z++)
                Cube("Floor seam", root, new Vector3(0, -.012f, z), new Vector3(15, .004f, .008f), new Color(.70f, .59f, .47f));
            // A quiet reading corner gives the lecturer a real classroom setting.
            Cube("Book shelf", root, new Vector3(-4.2f, .82f, 5.65f), new Vector3(1.6f, .12f, .40f), new Color(.66f, .46f, .30f));
            for (var book = 0; book < 7; book++)
                Cube("Colourful book", root, new Vector3(-4.78f + book * .18f, 1.09f, 5.66f),
                    new Vector3(.14f, .40f + (book % 3) * .07f, .25f),
                    book % 3 == 0 ? Aqua : book % 3 == 1 ? Coral : Sunshine);
            var window = ClassroomUi.Panel("Window frame", root, new Vector3(-4.2f, 2.67f, 5.80f),
                new Vector2(1.55f, 1.13f), Color.white, .08f, true).transform;
            ClassroomUi.Panel("Blue glass", window, new Vector3(0, 0, -.03f), new Vector2(1.35f, .94f), new Color(.69f, .85f, .94f), .05f);
            ClassroomUi.Panel("Window crossbar", window, new Vector3(0, 0, -.045f), new Vector2(1.35f, .04f), Color.white, .01f);
            ClassroomUi.Panel("Window mullion", window, new Vector3(0, 0, -.05f), new Vector2(.04f, .94f), Color.white, .01f);
            for (var side = -1; side <= 1; side += 2)
            {
                Cube("Desk top", root, new Vector3(side * 4.7f, .70f, .2f), new Vector3(1.65f, .10f, .9f), new Color(.80f, .88f, .91f));
                for (var leg = -1; leg <= 1; leg += 2)
                    Cube("Desk leg", root, new Vector3(side * 4.7f + leg * .60f, .34f, .2f), new Vector3(.075f, .70f, .075f), new Color(.63f, .73f, .79f));
            }
            var podium = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            podium.name = "Tutor round platform"; podium.transform.SetParent(root, false);
            podium.transform.localPosition = new Vector3(-3.8f, .06f, 4.20f);
            podium.transform.localScale = new Vector3(1.65f, .06f, 1.25f);
            podium.GetComponent<Renderer>().sharedMaterial = Material(new Color(.75f, .87f, .92f));
            TutorAvatar.Build(root, new Vector3(-3.8f, .12f, 4.20f));
            RenderSettings.ambientLight = new Color(.80f, .85f, .90f);
            var sun = new GameObject("Classroom soft light").AddComponent<Light>();
            sun.transform.SetParent(root, false); sun.type = LightType.Directional;
            sun.intensity = .8f; sun.transform.rotation = Quaternion.Euler(35, -25, 0);
            return root;
        }

        public static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale, Color color, Quaternion rotation = default)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube); item.name = name;
            item.transform.SetParent(parent, false); item.transform.localPosition = position;
            item.transform.localScale = scale; item.transform.localRotation = rotation == default ? Quaternion.identity : rotation;
            item.GetComponent<Renderer>().sharedMaterial = LitMaterial(color); return item;
        }

        public static TextMesh Text(string value, Transform parent, Vector3 position, float size, Color color, TextAnchor anchor)
        {
            var text = new GameObject(value).AddComponent<TextMesh>();
            text.transform.SetParent(parent, false); text.transform.localPosition = position;
            text.text = value; text.fontSize = 96; text.characterSize = size; text.color = color.linear; text.anchor = anchor;
            var font = Resources.Load<Font>("ClassroomRounded");
            if (font != null)
            {
                text.font = font;
                text.GetComponent<Renderer>().sharedMaterial = font.material;
            }
            text.alignment = anchor == TextAnchor.MiddleCenter ? TextAlignment.Center :
                anchor == TextAnchor.MiddleRight ? TextAlignment.Right : TextAlignment.Left;
            return text;
        }

        public static Material Material(Color color)
        {
            // Unity's built-in unlit shader is the safest material path for
            // Quest Vulkan and supports single-pass stereo without custom
            // shader variants. Keep the bundled shader as a desktop/fallback
            // option for environments where the built-in shader is unavailable.
            var shader = Shader.Find("Unlit/Color") ??
                Resources.Load<Shader>("BCSignVRUnlit") ?? Shader.Find("BCSignVR/UnlitColor");
            if (shader == null)
                shader = Resources.Load<Shader>("BCSignVRSoftLit") ?? Shader.Find("BCSignVR/SoftLit");
            if (shader == null) throw new System.InvalidOperationException("Bundled BCSignVR shader was not found.");
            return new Material(shader) { color = color };
        }

        public static Material LitMaterial(Color color)
        {
            var shader = Shader.Find("Unlit/Color") ?? Resources.Load<Shader>("BCSignVRUnlit") ??
                Resources.Load<Shader>("BCSignVRSoftLit");
            return shader == null ? Material(color) : new Material(shader) { color = color };
        }
    }
}
