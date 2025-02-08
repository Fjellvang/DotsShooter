using System;
using DotsShooter.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DotsShooter.UI
{
    public class ShopMenuPresenter : MonoBehaviour
    {
        [Tooltip("Reference to the View (UI)")] [SerializeField]
        private UIDocument document;
        [SerializeField]
        private PlayerStats _playerStats;
        
        private VisualElement _root;
        
        private CustomButton _quitButton;
        private CustomButton _continueButton;
        
        private PowerUpButtonWithText _radiusPowerupButton;
        private PowerUpButtonWithText _moveSpeedPowerupButton;
        private PowerUpButtonWithText _damagePowerupButton;
        private PowerUpButtonWithText _attackSpeedPowerupButton;
        
        private void Awake()
        {
            _root = document.rootVisualElement;
            
            
            _quitButton = _root.Q<CustomButton>("quitButton");
            _continueButton = _root.Q<CustomButton>("continueButton");
            
            _radiusPowerupButton = _root.Q<PowerUpButtonWithText>("Radius");
            _moveSpeedPowerupButton = _root.Q<PowerUpButtonWithText>("MoveSpeed");
            _damagePowerupButton = _root.Q<PowerUpButtonWithText>("Damage");
            _attackSpeedPowerupButton = _root.Q<PowerUpButtonWithText>("AttackSpeed");
        }

        private void OnEnable()
        {
            _quitButton.Button.clicked += OnQuitButtonClicked;
            _continueButton.Button.clicked += OnContinueButtonClicked;
            
            _radiusPowerupButton.Button.clicked += OnRadiusPowerupButtonClicked;
            _moveSpeedPowerupButton.Button.clicked += OnMoveSpeedPowerupButtonClicked;
            _damagePowerupButton.Button.clicked += OnDamagePowerupButtonClicked;
            _attackSpeedPowerupButton.Button.clicked += OnAttackSpeedPowerupButtonClicked;

            UpdateLabels();
        }

        private void UpdateLabels()
        {
            _radiusPowerupButton.Value = _playerStats.ExplosionRadius;
            _moveSpeedPowerupButton.Value = _playerStats.MoveSpeed;
            _damagePowerupButton.Value = _playerStats.Damage;
            _attackSpeedPowerupButton.Value = _playerStats.AttackSpeed;
        }

        private void OnAttackSpeedPowerupButtonClicked()
        {
            _playerStats.AttackSpeed *= 0.9f;
            UpdateLabels();
        }

        private void OnDamagePowerupButtonClicked()
        {
            _playerStats.Damage += 1f;
            UpdateLabels();
        }

        private void OnMoveSpeedPowerupButtonClicked()
        {
            _playerStats.MoveSpeed += 1f;
            UpdateLabels();
        }

        private void OnRadiusPowerupButtonClicked()
        {
            _playerStats.ExplosionRadius *= 1.05f;
            UpdateLabels();
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