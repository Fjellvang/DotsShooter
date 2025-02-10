using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public class GameManager : Singleton<GameManager>
    {
        private bool _isPaused;
        [SerializeField]
        GameStateTracker _gameStateTracker;

        private void Start()
        {
            // could be cleaner, but ensure we are not "paused" when the game starts
            EnsureGameUnpaused();
            _gameStateTracker.Initialize();
        }

        private static void EnsureGameUnpaused()
        {
            var simulationSystemGroup =
                World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            if (!simulationSystemGroup.Enabled)
            {
                simulationSystemGroup.Enabled = true;
            }
        }

        public void TogglePause()
        {
            _isPaused = !_isPaused;
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().Enabled = !_isPaused;
        }

        private void OnEnable()
        {
            if (Helpers.TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnTogglePause += TogglePause;
            }
        }

        private void OnDisable()
        {
            if (Helpers.TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnTogglePause -= TogglePause;
            }
        }
    }
}