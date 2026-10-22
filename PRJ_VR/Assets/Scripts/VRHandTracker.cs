using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

/// <summary>
/// Controls VR hand tracking for Meta Quest and PC VR (via OpenXR),
/// with built-in desktop simulation when testing without a VR headset.
/// Uses stylized spheres for temporary hand representation and gesture tracking.
/// </summary>
public class VRHandTracker : MonoBehaviour
{
    public enum HandSide { Left, Right }

    [Header("Hand Identity")]
    public HandSide handSide = HandSide.Left;

    [Header("Visual Components")]
    public Transform visualRoot;
    public MeshRenderer handRenderer;

    [Header("Styling & Gesture Colors")]
    public Color normalColor = new Color(0f, 0.82f, 1f, 0.85f);       // Cyan / Teal for Left Hand
    public Color activeGestureColor = new Color(1f, 0.95f, 0.3f, 1f);  // Bright Amber / Gold on pinch/grip
    
    [Header("Scale Feedback")]
    public Vector3 openHandScale = new Vector3(0.085f, 0.085f, 0.085f);
    public Vector3 pinchedScale = new Vector3(0.055f, 0.055f, 0.045f);

    [Header("Desktop Simulation (No VR Headset)")]
    public bool enableDesktopSimulation = true;
    public Vector3 desktopOffset = new Vector3(0.22f, -0.16f, 0.45f);

    // Runtime state
    private InputDevice trackedDevice;
    private MaterialPropertyBlock propBlock;
    private static readonly int ColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private Camera targetCamera;

    /// <summary>
    /// Current normalized gesture intensity (0 = open hand, 1 = full pinch / grip).
    /// </summary>
    public float GestureAmount { get; private set; }
    public bool IsPinching => GestureAmount > 0.5f;
    public bool IsTrackedByHardware { get; private set; }

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();

        // Default colors based on side if not custom
        if (handSide == HandSide.Right && normalColor == new Color(0f, 0.82f, 1f, 0.85f))
        {
            normalColor = new Color(1f, 0.48f, 0.12f, 0.85f); // Vibrant Coral / Orange for Right Hand
        }

        UpdateVisualColor(normalColor);
    }

    private void Start()
    {
        targetCamera = Camera.main;
        if (targetCamera == null)
        {
            targetCamera = FindFirstObjectByType<Camera>();
        }

        EnsureVisualHierarchy();
        FindDevice();
    }

    private void EnsureVisualHierarchy()
    {
        if (visualRoot == null)
        {
            Transform existing = transform.Find("HandVisual");
            if (existing != null)
            {
                visualRoot = existing;
            }
            else
            {
                // Create sphere visual primitive if not present
                GameObject sphereObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphereObj.name = "HandVisual";
                sphereObj.transform.SetParent(transform, false);
                sphereObj.transform.localPosition = Vector3.zero;
                sphereObj.transform.localRotation = Quaternion.identity;
                sphereObj.transform.localScale = openHandScale;

                SphereCollider col = sphereObj.GetComponent<SphereCollider>();
                if (col != null)
                {
                    col.isTrigger = true;
                }

                visualRoot = sphereObj.transform;
                handRenderer = sphereObj.GetComponent<MeshRenderer>();
            }
        }

        if (handRenderer == null && visualRoot != null)
        {
            handRenderer = visualRoot.GetComponent<MeshRenderer>();
        }
    }

    private void Update()
    {
        if (!trackedDevice.isValid)
        {
            FindDevice();
        }

        bool hardwareTrackingActive = false;

        if (trackedDevice.isValid)
        {
            // Read 6-DOF pose from OpenXR / Quest
            if (trackedDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
            {
                transform.localPosition = pos;
                hardwareTrackingActive = true;
            }

            if (trackedDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
            {
                transform.localRotation = rot;
                hardwareTrackingActive = true;
            }

            // Read gestures: Trigger (pinch / index point) and Grip (fist grab)
            trackedDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerVal);
            trackedDevice.TryGetFeatureValue(CommonUsages.grip, out float gripVal);

            GestureAmount = Mathf.Max(triggerVal, gripVal);
        }

        IsTrackedByHardware = hardwareTrackingActive;

        // Desktop Fallback when testing without Meta Quest headset
        if (!hardwareTrackingActive && enableDesktopSimulation)
        {
            SimulateDesktopHands();
        }

        // Apply smooth visual feedback for gestures
        ApplyVisualFeedback();
    }

    private void SimulateDesktopHands()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) return;
        }

        // Position hand in front of the player's view
        Vector3 sideOffset = desktopOffset;
        if (handSide == HandSide.Left)
        {
            sideOffset.x = -Mathf.Abs(sideOffset.x);
        }
        else
        {
            sideOffset.x = Mathf.Abs(sideOffset.x);
        }

        // Add subtle natural floating sway
        float time = Time.time * 1.5f;
        float swayX = Mathf.Sin(time + (handSide == HandSide.Left ? 0f : 1.5f)) * 0.008f;
        float swayY = Mathf.Cos(time * 0.8f) * 0.008f;
        Vector3 targetPos = targetCamera.transform.position 
            + targetCamera.transform.right * (sideOffset.x + swayX)
            + targetCamera.transform.up * (sideOffset.y + swayY)
            + targetCamera.transform.forward * sideOffset.z;

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 12f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetCamera.transform.rotation, Time.deltaTime * 12f);

        // Mouse click triggers test gestures on PC:
        // Left Mouse Button -> Left Hand gesture
        // Right Mouse Button -> Right Hand gesture
        bool isClicking = false;
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            isClicking = (handSide == HandSide.Left && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)
                      || (handSide == HandSide.Right && UnityEngine.InputSystem.Mouse.current.rightButton.isPressed);
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (!isClicking)
        {
            isClicking = (handSide == HandSide.Left && Input.GetMouseButton(0)) 
                      || (handSide == HandSide.Right && Input.GetMouseButton(1));
        }
#endif

        float targetGesture = isClicking ? 1f : 0f;
        GestureAmount = Mathf.MoveTowards(GestureAmount, targetGesture, Time.deltaTime * 8f);
    }

    private void ApplyVisualFeedback()
    {
        if (visualRoot == null) return;

        // Animate scale on gesture (squeeze / pinch)
        Vector3 targetScale = Vector3.Lerp(openHandScale, pinchedScale, GestureAmount);
        visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, targetScale, Time.deltaTime * 14f);

        // Animate color and brightness on gesture
        Color targetColor = Color.Lerp(normalColor, activeGestureColor, GestureAmount);
        UpdateVisualColor(targetColor);
    }

    private void UpdateVisualColor(Color color)
    {
        if (handRenderer == null) return;

        handRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(ColorPropertyId, color);
        // Also support Standard/URP Emission if available
        propBlock.SetColor(EmissionColorId, color * (0.2f + GestureAmount * 0.6f));
        handRenderer.SetPropertyBlock(propBlock);
    }

    private void FindDevice()
    {
        XRNode node = (handSide == HandSide.Left) ? XRNode.LeftHand : XRNode.RightHand;
        trackedDevice = InputDevices.GetDeviceAtXRNode(node);
    }
}
