using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Controls the pause/main menu, level restart, VR calibration,
/// and exit confirmation popup workflow.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject menuPanel;
    public GameObject exitConfirmationPanel;

    [Header("UI Elements")]
    public TextMeshProUGUI levelNumberText;

    // Menu State
    private bool isMenuOpen = false;
    public bool IsMenuOpen => isMenuOpen;

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
        // Ensure panels start in correct hidden state
        if (menuPanel != null) menuPanel.SetActive(false);
        if (exitConfirmationPanel != null) exitConfirmationPanel.SetActive(false);

        // Display current scene/level info
        UpdateLevelDisplay();
    }

    private void Update()
    {
        // Toggle menu on Escape key press (New Input System)
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // If in exit confirmation, Esc returns to main menu panel
            if (exitConfirmationPanel != null && exitConfirmationPanel.activeSelf)
            {
                OnCancelExit();
            }
            else
            {
                ToggleMenu();
            }
        }
    }

    // Update level display with current scene name
    void UpdateLevelDisplay()
    {
        if (levelNumberText != null)
        {
            string currentSceneName = SceneManager.GetActiveScene().name;
            levelNumberText.text = "Active Scene: " + currentSceneName;
        }
    }

    public void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;

        if (menuPanel != null) menuPanel.SetActive(isMenuOpen);
        if (exitConfirmationPanel != null) exitConfirmationPanel.SetActive(false);

        // Update mouse cursor state and pause/resume game time
        ApplyCursorAndPauseState(isMenuOpen);
    }

    public void CloseMenu()
    {
        isMenuOpen = false;
        if (menuPanel != null) menuPanel.SetActive(false);
        if (exitConfirmationPanel != null) exitConfirmationPanel.SetActive(false);

        ApplyCursorAndPauseState(false);
    }

    private void ApplyCursorAndPauseState(bool menuActive)
    {
        Cursor.lockState = menuActive ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = menuActive;
        Time.timeScale = menuActive ? 0f : 1f;
    }

    // --- BUTTON CALLBACKS ---

    // 1. Restart Current Level/Scene
    public void OnRestartButtonClicked()
    {
        Time.timeScale = 1f; // Reset time scale before reloading
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentSceneIndex);
    }

    // 2. Calibrate VR
    public void OnCalibrateButtonClicked()
    {
        Debug.Log("[GameManager] Calibrate VR clicked.");
        if (ScoreHUD.Instance != null)
        {
            ScoreHUD.Instance.ShowToast("VR Position & Recenter Calibrated!");
        }
#if !UNITY_EDITOR
        UnityEngine.XR.InputTracking.Recenter();
#endif
    }

    // 3. Open Exit Confirmation Popup
    public void OnExitButtonClicked()
    {
        if (exitConfirmationPanel != null)
        {
            exitConfirmationPanel.SetActive(true);
            if (menuPanel != null) menuPanel.SetActive(false);
        }
    }

    // 4. Confirm Exit (Continue to Exit / Close Game)
    public void OnConfirmExit()
    {
        Debug.Log("[GameManager] Confirm exit chosen. Quitting game...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // 5. Cancel Exit Popup (Remain in Game)
    public void OnCancelExit()
    {
        if (exitConfirmationPanel != null) exitConfirmationPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
    }
}
