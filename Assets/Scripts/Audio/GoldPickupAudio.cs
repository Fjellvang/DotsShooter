namespace DotsShooter.Audio
{
    public class GoldPickupAudio : AudioClipPlayer
    {
        private void OnEnable()
        {
            if (Helpers.TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnGoldPickup += Play;
            }
        }
        
        private void OnDisable()
        {
            if (Helpers.TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnGoldPickup -= Play;
            }
        }
    }
}