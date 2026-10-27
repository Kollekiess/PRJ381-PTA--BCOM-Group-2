using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

/// <summary>
/// Displays trained static-letter recognition feedback from the laptop webcam bridge.
/// The group's classifier is not yet independently validated for SASL teaching.
/// </summary>
public class WebcamHandBridge : MonoBehaviour
{
    [Serializable]
    private class BridgeState
    {
        public bool handDetected;
        public string handedness;
        public string targetLabel;
        public string predictedLabel;
        public float confidence;
        public float holdProgress;
        public bool matched;
        public string feedback;
        public float[] landmarks;
    }

    [Serializable]
    private class BridgeBootstrap
    {
        public string token;
    }

    [Header("Webcam bridge")]
    [Tooltip("For a USB-connected Quest, keep localhost and run adb reverse tcp:8765 tcp:8765 on the Mac. For Wi-Fi, set the Mac LAN URL and token before building.")]
    [SerializeField] private string bridgeBaseUrl = "http://127.0.0.1:8765";
    [Tooltip("Temporary token printed by webcam_bridge.py when it starts.")]
    [SerializeField] private string bridgeToken = "";
    [SerializeField, Min(0.1f)] private float pollIntervalSeconds = 0.25f;
    [SerializeField] private TextMeshProUGUI feedbackText;

    private string normalizedBaseUrl;
    private bool requestInFlight;

    private void Start()
    {
        normalizedBaseUrl = (bridgeBaseUrl ?? "").Trim().TrimEnd('/');
        if (feedbackText == null)
            feedbackText = CreateOverlay();
        if (feedbackText != null)
            feedbackText.text = "Webcam hand bridge\nStarting…";
        StartCoroutine(PollBridge());
    }

    private TextMeshProUGUI CreateOverlay()
    {
        var canvasObject = new GameObject("Webcam Hand Tracking Feedback");
        var canvas = canvasObject.AddComponent<Canvas>();
        var camera = Camera.main;
        canvas.renderMode = camera != null ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();
        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);
        if (camera != null)
        {
            canvasObject.transform.SetParent(camera.transform, false);
            canvasObject.transform.localPosition = new Vector3(0f, 0f, 1.4f);
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * 0.001f;
        }

        var panelObject = new GameObject("Feedback Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);
        var panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(60f, -60f);
        panelRect.sizeDelta = new Vector2(650f, 210f);
        panelObject.GetComponent<Image>().color = new Color(0.03f, 0.08f, 0.12f, 0.9f);

        var labelObject = new GameObject("Feedback Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(panelObject.transform, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(18f, 14f);
        labelRect.offsetMax = new Vector2(-18f, -14f);
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = 28f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.TopLeft;
        return label;
    }

    private IEnumerator PollBridge()
    {
        if (string.IsNullOrWhiteSpace(bridgeToken))
            yield return FetchLocalBridgeToken();

        var wait = new WaitForSecondsRealtime(Mathf.Max(0.1f, pollIntervalSeconds));
        while (true)
        {
            if (!requestInFlight)
            {
                requestInFlight = true;
                using (var request = UnityWebRequest.Get(normalizedBaseUrl + "/api/state"))
                {
                    request.timeout = 3;
                    if (!string.IsNullOrWhiteSpace(bridgeToken))
                        request.SetRequestHeader("X-Bridge-Token", bridgeToken.Trim());
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        BridgeState state = null;
                        try { state = JsonUtility.FromJson<BridgeState>(request.downloadHandler.text); }
                        catch (Exception exception) { Debug.LogWarning("Invalid webcam bridge response: " + exception.Message); }
                        if (state != null)
                            ShowState(state);
                    }
                    else
                    {
                        ShowMessage("Webcam bridge offline\n" + request.error +
                            "\nCheck the laptop URL, token, and network.");
                    }
                }
                requestInFlight = false;
            }
            yield return wait;
        }
    }

    private IEnumerator FetchLocalBridgeToken()
    {
        using (var request = UnityWebRequest.Get(normalizedBaseUrl + "/api/bootstrap"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var bootstrap = JsonUtility.FromJson<BridgeBootstrap>(request.downloadHandler.text);
                    if (bootstrap != null && !string.IsNullOrWhiteSpace(bootstrap.token))
                        bridgeToken = bootstrap.token.Trim();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Invalid webcam bridge bootstrap response: " + exception.Message);
                }
            }
        }
    }

    private void ShowState(BridgeState state)
    {
        string label = string.IsNullOrEmpty(state.targetLabel) ? "Reference not set" : state.targetLabel;
        string hand = state.handDetected ? state.handedness + " hand detected" : "Show one hand to the webcam";
        string score = state.handDetected
            ? "\nRecognized: " + (string.IsNullOrEmpty(state.predictedLabel) ? "?" : state.predictedLabel) +
              " (" + Mathf.RoundToInt(state.confidence * 100f) + "%)" +
              "\nHold: " + Mathf.RoundToInt(state.holdProgress * 100f) + "%"
            : "";
        ShowMessage(label + "\n" + hand + score + "\n" + state.feedback);
    }

    private void ShowMessage(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }
}
