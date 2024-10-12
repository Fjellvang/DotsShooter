using DotsShooter;
using DotsShooter.Events;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenuUI;   // Reference to the Pause Menu UI Panel
    public Button quitButton;        // Reference to the Quit Button
    public Button continueButton;        // Reference to the Quit Button
    private bool ShowPauseMenu = false;

    void Start()
    {
        // Initially, hide the pause menu
        pauseMenuUI.SetActive(false);

        // Add listener to buttons
        continueButton.onClick.AddListener(ContinueGame);
        quitButton.onClick.AddListener(QuitGame);
    }


    private void OnEnable()
    {
        var eventSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<EventSystem>(); 
        eventSystem.OnTogglePause += ToggleTogglePauseMenu;
    }
    
    private void ContinueGame()
    {
        ToggleTogglePauseMenu();
    }

    public void ToggleTogglePauseMenu()
    {
        ShowPauseMenu = !ShowPauseMenu;
        pauseMenuUI.SetActive(ShowPauseMenu);   // Hide the pause menu
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