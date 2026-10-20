using System;
using UnityEngine;

namespace BCSignVR
{
    /// <summary>Displays the small local webcam thumbnail carried by the landmark bridge.</summary>
    public sealed class WebcamPreviewView
    {
        readonly GameObject quad;
        readonly Renderer renderer;
        readonly Texture2D texture;
        string previous;

        public WebcamPreviewView(string name, Transform parent, Vector3 center, Vector2 size)
        {
            quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = center;
            quad.transform.localScale = new Vector3(size.x, size.y, 1);
            UnityEngine.Object.Destroy(quad.GetComponent<Collider>());
            texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            texture.name = name + " texture";
            var shader = Resources.Load<Shader>("BCSignVRUnlitTexture");
            renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(shader) { mainTexture = texture };
            quad.SetActive(false);
        }

        public void SetBase64(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                quad.SetActive(false);
                return;
            }
            if (value != previous)
            {
                try
                {
                    texture.LoadImage(Convert.FromBase64String(value), false);
                    previous = value;
                }
                catch (FormatException)
                {
                    quad.SetActive(false);
                    return;
                }
            }
            quad.SetActive(true);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(texture);
            UnityEngine.Object.Destroy(quad);
        }
    }
}
