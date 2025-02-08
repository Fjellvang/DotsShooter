using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DotsShooter.UI
{
    public class ShopMenuPresenter : MonoBehaviour
    {
        [Tooltip("Reference to the View (UI)")] [SerializeField]
        private UIDocument document;
        
        private VisualElement _root;
        
        private void Awake()
        {
            _root = document.rootVisualElement;
            
            _root.Q<CustomButton>("quitButton").Button.clicked += OnQuitButtonClicked;
            _root.Q<CustomButton>("continueButton").Button.clicked += OnContinueButtonClicked;
        }

        private void OnContinueButtonClicked()
        {
            SceneManager.LoadScene(2); // TODO: Get a proper reference
        }

        private void OnQuitButtonClicked()
        {
            SceneManager.LoadScene(0); // TODO: Get a proper reference
        }
    }
}