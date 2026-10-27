using UnityEngine;

namespace BCSignVR
{
    /// <summary>
    /// Shows a teaching/reference pose as a filled Bob-style hand sprite.
    /// Live learner landmarks use HandSkeletonView so every bend and joint
    /// remains faithful to the user's actual tracked hand.
    /// </summary>
    public sealed class CartoonHandReplayView
    {
        readonly Transform root;
        readonly Transform hand;
        readonly Renderer renderer;
        string textureName;

        public CartoonHandReplayView(string name, Transform parent, Color color, bool solid = false)
        {
            root = new GameObject(name).transform;
            root.SetParent(parent, false);
            hand = ClassroomUi.Picture("Bob animated hand", "CartoonHandOpen", root,
                Vector3.zero, new Vector2(1.32f, 1.32f)).transform;
            renderer = hand.GetComponent<Renderer>();
            textureName = "CartoonHandOpen";
        }

        public void SetPose(HandPoseFrame frame, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            root.gameObject.SetActive(frame != null && frame.tracked);
            if (frame == null || !frame.tracked) return;

            root.position = position;
            // Keep the sprite facing the classroom board. The pose contributes
            // a small readable tilt, while the recorded wrist path supplies
            // the actual sign movement.
            var tilt = Mathf.Clamp(HandTilt(frame), -28f, 28f);
            root.rotation = root.parent == null
                ? Quaternion.Euler(0, 0, tilt)
                : root.parent.rotation * Quaternion.Euler(0, 0, tilt);
            root.localScale = Vector3.one * scale;

            var nextTexture = TextureFor(frame);
            if (nextTexture == textureName || renderer == null) return;
            var texture = Resources.Load<Texture2D>(nextTexture);
            if (texture != null)
            {
                renderer.sharedMaterial.mainTexture = texture;
                textureName = nextTexture;
            }
        }

        static string TextureFor(HandPoseFrame frame)
        {
            var mask = 0;
            if (Extended(frame, 6)) mask |= 1;   // index
            if (Extended(frame, 11)) mask |= 2;  // middle
            if (Extended(frame, 16)) mask |= 4;  // ring
            if (Extended(frame, 21)) mask |= 8;  // little
            var thumb = ThumbExtended(frame);

            // These are broad presentation frames only; the recorded landmark
            // sequence remains the source of truth for the sign's movement.
            if (mask == 15) return "CartoonHandOpen";
            if (mask == 0 && !thumb) return "CartoonHandFist";
            if (mask == 1 && !thumb) return "CartoonHandPoint";
            if (mask == 3 && !thumb) return "CartoonHandV";
            if (mask == 7 && !thumb) return "CartoonHandW";
            if (mask == 8 && !thumb) return "CartoonHandI";
            if (mask == 8 && thumb) return "CartoonHandY";
            if (mask == 0 && thumb) return "CartoonHandA";
            return mask >= 2 ? "CartoonHandOpen" : "CartoonHandPoint";
        }

        static bool Extended(HandPoseFrame frame, int baseIndex)
        {
            var wrist = frame.joints[0];
            var mcp = Vector3.Distance(wrist, frame.joints[baseIndex]);
            var tip = Vector3.Distance(wrist, frame.joints[baseIndex + 4]);
            return tip > mcp * 1.52f;
        }

        static bool ThumbExtended(HandPoseFrame frame)
        {
            var palm = frame.joints[1];
            var baseDistance = Vector3.Distance(palm, frame.joints[2]);
            var tipDistance = Vector3.Distance(palm, frame.joints[5]);
            return tipDistance > baseDistance * 1.65f;
        }

        static float HandTilt(HandPoseFrame frame)
        {
            var direction = frame.joints[15] - frame.joints[0];
            if (direction.sqrMagnitude < .0001f) return 0f;
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        }
    }
}
