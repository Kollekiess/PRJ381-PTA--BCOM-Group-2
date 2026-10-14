using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

[ExecuteAlways]
public class TeacherInteraction : MonoBehaviour, ICardboardInteractable
{
    [Header("References")]
    public ChalkboardLesson chalkboard;
    public Transform player;

    [Header("Interaction Settings")]
    public float interactionDistance = 3.8f;

    [Header("Speech Bubble & Dialogue")]
    public Transform bubbleContainer;
    public TextMeshPro dialogueTmp;
    public GameObject promptIndicator;
    public GameObject speechBubbleObj;

    [Header("Lesson Navigation State")]
    public int selectedLessonIndex = 0;
    public bool isDialogueActive = false;
    private bool isPlayerInRange = false;

    private Coroutine typeCoroutine;

    void Start()
    {
        FindReferences();
        if (Application.isPlaying)
        {
            isDialogueActive = false;
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

        // SAFE INPUT HANDLING VIA NEW INPUT SYSTEM ONLY (No UnityEngine.Input calls)
        var kb = Keyboard.current;
        if (kb == null) return;

        // E key toggles dialogue
        if (kb.eKey.wasPressedThisFrame)
        {
            if (!isDialogueActive) OpenDialogue();
            else CloseDialogue();
            return;
        }

        if (isDialogueActive)
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
            // Start lesson: Space or Enter
            else if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                StartCurrentLesson();
            }
        }
    }

    public void NextLesson()
    {
        if (chalkboard != null && chalkboard.lessons != null && chalkboard.lessons.Count > 0)
        {
            selectedLessonIndex = (selectedLessonIndex + 1) % chalkboard.lessons.Count;
        }
        else
        {
            selectedLessonIndex = (selectedLessonIndex + 1) % 7;
        }
        UpdateDialogueText();
    }

    public void PreviousLesson()
    {
        int count = (chalkboard != null && chalkboard.lessons != null && chalkboard.lessons.Count > 0) ? chalkboard.lessons.Count : 7;
        selectedLessonIndex = (selectedLessonIndex - 1 + count) % count;
        UpdateDialogueText();
    }

    public void StartCurrentLesson()
    {
        if (chalkboard != null)
        {
            chalkboard.DisplayLesson(selectedLessonIndex);
        }

        // Show confirmation in dialogue
        string lessonTitle = "Lesson";
        if (chalkboard != null && chalkboard.lessons != null && selectedLessonIndex < chalkboard.lessons.Count)
        {
            lessonTitle = chalkboard.lessons[selectedLessonIndex].lessonTitle;
        }

        string text = $"<b>Teacher Bot</b>\n" +
                      $"<color=#81C784><b>Starting Lesson {selectedLessonIndex + 1}: {lessonTitle}!</b></color>\n" +
                      $"Watch the chalkboard to follow along with each sign!\n\n" +
                      $"<color=#B0BEC5>[A/D] Change Lesson    [E] Close</color>";

        SetDialogueText(text);

        // Wave animation
        var anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.Play("Armature|Armature|Big_Wave_Hello|baselayer", 0, 0f);
        }
    }

    public void OpenDialogue()
    {
        isDialogueActive = true;
        if (promptIndicator != null) promptIndicator.SetActive(false);
        if (speechBubbleObj != null) speechBubbleObj.SetActive(true);

        if (chalkboard != null) chalkboard.InitializeLessonsIfEmpty();

        UpdateDialogueText();
    }

    public void CloseDialogue()
    {
        isDialogueActive = false;
        if (typeCoroutine != null) StopCoroutine(typeCoroutine);
        if (speechBubbleObj != null) speechBubbleObj.SetActive(false);
        if (promptIndicator != null && isPlayerInRange) promptIndicator.SetActive(true);
    }

    private void UpdateDialogueText()
    {
        if (chalkboard == null) chalkboard = FindFirstObjectByType<ChalkboardLesson>();
        if (chalkboard != null) chalkboard.InitializeLessonsIfEmpty();

        int totalLessons = (chalkboard != null && chalkboard.lessons != null) ? chalkboard.lessons.Count : 7;
        string title = "Everyday Greetings";
        string sub = "Everyday Greetings & Politeness";
        string summary = "Essential greetings: HELLO, THANK YOU, and PLEASE.";

        if (chalkboard != null && chalkboard.lessons != null && selectedLessonIndex < chalkboard.lessons.Count)
        {
            var l = chalkboard.lessons[selectedLessonIndex];
            title = l.lessonTitle;
            sub = l.lessonSubtitle;
            summary = l.shortSummary;
        }

        string text = $"<b>Teacher Bot</b>\n" +
                      $"<b>Lesson {selectedLessonIndex + 1} of {totalLessons}: {title}</b>\n" +
                      $"<size=80%><color=#B0BEC5>{sub}</color></size>\n" +
                      $"{summary}\n\n" +
                      $"<color=#FFE082>Gaze/Tap: Next Lesson  •  Chalkboard: Start  •  [A/D/Space]</color>\n" +
                      $"<size=75%><color=#90A4AE>[E] Close Dialogue</color></size>";

        SetDialogueText(text);
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
        else
        {
            NextLesson();
        }
    }

}
