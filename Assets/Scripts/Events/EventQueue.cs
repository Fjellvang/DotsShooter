using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DotsShooter.Events
{
    public struct EventQueue : IComponentData
    {
        public NativeQueue<Event> Value;
    }
    // This is a simple event struct that can be used to pass events around
    // might be too simple, but it's a good starting point
    public struct Event
    {
        public EventType EventType;
        public float3 Location;
    }
    public enum EventType
    {
        EnemyDied,
        PlayerDied,
        BulletDied,
        PauseRequested,
    }
}