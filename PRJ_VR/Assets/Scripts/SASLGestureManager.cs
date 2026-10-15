using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Central manager for SASL gesture detection and validation across both
/// VR (OpenXR / Meta Quest via VRHandTracker) and Desktop testing environments.
/// </summary>
public class SASLGestureManager : MonoBehaviour
{
    public static SASLGestureManager Instance { get; private set; }

    [Header("Hand Trackers")]
    public VRHandTracker leftHand;
    public VRHandTracker rightHand;

    [Header("Gesture Detection Thresholds")]
    public float gestureActivationThreshold = 0.55f;
    public float gestureHoldDuration = 0.5f;

    [Header("Current Target Sign (0, 1, 2)")]
    public int targetedSignIndex = 0;

    private float leftHoldTimer = 0f;
    private float rightHoldTimer = 0f;
    private bool gestureFiredThisHold = false;

    [Header("Breadcrumb & 'Eat' Sign Interaction")]
    public BreadcrumbSpawner breadcrumbSpawner;
    public float mouthDistanceThreshold = 0.28f;
    private float eatHoldTimer = 0f;
    private bool eatGestureFiredThisHold = false;

    private ChalkboardLesson chalkboard;
    private TeacherInteraction teacher;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        FindReferences();
    }

    private void FindReferences()
    {
        if (leftHand == null || rightHand == null)
        {
            var trackers = FindObjectsByType<VRHandTracker>(FindObjectsSortMode.None);
            foreach (var t in trackers)
            {
                if (t.handSide == VRHandTracker.HandSide.Left) leftHand = t;
                else if (t.handSide == VRHandTracker.HandSide.Right) rightHand = t;
            }
        }

        if (chalkboard == null) chalkboard = FindFirstObjectByType<ChalkboardLesson>();
        if (teacher == null) teacher = FindFirstObjectByType<TeacherInteraction>();
        if (breadcrumbSpawner == null) breadcrumbSpawner = FindFirstObjectByType<BreadcrumbSpawner>();
    }

    private void Update()
    {
        if (leftHand == null || rightHand == null || breadcrumbSpawner == null) FindReferences();

        // 1. Check VR Hand gestures
        bool leftActive = leftHand != null && leftHand.GestureAmount >= gestureActivationThreshold;
        bool rightActive = rightHand != null && rightHand.GestureAmount >= gestureActivationThreshold;

        // --- SASL 'EAT / FOOD' GESTURE DETECTION (Hand brought to mouth + pinched) ---
        Camera headCam = Camera.main;
        if (headCam == null) headCam = FindFirstObjectByType<Camera>();

        bool isEatGestureActive = false;
        if (headCam != null)
        {
            // Mouth is roughly 14cm below eyes and 8cm forward
            Vector3 mouthPos = headCam.transform.position - headCam.transform.up * 0.14f + headCam.transform.forward * 0.08f;

            bool leftNearMouth = leftHand != null && Vector3.Distance(leftHand.transform.position, mouthPos) <= mouthDistanceThreshold;
            bool rightNearMouth = rightHand != null && Vector3.Distance(rightHand.transform.position, mouthPos) <= mouthDistanceThreshold;

            if ((leftNearMouth && leftActive) || (rightNearMouth && rightActive))
            {
                isEatGestureActive = true;
            }
        }

        if (isEatGestureActive)
        {
            eatHoldTimer += Time.deltaTime;
            if (eatHoldTimer >= 0.35f && !eatGestureFiredThisHold)
            {
                eatGestureFiredThisHold = true;
                TriggerEatSignGesture();
            }
        }
        else
        {
            eatHoldTimer = 0f;
            if (!leftActive && !rightActive)
            {
                eatGestureFiredThisHold = false;
            }
        }

        // --- STANDARD GENERAL GESTURE HOLD ---
        if (leftActive) leftHoldTimer += Time.deltaTime;
        else leftHoldTimer = 0f;

        if (rightActive) rightHoldTimer += Time.deltaTime;
        else rightHoldTimer = 0f;

        bool isHoldingGesture = (leftHoldTimer >= gestureHoldDuration) || (rightHoldTimer >= gestureHoldDuration);

        if (isHoldingGesture && !gestureFiredThisHold && !isEatGestureActive)
        {
            gestureFiredThisHold = true;
            OnGestureRecognized(targetedSignIndex);
        }
        else if (!leftActive && !rightActive)
        {
            gestureFiredThisHold = false;
        }

        // 2. Desktop Keyboard Shortcuts for Testing / Desktop Play Mode (New Input System)
        var kb = Keyboard.current;
        if (kb != null)
        {
            // Direct sign testing: keys 1, 2, 3
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
            {
                OnGestureRecognized(0);
            }
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
            {
                OnGestureRecognized(1);
            }
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
            {
                OnGestureRecognized(2);
            }
            // G key = Trigger current targeted sign gesture
            else if (kb.gKey.wasPressedThisFrame)
            {
                OnGestureRecognized(targetedSignIndex);
            }
            // E key = Trigger SASL 'EAT' gesture directly (when not busy interacting with teacher dialogue)
            else if (kb.eKey.wasPressedThisFrame)
            {
                bool teacherBusy = (teacher != null && (teacher.isDialogueActive || teacher.IsPlayerInRange));
                if (!teacherBusy)
                {
                    TriggerEatSignGesture();
                }
            }
        }
    }

    /// <summary>
    /// Executes the SASL 'EAT / FOOD' gesture.
    /// Summons a breadcrumb for birds (up to 4 max in scene) and awards lesson progress if applicable.
    /// </summary>
    public void TriggerEatSignGesture()
    {
        if (breadcrumbSpawner == null) breadcrumbSpawner = FindFirstObjectByType<BreadcrumbSpawner>();

        bool crumbTossed = false;
        if (breadcrumbSpawner != null)
        {
            crumbTossed = breadcrumbSpawner.TossBreadcrumb();
        }

        if (crumbTossed && ScoreHUD.Instance != null)
        {
            ScoreHUD.Instance.ShowToast("SASL Sign: 'EAT / FOOD' — Breadcrumb tossed for birds!");
        }

        // If teacher is running a quiz on Lesson 4 (Everyday Needs: Sign 1 is EAT / FOOD):
        if (teacher != null && teacher.IsQuizActive)
        {
            if (teacher.selectedLessonIndex == 3 && teacher.quizQuestionIndex == 0)
            {
                teacher.OnPlayerDemonstratedGesture(0);
                return;
            }
        }

        // If chalkboard is open to Lesson 4 (index 3):
        if (chalkboard != null && chalkboard.isLessonActive && chalkboard.currentLessonIndex == 3)
        {
            OnGestureRecognized(0);
        }
    }

    /// <summary>
    /// Invoked when a sign gesture is completed by the player.
    /// Routes the gesture to Teacher Quiz (if active) or Chalkboard Lesson Practice.
    /// </summary>
    public void OnGestureRecognized(int signIndex)
    {
        if (teacher == null) teacher = FindFirstObjectByType<TeacherInteraction>();
        if (chalkboard == null) chalkboard = FindFirstObjectByType<ChalkboardLesson>();

        // If the teacher is currently testing the player in a quiz:
        if (teacher != null && teacher.IsQuizActive)
        {
            teacher.OnPlayerDemonstratedGesture(signIndex);
            return;
        }

        // Otherwise, player is practicing lesson on the chalkboard:
        if (chalkboard != null && chalkboard.isLessonActive)
        {
            int lIdx = chalkboard.currentLessonIndex;
            string signName = chalkboard.GetSignTitle(signIndex);

            // Award points via score manager (strictly once per step/gesture)
            if (SASLScoreManager.Instance != null)
            {
                bool awarded = SASLScoreManager.Instance.AwardStepPoints(lIdx, signIndex, signName);
                if (awarded)
                {
                    chalkboard.MarkSignCompleted(signIndex);
                    if (lIdx == 3 && signIndex == 0)
                    {
                        if (breadcrumbSpawner == null) breadcrumbSpawner = FindFirstObjectByType<BreadcrumbSpawner>();
                        if (breadcrumbSpawner != null) breadcrumbSpawner.TossBreadcrumb();
                    }
                }
                else
                {
                    if (ScoreHUD.Instance != null)
                    {
                        ScoreHUD.Instance.ShowToast($"Sign {signIndex + 1} ({signName}) Already Mastered!");
                    }
                }
            }
        }
        else
        {
            // If chalkboard not active yet, show hint
            if (ScoreHUD.Instance != null)
            {
                ScoreHUD.Instance.ShowToast("Talk to Teacher or view Chalkboard to start lesson!");
            }
        }
    }

    public void SetTargetedSign(int index)
    {
        targetedSignIndex = Mathf.Clamp(index, 0, 2);
    }
}
