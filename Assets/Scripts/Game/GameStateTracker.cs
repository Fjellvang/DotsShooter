using DotsShooter.Metaplay;
using Game.Logic.PlayerActions;
using UnityEngine;

namespace DotsShooter
{
    [CreateAssetMenu(fileName = "GameStateTracker", menuName = "Game/GameStateTracker")]
    public class GameStateTracker : ScriptableObject
    {
        public int Round => _round;
        private int _round;
        
        public void StartGame()
        {
            _round = 1;
            MetaplayClient.PlayerContext.ExecuteAction(new PlayerStartGame());
            Debug.Log($"Game started. Round: {_round}");
        }

        public void IncreaseRound() => _round++;
    }
}