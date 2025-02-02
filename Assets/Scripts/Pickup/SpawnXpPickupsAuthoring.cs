using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Pickup
{
    public class SpawnXpPickupsAuthoring : MonoBehaviour
    {
        public class SpawnXpPickupsBaker : Baker<SpawnXpPickupsAuthoring>
        {
            public override void Bake(SpawnXpPickupsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<SpawnXpPickups>(entity);
            }
        }
    }
}