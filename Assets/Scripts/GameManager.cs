using System;
using DotsShooter.Events;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public class GameManager : MonoBehaviour
    {
        
        private bool _isPaused;

        private void Start()
        {
            // could be cleaner, but ensure we are not "paused" when the game starts
            if (!World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().Enabled)
            {
                World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().Enabled = true;
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