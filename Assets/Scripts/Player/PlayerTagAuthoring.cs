using DotsShooter.Damage;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Player
{
    public struct PlayerTag : IComponentData { }

    public class PlayerTagAuthoring : MonoBehaviour
    {
        public class PlayerTagBaker : Baker<PlayerTagAuthoring>
        {
            public override void Bake(PlayerTagAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<PlayerTag>(entity);
                AddComponent<PlayerInput>(entity);
                AddComponent<PlayerWasDamaged>(entity);
                SetComponentEnabled<PlayerWasDamaged>(entity, false);
            }
        }
    }
}