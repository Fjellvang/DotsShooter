using DotsShooter.Metaplay;
using Game.Logic.PlayerActions;
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
        
        private CustomButton _quitButton;
        private CustomButton _continueButton;
        
        private PowerUpButtonWithText _radiusPowerupButton;
        private PowerUpButtonWithText _moveSpeedPowerupButton;
        private PowerUpButtonWithText _damagePowerupButton;
        private PowerUpButtonWithText _attackSpeedPowerupButton;
        private PowerUpButtonWithText _rangePowerupButton;
        
        private void Awake()
        {
            _root = document.rootVisualElement;
            
            
            _quitButton = _root.Q<CustomButton>("quitButton");
            _continueButton = _root.Q<CustomButton>("continueButton");
            
            _radiusPowerupButton = _root.Q<PowerUpButtonWithText>("Radius");
            _moveSpeedPowerupButton = _root.Q<PowerUpButtonWithText>("MoveSpeed");
            _damagePowerupButton = _root.Q<PowerUpButtonWithText>("Damage");
            _attackSpeedPowerupButton = _root.Q<PowerUpButtonWithText>("AttackSpeed");
            _rangePowerupButton = _root.Q<PowerUpButtonWithText>("Range");
        }

        private void OnEnable()
        {
            _quitButton.Button.clicked += OnQuitButtonClicked;
            _continueButton.Button.clicked += OnContinueButtonClicked;
            
            _radiusPowerupButton.Button.clicked += OnRadiusPowerupButtonClicked;
            _moveSpeedPowerupButton.Button.clicked += OnMoveSpeedPowerupButtonClicked;
            _damagePowerupButton.Button.clicked += OnDamagePowerupButtonClicked;
            _attackSpeedPowerupButton.Button.clicked += OnAttackSpeedPowerupButtonClicked;
            _rangePowerupButton.Button.clicked += OnRangePowerupButtonClicked;
            UpdateLabels();
        }
        public void UpdateLabels()
        {
            var playerStats = MetaplayClient.PlayerModel.GameStats;
            _radiusPowerupButton.Value = playerStats.ExplosionRadius.Float;
            _moveSpeedPowerupButton.Value = playerStats.MoveSpeed.Float;
            _damagePowerupButton.Value = playerStats.Damage.Float;
            _attackSpeedPowerupButton.Value = playerStats.AttackSpeed.Float;
            _rangePowerupButton.Value = playerStats.Range.Float;
        }

        private void OnRangePowerupButtonClicked()
        {
            MetaplayClient.PlayerContext.ExecuteAction(new PlayerBuyStat(PlayerStat.Range));
        }


        private void OnAttackSpeedPowerupButtonClicked()
        {
            MetaplayClient.PlayerContext.ExecuteAction(new PlayerBuyStat(PlayerStat.AttackSpeed));
        }

        private void OnDamagePowerupButtonClicked()
        {
            MetaplayClient.PlayerContext.ExecuteAction(new PlayerBuyStat(PlayerStat.Damage));
        }

        private void OnMoveSpeedPowerupButtonClicked()
        {
            MetaplayClient.PlayerContext.ExecuteAction(new PlayerBuyStat(PlayerStat.MoveSpeed));
        }

        private void OnRadiusPowerupButtonClicked()
        {
            MetaplayClient.PlayerContext.ExecuteAction(new PlayerBuyStat(PlayerStat.ExplosionRadius));
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