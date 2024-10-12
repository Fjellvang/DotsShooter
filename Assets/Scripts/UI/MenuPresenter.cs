using DotsShooter.Events;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;


public class MenuPresenter : MonoBehaviour
{
    [Tooltip("Reference to the View (UI)")] [SerializeField]
    private UIDocument m_Document;
    // Root element of the UI
    private VisualElement _root;

    private CustomButton _continueButton;
    private CustomButton _quitButton;
    
    private VisualElement _pauseContainer;
    
    // TODO: Consider abstracting the event system access
    private EventSystem EventSystem => World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<EventSystem>();
    
    private void OnEnable()
    {
        _root = m_Document.rootVisualElement;

        // Find and assign our UI elements
        _continueButton = _root.Q<CustomButton>("continue-button");
        _quitButton = _root.Q<CustomButton>("quit-button");
        _pauseContainer = _root.Q<VisualElement>("pause-container");

        _continueButton.Button.clicked += ResumeGame;
        _quitButton.Button.clicked += QuitGame;
        EventSystem.OnTogglePause += ToggleMenu;
    }


    private void OnDisable()
    {
        EventSystem.OnTogglePause -= ToggleMenu;
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

    private void ToggleMenu()
    {
        var c = _pauseContainer.GetClasses();
        _pauseContainer.ToggleInClassList("pause-container-hidden");
    }
}