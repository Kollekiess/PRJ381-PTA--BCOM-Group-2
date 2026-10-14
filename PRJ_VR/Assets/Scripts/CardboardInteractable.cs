using UnityEngine;
using UnityEngine.Events;

public interface ICardboardInteractable
{
    void OnGazeEnter();
    void OnGazeExit();
    void OnGazeTrigger();
}

public class CardboardInteractable : MonoBehaviour, ICardboardInteractable
{
    [Header("Gaze Events")]
    public UnityEvent onGazeEnter;
    public UnityEvent onGazeExit;
    public UnityEvent onGazeTrigger;

    [Header("Visual Feedback (Optional)")]
    public bool highlightOnGaze = false;
    public Color highlightColor = new Color(1f, 0.9f, 0.5f, 1f);
    public Transform scaleTarget;
    public float hoverScaleMultiplier = 1.0f;

    private Renderer[] renderers;
    private Color[] originalColors;
    private Vector3 originalScale;
    private bool isHovered = false;

    void Awake()
    {
        if (scaleTarget == null) scaleTarget = transform;
        originalScale = scaleTarget.localScale;

        if (highlightOnGaze)
        {
            // Only inspect immediate renderers on this specific object, never children!
            renderers = GetComponents<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                originalColors = new Color[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i].sharedMaterial != null)
                    {
                        if (renderers[i].sharedMaterial.HasProperty("_Color"))
                        {
                            originalColors[i] = renderers[i].sharedMaterial.color;
                        }
                        else if (renderers[i].sharedMaterial.HasProperty("_BaseColor"))
                        {
                            originalColors[i] = renderers[i].sharedMaterial.GetColor("_BaseColor");
                        }
                    }
                }
            }
        }
    }

    public virtual void OnGazeEnter()
    {
        isHovered = true;
        if (highlightOnGaze && renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].material == null) continue;
                if (renderers[i].material.HasProperty("_Color"))
                {
                    renderers[i].material.color = highlightColor;
                }
                else if (renderers[i].material.HasProperty("_BaseColor"))
                {
                    renderers[i].material.SetColor("_BaseColor", highlightColor);
                }
            }
        }

        if (scaleTarget != null && hoverScaleMultiplier != 1.0f)
        {
            scaleTarget.localScale = originalScale * hoverScaleMultiplier;
        }

        onGazeEnter?.Invoke();
    }

    public virtual void OnGazeExit()
    {
        isHovered = false;
        if (highlightOnGaze && renderers != null && originalColors != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].material == null) continue;
                if (renderers[i].material.HasProperty("_Color"))
                {
                    renderers[i].material.color = originalColors[i];
                }
                else if (renderers[i].material.HasProperty("_BaseColor"))
                {
                    renderers[i].material.SetColor("_BaseColor", originalColors[i]);
                }
            }
        }

        if (scaleTarget != null)
        {
            scaleTarget.localScale = originalScale;
        }

        onGazeExit?.Invoke();
    }

    public virtual void OnGazeTrigger()
    {
        onGazeTrigger?.Invoke();
    }

    void OnDisable()
    {
        if (isHovered) OnGazeExit();
    }
}
