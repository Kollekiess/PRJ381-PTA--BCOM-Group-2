using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class ResponsiveUIConfigurator : MonoBehaviour
{
    [Header("Reference Resolution")]
    public Vector2 targetReferenceResolution = new Vector2(1920f, 1080f);
    public float targetMatchFactor = 0.5f;

    [Header("Panels Sizing")]
    public Vector2 mainMenuSize = new Vector2(480f, 580f);
    public Vector2 exitConfirmationSize = new Vector2(560f, 280f);

    void Awake()
    {
        ApplyResponsiveSettings();
    }

    void OnValidate()
    {
        ApplyResponsiveSettings();
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Apply Responsive UI Scaling")]
    public static void ApplyToScene()
    {
        var config = FindFirstObjectByType<ResponsiveUIConfigurator>();
        if (config == null)
        {
            var canvas = GameObject.Find("MenuCanvas");
            if (canvas != null)
            {
                config = canvas.AddComponent<ResponsiveUIConfigurator>();
            }
        }
        if (config != null)
        {
            config.ApplyResponsiveSettings();
            EditorUtility.SetDirty(config.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(config.gameObject.scene);
            Debug.Log("[ResponsiveUI] Successfully applied responsive UI settings to MenuCanvas!");
        }
    }
#endif

    public void ApplyResponsiveSettings()
    {
        Canvas canvas = GetComponent<Canvas>() ?? GameObject.Find("MenuCanvas")?.GetComponent<Canvas>();
        if (canvas == null) return;

        // 1. Configure CanvasScaler for multi-resolution scaling
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = targetReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = targetMatchFactor;
        scaler.referencePixelsPerUnit = 100f;

        // 2. Configure MainMenuPanel
        Transform mainPanelT = canvas.transform.Find("MainMenuPanel");
        if (mainPanelT != null)
        {
            RectTransform rt = mainPanelT.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = mainMenuSize;
            }

            // Title
            Transform titleT = mainPanelT.Find("Text");
            if (titleT != null)
            {
                RectTransform trt = titleT.GetComponent<RectTransform>();
                trt.anchorMin = new Vector2(0.5f, 0.5f);
                trt.anchorMax = new Vector2(0.5f, 0.5f);
                trt.pivot = new Vector2(0.5f, 0.5f);
                trt.anchoredPosition = new Vector2(0f, 205f);
                trt.sizeDelta = new Vector2(400f, 50f);

                TextMeshProUGUI tmp = titleT.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.fontSize = 32f;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 22f;
                    tmp.fontSizeMax = 36f;
                }
            }

            // Setup buttons
            ConfigureButton(mainPanelT, "Restart Button", new Vector2(0f, 115f), new Vector2(320f, 54f), "Restart Current Level");
            ConfigureButton(mainPanelT, "Calibrate Button", new Vector2(0f, 45f), new Vector2(320f, 54f), "Calibrate VR");
            ConfigureButton(mainPanelT, "Close Menu Button", new Vector2(0f, -25f), new Vector2(320f, 54f), "Close Menu");
            ConfigureButton(mainPanelT, "Exit Button", new Vector2(0f, -95f), new Vector2(320f, 54f), "Exit Game");

            // Level Text
            Transform levelT = mainPanelT.Find("Level Text");
            if (levelT != null)
            {
                RectTransform lrt = levelT.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0.5f, 0.5f);
                lrt.anchorMax = new Vector2(0.5f, 0.5f);
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.anchoredPosition = new Vector2(0f, -185f);
                lrt.sizeDelta = new Vector2(380f, 40f);

                TextMeshProUGUI ltmp = levelT.GetComponent<TextMeshProUGUI>();
                if (ltmp != null)
                {
                    ltmp.fontSize = 18f;
                    ltmp.alignment = TextAlignmentOptions.Center;
                    ltmp.enableAutoSizing = true;
                    ltmp.fontSizeMin = 14f;
                    ltmp.fontSizeMax = 20f;
                }
            }
        }

        // 3. Configure ExitConfirmationPanel
        Transform exitPanelT = canvas.transform.Find("ExitConfirmationPanel");
        if (exitPanelT != null)
        {
            RectTransform rt = exitPanelT.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = exitConfirmationSize;
            }

            Transform promptT = exitPanelT.Find("Text");
            if (promptT != null)
            {
                RectTransform prt = promptT.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0.5f, 0.5f);
                prt.anchorMax = new Vector2(0.5f, 0.5f);
                prt.pivot = new Vector2(0.5f, 0.5f);
                prt.anchoredPosition = new Vector2(0f, 48f);
                prt.sizeDelta = new Vector2(500f, 90f);

                TextMeshProUGUI ptmp = promptT.GetComponent<TextMeshProUGUI>();
                if (ptmp != null)
                {
                    ptmp.text = "<b><size=120%>Exit Game?</size></b>\n\n<size=80%><color=#CFD8DC>Do you want to continue to exit or remain in the game?</color></size>";
                    ptmp.fontSize = 24f;
                    ptmp.fontStyle = FontStyles.Normal;
                    ptmp.alignment = TextAlignmentOptions.Center;
                    ptmp.enableAutoSizing = true;
                    ptmp.fontSizeMin = 16f;
                    ptmp.fontSizeMax = 28f;
                    ptmp.raycastTarget = false;
                }
            }

            ConfigureButton(exitPanelT, "Confirm Button", new Vector2(-125f, -48f), new Vector2(210f, 54f), "Continue to Exit");
            ConfigureButton(exitPanelT, "Cancel Button", new Vector2(125f, -48f), new Vector2(210f, 54f), "Remain in Game");
        }
    }

    private void ConfigureButton(Transform parent, string buttonName, Vector2 pos, Vector2 size, string textLabel)
    {
        Transform btnT = parent.Find(buttonName);
        if (btnT == null) return;

        RectTransform brt = btnT.GetComponent<RectTransform>();
        if (brt != null)
        {
            brt.anchorMin = new Vector2(0.5f, 0.5f);
            brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = pos;
            brt.sizeDelta = size;
        }

        Transform textChild = btnT.Find("Text (TMP)");
        if (textChild != null)
        {
            RectTransform textRt = textChild.GetComponent<RectTransform>();
            if (textRt != null)
            {
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = Vector2.zero;
                textRt.offsetMax = Vector2.zero;
            }

            TextMeshProUGUI btmp = textChild.GetComponent<TextMeshProUGUI>();
            if (btmp != null)
            {
                if (!string.IsNullOrEmpty(textLabel)) btmp.text = textLabel;
                btmp.fontSize = 18f;
                btmp.fontStyle = FontStyles.Bold;
                btmp.alignment = TextAlignmentOptions.Center;
                btmp.enableAutoSizing = true;
                btmp.fontSizeMin = 13f;
                btmp.fontSizeMax = 20f;
                btmp.raycastTarget = false; // Never block parent button clicks
            }
        }
    }
}
