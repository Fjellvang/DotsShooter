using System.Collections;
using DotsShooter;
using DotsShooter.Health;
using DotsShooter.Player;
using DotsShooter.UI;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class HealthPresenter : MonoBehaviour
{
    [Tooltip("Reference to the View (UI)")] [SerializeField]
    private UIDocument m_Document;
    
    // Root element of the UI
    private VisualElement _root;

    // UI elements
    private ProgressBar _healthBar;

    private VisualElement _progressBar;


    private IEnumerator Start()
    {
        // Wait a single frame to ensure the health component has been created
        yield return null;

        Initialize();
    }

    private void Initialize()
    {
        var q = World.DefaultGameObjectInjectionWorld.EntityManager
            .CreateEntityQuery(typeof(HealthComponent), typeof(PlayerTag));
        var healthArray = q.ToComponentDataArray<HealthComponent>(Allocator.TempJob);
        if (healthArray.Length <= 0)
        {
            Debug.LogWarning("No player entity found with HealthComponent");
            UpdateUI(100,100);
        }
        else
        {
            var healthData = healthArray[0];
            UpdateUI(healthData.Health, healthData.MaxHealth);
        }

        healthArray.Dispose();
    }

    private void OnEnable()
    {
        _root = m_Document.rootVisualElement;

        // Find and assign our UI elements
        _healthBar = _root.Q<ProgressBar>("health-bar");
        _progressBar = _healthBar?.Q<VisualElement>(className: "unity-progress-bar__progress");

        var uiSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<UiSystem>();
        uiSystem.PlayerWasDamaged += HealthChanged;
    }

    private void OnDisable()
    {
        var uiSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<UiSystem>();
        uiSystem.PlayerWasDamaged -= HealthChanged;
    }

    private void HealthChanged(HealthEventDetails details)
    {
        UpdateUI(details.Health, details.MaxHealth);
    }


    private void UpdateUI(int health, int maxHealth)
    {
        // Calculate percentage health
        float healthRatio = (float)health / maxHealth;

        // Interpolate color
        Color healthColor = Color.Lerp(Color.red, Color.green, healthRatio);

        // Update health bar value
        _healthBar.value = healthRatio * 100f;

        // Update health bar color
        _progressBar.style.backgroundColor = new StyleColor(healthColor);
    }
}