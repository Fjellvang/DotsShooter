using System;
using DotsShooter.Events;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;


public class MenuPresenter : MonoBehaviour
{
    [Tooltip("Reference to the View (UI)")] [SerializeField]
    private UIDocument m_Document;
    // Root element of the UI
    private VisualElement _root;

    private CustomButton _continueButton;
    private CustomButton _quitButton;
    private CustomButton _retryButton;
    
    private VisualElement _pauseContainer;
    private VisualElement _gameOverOverlay;
    
    // TODO: Consider abstracting the event system access
    private EventSystem EventSystem => World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<EventSystem>();
    
    private void OnEnable()
    {
        _root = m_Document.rootVisualElement;

        // Find and assign our UI elements
        _continueButton = _root.Q<CustomButton>("continue-button");
        _quitButton = _root.Q<CustomButton>("quit-button");
        _pauseContainer = _root.Q<VisualElement>("pause-container");
        _gameOverOverlay = _root.Q<VisualElement>("game-over-overlay");
        _retryButton = _gameOverOverlay.Q<CustomButton>("retry-button");

        _continueButton.Button.clicked += ResumeGame;
        _quitButton.Button.clicked += QuitGame;
        _retryButton.Button.clicked += RestartGame;
        EventSystem.OnTogglePause += ToggleMenu;
        EventSystem.OnPlayerDied += ShowGameOver;
    }

    private void ShowGameOver(float3 _) // TODO: We should abstract some event args..
    {
        _gameOverOverlay.RemoveFromClassList("game-over-overlay--hidden");
    }

    private void OnDisable()
    {
        if (!World.DefaultGameObjectInjectionWorld?.IsCreated ?? true) return;
        EventSystem.OnTogglePause -= ToggleMenu;
        EventSystem.OnPlayerDied -= ShowGameOver;
    }

    private void ResumeGame()
    {
        EventSystem.TogglePause();
    }
    
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

    public void RestartGame()
    {
        // Reload the current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void ToggleMenu()
    {
        var c = _pauseContainer.GetClasses();
        _pauseContainer.ToggleInClassList("pause-container-hidden");
    }
}