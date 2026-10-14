using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using System.Collections.Generic;

public class CardboardManager : MonoBehaviour
{
    public static CardboardManager Instance { get; private set; }

    [Header("Stereo VR Simulator (Editor & Standalone)")]
    [Tooltip("Toggle side-by-side stereoscopic dual-camera view (Cardboard lenses)")]
    public bool enableStereoSimulator = false;
    [Tooltip("Interpupillary distance in meters (standard is ~0.064m / 64mm)")]
    public float interpupillaryDistance = 0.064f;
    [Tooltip("Hotkey to toggle stereo simulation in Editor / Play mode")]
    public Key toggleStereoKey = Key.C;

    [Header("Mobile Gyroscope Settings")]
    public bool enableMobileGyro = true;
    public float gyroSmoothFactor = 12f;

    [Header("On-Screen VR Testing HUD")]
    public bool showTestingHud = true;
    public Key toggleHudKey = Key.H;

    private Camera mainCamera;
    private Camera leftEyeCamera;
    private Camera rightEyeCamera;
    private GameObject stereoRigContainer;
    private bool isGyroSupported = false;
    private Quaternion gyroInitialRotation = Quaternion.identity;

    public static bool IsNativeXRActive()
    {
        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        foreach (var d in displays)
        {
            if (d.running) return true;
        }
        return XRSettings.isDeviceActive;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        FindMainCamera();

        // Enable gyro on mobile if available and native XR is not handling tracking
        if (SystemInfo.supportsGyroscope && enableMobileGyro && !IsNativeXRActive())
        {
            Input.gyro.enabled = true;
            isGyroSupported = true;
        }
    }

    void Start()
    {
        FindMainCamera();
        SetupStereoRig();

        // If native OpenXR / Quest is active, native headset displays handle stereoscopy
        if (IsNativeXRActive())
        {
            enableStereoSimulator = false;
        }

        UpdateStereoMode(enableStereoSimulator);
    }

    void FindMainCamera()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var camGo = GameObject.Find("Player Camera");
                if (camGo != null) mainCamera = camGo.GetComponent<Camera>();
            }
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            // Toggle Stereo Simulator (only when native OpenXR is not active)
            if (kb[toggleStereoKey].wasPressedThisFrame)
            {
                if (IsNativeXRActive())
                {
                    Debug.Log("[VR] Native OpenXR / Quest display is active; stereo rendering is managed natively by the headset.");
                }
                else
                {
                    enableStereoSimulator = !enableStereoSimulator;
                    UpdateStereoMode(enableStereoSimulator);
                    Debug.Log("[Cardboard] Stereo VR Simulator " + (enableStereoSimulator ? "ENABLED" : "DISABLED"));
                }
            }

            // Recenter view
            if (kb.rKey.wasPressedThisFrame)
            {
                Recenter();
            }

            // Toggle HUD
            if (kb[toggleHudKey].wasPressedThisFrame)
            {
                showTestingHud = !showTestingHud;
            }
        }

        // Keep stereo cameras synchronized with main camera
        if (enableStereoSimulator && leftEyeCamera != null && rightEyeCamera != null && mainCamera != null)
        {
            SyncStereoCameras();
        }

        // Apply mobile gyroscope head tracking if playing on device without XR plugin active
        if (isGyroSupported && Application.isMobilePlatform && !UnityEngine.XR.XRSettings.enabled)
        {
            ApplyMobileGyroRotation();
        }
    }

    void SetupStereoRig()
    {
        if (mainCamera == null) return;

        stereoRigContainer = new GameObject("CardboardStereoRig");
        stereoRigContainer.transform.SetParent(mainCamera.transform.parent, false);
        stereoRigContainer.transform.localPosition = mainCamera.transform.localPosition;
        stereoRigContainer.transform.localRotation = mainCamera.transform.localRotation;

        // 1. Left Eye Camera
        var leftGo = new GameObject("LeftEyeCamera");
        leftGo.transform.SetParent(stereoRigContainer.transform, false);
        leftEyeCamera = leftGo.AddComponent<Camera>();
        leftEyeCamera.CopyFrom(mainCamera);
        leftEyeCamera.rect = new Rect(0f, 0f, 0.5f, 1f);
        // Remove AudioListener from stereo eyes
        var al = leftGo.GetComponent<AudioListener>();
        if (al != null) Destroy(al);

        // 2. Right Eye Camera
        var rightGo = new GameObject("RightEyeCamera");
        rightGo.transform.SetParent(stereoRigContainer.transform, false);
        rightEyeCamera = rightGo.AddComponent<Camera>();
        rightEyeCamera.CopyFrom(mainCamera);
        rightEyeCamera.rect = new Rect(0.5f, 0f, 0.5f, 1f);
        var ar = rightGo.GetComponent<AudioListener>();
        if (ar != null) Destroy(ar);

        stereoRigContainer.SetActive(false);
    }

    void SyncStereoCameras()
    {
        if (stereoRigContainer == null || mainCamera == null) return;

        stereoRigContainer.transform.position = mainCamera.transform.position;
        stereoRigContainer.transform.rotation = mainCamera.transform.rotation;

        Vector3 halfIpd = mainCamera.transform.right * (interpupillaryDistance * 0.5f);
        if (leftEyeCamera != null)
        {
            leftEyeCamera.transform.position = mainCamera.transform.position - halfIpd;
            leftEyeCamera.transform.rotation = mainCamera.transform.rotation;
            leftEyeCamera.fieldOfView = mainCamera.fieldOfView;
        }
        if (rightEyeCamera != null)
        {
            rightEyeCamera.transform.position = mainCamera.transform.position + halfIpd;
            rightEyeCamera.transform.rotation = mainCamera.transform.rotation;
            rightEyeCamera.fieldOfView = mainCamera.fieldOfView;
        }
    }

    public void UpdateStereoMode(bool stereoActive)
    {
        if (stereoRigContainer != null)
        {
            stereoRigContainer.SetActive(stereoActive);
        }

        if (mainCamera != null)
        {
            // If stereo simulator is active, hide main camera render to prevent duplicate rendering
            mainCamera.enabled = !stereoActive;
        }
    }

    public bool IsTriggerPressedThisFrame()
    {
        // 1. Official Google Cardboard XR Plugin Trigger
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        try
        {
            if (Google.XR.Cardboard.Api.IsTriggerPressed) return true;
        }
        catch { }
#endif

        // 2. Mobile Touchscreen Tap (Cardboard conductive trigger taps screen)
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            return true;
        }

        // 3. Mouse Left Click (Editor simulation)
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        // 4. Keyboard Space / Enter (Accessibility & testing)
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
        {
            return true;
        }

        // 5. OpenXR / Meta Quest Controller Trigger & Primary Action Buttons
        var xrControllers = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
            UnityEngine.XR.InputDeviceCharacteristics.HeldInHand | UnityEngine.XR.InputDeviceCharacteristics.Controller, 
            xrControllers);
        foreach (var dev in xrControllers)
        {
            if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool triggerDown) && triggerDown)
            {
                return true;
            }
            if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool primaryDown) && primaryDown)
            {
                return true;
            }
        }

        return false;
    }

    public void Recenter()
    {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        try
        {
            Google.XR.Cardboard.Api.Recenter();
        }
        catch { }
#endif
        if (isGyroSupported)
        {
            gyroInitialRotation = Input.gyro.attitude;
        }
        Debug.Log("[Cardboard] Head orientation recentered.");
    }

    public void CalibrateVR()
    {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        try
        {
            Google.XR.Cardboard.Api.ScanDeviceParams();
        }
        catch { }
#endif
        Debug.Log("[Cardboard] ScanDeviceParams requested for Cardboard profile calibration.");
    }

    private void ApplyMobileGyroRotation()
    {
        Quaternion gyroAttitude = Input.gyro.attitude;
        // Convert phone gyroscope coordinate system to Unity
        Quaternion rot = new Quaternion(gyroAttitude.x, gyroAttitude.y, -gyroAttitude.z, -gyroAttitude.w);
        rot = Quaternion.Euler(90f, 0f, 0f) * rot;

        if (mainCamera != null)
        {
            mainCamera.transform.localRotation = Quaternion.Slerp(mainCamera.transform.localRotation, rot, Time.deltaTime * gyroSmoothFactor);
        }
    }

    void OnGUI()
    {
        if (!showTestingHud) return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.alignment = TextAnchor.MiddleLeft;
        style.fontSize = 12;
        style.normal.textColor = Color.white;

        if (IsNativeXRActive())
        {
            string xrText = $"<b>[OPENXR / META QUEST ACTIVE]</b>\n" +
                            $"• Native Head-Tracking & Stereoscopic Displays Active\n" +
                            $"• Controller Trigger or Gaze Dwell (1.5s): Select / Interact with Teacher & Chalkboard\n" +
                            $"• Controller Trigger / Click on Ground: Toss Breadcrumbs for Birds  |  <b>[H]</b>: Toggle HUD";
            GUI.Box(new Rect(10, 10, 600, 65), xrText, style);
        }
        else
        {
            string mode = enableStereoSimulator ? "<color=#A7FFEB>STEREOSCOPIC (Split 3D)</color>" : "<color=#FFE082>MONOSCOPIC (Single)</color>";
            string text = $"<b>[GOOGLE CARDBOARD READY / SIMULATOR]</b>  Mode: {mode}\n" +
                          $"• <b>Mouse</b>: Look around  |  <b>[C]</b>: Toggle Stereo Dual-Eye  |  <b>[R]</b>: Recenter\n" +
                          $"• <b>Gaze Dwell (1.5s)</b> or <b>Click/Tap</b>: Interact with Teacher & Buttons\n" +
                          $"• <b>Click/Tap</b> (on open ground): Toss Breadcrumb for Birds  |  <b>[H]</b>: Toggle Help";
            GUI.Box(new Rect(10, 10, 560, 70), text, style);
        }
    }
}
