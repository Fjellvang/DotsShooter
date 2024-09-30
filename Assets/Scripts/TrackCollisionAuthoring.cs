using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public class TrackCollisionAuthoring : MonoBehaviour
    {
        public class TrackCollisionBaker : Baker<TrackCollisionAuthoring>
        {
            public override void Bake(TrackCollisionAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new TrackCollision { FrameCount = 0 });
            }
        }
    }
    
	public struct TrackCollision : IComponentData
	{
		public int FrameCount;
	}
}