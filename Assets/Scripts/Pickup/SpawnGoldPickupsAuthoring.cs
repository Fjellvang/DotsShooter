using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Pickup
{
    public class SpawnGoldPickupsAuthoring : MonoBehaviour
    {
        public class SpawnXpPickupsBaker : Baker<SpawnGoldPickupsAuthoring>
        {
            public override void Bake(SpawnGoldPickupsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<SpawnGoldPickups>(entity);
            }
        }
    }
}