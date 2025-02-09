using System;
using UnityEngine;

namespace DotsShooter.Gold
{
    public class GoldTracker : MonoBehaviour
    {
        [SerializeField]
        PlayerGold _playerGold;
        
        public void AddGold(int amount)
        {
            _playerGold.Gold += amount;
        }
        
        public void AddGold() => AddGold(1);

        private void OnEnable()
        {
            if(Helpers.TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnGoldPickup += AddGold;
            }
        }
        
        private void OnDisable()
        {
            if(Helpers.TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnGoldPickup -= AddGold;
            }
        }
    }
}