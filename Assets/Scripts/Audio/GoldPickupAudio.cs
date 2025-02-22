using Game.Logic.GameConfigs;

namespace DotsShooter.Audio
{
    public class GoldPickupAudio : AudioClipPlayer
    {
        private void OnEnable()
        {
            if (Helpers.TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnGoldPickup += HandleGoldPickup;
            }
        }
        
        private void OnDisable()
        {
            if (Helpers.TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnGoldPickup -= HandleGoldPickup;
            }
        }
        
        private void HandleGoldPickup(CoinType _)
        {
            Play();
        }
    }
}