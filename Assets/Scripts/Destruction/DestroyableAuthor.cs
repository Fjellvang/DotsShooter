using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Destruction
{
    public class DestroyableAuthor : MonoBehaviour
    {
        public class DestroyableBaker : Baker<DestroyableAuthor>
        {
            public override void Bake(DestroyableAuthor authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<DestroyNextFrame>(entity);
                AddComponent<MarkedForDestruction>(entity);
                SetComponentEnabled<DestroyNextFrame>(entity, false);
                SetComponentEnabled<MarkedForDestruction>(entity, false);
            }
        }
    }
}