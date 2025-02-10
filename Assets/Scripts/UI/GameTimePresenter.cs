using DotsShooter.Time;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class GameTimePresenter : MonoBehaviour
{
    [Tooltip("Reference to the View (UI)")] [SerializeField]
    private UIDocument m_Document;
    
    // Root element of the UI
    private VisualElement _root;

    private LabelContainer _timeLabel;
    
    private EntityQuery _entityQuery;
    private EntityManager _entityManager;
    private bool _isGameLoaded;

    private void Awake()
    {
        _isGameLoaded = false;
    }

    private void Start()
    {
        _root = m_Document.rootVisualElement;
        _timeLabel = _root.Q<LabelContainer>("time-left");
    }

    public void GameLoaded()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _entityQuery = _entityManager.CreateEntityQuery(typeof(SimulationTime));
        _isGameLoaded = true;
    }

    private void Update()
    {
        if(!_isGameLoaded) return;

        var simulationTime = _entityQuery.GetSingleton<SimulationTime>();

        var timeleft = simulationTime.GameTime - simulationTime.ElapsedTime;

        _timeLabel.Value = Mathf.Round(timeleft);
    }
}