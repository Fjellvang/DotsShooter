using DotsShooter.Events;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public class GameManager : MonoBehaviour
    {
        
        private bool _isPaused;

        public void ToggleTogglePause()
        {
            _isPaused = !_isPaused;
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().Enabled = !_isPaused;
        }

        private void OnEnable()
        {
            var eventSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<EventSystem>(); 
            eventSystem.OnTogglePause += ToggleTogglePause;
        }
    }
}