using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages player scoring, step-by-step SASL gesture points,
/// and teacher-led task evaluation rewards.
/// Strictly enforces:
/// 1. Only rewards completing gestures during a lesson once per step and gesture.
/// 2. Awards the total points received from the lesson once again upon passing the teacher-led task.
/// </summary>
public class SASLScoreManager : MonoBehaviour
{
    public static SASLScoreManager Instance { get; private set; }

    [Header("Points Configuration")]
    [Tooltip("Base points awarded for learning/completing a gesture step during a lesson.")]
    public int pointsPerGestureStep = 50;

    [Header("Current Progress")]
    [SerializeField] private int totalScore = 0;
    [SerializeField] private int currentLessonIndex = 0;
    [SerializeField] private int currentLessonPointsEarned = 0;

    // Track completed steps to prevent score grinding: "L{lessonIndex}_S{stepIndex}"
    private readonly HashSet<string> completedSteps = new HashSet<string>();
    // Track teacher test bonus per lesson
    private readonly HashSet<int> completedTeacherLessons = new HashSet<int>();

    // Events for UI and gameplay listeners
    public event Action<int, int, string> OnScoreChanged; // (totalScore, delta, reason)
    public event Action<int, int, string> OnStepCompleted; // (lessonIndex, stepIndex, signName)
    public event Action<int, int> OnTeacherTestCompleted; // (lessonIndex, bonusPoints)

    // Audio feedback
    private AudioSource audioSource;
    private AudioClip chimeClip;
    private AudioClip fanfareClip;

    public int TotalScore => totalScore;
    public int CurrentLessonIndex => currentLessonIndex;
    public int CurrentLessonPointsEarned => currentLessonPointsEarned;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializeAudio();
    }

    /// <summary>
    /// Sets active lesson and tracks points for this lesson session.
    /// </summary>
    public void SetActiveLesson(int lessonIndex)
    {
        if (currentLessonIndex != lessonIndex)
        {
            currentLessonIndex = lessonIndex;
            // Recalculate points earned for this lesson
            currentLessonPointsEarned = 0;
            for (int i = 0; i < 3; i++)
            {
                if (IsStepCompleted(lessonIndex, i))
                {
                    currentLessonPointsEarned += pointsPerGestureStep;
                }
            }
        }
    }

    /// <summary>
    /// Checks if a gesture step was already completed and awarded for a lesson.
    /// </summary>
    public bool IsStepCompleted(int lessonIndex, int stepIndex)
    {
        return completedSteps.Contains(GetStepKey(lessonIndex, stepIndex));
    }

    /// <summary>
    /// Counts how many steps have been completed for a given lesson (0 to 3).
    /// </summary>
    public int GetCompletedStepCount(int lessonIndex)
    {
        int count = 0;
        for (int i = 0; i < 3; i++)
        {
            if (IsStepCompleted(lessonIndex, i)) count++;
        }
        return count;
    }

    /// <summary>
    /// Checks if all 3 steps of a lesson have been completed.
    /// </summary>
    public bool AreAllLessonStepsCompleted(int lessonIndex)
    {
        return GetCompletedStepCount(lessonIndex) >= 3;
    }

    /// <summary>
    /// Checks if the teacher-led task was already completed and awarded for this lesson.
    /// </summary>
    public bool IsTeacherTaskCompleted(int lessonIndex)
    {
        return completedTeacherLessons.Contains(lessonIndex);
    }

    /// <summary>
    /// Awards points for completing a gesture during a lesson.
    /// Strictly rewards once per step/gesture per lesson.
    /// </summary>
    public bool AwardStepPoints(int lessonIndex, int stepIndex, string signName)
    {
        string key = GetStepKey(lessonIndex, stepIndex);
        if (completedSteps.Contains(key))
        {
            // Already awarded once for this step
            Debug.Log($"[SASLScoreManager] Step {stepIndex + 1} ({signName}) in Lesson {lessonIndex + 1} was already completed. No duplicate points.");
            return false;
        }

        completedSteps.Add(key);
        currentLessonIndex = lessonIndex;
        currentLessonPointsEarned += pointsPerGestureStep;
        totalScore += pointsPerGestureStep;

        PlayChimeSound();

        string reason = $"+{pointsPerGestureStep} PTS: {signName.ToUpper()} Learned!";
        Debug.Log($"[SASLScoreManager] {reason} Total Score: {totalScore}");

        OnScoreChanged?.Invoke(totalScore, pointsPerGestureStep, reason);
        OnStepCompleted?.Invoke(lessonIndex, stepIndex, signName);

        return true;
    }

    /// <summary>
    /// Awards the teacher-led task completion points.
    /// Strictly awards the total points received from the lesson once again.
    /// </summary>
    public bool AwardTeacherTaskPoints(int lessonIndex)
    {
        if (completedTeacherLessons.Contains(lessonIndex))
        {
            Debug.Log($"[SASLScoreManager] Teacher task for Lesson {lessonIndex + 1} was already awarded. No duplicate bonus.");
            return false;
        }

        completedTeacherLessons.Add(lessonIndex);

        // Calculate total points received from this lesson (e.g. 3 steps * 50 = 150)
        int bonusPoints = currentLessonPointsEarned > 0 ? currentLessonPointsEarned : (3 * pointsPerGestureStep);
        totalScore += bonusPoints;

        PlayFanfareSound();

        string reason = $"+{bonusPoints} PTS: Lesson {lessonIndex + 1} Teacher Test Passed!";
        Debug.Log($"[SASLScoreManager] {reason} (Total points from lesson awarded again for teacher task). Total Score: {totalScore}");

        OnScoreChanged?.Invoke(totalScore, bonusPoints, reason);
        OnTeacherTestCompleted?.Invoke(lessonIndex, bonusPoints);

        return true;
    }

    private string GetStepKey(int lessonIndex, int stepIndex)
    {
        return $"L{lessonIndex}_S{stepIndex}";
    }

    // --- PROCEDURAL AUDIO GENERATION ---
    private void InitializeAudio()
    {
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D crisp UI audio

        chimeClip = GenerateSynthChime();
        fanfareClip = GenerateSynthFanfare();
    }

    public void PlayChimeSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayCompletedLesson();
            return;
        }

        if (audioSource != null && chimeClip != null)
        {
            audioSource.PlayOneShot(chimeClip, 0.7f);
        }
    }

    public void PlayFanfareSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayTeacherTestPassed();
            return;
        }

        if (audioSource != null && fanfareClip != null)
        {
            audioSource.PlayOneShot(fanfareClip, 0.85f);
        }
    }

    private AudioClip GenerateSynthChime()
    {
        int sampleRate = 44100;
        float duration = 0.45f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // Harmonic frequencies (E5 -> G#5 -> B5 arpeggio chime)
        float[] freqs = new float[] { 659.25f, 830.61f, 987.77f };
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 9.0f); // Fast decay
            float val = 0f;
            for (int f = 0; f < freqs.Length; f++)
            {
                val += Mathf.Sin(2f * Mathf.PI * freqs[f] * t) * (1f / (f + 1));
            }
            samples[i] = val * envelope * 0.35f;
        }

        AudioClip clip = AudioClip.Create("ChimeAudio", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateSynthFanfare()
    {
        int sampleRate = 44100;
        float duration = 0.85f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // Triumphant 4-note major fanfare: C5 (0.0s), E5 (0.15s), G5 (0.3s), C6 (0.45s sustained)
        float[] freqs = new float[] { 523.25f, 659.25f, 783.99f, 1046.50f };
        float noteLen = 0.15f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            int noteIndex = Mathf.Clamp((int)(t / noteLen), 0, freqs.Length - 1);
            float noteTime = t - (noteIndex * noteLen);
            float envelope = Mathf.Exp(-noteTime * (noteIndex == 3 ? 3.5f : 6.0f));
            float freq = freqs[noteIndex];

            float val = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * freq * t);
            samples[i] = val * envelope * 0.35f;
        }

        AudioClip clip = AudioClip.Create("FanfareAudio", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
