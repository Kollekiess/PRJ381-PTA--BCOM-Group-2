using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

public class GameManager : MonoBehaviour
{
   [Header("UI Panels")]
   public GameObject menuPanel;
   public GameObject exitConfirmationPanel;

   [Header("UI Elements")]
   public TextMeshProUGUI levelNumberText;

   [Header("Settings")]
   public KeyCode toggleMenuKey = KeyCode.Escape;

   //Menu Boolean
   private bool isMenuOpen = false;

   void Start()
   {
    // Ensure panels start in correct state
    if (menuPanel != null) menuPanel.SetActive(false);
    if (exitConfirmationPanel != null) exitConfirmationPanel.SetActive(false);

    // Display current scene/level info
    UpdateLevelDisplay();
   }

   void Update()
   {
    // Toggle menu on Escape key press (New Input System)
    if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
    {
        ToggleMenu();
    }
   }
   
   
   // Update level display with current scene name
   void UpdateLevelDisplay()
   {
        if (levelNumberText != null)
        {
            string currentSceneName = SceneManager.GetActiveScene().name;
            // You can customize this format to show "Level 1", "Level 2", etc.
            levelNumberText.text = "Level: " + currentSceneName;
        }
   }

   public void ToggleMenu()
   {
    isMenuOpen = !isMenuOpen;
    // Toggle main panel visibility
    if (menuPanel != null) menuPanel.SetActive(isMenuOpen);

    // Always hide the exit confirmation popup when toggling menu
    if (exitConfirmationPanel != null) exitConfirmationPanel.SetActive(false);

    // Update mouse cursor state and pause/resume game time
    Cursor.lockState = isMenuOpen ? CursorLockMode.None : CursorLockMode.Locked;
    Cursor.visible = isMenuOpen;
    Time.timeScale = isMenuOpen ? 0f : 1f;

   }

    // --- BUTTON CALLBACKS ---

    // 1. Restart Current Level/Scene
    public void OnRestartButtonClicked()
    {
        Time.timeScale = 1f; // Reset time scale before reloading
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentSceneIndex);
    }

    // 2. Calibrate (Placeholder)
    public void OnCalibrateButtonClicked()
    {
        Debug.Log("Calibrate clicked - Ready for calibration logic.");
    }

    // 3. Open Exit Confirmation Popup
    public void OnExitButtonClicked()
    {
        if (exitConfirmationPanel != null)
        {
            exitConfirmationPanel.SetActive(true);
            menuPanel.SetActive(false);
        }
    }

    // 4. Confirm Exit (Close Game)
    public void OnConfirmExit()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    // 5. Cancel Exit Popup
    public void OnCancelExit()
    {
        if (exitConfirmationPanel != null)
        {
            exitConfirmationPanel.SetActive(false);
            menuPanel.SetActive(true);
        }
    }
}
