using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Renders the Gamified SASL Reward and Score tracking HUD in the Top-Left corner.
/// Displays current points, lesson progress, step completion pips, and animated point toast popups.
/// Compatible with both Screen-Space and VR Headsets (Camera-anchored World/Overlay).
/// </summary>
public class ScoreHUD : MonoBehaviour
{
    public static ScoreHUD Instance { get; private set; }

    [Header("UI Element References")]
    public RectTransform hudContainer;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI lessonTitleText;
    public TextMeshProUGUI[] stepPipsText;
    public TextMeshProUGUI teacherStatusText;

    [Header("Toast Notification")]
    public RectTransform toastContainer;
    public TextMeshProUGUI toastText;
    public CanvasGroup toastCanvasGroup;

    private Coroutine toastCoroutine;
    private Coroutine scoreAnimCoroutine;
    private int displayedScore = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        EnsureHUDHierarchy();
    }

    private void Start()
    {
        if (SASLScoreManager.Instance != null)
        {
            SASLScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
            SASLScoreManager.Instance.OnStepCompleted += HandleStepCompleted;
            SASLScoreManager.Instance.OnTeacherTestCompleted += HandleTeacherTestCompleted;
            UpdateDisplay(SASLScoreManager.Instance.TotalScore);
        }
    }

    private void OnDestroy()
    {
        if (SASLScoreManager.Instance != null)
        {
            SASLScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
            SASLScoreManager.Instance.OnStepCompleted -= HandleStepCompleted;
            SASLScoreManager.Instance.OnTeacherTestCompleted -= HandleTeacherTestCompleted;
        }
    }

    private void HandleScoreChanged(int newTotal, int delta, string reason)
    {
        AnimateScoreChange(newTotal);
        ShowToast(reason);
        RefreshLessonPips();
    }

    private void HandleStepCompleted(int lessonIndex, int stepIndex, string signName)
    {
        RefreshLessonPips();
    }

    private void HandleTeacherTestCompleted(int lessonIndex, int bonusPoints)
    {
        RefreshLessonPips();
    }

    public void UpdateDisplay(int totalScore)
    {
        displayedScore = totalScore;
        if (scoreText != null)
        {
            scoreText.text = $"<color=#FFD54F>SCORE:</color> <b>{totalScore:N0}</b> <size=70%><color=#FFE082>PTS</color></size>";
        }
        RefreshLessonPips();
    }

    private void AnimateScoreChange(int targetScore)
    {
        if (scoreAnimCoroutine != null) StopCoroutine(scoreAnimCoroutine);
        scoreAnimCoroutine = StartCoroutine(ScoreCounterRoutine(targetScore));
    }

    private IEnumerator ScoreCounterRoutine(int targetScore)
    {
        int startScore = displayedScore;
        float elapsed = 0f;
        float duration = 0.5f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Ease out quad
            t = 1f - (1f - t) * (1f - t);
            displayedScore = (int)Mathf.Lerp(startScore, targetScore, t);
            if (scoreText != null)
            {
                scoreText.text = $"<color=#FFD54F>SCORE:</color> <b>{displayedScore:N0}</b> <size=70%><color=#FFE082>PTS</color></size>";
            }
            yield return null;
        }

        displayedScore = targetScore;
        if (scoreText != null)
        {
            scoreText.text = $"<color=#FFD54F>SCORE:</color> <b>{displayedScore:N0}</b> <size=70%><color=#FFE082>PTS</color></size>";
        }
    }

    public void RefreshLessonPips()
    {
        var sm = SASLScoreManager.Instance;
        if (sm == null) return;

        var cb = FindFirstObjectByType<ChalkboardLesson>();
        int currentL = sm.CurrentLessonIndex;
        string lTitle = $"Lesson {currentL + 1}";

        string[] signNames = new string[] { "Sign 1", "Sign 2", "Sign 3" };
        if (cb != null && cb.lessons != null && currentL < cb.lessons.Count)
        {
            var lData = cb.lessons[currentL];
            if (!string.IsNullOrEmpty(lData.lessonTitle)) lTitle = lData.lessonTitle;
            if (lData.sign1 != null && !string.IsNullOrEmpty(lData.sign1.title)) signNames[0] = lData.sign1.title;
            if (lData.sign2 != null && !string.IsNullOrEmpty(lData.sign2.title)) signNames[1] = lData.sign2.title;
            if (lData.sign3 != null && !string.IsNullOrEmpty(lData.sign3.title)) signNames[2] = lData.sign3.title;
        }

        if (lessonTitleText != null)
        {
            lessonTitleText.text = $"<b>{lTitle.ToUpper()}</b>";
        }

        if (stepPipsText != null)
        {
            for (int i = 0; i < stepPipsText.Length && i < 3; i++)
            {
                if (stepPipsText[i] != null)
                {
                    bool isDone = sm.IsStepCompleted(currentL, i);
                    string name = CleanSignTitle(signNames[i]);
                    if (isDone)
                    {
                        stepPipsText[i].text = $"<color=#81C784>[DONE] {name}</color>";
                    }
                    else
                    {
                        stepPipsText[i].text = $"<color=#B0BEC5>[  ] {name}</color>";
                    }
                }
            }
        }

        if (teacherStatusText != null)
        {
            bool isTeacherDone = sm.IsTeacherTaskCompleted(currentL);
            bool allStepsDone = sm.AreAllLessonStepsCompleted(currentL);

            if (isTeacherDone)
            {
                teacherStatusText.text = $"<color=#81C784>[PASSED] Teacher Test: <b>+{sm.CurrentLessonPointsEarned} PTS</b></color>";
            }
            else if (allStepsDone)
            {
                teacherStatusText.text = $"<color=#FFD54F>[READY] Teacher Test: <b>Talk to Teacher!</b></color>";
            }
            else
            {
                int remaining = 3 - sm.GetCompletedStepCount(currentL);
                teacherStatusText.text = $"<color=#90A4AE>Teacher Test: Practice {remaining} more sign{(remaining > 1 ? "s" : "")}</color>";
            }
        }
    }

    private string CleanSignTitle(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Sign";
        // Remove leading numbers e.g. "1. HELLO" -> "HELLO"
        string s = raw.Trim();
        if (s.Length > 2 && char.IsDigit(s[0]) && (s[1] == '.' || s[1] == ' '))
        {
            s = s.Substring(2).Trim();
        }
        return s;
    }

    public void ShowToast(string message)
    {
        if (toastContainer == null || toastText == null || toastCanvasGroup == null) return;
        if (toastCoroutine != null) StopCoroutine(toastCoroutine);
        toastCoroutine = StartCoroutine(ToastRoutine(message));
    }

    private IEnumerator ToastRoutine(string message)
    {
        toastText.text = message;
        toastContainer.gameObject.SetActive(true);

        // Pop in animation
        Vector2 startPos = new Vector2(25f, -240f);
        Vector2 endPos = new Vector2(25f, -220f);
        toastContainer.anchoredPosition = startPos;
        toastCanvasGroup.alpha = 0f;

        float inTime = 0.25f;
        float elapsed = 0f;
        while (elapsed < inTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / inTime;
            toastCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            toastContainer.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }
        toastCanvasGroup.alpha = 1f;
        toastContainer.anchoredPosition = endPos;

        yield return new WaitForSeconds(2.2f);

        // Fade out
        float outTime = 0.35f;
        elapsed = 0f;
        while (elapsed < outTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / outTime;
            toastCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            toastContainer.anchoredPosition = Vector2.Lerp(endPos, endPos + new Vector2(0f, 15f), t);
            yield return null;
        }

        toastCanvasGroup.alpha = 0f;
        toastContainer.gameObject.SetActive(false);
    }

    private void EnsureHUDHierarchy()
    {
        if (hudContainer != null) return;

        // Auto-configure or locate inside Canvas
        Transform existing = transform.Find("TopLeftScoreHUD");
        if (existing != null)
        {
            hudContainer = existing.GetComponent<RectTransform>();
            return;
        }

        // Build container
        GameObject containerObj = new GameObject("TopLeftScoreHUD");
        containerObj.transform.SetParent(transform, false);

        hudContainer = containerObj.AddComponent<RectTransform>();
        hudContainer.anchorMin = new Vector2(0f, 1f);
        hudContainer.anchorMax = new Vector2(0f, 1f);
        hudContainer.pivot = new Vector2(0f, 1f);
        hudContainer.anchoredPosition = new Vector2(20f, -20f);
        hudContainer.sizeDelta = new Vector2(340f, 180f);

        // Background panel
        var bgImg = containerObj.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.10f, 0.16f, 0.88f); // Sleek modern dark cyan-tinted glass

        var outline = containerObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.85f, 1f, 0.5f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Score Text
        GameObject scoreObj = new GameObject("ScoreText");
        scoreObj.transform.SetParent(containerObj.transform, false);
        var scoreRt = scoreObj.AddComponent<RectTransform>();
        scoreRt.anchorMin = new Vector2(0f, 1f);
        scoreRt.anchorMax = new Vector2(1f, 1f);
        scoreRt.pivot = new Vector2(0f, 1f);
        scoreRt.anchoredPosition = new Vector2(15f, -12f);
        scoreRt.sizeDelta = new Vector2(-30f, 38f);
        scoreText = scoreObj.AddComponent<TextMeshProUGUI>();
        scoreText.fontSize = 24f;
        scoreText.color = Color.white;
        scoreText.text = "<color=#FFD54F>SCORE:</color> <b>0</b> <size=70%><color=#FFE082>PTS</color></size>";

        // Lesson Title Text
        GameObject lTitleObj = new GameObject("LessonTitleText");
        lTitleObj.transform.SetParent(containerObj.transform, false);
        var lTitleRt = lTitleObj.AddComponent<RectTransform>();
        lTitleRt.anchorMin = new Vector2(0f, 1f);
        lTitleRt.anchorMax = new Vector2(1f, 1f);
        lTitleRt.pivot = new Vector2(0f, 1f);
        lTitleRt.anchoredPosition = new Vector2(15f, -50f);
        lTitleRt.sizeDelta = new Vector2(-30f, 24f);
        lessonTitleText = lTitleObj.AddComponent<TextMeshProUGUI>();
        lessonTitleText.fontSize = 14f;
        lessonTitleText.color = new Color(0.45f, 0.85f, 1f, 1f);
        lessonTitleText.text = "<b>LESSON 1: GREETINGS</b>";

        // Step Pips (3 signs)
        stepPipsText = new TextMeshProUGUI[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject pipObj = new GameObject($"StepPip_{i}");
            pipObj.transform.SetParent(containerObj.transform, false);
            var pipRt = pipObj.AddComponent<RectTransform>();
            pipRt.anchorMin = new Vector2(0f, 1f);
            pipRt.anchorMax = new Vector2(1f, 1f);
            pipRt.pivot = new Vector2(0f, 1f);
            pipRt.anchoredPosition = new Vector2(15f, -76f - (i * 22f));
            pipRt.sizeDelta = new Vector2(-30f, 20f);
            var tmp = pipObj.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 13f;
            tmp.color = new Color(0.7f, 0.75f, 0.8f, 1f);
            tmp.text = $"[○] Sign {i + 1}";
            stepPipsText[i] = tmp;
        }

        // Teacher status text
        GameObject tStatusObj = new GameObject("TeacherStatusText");
        tStatusObj.transform.SetParent(containerObj.transform, false);
        var tStatusRt = tStatusObj.AddComponent<RectTransform>();
        tStatusRt.anchorMin = new Vector2(0f, 1f);
        tStatusRt.anchorMax = new Vector2(1f, 1f);
        tStatusRt.pivot = new Vector2(0f, 1f);
        tStatusRt.anchoredPosition = new Vector2(15f, -148f);
        tStatusRt.sizeDelta = new Vector2(-30f, 24f);
        teacherStatusText = tStatusObj.AddComponent<TextMeshProUGUI>();
        teacherStatusText.fontSize = 12f;
        teacherStatusText.color = new Color(1f, 0.85f, 0.3f, 1f);
        teacherStatusText.text = "Teacher Test: Practice signs first";

        // Toast Container
        GameObject toastObj = new GameObject("ScoreToast");
        toastObj.transform.SetParent(transform, false);
        toastContainer = toastObj.AddComponent<RectTransform>();
        toastContainer.anchorMin = new Vector2(0f, 1f);
        toastContainer.anchorMax = new Vector2(0f, 1f);
        toastContainer.pivot = new Vector2(0f, 1f);
        toastContainer.anchoredPosition = new Vector2(25f, -220f);
        toastContainer.sizeDelta = new Vector2(330f, 44f);

        var toastBg = toastObj.AddComponent<Image>();
        toastBg.color = new Color(0.1f, 0.45f, 0.25f, 0.92f); // Vibrant emerald success toast
        var toastOutline = toastObj.AddComponent<Outline>();
        toastOutline.effectColor = new Color(0.4f, 1f, 0.6f, 0.75f);
        toastOutline.effectDistance = new Vector2(1.5f, -1.5f);

        toastCanvasGroup = toastObj.AddComponent<CanvasGroup>();
        toastCanvasGroup.alpha = 0f;

        GameObject toastTxtObj = new GameObject("ToastText");
        toastTxtObj.transform.SetParent(toastObj.transform, false);
        var toastTxtRt = toastTxtObj.AddComponent<RectTransform>();
        toastTxtRt.anchorMin = Vector2.zero;
        toastTxtRt.anchorMax = Vector2.one;
        toastTxtRt.offsetMin = new Vector2(10f, 5f);
        toastTxtRt.offsetMax = new Vector2(-10f, -5f);
        toastText = toastTxtObj.AddComponent<TextMeshProUGUI>();
        toastText.fontSize = 15f;
        toastText.fontStyle = FontStyles.Bold;
        toastText.alignment = TextAlignmentOptions.Center;
        toastText.color = Color.white;
        toastText.text = "+50 PTS: Sign Learned!";

        toastObj.SetActive(false);
    }
}
