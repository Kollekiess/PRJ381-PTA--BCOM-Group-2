using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class CardboardGazeController : MonoBehaviour
{
    [Header("Gaze Settings")]
    public float maxGazeDistance = 20.0f;
    public float gazeHoldTime = 2.0f; // Time in seconds to hold gaze before clicking

    [Header("UI Reticle")]
    public Image gazeImage; // Radial Filled Image centered on screen

    private float timer = 0f;
    private GameObject currentGazeObject;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        if(gazeImage != null) gazeImage.fillAmount = 0f;
    }

    void Update()
    {
        HandleGazeRaycast();
        HandleScreenTap();
    }

    void HandleGazeRaycast()
    {
        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        RaycastHit hit;

        //Checking for 3D colliders or UI Elements
        GameObject hitObject = DetectGazeTarget(ray, out hit);

        if(hitObject != null)
        {
            //If gazing at an object reset the timer
            if(currentGazeObject != hitObject)
            {
                currentGazeObject = hitObject;
                timer = 0f;
            }

            //Incrament Timer
            timer += Time.deltaTime;

            //Checking for UI Gaze
            if(gazeImage != null)  gazeImage.fillAmount = Mathf.Clamp01(timer / gazeHoldTime);

            //Trigger click when timer completes
            if(timer >= gazeHoldTime)
            {
                ExecuteClick(currentGazeObject);
                ResetGaze();
            }
        }
        else
        {
            ResetGaze();
        }
    }

    void HandleScreenTap()
    {
        bool isTapped = false;

        //Checking Touch Screen or Mouse Click
        if(Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            isTapped = true;
        }
        else if(Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            isTapped = true;
        }

        if(isTapped && currentGazeObject != null)
        {
            ExecuteClick(currentGazeObject);
            ResetGaze();
        }
    }

    GameObject DetectGazeTarget(Ray ray, out RaycastHit hit)
    {
        hit = default;
        
        //Checking for UI that uses the graphic raycaster
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = new Vector2(Screen.width / 2f, Screen.height / 2f);

        List<RaycastResult> results = new List<RaycastResult>();

        if(EventSystem.current != null)
        {
            EventSystem.current.RaycastAll(pointerData, results);

            foreach(RaycastResult result in results)
            {
                //Check for interaction capability
                if(result.gameObject.GetComponent<Button>() != null || result.gameObject.GetComponentInParent<Button>() != null)
                {
                    hit = new RaycastHit(); // Placeholder for UI hits
                    return result.gameObject;
                }
            }
        }

        //Checking for 3D physics objects
        if(Physics.Raycast(ray, out hit, maxGazeDistance)) return hit.collider.gameObject;

        return null;
    }

    void ExecuteClick(GameObject target)
    {
        //Trigger UI Button click event
        Button button = target.GetComponent<Button>();

        if(button == null) button = target.GetComponentInParent<Button>();

        if(button != null && button.interactable)
        {
            button.onClick.Invoke();
            Debug.Log("Gaze Triggered UI Button: " + button.gameObject.name);
        }
        else
        {
            // Alternative: Send a custom message to 3D objects
            target.SendMessage("OnGazeTrigger", SendMessageOptions.DontRequireReceiver);
        }
    }

    void ResetGaze()
    {
        currentGazeObject = null;
        timer = 0f;
        if(gazeImage != null) gazeImage.fillAmount = 0f;
    }
}