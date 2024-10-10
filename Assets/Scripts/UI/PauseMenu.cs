using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenuUI;   // Reference to the Pause Menu UI Panel
    public Button continueButton;    // Reference to the Continue Button
    public Button quitButton;        // Reference to the Quit Button
    private bool isPaused = false;   // State to track if the game is paused

    void Start()
    {
        // Initially, hide the pause menu
        pauseMenuUI.SetActive(false);

        // Add listener to buttons
        continueButton.onClick.AddListener(ResumeGame);
        quitButton.onClick.AddListener(QuitGame);
    }

    void Update()
    {
        // Check if Esc key is pressed
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    // Function to resume the game
    public void ResumeGame()
    {
        pauseMenuUI.SetActive(false);   // Hide the pause menu
        Time.timeScale = 1f;            // Resume time
        isPaused = false;
    }

    // Function to pause the game
    void PauseGame()
    {
        pauseMenuUI.SetActive(true);    // Show the pause menu
        Time.timeScale = 0f;            // Stop time
        isPaused = true;
    }

    // Function to quit the game
    public void QuitGame()
    {
        // If running in the editor, stop playing
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Quit the application
        Application.Quit();
#endif
    }
}