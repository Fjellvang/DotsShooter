using System;
using DotsShooter.State;
using DotsShooter.State.Hierarchical;
using Unity.Mathematics;
using UnityEditor;
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
        }

        private void OnEnable()
        {
            Debug.Log("GameStateTracker enabled");
            if(Helpers.TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnPlayerDied += OnPlayerDied;
            }
            else
            {
                Debug.LogError("Event system not found");
            }
        }

        private void OnPlayerDied(float3 obj)
        {
            
        }
    }
}