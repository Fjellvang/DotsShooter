using System.Diagnostics.CodeAnalysis;
using DotsShooter.Events;
using Unity.Entities;
using Unity.Transforms;

namespace DotsShooter
{
    public static class Helpers
    {
        //TODO: Move to be part of a dead system
        /// <summary>
        /// Marks an entity hierarchy for destruction
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="ecb"></param>
        /// <param name="childBufferFromEntity"></param>
        public static void DestroyEntityHierarchy(Entity entity, ref EntityCommandBuffer ecb, ref BufferLookup<Child> childBufferFromEntity)
        {
            // Destroy all child entities
            if (childBufferFromEntity.HasBuffer(entity))
            {
                var childBuffer = childBufferFromEntity[entity];
                for (int i = 0; i < childBuffer.Length; i++)
                {
                    Entity childEntity = childBuffer[i].Value;
                    DestroyEntityHierarchy(childEntity,ref ecb, ref childBufferFromEntity);
                }
            }

            // Destroy the entity itself
            ecb.DestroyEntity(entity);
        }
        
        public static bool TryGetEventSystem([NotNullWhen(true)] out EventSystem eventSystem)
        {
            eventSystem = null;
            if (!World.DefaultGameObjectInjectionWorld?.IsCreated ?? true) return false;
            eventSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<EventSystem>();
            return eventSystem != null;
        }

        public static bool TryGetSystem<T>([NotNullWhen(true)] out T system) where T : ComponentSystemBase
        {
            system = null;
            if (!World.DefaultGameObjectInjectionWorld?.IsCreated ?? true) return false;
            system = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<T>();
            return system != null;
        }
    }
}