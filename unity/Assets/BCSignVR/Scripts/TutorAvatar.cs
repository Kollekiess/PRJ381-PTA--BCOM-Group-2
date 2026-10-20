using System.Collections.Generic;
using UnityEngine;

namespace BCSignVR
{
    /// <summary>A small, original classroom guide built entirely from rounded geometry.</summary>
    public sealed class TutorAvatar : MonoBehaviour
    {
        static readonly Color Skin = new(.91f, .68f, .48f);
        static readonly Color SkinLight = new(.98f, .77f, .57f);
        static readonly Color SkinShadow = new(.72f, .46f, .32f);
        static readonly Color Hair = new(.12f, .10f, .11f);
        static readonly Color HairLight = new(.19f, .15f, .15f);
        static readonly Color Teal = new(.27f, .37f, .53f);
        static readonly Color TealShadow = new(.19f, .26f, .39f);
        static readonly Color Blue = new(.17f, .25f, .39f);
        static readonly Color White = new(.98f, .99f, 1f);
        static readonly Color Ink = new(.09f, .13f, .20f);

        readonly Dictionary<Color, Material> materials = new();
        Transform head;
        Transform leftEye;
        Transform rightEye;
        Transform leftArm;
        Transform rightArm;
        Transform leftBrow, rightBrow;
        float encouragement;
        TextMesh speech;
        float nextTipAt;
        string lastTip;

        public void Encourage() => encouragement = 2.5f;

        public void Say(string message, bool immediate = false)
        {
            if (speech == null || message == lastTip || !immediate && Time.time < nextTipAt) return;
            lastTip = message; nextTipAt = Time.time + 2.5f;
            var lines = ""; var column = 0;
            foreach (var word in message.Split(' '))
            {
                if (column > 0 && column + word.Length + 1 > 24) { lines += "\n"; column = 0; }
                if (column > 0) { lines += " "; column++; }
                lines += word; column += word.Length;
            }
            speech.text = lines;
        }

        public static Transform Build(Transform parent, Vector3 position)
        {
            var root = new GameObject("Classroom Guide").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            var avatar = root.gameObject.AddComponent<TutorAvatar>();
            avatar.Create();
            return root;
        }

        void Create()
        {
            var bubble = ClassroomUi.Panel("Tutor speech bubble", transform, new Vector3(0, 2.75f, -.30f),
                new Vector2(1.85f, .88f), Color.white, .19f, true).transform;
            var tail = ClassroomUi.Panel("Speech tail", bubble, new Vector3(0, -.43f, .015f),
                new Vector2(.20f, .20f), Color.white, .03f).transform;
            tail.localRotation = Quaternion.Euler(0, 0, 45);
            speech = ClassroomUi.Label("", bubble, 0, .01f, .13f, ClassroomUi.Ink, TextAnchor.MiddleCenter, 1.65f);
            Say("Welcome! Let's learn one hand shape at a time.", true);
            // Soft sole and toe layers keep the feet readable against a pale floor.
            Shape("Left Sole", transform, new(-.17f, .065f, -.055f), new(.28f, .10f, .43f), White);
            Shape("Right Sole", transform, new(.17f, .065f, -.055f), new(.28f, .10f, .43f), White);
            Shape("Left Shoe", transform, new(-.17f, .115f, -.065f), new(.26f, .14f, .40f), Blue);
            Shape("Right Shoe", transform, new(.17f, .115f, -.065f), new(.26f, .14f, .40f), Blue);
            Shape("Left Shoe Trim", transform, new(-.17f, .16f, -.14f), new(.17f, .025f, .14f), White);
            Shape("Right Shoe Trim", transform, new(.17f, .16f, -.14f), new(.17f, .025f, .14f), White);
            Limb("Left Trouser", transform, new(-.15f, .17f, .02f), new(-.15f, .72f, .02f), .19f, Blue);
            Limb("Right Trouser", transform, new(.15f, .17f, .02f), new(.15f, .72f, .02f), .19f, Blue);
            Shape("Trouser Waist", transform, new(0f, .65f, .02f), new(.49f, .24f, .31f), Blue);

            Shape("White shirt", transform, new(0f, 1.05f, .015f), new(.66f, .84f, .39f), White);
            Shape("Shirt shadow", transform, new(0f, .72f, -.005f), new(.48f, .10f, .34f), new Color(.78f, .84f, .89f));
            Limb("Neck", transform, new(0f, 1.35f, 0f), new(0f, 1.57f, 0f), .20f, Skin);
            Shape("White Collar", transform, new(0f, 1.415f, -.035f), new(.31f, .095f, .26f), White);
            Shape("Collar Opening", transform, new(0f, 1.449f, -.03f), new(.20f, .045f, .19f), SkinShadow);
            Shape("White shirt front", transform, new(0f, 1.19f, -.185f), new(.22f, .40f, .055f), White);
            Shape("Red tie knot", transform, new(0f, 1.34f, -.23f), new(.09f, .09f, .04f), new Color(.75f, .23f, .23f));
            Shape("Red tie", transform, new(0f, 1.17f, -.23f), new(.085f, .30f, .04f), new Color(.75f, .23f, .23f));

            // The badge is a simple speech mark, avoiding tiny text on the character.
            Shape("Badge Rim", transform, new(.125f, 1.16f, -.181f), new(.16f, .16f, .035f), TealShadow);
            Shape("Badge", transform, new(.125f, 1.16f, -.202f), new(.13f, .13f, .025f), White);
            Shape("Badge Mark", transform, new(.125f, 1.17f, -.22f), new(.060f, .043f, .012f), Teal);
            Shape("Badge Tail", transform, new(.107f, 1.145f, -.22f), new(.025f, .025f, .012f), Teal);

            leftArm = Arm("Left Arm", -.28f, -1f);
            rightArm = Arm("Right Arm", .28f, 1f);

            head = new GameObject("Head Pivot").transform;
            head.SetParent(transform, false);
            head.localPosition = new Vector3(0f, 1.79f, 0f);
            head.localScale = new Vector3(1.18f, 1.12f, 1.08f);
            Shape("Hair Back", head, new(0f, .045f, .035f), new(.70f, .69f, .55f), Hair);
            Shape("Face", head, Vector3.zero, new(.66f, .72f, .55f), Skin);
            Shape("Left Ear", head, new(-.334f, -.005f, -.005f), new(.115f, .17f, .105f), Skin);
            Shape("Right Ear", head, new(.334f, -.005f, -.005f), new(.115f, .17f, .105f), Skin);
            Shape("Left Ear Inner", head, new(-.355f, -.005f, -.054f), new(.045f, .085f, .025f), SkinShadow);
            Shape("Right Ear Inner", head, new(.355f, -.005f, -.054f), new(.045f, .085f, .025f), SkinShadow);

            // Hair is swept to one side; the forehead stays open around both eyebrows.
            Shape("Hair Crown", head, new(0f, .267f, .075f), new(.63f, .19f, .40f), Hair);
            Shape("Hair Sweep", head, new(-.065f, .277f, -.10f), new(.44f, .10f, .17f), HairLight,
                Quaternion.Euler(0f, 0f, -12f));
            Shape("Hair Fringe", head, new(-.18f, .22f, -.205f), new(.27f, .17f, .15f), Hair,
                Quaternion.Euler(0f, 0f, -24f));
            Shape("Left Sideburn", head, new(-.295f, .07f, -.10f), new(.075f, .22f, .18f), Hair);
            Shape("Right Sideburn", head, new(.295f, .07f, -.10f), new(.075f, .22f, .18f), Hair);

            leftEye = Eye("Left Eye", -.108f);
            rightEye = Eye("Right Eye", .108f);
            leftBrow = Shape("Left Eyebrow", head, new(-.108f, .142f, -.258f), new(.14f, .043f, .027f), Hair,
                Quaternion.Euler(0f, 0f, -16f)).transform;
            rightBrow = Shape("Right Eyebrow", head, new(.108f, .142f, -.258f), new(.14f, .043f, .027f), Hair,
                Quaternion.Euler(0f, 0f, 16f)).transform;
            Shape("Nose", head, new(0f, -.025f, -.278f), new(.085f, .105f, .095f), SkinLight);
            Shape("Nose Base", head, new(0f, -.059f, -.304f), new(.065f, .025f, .035f), SkinShadow);
            Shape("Left Moustache", head, new(-.055f, -.105f, -.292f), new(.11f, .045f, .025f), Hair,
                Quaternion.Euler(0, 0, 12f));
            Shape("Right Moustache", head, new(.055f, -.105f, -.292f), new(.11f, .045f, .025f), Hair,
                Quaternion.Euler(0, 0, -12f));
            Shape("Left Cheek", head, new(-.19f, -.075f, -.235f), new(.075f, .035f, .018f), SkinLight);
            Shape("Right Cheek", head, new(.19f, -.075f, -.235f), new(.075f, .035f, .018f), SkinLight);
            Smile();
        }

        Transform Eye(string name, float x)
        {
            var root = new GameObject(name).transform;
            root.SetParent(head, false);
            root.localPosition = new Vector3(x, .045f, -.263f);
            Shape("Eye Outline", root, Vector3.zero, new(.084f, .106f, .030f), Ink);
            Shape("Eye White", root, new(0f, .002f, -.014f), new(.069f, .088f, .018f), White);
            Shape("Pupil", root, new(0f, .001f, -.027f), new(.033f, .059f, .014f), Ink);
            Shape("Catchlight", root, new(-.008f, .018f, -.037f), new(.012f, .016f, .007f), White);
            return root;
        }

        Transform Arm(string name, float x, float side)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(x, 1.30f, .005f);
            Limb("Rounded Sleeve", root, Vector3.zero, new(side * .115f, -.245f, -.012f), .225f, White);
            Limb("Forearm", root, new(side * .105f, -.22f, -.012f), new(side * .15f, -.43f, -.075f), .125f, Skin);
            Shape("Hand", root, new(side * .155f, -.465f, -.075f), new(.15f, .20f, .13f), SkinLight,
                Quaternion.Euler(0f, 0f, side * 9f));
            Shape("Thumb", root, new(side * .10f, -.44f, -.12f), new(.065f, .10f, .08f), Skin);
            return root;
        }

        void Smile()
        {
            var line = new GameObject("Curved Smile").AddComponent<LineRenderer>();
            line.transform.SetParent(head, false);
            line.useWorldSpace = false;
            line.positionCount = 17;
            line.widthMultiplier = .014f;
            line.numCapVertices = 6;
            line.numCornerVertices = 4;
            line.sharedMaterial = GetMaterial(new Color(.30f, .14f, .12f));
            for (var i = 0; i < line.positionCount; i++)
            {
                var t = i / 16f * 2f - 1f;
                line.SetPosition(i, new Vector3(t * .102f, -.143f + .038f * t * t, -.266f + .014f * t * t));
            }
        }

        GameObject Shape(string name, Transform parent, Vector3 position, Vector3 scale, Color color,
            Quaternion rotation = default)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.transform.localRotation = rotation == default ? Quaternion.identity : rotation;
            item.GetComponent<Renderer>().sharedMaterial = GetMaterial(color);
            item.GetComponent<Collider>().enabled = false;
            return item;
        }

        void Limb(string name, Transform parent, Vector3 start, Vector3 end, float width, Color color)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = (start + end) * .5f;
            item.transform.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
            item.transform.localScale = new Vector3(width, Vector3.Distance(start, end) * .5f, width);
            item.GetComponent<Renderer>().sharedMaterial = GetMaterial(color);
            item.GetComponent<Collider>().enabled = false;
        }

        Material GetMaterial(Color color)
        {
            if (!materials.TryGetValue(color, out var material))
            {
                material = ClassroomFactory.LitMaterial(color);
                materials.Add(color, material);
            }
            return material;
        }

        void Update()
        {
            // Tiny gestures keep the guide alive without distracting from the signing lesson.
            var time = Time.time;
            encouragement = Mathf.Max(0, encouragement - Time.deltaTime);
            var pleased = encouragement > 0;
            leftBrow.localRotation = Quaternion.Euler(0, 0, pleased ? 5 : -16);
            rightBrow.localRotation = Quaternion.Euler(0, 0, pleased ? -5 : 16);
            head.localRotation = Quaternion.Euler(Mathf.Sin(time * .65f) * 1.2f, Mathf.Sin(time * .43f) * 2f,
                Mathf.Sin(time * .55f) * 1.8f);
            if (pleased) head.localRotation *= Quaternion.Euler(Mathf.Sin(time * 5f) * 5f, 0, 0);
            leftArm.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * .8f) * 1.5f);
            rightArm.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Sin(time * .8f) * 1.5f);
            var blink = Mathf.Repeat(time, 4.8f);
            var eyeHeight = blink < .16f ? Mathf.Lerp(.12f, 1f, Mathf.Abs(blink - .08f) / .08f) : 1f;
            leftEye.localScale = rightEye.localScale = new Vector3(1f, eyeHeight, 1f);
        }

        void OnDestroy()
        {
            foreach (var material in materials.Values)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
        }
    }
}
