using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DotsShooter
{
    public class GameManager : Singleton<GameManager>
    {
        private bool _isPaused;
        [SerializeField]
        GameStateTracker _gameStateTracker;
        
        [SerializeField]
        SceneAsset _shopScene;

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
        
        /// <summary>
        /// Function to subscribe to game related events, when the game is started.
        /// </summary>
        public void GameStarted()
        {
            EnsureGameUnpaused();
            if (Helpers.TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnTogglePause += TogglePause;
                eventSystem.OnPlayerDied += GameEnded;
                eventSystem.OnPlayerWon += PlayerWon;
            }
        }
        
        public void PlayerWon()
        {
            Debug.Log("Player won!");
            _gameStateTracker.IncreaseRound();
            UnsubscribeFromGameRelatedEvents();
            SceneManager.LoadScene(_shopScene.name);
        }
        public void GameEnded(float3 na)
        {
            UnsubscribeFromGameRelatedEvents();
        }


        private void OnDisable()
        {
            UnsubscribeFromGameRelatedEvents();
        }

        public void UnsubscribeFromGameRelatedEvents()
        {
            if (Helpers.TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnTogglePause -= TogglePause;
                eventSystem.OnPlayerDied -= GameEnded;
                eventSystem.OnPlayerWon -= PlayerWon;
            }
        }
    }
}