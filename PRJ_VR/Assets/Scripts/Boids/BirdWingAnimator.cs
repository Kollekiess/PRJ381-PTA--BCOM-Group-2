using UnityEngine;

[ExecuteAlways]
public class BirdWingAnimator : MonoBehaviour
{
    [Header("Wing References")]
    [SerializeField] private Transform leftWing;
    [SerializeField] private Transform rightWing;

    [Header("Flap Settings")]
    [Tooltip("Speed/frequency of wing flapping")]
    public float flapFrequency = 22f;

    [Tooltip("Maximum wing angle in degrees during flap")]
    public float flapAmplitude = 38f;

    [Tooltip("Flap phase duration in seconds")]
    public float minFlapDuration = 1.4f;
    public float maxFlapDuration = 2.4f;

    [Header("Glide Settings")]
    [Tooltip("Glide phase duration in seconds")]
    public float minGlideDuration = 2.0f;
    public float maxGlideDuration = 3.6f;

    [Tooltip("Wing angle while gliding (slight upward dihedral)")]
    public float glideDihedralAngle = 6f;

    [Tooltip("Transition smoothness between flapping and gliding")]
    public float transitionSpeed = 10f;

    [Header("Folding Settings")]
    [Tooltip("Z angle to fold wings along body on ground")]
    public float foldZAngle = 70f;
    public float foldSpeed = 6f;

    private float currentFlapDuration;
    private float currentGlideDuration;
    private float cycleTimer = 0f;
    private bool isFlapping = true;
    private float timeOffset;
    private float currentWingAngle = 0f;

    // Custom states
    private bool isWingsFolded = false;
    private bool isPanicFlapping = false;
    private float foldWeight = 0f; // 0 = flying, 1 = folded

    void Awake()
    {
        InitializeWings();
        ResetCycleTimers();
        timeOffset = Random.Range(0f, 100f);
    }

    void OnEnable()
    {
        InitializeWings();
        ResetCycleTimers();
        timeOffset = Random.Range(0f, 100f);
    }

    public void InitializeWings()
    {
        if (leftWing == null)
        {
            leftWing = transform.Find("Pigeon_Wing_L") ?? transform.Find("Cockatoo_Wing_L");
            if (leftWing == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    if (child.name.EndsWith("Wing_L")) { leftWing = child; break; }
                }
            }
        }

        if (rightWing == null)
        {
            rightWing = transform.Find("Pigeon_Wing_R") ?? transform.Find("Cockatoo_Wing_R");
            if (rightWing == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    if (child.name.EndsWith("Wing_R")) { rightWing = child; break; }
                }
            }
        }
    }

    void ResetCycleTimers()
    {
        currentFlapDuration = Random.Range(minFlapDuration, maxFlapDuration);
        currentGlideDuration = Random.Range(minGlideDuration, maxGlideDuration);
        cycleTimer = 0f;
    }

    public void SetWingsFolded(bool folded)
    {
        isWingsFolded = folded;
        if (folded) isPanicFlapping = false;
    }

    public void SetPanicFlap(bool panic)
    {
        isPanicFlapping = panic;
        if (panic) isWingsFolded = false;
    }

    void Update()
    {
        if (leftWing == null || rightWing == null)
        {
            InitializeWings();
            if (leftWing == null || rightWing == null) return;
        }

        float dt = Application.isPlaying ? Time.deltaTime : 0.016f;

        // Smooth fold transition weight
        float targetFoldWeight = isWingsFolded ? 1f : 0f;
        foldWeight = Mathf.MoveTowards(foldWeight, targetFoldWeight, dt * foldSpeed);

        float targetAngle;

        if (isPanicFlapping)
        {
            float freq = flapFrequency * 1.6f;
            float amp = flapAmplitude * 1.15f;
            float t = (Time.time + timeOffset) * freq;
            targetAngle = Mathf.Sin(t) * amp;
        }
        else
        {
            cycleTimer += dt;

            if (isFlapping)
            {
                if (cycleTimer >= currentFlapDuration)
                {
                    isFlapping = false;
                    cycleTimer = 0f;
                    currentGlideDuration = Random.Range(minGlideDuration, maxGlideDuration);
                    targetAngle = glideDihedralAngle;
                }
                else
                {
                    float t = (Time.time + timeOffset) * flapFrequency;
                    targetAngle = Mathf.Sin(t) * flapAmplitude;
                }
            }
            else
            {
                if (cycleTimer >= currentGlideDuration)
                {
                    isFlapping = true;
                    cycleTimer = 0f;
                    currentFlapDuration = Random.Range(minFlapDuration, maxFlapDuration);
                }
                targetAngle = glideDihedralAngle;
            }
        }

        // Interpolate flapping angle
        currentWingAngle = Mathf.Lerp(currentWingAngle, targetAngle, dt * transitionSpeed);

        // Compute flying rotation (flapping around local Y)
        Quaternion flyRotL = Quaternion.Euler(0f, currentWingAngle, 0f);
        Quaternion flyRotR = Quaternion.Euler(0f, -currentWingAngle, 0f);

        // Compute folded rotation (tucked back along body around local Z)
        Quaternion foldRotL = Quaternion.Euler(0f, 0f, foldZAngle);
        Quaternion foldRotR = Quaternion.Euler(0f, 0f, -foldZAngle);

        // Blend smoothly between flight and folded wings
        leftWing.localRotation = Quaternion.Slerp(flyRotL, foldRotL, foldWeight);
        rightWing.localRotation = Quaternion.Slerp(flyRotR, foldRotR, foldWeight);
    }
}
