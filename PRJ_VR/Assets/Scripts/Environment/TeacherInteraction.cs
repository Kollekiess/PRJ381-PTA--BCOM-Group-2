using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Controls the Teacher Robot's conversational dialogue, rigging animations,
/// and interactive testing/quizzing on covered SASL lessons.
/// Awards the teacher-led task completion bonus once the test is finished.
/// </summary>
[ExecuteAlways]
public class TeacherInteraction : MonoBehaviour, ICardboardInteractable
{
    public enum DialogueMode
    {
        LessonBrowse,
        Practicing,
        QuizTesting,
        QuizPassed
    }

    [Header("References")]
    public ChalkboardLesson chalkboard;
    public Transform player;

    [Header("Interaction Settings")]
    public float interactionDistance = 4.2f;

    [Header("Speech Bubble & Dialogue")]
    public Transform bubbleContainer;
    public TextMeshPro dialogueTmp;
    public GameObject promptIndicator;
    public GameObject speechBubbleObj;

    [Header("Lesson Navigation & Quiz State")]
    public int selectedLessonIndex = 0;
    public bool isDialogueActive = false;
    public DialogueMode currentMode = DialogueMode.LessonBrowse;
    public int quizQuestionIndex = 0; // 0, 1, 2 for the 3 signs

    private bool isPlayerInRange = false;
    private Animator animator;
    private Coroutine quizAdvanceCoroutine;

    public bool IsQuizActive => isDialogueActive && currentMode == DialogueMode.QuizTesting;
    public bool IsPlayerInRange => isPlayerInRange;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        FindReferences();
        if (Application.isPlaying)
        {
            isDialogueActive = false;
            currentMode = DialogueMode.LessonBrowse;
            if (speechBubbleObj != null) speechBubbleObj.SetActive(false);
            if (promptIndicator != null) promptIndicator.SetActive(false);
        }
    }

    void OnEnable()
    {
        FindReferences();
    }

    void FindReferences()
    {
        if (player == null)
        {
            var pObj = GameObject.Find("Player");
            if (pObj != null) player = pObj.transform;
            else if (Camera.main != null) player = Camera.main.transform;
        }

        if (chalkboard == null)
        {
            chalkboard = FindFirstObjectByType<ChalkboardLesson>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        }
    }

    void Update()
    {
        BillboardToViewer();

        if (!Application.isPlaying) return;

        if (player == null) FindReferences();
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        bool inRange = distance <= interactionDistance;

        // Player entered/exited range
        if (inRange != isPlayerInRange)
        {
            isPlayerInRange = inRange;
            if (promptIndicator != null)
            {
                promptIndicator.SetActive(inRange && !isDialogueActive);
            }

            if (!inRange && isDialogueActive)
            {
                CloseDialogue();
            }
        }

        if (!inRange) return;

        // SAFE INPUT HANDLING VIA NEW INPUT SYSTEM ONLY
        var kb = Keyboard.current;
        if (kb == null) return;

        // E key toggles dialogue
        if (kb.eKey.wasPressedThisFrame)
        {
            if (!isDialogueActive) OpenDialogue();
            else CloseDialogue();
            return;
        }

        if (!isDialogueActive) return;

        // T key triggers Quiz Test directly
        if (kb.tKey.wasPressedThisFrame)
        {
            StartQuizTest();
            return;
        }

        if (currentMode == DialogueMode.LessonBrowse)
        {
            // Next lesson: D or Right Arrow
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
            {
                NextLesson();
            }
            // Previous lesson: A or Left Arrow
            else if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)
            {
                PreviousLesson();
            }
            // Start lesson on chalkboard: Space or Enter
            else if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                StartCurrentLesson();
            }
        }
        else if (currentMode == DialogueMode.QuizTesting)
        {
            // In testing mode: space or enter can demonstrate sign or skip
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
            {
                OnPlayerDemonstratedGesture(quizQuestionIndex);
            }
        }
        else if (currentMode == DialogueMode.QuizPassed)
        {
            // Proceed to next lesson
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
            {
                NextLesson();
                currentMode = DialogueMode.LessonBrowse;
                StartCurrentLesson();
            }
        }
    }

    public void OpenDialogue()
    {
        isDialogueActive = true;
        if (promptIndicator != null) promptIndicator.SetActive(false);
        if (speechBubbleObj != null) speechBubbleObj.SetActive(true);

        if (chalkboard != null) chalkboard.InitializeLessonsIfEmpty();

        PlayTeacherAnimation("Wave");

        // Check if player has practiced and should be offered the quiz test
        var sm = SASLScoreManager.Instance;
        if (sm != null && sm.AreAllLessonStepsCompleted(selectedLessonIndex) && !sm.IsTeacherTaskCompleted(selectedLessonIndex))
        {
            currentMode = DialogueMode.Practicing;
            ShowReadyForTestPrompt();
        }
        else
        {
            currentMode = DialogueMode.LessonBrowse;
            UpdateBrowseDialogueText();
        }
    }

    public void CloseDialogue()
    {
        isDialogueActive = false;
        if (quizAdvanceCoroutine != null) StopCoroutine(quizAdvanceCoroutine);
        if (speechBubbleObj != null) speechBubbleObj.SetActive(false);
        if (promptIndicator != null && isPlayerInRange) promptIndicator.SetActive(true);
        PlayTeacherAnimation("Idle");
    }

    public void NextLesson()
    {
        int count = (chalkboard != null && chalkboard.lessons != null && chalkboard.lessons.Count > 0) ? chalkboard.lessons.Count : 7;
        selectedLessonIndex = (selectedLessonIndex + 1) % count;
        currentMode = DialogueMode.LessonBrowse;
        UpdateBrowseDialogueText();
    }

    public void PreviousLesson()
    {
        int count = (chalkboard != null && chalkboard.lessons != null && chalkboard.lessons.Count > 0) ? chalkboard.lessons.Count : 7;
        selectedLessonIndex = (selectedLessonIndex - 1 + count) % count;
        currentMode = DialogueMode.LessonBrowse;
        UpdateBrowseDialogueText();
    }

    public void StartCurrentLesson()
    {
        if (chalkboard != null)
        {
            chalkboard.DisplayLesson(selectedLessonIndex);
        }

        string lessonTitle = GetCurrentLessonTitle();

        string text = $"<b>Teacher Bot</b>\n" +
                      $"<color=#81C784><b>Starting Lesson {selectedLessonIndex + 1}: {lessonTitle}!</b></color>\n" +
                      $"1. Watch each sign on the chalkboard.\n" +
                      $"2. Practice with your VR hands (pinch / hold).\n" +
                      $"3. Come back to me for your <b>Test Quiz</b>!\n\n" +
                      $"<color=#FFE082>[T] Start Quiz Test    [A/D] Change Lesson    [E] Close</color>";

        SetDialogueText(text);
        PlayTeacherAnimation("Wave");
    }

    // --- QUIZ AND EVALUATION LOGIC ---
    public void StartQuizTest()
    {
        currentMode = DialogueMode.QuizTesting;
        quizQuestionIndex = 0;
        isDialogueActive = true;
        if (speechBubbleObj != null) speechBubbleObj.SetActive(true);

        ShowCurrentQuizQuestion();
    }

    private void ShowCurrentQuizQuestion()
    {
        string signName = GetSignTitle(quizQuestionIndex);
        string lessonTitle = GetCurrentLessonTitle();

        // Animate teacher demonstrating the sign
        PlayGestureDemonstration(quizQuestionIndex);

        string text = $"<b>Teacher Bot — Lesson {selectedLessonIndex + 1} Quiz</b>\n" +
                      $"<color=#FFE082><b>Question {quizQuestionIndex + 1} of 3:</b></color> Show me the sign for:\n" +
                      $"<size=120%><color=#81C784><b>\"{signName.ToUpper()}\"</b></color></size>\n\n" +
                      $"<color=#B0BEC5>Perform the gesture with your VR hand tracker or press [1,2,3/Space]!</color>";

        SetDialogueText(text);
    }

    /// <summary>
    /// Called when the player demonstrates a gesture while the Teacher Quiz is active.
    /// </summary>
    public void OnPlayerDemonstratedGesture(int signIndex)
    {
        if (!IsQuizActive) return;

        if (signIndex == quizQuestionIndex)
        {
            // Correct gesture demonstrated!
            string signName = GetSignTitle(quizQuestionIndex);
            PlayTeacherAnimation("Curl");

            if (quizQuestionIndex < 2)
            {
                // Advance to next question
                string text = $"<b>Teacher Bot</b>\n" +
                              $"<color=#81C784><b>✔ Spot on! That's {signName.ToUpper()}!</b></color>\n\n" +
                              $"Getting ready for Question {quizQuestionIndex + 2}...";
                SetDialogueText(text);

                if (quizAdvanceCoroutine != null) StopCoroutine(quizAdvanceCoroutine);
                quizAdvanceCoroutine = StartCoroutine(AdvanceQuizDelay(quizQuestionIndex + 1));
            }
            else
            {
                // Completed all 3 questions!
                CompleteQuizTest();
            }
        }
        else
        {
            // Wrong gesture
            PlayTeacherAnimation("Disagree");
            string targetSign = GetSignTitle(quizQuestionIndex);
            string text = $"<b>Teacher Bot</b>\n" +
                          $"<color=#FF8A80><b>Not quite! Try again.</b></color>\n" +
                          $"I asked for: <color=#FFE082><b>{targetSign.ToUpper()}</b></color>!\n\n" +
                          $"<size=75%><color=#B0BEC5>[VR: Pinch & Hold | PC: Press {quizQuestionIndex + 1} or Click]</color></size>";
            SetDialogueText(text);
        }
    }

    private IEnumerator AdvanceQuizDelay(int nextQuestionIndex)
    {
        yield return new WaitForSeconds(1.3f);
        quizQuestionIndex = nextQuestionIndex;
        ShowCurrentQuizQuestion();
    }

    private void CompleteQuizTest()
    {
        currentMode = DialogueMode.QuizPassed;
        PlayTeacherAnimation("Celebrate");

        int bonus = 150;
        if (SASLScoreManager.Instance != null)
        {
            bonus = SASLScoreManager.Instance.CurrentLessonPointsEarned > 0 
                ? SASLScoreManager.Instance.CurrentLessonPointsEarned 
                : 150;
            SASLScoreManager.Instance.AwardTeacherTaskPoints(selectedLessonIndex);
        }

        string lessonTitle = GetCurrentLessonTitle();
        string text = $"<b>Teacher Bot — Test Passed! [EXCELLENT]</b>\n" +
                      $"<color=#81C784><b>Outstanding! You passed the test for {lessonTitle}!</b></color>\n\n" +
                      $"<color=#FFD54F><b>+{bonus} BONUS POINTS</b></color> awarded for completing the Teacher-Led Task!\n" +
                      $"You've proven complete mastery of these SASL signs!\n\n" +
                      $"<color=#80D8FF>[Space/Enter] Next Lesson    [E] Close Dialogue</color>";

        SetDialogueText(text);
    }

    private void ShowReadyForTestPrompt()
    {
        string lessonTitle = GetCurrentLessonTitle();
        string text = $"<b>Teacher Bot</b>\n" +
                      $"<color=#81C784><b>You practiced all signs for {lessonTitle}!</b></color>\n" +
                      $"Ready to prove your skills and earn the <b>Teacher Bonus Points</b>?\n\n" +
                      $"<color=#FFD54F><b>[T] or Gaze: Take Quiz Test!</b></color>\n" +
                      $"<color=#B0BEC5>[A/D] Change Lesson    [E] Close</color>";

        SetDialogueText(text);
    }

    private void UpdateBrowseDialogueText()
    {
        if (chalkboard == null) chalkboard = FindFirstObjectByType<ChalkboardLesson>();
        if (chalkboard != null) chalkboard.InitializeLessonsIfEmpty();

        int totalLessons = (chalkboard != null && chalkboard.lessons != null) ? chalkboard.lessons.Count : 7;
        string title = GetCurrentLessonTitle();
        string sub = "Everyday Greetings & Politeness";
        string summary = "Essential greetings: HELLO, THANK YOU, and PLEASE.";

        if (chalkboard != null && chalkboard.lessons != null && selectedLessonIndex < chalkboard.lessons.Count)
        {
            var l = chalkboard.lessons[selectedLessonIndex];
            sub = l.lessonSubtitle;
            summary = l.shortSummary;
        }

        string text = $"<b>Teacher Bot</b>\n" +
                      $"<b>Lesson {selectedLessonIndex + 1} of {totalLessons}: {title}</b>\n" +
                      $"<size=80%><color=#B0BEC5>{sub}</color></size>\n" +
                      $"{summary}\n\n" +
                      $"<color=#81C784><b>[Space] Start on Chalkboard    [T] Take Quiz Test</b></color>\n" +
                      $"<color=#FFE082>[A/D] Switch Lesson    [E] Close Dialogue</color>";

        SetDialogueText(text);
    }

    private string GetCurrentLessonTitle()
    {
        if (chalkboard != null && chalkboard.lessons != null && selectedLessonIndex < chalkboard.lessons.Count)
        {
            return chalkboard.lessons[selectedLessonIndex].lessonTitle;
        }
        return $"Lesson {selectedLessonIndex + 1}";
    }

    private string GetSignTitle(int signIndex)
    {
        if (chalkboard != null)
        {
            return chalkboard.GetSignTitle(signIndex);
        }
        return $"Sign {signIndex + 1}";
    }

    private void PlayGestureDemonstration(int signIndex)
    {
        // Animate teacher model with relevant demonstration clip
        switch (signIndex)
        {
            case 0:
                PlayTeacherAnimation("Wave");
                break;
            case 1:
                PlayTeacherAnimation("Heart");
                break;
            case 2:
                PlayTeacherAnimation("Heart");
                break;
            default:
                PlayTeacherAnimation("Curl");
                break;
        }
    }

    public void PlayTeacherAnimation(string stateName)
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        }
        if (animator == null) return;

        // Try playing short state name first, fallback to full clip name if needed
        if (animator.HasState(0, Animator.StringToHash(stateName)))
        {
            animator.CrossFade(stateName, 0.15f);
        }
        else
        {
            animator.Play(stateName);
        }
    }

    private void SetDialogueText(string content)
    {
        if (dialogueTmp == null) return;
        dialogueTmp.text = content;
        dialogueTmp.maxVisibleCharacters = content.Length;
        dialogueTmp.ForceMeshUpdate();
    }

    void BillboardToViewer()
    {
        if (bubbleContainer == null) return;

        Camera targetCam = null;
        if (Application.isPlaying)
        {
            targetCam = Camera.main;
        }
#if UNITY_EDITOR
        else
        {
            var sceneView = UnityEditor.SceneView.lastActiveSceneView;
            if (sceneView != null) targetCam = sceneView.camera;
        }
#endif
        if (targetCam == null) targetCam = Camera.main;

        if (targetCam != null)
        {
            Vector3 camPos = targetCam.transform.position;
            Vector3 dir = (camPos - bubbleContainer.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                bubbleContainer.rotation = Quaternion.LookRotation(-dir.normalized);
            }
        }
    }

    // --- CARDBOARD VR INTERACTION ---
    public void OnGazeEnter()
    {
        if (!isDialogueActive && promptIndicator != null)
        {
            promptIndicator.SetActive(true);
        }
    }

    public void OnGazeExit()
    {
        if (!isDialogueActive && promptIndicator != null)
        {
            promptIndicator.SetActive(isPlayerInRange);
        }
    }

    public void OnGazeTrigger()
    {
        if (!isDialogueActive)
        {
            OpenDialogue();
        }
        else if (currentMode == DialogueMode.LessonBrowse)
        {
            // Trigger starts lesson on chalkboard
            StartCurrentLesson();
        }
        else if (currentMode == DialogueMode.Practicing)
        {
            StartQuizTest();
        }
        else if (currentMode == DialogueMode.QuizTesting)
        {
            OnPlayerDemonstratedGesture(quizQuestionIndex);
        }
        else if (currentMode == DialogueMode.QuizPassed)
        {
            NextLesson();
            currentMode = DialogueMode.LessonBrowse;
            StartCurrentLesson();
        }
    }
}
