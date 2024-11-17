namespace DotsShooter.Audio
{
    public class XpPickupAudio : AudioClipPlayer
    {
        private void OnEnable()
        {
            if (TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnXpPickup += Play;
            }
        }
        
        private void OnDisable()
        {
            if (TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnXpPickup -= Play;
            }
        }
    }
}