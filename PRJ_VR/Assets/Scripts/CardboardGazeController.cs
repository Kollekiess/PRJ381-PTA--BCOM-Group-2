using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class CardboardGazeController : MonoBehaviour
{
    [Header("Gaze Settings")]
    public float maxGazeDistance = 30.0f;
    public float gazeHoldTime = 1.5f;

    [Header("Reticle Visuals")]
    public Image gazeProgressRing;
    public Image gazeCenterDot;
    public Canvas reticleCanvas;
    public float defaultDistance = 1.6f;
    public Color normalColor = new Color(1f, 1f, 1f, 0.65f);
    public Color hoverColor = new Color(0.2f, 0.9f, 1f, 0.95f);
    public Color triggerColor = new Color(0.3f, 1f, 0.4f, 1f);

    [Header("Optional Crumb Spawner Trigger")]
    public BreadcrumbSpawner breadcrumbSpawner;

    private float timer = 0f;
    private GameObject currentGazeObject;
    private ICardboardInteractable currentInteractable;
    private Transform cameraTransform;

    void Awake()
    {
        EnsureCameraReference();
        EnsureReticleSetup();
    }

    void Start()
    {
        EnsureCameraReference();
        if (breadcrumbSpawner == null)
        {
            breadcrumbSpawner = FindFirstObjectByType<BreadcrumbSpawner>();
        }
        ResetGaze();
    }

    void EnsureCameraReference()
    {
        if (cameraTransform == null)
        {
            if (Camera.main != null) cameraTransform = Camera.main.transform;
            else
            {
                var camGo = GameObject.Find("Player Camera");
                if (camGo != null) cameraTransform = camGo.transform;
            }
        }
    }

    void EnsureReticleSetup()
    {
        EnsureCameraReference();
        if (cameraTransform == null) return;

        var existingCanvas = cameraTransform.Find("CardboardReticleCanvas");
        if (existingCanvas != null)
        {
            reticleCanvas = existingCanvas.GetComponent<Canvas>();
            gazeProgressRing = existingCanvas.Find("ProgressRing")?.GetComponent<Image>();
            gazeCenterDot = existingCanvas.Find("CenterDot")?.GetComponent<Image>();
            return;
        }

        GameObject canvasGo = new GameObject("CardboardReticleCanvas");
        canvasGo.transform.SetParent(cameraTransform, false);
        canvasGo.transform.localPosition = new Vector3(0f, 0f, defaultDistance);
        canvasGo.transform.localRotation = Quaternion.identity;
        canvasGo.transform.localScale = Vector3.one * 0.0018f;

        reticleCanvas = canvasGo.AddComponent<Canvas>();
        reticleCanvas.renderMode = RenderMode.WorldSpace;
        canvasGo.AddComponent<CanvasScaler>();

        GameObject dotGo = new GameObject("CenterDot");
        dotGo.transform.SetParent(canvasGo.transform, false);
        var dotRt = dotGo.AddComponent<RectTransform>();
        dotRt.sizeDelta = new Vector2(10f, 10f);
        gazeCenterDot = dotGo.AddComponent<Image>();
        gazeCenterDot.color = normalColor;

        GameObject ringGo = new GameObject("ProgressRing");
        ringGo.transform.SetParent(canvasGo.transform, false);
        var ringRt = ringGo.AddComponent<RectTransform>();
        ringRt.sizeDelta = new Vector2(36f, 36f);
        gazeProgressRing = ringGo.AddComponent<Image>();
        gazeProgressRing.type = Image.Type.Filled;
        gazeProgressRing.fillMethod = Image.FillMethod.Radial360;
        gazeProgressRing.fillOrigin = (int)Image.Origin360.Top;
        gazeProgressRing.fillClockwise = true;
        gazeProgressRing.fillAmount = 0f;
        gazeProgressRing.color = hoverColor;
    }

    void Update()
    {
        EnsureCameraReference();
        HandleGazeRaycast();
        HandleTrigger();
    }

    void HandleGazeRaycast()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        RaycastHit hit;

        GameObject hitObject = DetectInteractableTarget(ray, out hit);

        if (hitObject != null)
        {
            if (currentGazeObject != hitObject)
            {
                if (currentInteractable != null) currentInteractable.OnGazeExit();

                currentGazeObject = hitObject;
                currentInteractable = currentGazeObject.GetComponent<ICardboardInteractable>();
                if (currentInteractable == null) currentInteractable = currentGazeObject.GetComponentInParent<ICardboardInteractable>();

                if (currentInteractable != null) currentInteractable.OnGazeEnter();
                timer = 0f;
            }

            timer += Time.deltaTime;

            if (gazeProgressRing != null)
            {
                gazeProgressRing.color = hoverColor;
                gazeProgressRing.fillAmount = Mathf.Clamp01(timer / gazeHoldTime);
            }

            if (timer >= gazeHoldTime)
            {
                if (gazeProgressRing != null) gazeProgressRing.color = triggerColor;
                ExecuteClick(currentGazeObject);
                ResetGaze();
            }
        }
        else
        {
            ResetGaze();
        }
    }

    void HandleTrigger()
    {
        bool isTriggered = false;

        if (CardboardManager.Instance != null)
        {
            isTriggered = CardboardManager.Instance.IsTriggerPressedThisFrame();
        }
        else
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) isTriggered = true;
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) isTriggered = true;
        }

        if (isTriggered)
        {
            if (currentGazeObject != null)
            {
                ExecuteClick(currentGazeObject);
                ResetGaze();
            }
            else
            {
                if (breadcrumbSpawner != null)
                {
                    breadcrumbSpawner.TossBreadcrumb();
                }
            }
        }
    }

    GameObject DetectInteractableTarget(Ray ray, out RaycastHit hit)
    {
        hit = default;

        // 1. UI Buttons
        if (EventSystem.current != null)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (var result in results)
            {
                if (result.gameObject == null) continue;
                if (reticleCanvas != null && result.gameObject.transform.IsChildOf(reticleCanvas.transform)) continue;

                if (result.gameObject.GetComponent<Button>() != null || result.gameObject.GetComponentInParent<Button>() != null)
                {
                    hit = new RaycastHit();
                    return result.gameObject;
                }
            }
        }

        // 2. Physics 3D Colliders
        if (Physics.Raycast(ray, out hit, maxGazeDistance))
        {
            GameObject obj = hit.collider.gameObject;

            if (obj.GetComponent<ICardboardInteractable>() != null || 
                obj.GetComponentInParent<ICardboardInteractable>() != null ||
                obj.GetComponent<TeacherInteraction>() != null ||
                obj.GetComponentInParent<TeacherInteraction>() != null ||
                
                obj.name.Contains("Button") ||
                false)
            {
                return obj;
            }
        }

        return null;
    }

    void ExecuteClick(GameObject target)
    {
        if (target == null) return;

        // UI Button
        Button button = target.GetComponent<Button>() ?? target.GetComponentInParent<Button>();
        if (button != null && button.interactable)
        {
            button.onClick.Invoke();
            Debug.Log("[Cardboard Gaze] Clicked UI Button: " + button.gameObject.name);
            return;
        }

        // ICardboardInteractable
        var interactable = target.GetComponent<ICardboardInteractable>() ?? target.GetComponentInParent<ICardboardInteractable>();
        if (interactable != null)
        {
            interactable.OnGazeTrigger();
            Debug.Log("[Cardboard Gaze] Triggered ICardboardInteractable on: " + target.name);
            return;
        }

        target.SendMessage("OnGazeTrigger", SendMessageOptions.DontRequireReceiver);
    }

    void ResetGaze()
    {
        if (currentInteractable != null)
        {
            currentInteractable.OnGazeExit();
            currentInteractable = null;
        }

        currentGazeObject = null;
        timer = 0f;

        if (gazeProgressRing != null)
        {
            gazeProgressRing.fillAmount = 0f;
            gazeProgressRing.color = normalColor;
        }
    }
}