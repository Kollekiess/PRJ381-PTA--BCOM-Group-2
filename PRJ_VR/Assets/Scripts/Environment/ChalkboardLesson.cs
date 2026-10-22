using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[ExecuteAlways]
public class ChalkboardLesson : MonoBehaviour
{
    [System.Serializable]
    public class LessonSign
    {
        public string title;
        [TextArea(2, 4)]
        public string description;
        public Texture imageTexture;
    }

    [System.Serializable]
    public class LessonData
    {
        public string lessonTitle;
        public string lessonSubtitle;
        public string shortSummary;
        public LessonSign sign1;
        public LessonSign sign2;
        public LessonSign sign3;
    }

    [Header("Containers")]
    public GameObject idleContainer;
    public GameObject lessonContainer;

    [Header("Lesson Display References")]
    public TextMeshPro headerTmp;
    public TextMeshPro col1TitleTmp;
    public TextMeshPro col1DescTmp;
    public Renderer col1ImageRend;

    public TextMeshPro col2TitleTmp;
    public TextMeshPro col2DescTmp;
    public Renderer col2ImageRend;

    public TextMeshPro col3TitleTmp;
    public TextMeshPro col3DescTmp;
    public Renderer col3ImageRend;

    [Header("Lesson Library (7 Lessons)")]
    public List<LessonData> lessons = new List<LessonData>();
    public int currentLessonIndex = 0;
    public bool isLessonActive = false;

    private Coroutine lessonCoroutine;

    void Awake()
    {
        InitializeLessonsIfEmpty();
        if (Application.isPlaying)
        {
            // Requirement: Board is empty on loading in
            ClearBoard();
        }
    }

    void Start()
    {
        if (Application.isPlaying)
        {
            ClearBoard();
        }
    }

    public void ClearBoard()
    {
        isLessonActive = false;
        if (idleContainer != null) idleContainer.SetActive(false);
        if (lessonContainer != null) lessonContainer.SetActive(false);
    }

    public void DisplayLesson(int lessonIndex)
    {
        InitializeLessonsIfEmpty();
        if (lessons == null || lessons.Count == 0) return;

        currentLessonIndex = Mathf.Clamp(lessonIndex, 0, lessons.Count - 1);
        LessonData lesson = lessons[currentLessonIndex];
        isLessonActive = true;

        if (idleContainer != null) idleContainer.SetActive(false);
        if (lessonContainer != null) lessonContainer.SetActive(true);

        // Header
        if (headerTmp != null)
        {
            headerTmp.text = $"<b>LESSON {currentLessonIndex + 1}: {lesson.lessonTitle.ToUpper()}</b>\n" +
                             $"<size=70%><color=#FFE082>{lesson.lessonSubtitle}</color></size>";
            headerTmp.ForceMeshUpdate();
        }

        // Column 1 (Left)
        ApplySign(lesson.sign1, col1TitleTmp, col1DescTmp, col1ImageRend);

        // Column 2 (Center)
        ApplySign(lesson.sign2, col2TitleTmp, col2DescTmp, col2ImageRend);

        // Column 3 (Right)
        ApplySign(lesson.sign3, col3TitleTmp, col3DescTmp, col3ImageRend);

        if (SASLScoreManager.Instance != null)
        {
            SASLScoreManager.Instance.SetActiveLesson(currentLessonIndex);
            for (int i = 0; i < 3; i++)
            {
                if (SASLScoreManager.Instance.IsStepCompleted(currentLessonIndex, i))
                {
                    ApplyCompletedTitle(i);
                }
            }
        }
        if (ScoreHUD.Instance != null)
        {
            ScoreHUD.Instance.RefreshLessonPips();
        }

        if (Application.isPlaying)
        {
            if (lessonCoroutine != null) StopCoroutine(lessonCoroutine);
            lessonCoroutine = StartCoroutine(TypeLessonSequence());
        }
        else
        {
            SetAllMaxVisible(9999);
        }
    }

    public string GetSignTitle(int signIndex)
    {
        if (lessons == null || currentLessonIndex >= lessons.Count) return $"Sign {signIndex + 1}";
        var l = lessons[currentLessonIndex];
        if (signIndex == 0 && l.sign1 != null) return l.sign1.title;
        if (signIndex == 1 && l.sign2 != null) return l.sign2.title;
        if (signIndex == 2 && l.sign3 != null) return l.sign3.title;
        return $"Sign {signIndex + 1}";
    }

    public void MarkSignCompleted(int signIndex)
    {
        ApplyCompletedTitle(signIndex);
        if (ScoreHUD.Instance != null)
        {
            ScoreHUD.Instance.RefreshLessonPips();
        }
    }

    private void ApplyCompletedTitle(int signIndex)
    {
        string rawTitle = GetSignTitle(signIndex);
        string markedTitle = $"<b><color=#81C784>[✓ COMPLETED]</color>\n{rawTitle}</b>";

        if (signIndex == 0 && col1TitleTmp != null)
        {
            col1TitleTmp.text = markedTitle;
            col1TitleTmp.ForceMeshUpdate();
        }
        else if (signIndex == 1 && col2TitleTmp != null)
        {
            col2TitleTmp.text = markedTitle;
            col2TitleTmp.ForceMeshUpdate();
        }
        else if (signIndex == 2 && col3TitleTmp != null)
        {
            col3TitleTmp.text = markedTitle;
            col3TitleTmp.ForceMeshUpdate();
        }
    }

    private void ApplySign(LessonSign sign, TextMeshPro titleTmp, TextMeshPro descTmp, Renderer imgRend)
    {
        if (sign == null) return;
        if (titleTmp != null)
        {
            titleTmp.text = $"<b>{sign.title}</b>";
            titleTmp.ForceMeshUpdate();
        }
        if (descTmp != null)
        {
            descTmp.text = sign.description;
            descTmp.ForceMeshUpdate();
        }
        if (imgRend != null && sign.imageTexture != null)
        {
            Material mat = imgRend.sharedMaterial;
            if (Application.isPlaying) mat = imgRend.material;
            if (mat != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", sign.imageTexture);
                mat.mainTexture = sign.imageTexture;
            }
        }
    }

    private void SetAllMaxVisible(int count)
    {
        if (headerTmp != null) { headerTmp.maxVisibleCharacters = count; headerTmp.ForceMeshUpdate(); }
        if (col1TitleTmp != null) { col1TitleTmp.maxVisibleCharacters = count; col1TitleTmp.ForceMeshUpdate(); }
        if (col1DescTmp != null) { col1DescTmp.maxVisibleCharacters = count; col1DescTmp.ForceMeshUpdate(); }
        if (col2TitleTmp != null) { col2TitleTmp.maxVisibleCharacters = count; col2TitleTmp.ForceMeshUpdate(); }
        if (col2DescTmp != null) { col2DescTmp.maxVisibleCharacters = count; col2DescTmp.ForceMeshUpdate(); }
        if (col3TitleTmp != null) { col3TitleTmp.maxVisibleCharacters = count; col3TitleTmp.ForceMeshUpdate(); }
        if (col3DescTmp != null) { col3DescTmp.maxVisibleCharacters = count; col3DescTmp.ForceMeshUpdate(); }
    }

    private IEnumerator TypeLessonSequence()
    {
        SetAllMaxVisible(0);

        yield return StartCoroutine(TypeTmp(headerTmp, 0.025f));
        yield return new WaitForSeconds(0.15f);

        yield return StartCoroutine(TypeTmp(col1TitleTmp, 0.03f));
        yield return StartCoroutine(TypeTmp(col1DescTmp, 0.02f));
        yield return new WaitForSeconds(0.15f);

        yield return StartCoroutine(TypeTmp(col2TitleTmp, 0.03f));
        yield return StartCoroutine(TypeTmp(col2DescTmp, 0.02f));
        yield return new WaitForSeconds(0.15f);

        yield return StartCoroutine(TypeTmp(col3TitleTmp, 0.03f));
        yield return StartCoroutine(TypeTmp(col3DescTmp, 0.02f));
    }

    private IEnumerator TypeTmp(TextMeshPro tmp, float delay)
    {
        if (tmp == null) yield break;
        tmp.maxVisibleCharacters = 0;
        tmp.ForceMeshUpdate();
        int total = tmp.textInfo.characterCount;
        for (int i = 0; i <= total; i++)
        {
            tmp.maxVisibleCharacters = i;
            if (i > 0 && i < total && i % 2 == 0)
            {
                AudioManager.Instance?.PlayLessonTextTyping();
            }
            yield return new WaitForSeconds(delay);
        }
        tmp.maxVisibleCharacters = total;
    }

    public void InitializeLessonsIfEmpty()
    {
        if (lessons != null && lessons.Count >= 7)
        {
            // Auto-migrate any previously serialized ASL title to SASL
            if (lessons[0] != null && lessons[0].lessonTitle != null && lessons[0].lessonTitle.Contains("ASL"))
            {
                lessons[0].lessonTitle = "South African Sign Language (SASL)";
            }
            return;
        }

        lessons = new List<LessonData>();

        // Lesson 1: Everyday Greetings
        lessons.Add(new LessonData
        {
            lessonTitle = "South African Sign Language (SASL)",
            lessonSubtitle = "Everyday Greetings & Politeness",
            shortSummary = "Essential greetings: <b>HELLO</b>, <b>THANK YOU</b>, and <b>PLEASE</b>.",
            sign1 = new LessonSign { title = "1. HELLO", description = "Hand at temple\nextending outward\n(friendly salute)", imageTexture = LoadTex("asl_hello") },
            sign2 = new LessonSign { title = "2. THANK YOU", description = "Fingertips on chin,\nmove palm forward\ntowards person", imageTexture = LoadTex("asl_thankyou") },
            sign3 = new LessonSign { title = "3. PLEASE", description = "Flat open palm on\nchest in a clockwise\ncircle gesture", imageTexture = LoadTex("asl_please") }
        });

        // Lesson 2: Responses & Help
        lessons.Add(new LessonData
        {
            lessonTitle = "Basic Responses",
            lessonSubtitle = "Everyday Affirmations & Assistance",
            shortSummary = "Core responses: <b>YES</b>, <b>NO</b>, and asking for <b>HELP</b>.",
            sign1 = new LessonSign { title = "1. YES", description = "S-hand fist nods\ndownward repeatedly\nfrom the wrist", imageTexture = LoadTex("asl_yes") },
            sign2 = new LessonSign { title = "2. NO", description = "Index & middle fingers\nsnap closed against\nthe thumb", imageTexture = LoadTex("asl_no") },
            sign3 = new LessonSign { title = "3. HELP", description = "Closed fist on flat\npalm, both lifted up\ntogether", imageTexture = LoadTex("asl_help") }
        });

        // Lesson 3: Family & Relationships
        lessons.Add(new LessonData
        {
            lessonTitle = "Family & People",
            lessonSubtitle = "Connecting with Loved Ones",
            shortSummary = "Key relationships: <b>MOTHER</b>, <b>FATHER</b>, and <b>FRIEND</b>.",
            sign1 = new LessonSign { title = "1. MOTHER", description = "Open 5-hand with thumb\ntapping chin twice\ngently", imageTexture = LoadTex("asl_mother") },
            sign2 = new LessonSign { title = "2. FATHER", description = "Open 5-hand with thumb\ntapping forehead twice\ngently", imageTexture = LoadTex("asl_father") },
            sign3 = new LessonSign { title = "3. FRIEND", description = "Index fingers hook\ntogether, reverse and\nhook again", imageTexture = LoadTex("asl_friend") }
        });

        // Lesson 4: Everyday Needs & Food
        lessons.Add(new LessonData
        {
            lessonTitle = "Everyday Needs",
            lessonSubtitle = "Food, Drinks & Requests",
            shortSummary = "Daily essentials: <b>EAT / FOOD</b>, <b>WATER</b>, and <b>MORE</b>.",
            sign1 = new LessonSign { title = "1. EAT / FOOD", description = "Flat O-hand fingertips\ntap lips twice\nin eating motion", imageTexture = LoadTex("asl_eat") },
            sign2 = new LessonSign { title = "2. WATER", description = "W-handshape index\nfinger taps lower lip\ntwice", imageTexture = LoadTex("asl_water") },
            sign3 = new LessonSign { title = "3. MORE", description = "Both flat-O hands\ntap fingertips together\nrepeatedly", imageTexture = LoadTex("asl_more") }
        });

        // Lesson 5: Feelings & Emotions
        lessons.Add(new LessonData
        {
            lessonTitle = "Feelings & Emotions",
            lessonSubtitle = "Expressing Emotional States",
            shortSummary = "Sharing feelings: <b>HAPPY</b>, <b>SAD</b>, and <b>LOVE</b>.",
            sign1 = new LessonSign { title = "1. HAPPY", description = "Open flat hands brush\nupward against chest\nin repeating circles", imageTexture = LoadTex("asl_happy") },
            sign2 = new LessonSign { title = "2. SAD", description = "Open palms facing face\ndraw slowly downward\nwith solemn look", imageTexture = LoadTex("asl_sad") },
            sign3 = new LessonSign { title = "3. LOVE", description = "Both arms crossed\nhugging against chest\nwith closed fists", imageTexture = LoadTex("asl_love") }
        });

        // Lesson 6: Core Questions
        lessons.Add(new LessonData
        {
            lessonTitle = "Core Questions",
            lessonSubtitle = "Inquiring & Asking Questions",
            shortSummary = "Key questions: <b>WHO</b>, <b>WHAT</b>, and <b>WHERE</b>.",
            sign1 = new LessonSign { title = "1. WHO", description = "Thumb on chin while\nindex finger wiggles\nup and down", imageTexture = LoadTex("asl_who") },
            sign2 = new LessonSign { title = "2. WHAT", description = "Open palms facing up\nmoving side-to-side in\nquestioning sweep", imageTexture = LoadTex("asl_what") },
            sign3 = new LessonSign { title = "3. WHERE", description = "Index finger pointing\nup, swaying left and\nright gently", imageTexture = LoadTex("asl_where") }
        });

        // Lesson 7: Time & Politeness
        lessons.Add(new LessonData
        {
            lessonTitle = "Time & Everyday Terms",
            lessonSubtitle = "Timeframes & Expressions",
            shortSummary = "Everyday terms: <b>TODAY</b>, <b>TOMORROW</b>, and <b>GOOD</b>.",
            sign1 = new LessonSign { title = "1. TODAY / NOW", description = "Both Y-hands drop\ndownward twice in\nfront of body", imageTexture = LoadTex("asl_today") },
            sign2 = new LessonSign { title = "2. TOMORROW", description = "A-hand thumb sweeps\nforward from cheek in\nan arc motion", imageTexture = LoadTex("asl_tomorrow") },
            sign3 = new LessonSign { title = "3. GOOD", description = "Flat fingertips from\nlips place down into\nopen palm", imageTexture = LoadTex("asl_good") }
        });
    }

    [ContextMenu("Reset / Reinitialize SASL Lessons")]
    public void ResetToSASLLessons()
    {
        lessons = null;
        InitializeLessonsIfEmpty();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private Texture LoadTex(string baseName)
    {
#if UNITY_EDITOR
        string[] exts = new string[] { ".jpg", ".png" };
        string[] folders = new string[] { "Assets/V_Work/Textures/", "Assets/Textures/" };
        foreach (var folder in folders)
        {
            foreach (var ext in exts)
            {
                string path = $"{folder}{baseName}{ext}";
                var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>(path);
                if (tex != null) return tex;

                if (baseName.StartsWith("sasl_"))
                {
                    string fallback = $"{folder}{baseName.Replace("sasl_", "asl_")}{ext}";
                    tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>(fallback);
                    if (tex != null) return tex;
                }
            }
        }
#endif
        return null;
    }
}
