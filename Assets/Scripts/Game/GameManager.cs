using System;
using System.Collections;
using DotsShooter.Metaplay;
using Game.Logic.PlayerActions;
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

        private readonly PlayerStatisticsTracker _playerStatisticsTracker = new ();
        
        [SerializeField]
        SceneAsset _shopScene;
        

        public int GetCurrentRound() => _gameStateTracker.Round;
        
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
                eventSystem.OnGoldPickup += _playerStatisticsTracker.IncreaseGoldCollected;
                eventSystem.OnEnemyDied += _playerStatisticsTracker.IncreaseKills;
                eventSystem.OnPlayerDied += RegisterStatisticWithServer;
            }
        }

        public void PlayerWon()
        {
            Debug.Log("Player won!");
            _gameStateTracker.IncreaseRound();
            _playerStatisticsTracker.IncreaseRoundsCompleted();
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

        private void RegisterStatisticWithServer(float3 _)
        {
            // TODO: I Doubt its good practice to invoke the server listener directly from the game manager
            // but for the sake of the example I will do it here
            MetaplayClient.PlayerContext.ExecuteAction(new PlayerSendLeaderboardStats(
                _playerStatisticsTracker.Kills,
                _playerStatisticsTracker.GoldCollected,
                _playerStatisticsTracker.RoundsCompleted));

        }

        public void UnsubscribeFromGameRelatedEvents()
        {
            if (Helpers.TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnTogglePause -= TogglePause;
                eventSystem.OnPlayerDied -= GameEnded;
                eventSystem.OnPlayerWon -= PlayerWon;
                eventSystem.OnGoldPickup -= _playerStatisticsTracker.IncreaseGoldCollected;
                eventSystem.OnEnemyDied -= _playerStatisticsTracker.IncreaseKills;
                eventSystem.OnPlayerDied -= RegisterStatisticWithServer;
            }
        }
    }
}