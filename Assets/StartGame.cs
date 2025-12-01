using UnityEngine;
using UnityEngine.UI; // Required for Button interaction

public class PauseManager : MonoBehaviour
{
    public GameObject pauseMenuUI; // Assign your UI Panel for the pause menu in the Inspector
    public Button resumeButton; // Assign your resume button in the Inspector

    void Start()
    {
        // Initially pause the game and show the pause menu
        PauseGame();
    }

    public void PauseGame()
    {
        Time.timeScale = 0f; // Halts all time-based operations
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(true); // Show the pause menu UI
        }
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f; // Resumes time-based operations
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false); // Hide the pause menu UI
        }
    }
}