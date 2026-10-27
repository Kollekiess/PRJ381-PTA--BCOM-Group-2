using UnityEngine;

namespace BCSignVR
{
    /// <summary>
    /// Loops a Bob-style hand along the movement path for signs whose meaning
    /// includes motion. The hand texture stays identical to the static lesson
    /// examples; only its wrist path and a soft guide line are animated.
    /// </summary>
    public sealed class MovementDemoView : MonoBehaviour
    {
        enum Motion { J, Z, Greeting }

        Motion motion;
        Transform hand;
        LineRenderer guide;
        float startedAt;

        public static GameObject Create(string name, string textureName, string movement,
            Transform parent, Vector3 center, Vector2 size)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = center;
            var view = root.AddComponent<MovementDemoView>();
            view.Configure(textureName, movement, size);
            return root;
        }

        void Configure(string textureName, string movement, Vector2 size)
        {
            motion = movement == "Z" ? Motion.Z : movement == "greeting" ? Motion.Greeting : Motion.J;
            hand = ClassroomUi.Picture("Bob animated movement hand", textureName, transform,
                Vector3.zero, size).transform;

            var guideObject = new GameObject("Soft movement guide");
            guideObject.transform.SetParent(transform, false);
            // Keep the guide behind Bob's opaque hand pixels, so the motion
            // cue never draws a blue line across his palm or fingers.
            guideObject.transform.localPosition = new Vector3(0, 0, .025f);
            guide = guideObject.AddComponent<LineRenderer>();
            guide.useWorldSpace = false;
            guide.material = ClassroomFactory.Material(new Color(.14f, .58f, .81f));
            guide.startColor = ClassroomUi.Blue.linear;
            guide.endColor = ClassroomUi.Blue.linear;
            guide.startWidth = .014f;
            guide.endWidth = .014f;
            guide.numCapVertices = 4;
            guide.numCornerVertices = 4;
            guide.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            guide.receiveShadows = false;
            guide.positionCount = 28;
            startedAt = Time.time;
            DrawGuide();
        }

        void Update()
        {
            if (hand == null || guide == null) return;
            // Give learners time to read the handshape and the movement path.
            // This is intentionally half the former playback rate.
            var phase = Mathf.Repeat((Time.time - startedAt) * (motion == Motion.Greeting ? .425f : .31f), 1f);
            hand.localPosition = MotionPoint(phase) + Vector3.forward * -.018f;
            hand.localRotation = Quaternion.Euler(0, 0, MotionTilt(phase));
            hand.localScale = Vector3.one * (1f + Mathf.Sin(phase * Mathf.PI * 2f) * .018f);
            DrawGuide();
        }

        void DrawGuide()
        {
            if (guide == null) return;
            for (var i = 0; i < guide.positionCount; i++)
            {
                var phase = i / (guide.positionCount - 1f);
                guide.SetPosition(i, MotionPoint(phase) + Vector3.forward * .005f);
            }
        }

        Vector3 MotionPoint(float phase)
        {
            switch (motion)
            {
                case Motion.Z:
                    if (phase < .34f)
                    {
                        var t = Ease(phase / .34f);
                        return Vector3.Lerp(new Vector3(-.25f, .24f, 0), new Vector3(.25f, .24f, 0), t);
                    }
                    if (phase < .67f)
                    {
                        var t = Ease((phase - .34f) / .33f);
                        return Vector3.Lerp(new Vector3(.25f, .24f, 0), new Vector3(-.25f, -.28f, 0), t);
                    }
                    return Vector3.Lerp(new Vector3(-.25f, -.28f, 0), new Vector3(.25f, -.28f, 0),
                        Ease((phase - .67f) / .33f));
                case Motion.Greeting:
                    return new Vector3(Mathf.Sin(phase * Mathf.PI * 2f) * .20f,
                        Mathf.Sin(phase * Mathf.PI * 4f) * .035f, 0);
                default:
                    if (phase < .62f)
                    {
                        var t = Ease(phase / .62f);
                        return Vector3.Lerp(new Vector3(.15f, .24f, 0), new Vector3(.15f, -.28f, 0), t);
                    }
                    var curl = Ease((phase - .62f) / .38f);
                    return new Vector3(Mathf.Lerp(.15f, -.23f, curl),
                        -.28f + Mathf.Sin(curl * Mathf.PI) * .14f, 0);
            }
        }

        float MotionTilt(float phase)
        {
            if (motion == Motion.Greeting) return Mathf.Sin(phase * Mathf.PI * 2f) * 5f;
            if (motion == Motion.Z) return phase < .34f ? 0 : phase < .67f ? -8f : 0;
            return phase > .62f ? Mathf.Sin((phase - .62f) / .38f * Mathf.PI) * -12f : 0;
        }

        static float Ease(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
