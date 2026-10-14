using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshPro))]
[ExecuteAlways]
public class ChalkboardTypewriter : MonoBehaviour
{
    [Header("Text Settings")]
    [TextArea(2, 5)]
    public string fullText = "Project by Daluvuyo";

    [Header("Chalk Styling")]
    public Color chalkColor = new Color(0.96f, 0.95f, 0.91f, 1f);
    public float fontSize = 6.0f;

    [Header("Writing Animation")]
    [Tooltip("Delay between writing each character in seconds")]
    public float characterDelay = 0.12f;

    [Tooltip("Initial delay before writing starts")]
    public float startDelay = 0.5f;

    [Tooltip("How long the completed text stays visible before looping")]
    public float holdDuration = 4.0f;

    [Tooltip("Whether to loop the writing effect continuously")]
    public bool loop = true;

    [Header("Editor Preview")]
    public bool animateInEditMode = true;

    private TextMeshPro tmp;
    private float timer = 0f;
    private int currentVisible = 0;
    private enum State { Starting, Typing, Holding, Pausing }
    private State currentState = State.Starting;

    void Awake()
    {
        SetupTextMesh();
        ResetAnimation();
    }

    void OnEnable()
    {
        SetupTextMesh();
        ResetAnimation();
    }

    public void SetupTextMesh()
    {
        if (tmp == null) tmp = GetComponent<TextMeshPro>();
        if (tmp == null) tmp = gameObject.AddComponent<TextMeshPro>();

        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = chalkColor;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.rectTransform.sizeDelta = new Vector2(6.5f, 2.4f);
        tmp.text = fullText;
        tmp.ForceMeshUpdate();
    }

    public void ResetAnimation()
    {
        timer = 0f;
        currentVisible = 0;
        currentState = State.Starting;
        if (tmp != null)
        {
            tmp.text = fullText;
            tmp.maxVisibleCharacters = 0;
        }
    }

    void Update()
    {
        if (!Application.isPlaying && !animateInEditMode)
        {
            if (tmp != null) tmp.maxVisibleCharacters = fullText.Length;
            return;
        }

        if (tmp == null) SetupTextMesh();
        if (tmp == null) return;

        float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
        timer += dt;

        switch (currentState)
        {
            case State.Starting:
                if (timer >= startDelay)
                {
                    timer = 0f;
                    currentVisible = 0;
                    currentState = State.Typing;
                }
                break;

            case State.Typing:
                float delay = characterDelay;
                if (currentVisible > 0 && currentVisible < fullText.Length)
                {
                    char c = fullText[currentVisible - 1];
                    if (c == ' ') delay *= 1.3f;
                    else if (c == ',' || c == '.') delay *= 2.0f;
                }

                if (timer >= delay)
                {
                    timer = 0f;
                    currentVisible++;
                    tmp.maxVisibleCharacters = currentVisible;

                    if (currentVisible >= fullText.Length)
                    {
                        currentState = State.Holding;
                    }
                }
                break;

            case State.Holding:
                if (timer >= holdDuration)
                {
                    timer = 0f;
                    if (loop)
                    {
                        currentState = State.Pausing;
                    }
                }
                break;

            case State.Pausing:
                if (timer >= 0.5f)
                {
                    ResetAnimation();
                }
                break;
        }
    }

    [ContextMenu("Show Full Text")]
    public void ShowFullText()
    {
        animateInEditMode = false;
        if (tmp != null) tmp.maxVisibleCharacters = fullText.Length;
    }

    [ContextMenu("Restart Animation")]
    public void RestartAnimation()
    {
        animateInEditMode = true;
        ResetAnimation();
    }
}