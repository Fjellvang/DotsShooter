using DotsShooter.Events;
using DotsShooter.Player;
using DotsShooter.PowerUp;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class PowerUpPresenter : MonoBehaviour
{
    [Tooltip("Reference to the View (UI)")] 
    [SerializeField]
    private UIDocument m_Document;
    // Root element of the UI
    private VisualElement _root;

    private VisualElement _powerUpContainer;
    
    private EntityManager _entityManager;
    private Entity _playerEntity;
    private EventSystem EventSystem => World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<EventSystem>();


    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        HidePowerUpContainer();
    }

    private void HidePowerUpContainer()
    {
        _powerUpContainer.style.display = DisplayStyle.None;
    }
    
    private void ShowPowerUpContainer()
    {
        _powerUpContainer.style.display = DisplayStyle.Flex;
    }

    public void OnPowerUpButtonClicked(PowerUpType powerupType)
    {
        // Ensure we're on the main thread
        if (World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            // TODO: this should probably be simplified and at best cached
            var powerUp = _entityManager.GetComponentDataRW<PowerUpComponent>(World.DefaultGameObjectInjectionWorld.GetExistingSystem<PowerUpSystem>());
            var writer = powerUp.ValueRW.PendingPowerUps.AsParallelWriter();
            writer
                .Enqueue(new PowerUp{ Type = powerupType});
        }
        
        HidePowerUpContainer();
    }
    
    private void OnEnable()
    {
        _root = m_Document.rootVisualElement;

        // Find and assign our UI elements
        _powerUpContainer = _root.Q<VisualElement>("power-up-container").Q<VisualElement>("container");

        CreateAndAddPowerUpButton("Attack Speed", PowerUpType.AttackSpeed);
        CreateAndAddPowerUpButton("Damage", PowerUpType.Damage);
        CreateAndAddPowerUpButton("Movement", PowerUpType.MovementSpeed);
        
        EventSystem.OnShowPowerUpMenu += ShowPowerUpContainer;
    }

    private void OnDisable()
    {
        if(!World.DefaultGameObjectInjectionWorld?.IsCreated ?? true) return;
        EventSystem.OnShowPowerUpMenu -= ShowPowerUpContainer;
    }

    private void CreateAndAddPowerUpButton(string label, PowerUpType powerUpType)
    {
        var button = new PowerupButton(label);
        button.Button.clicked += () => OnPowerUpButtonClicked(powerUpType);
        _powerUpContainer.Add(button);
    }
}