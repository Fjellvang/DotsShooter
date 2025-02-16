using DotsShooter.Metaplay;
using Game.Logic.PlayerActions;
using UnityEngine;

namespace DotsShooter.Gold
{
    public class GoldTracker : MonoBehaviour
    {
        public void AddGold(int amount)
        {
            // We could have just raised this event from the event system...
            MetaplayClient.PlayerContext.ExecuteAction(new PlayerAddGold(amount));
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