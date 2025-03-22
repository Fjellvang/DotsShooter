using System.Diagnostics.CodeAnalysis;
using DotsShooter.Events;
using DotsShooter.Player;
using DotsShooter.Weapons;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
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

        public static float3 FindClosestDirection(PhysicsWorldSingleton physics, in float3 position, in WeaponData weaponData, in PlayerStatModifications statModifications,
            NativeList<DistanceHit> overlapHits)
        {
            var closest = float3.zero;
            var range = weaponData.Range * statModifications.RangeMultiplier;
            if (physics.OverlapSphere(position, range, ref overlapHits, weaponData.CollisionFilter))
            {
                var closestDistance = overlapHits[0].Distance;
                var closestHit = overlapHits[0];
                for (int i = 1; i < overlapHits.Length; i++)
                {
                    if (!(overlapHits[i].Distance < closestDistance)) continue;
                    
                    closestDistance = overlapHits[i].Distance;
                    closestHit = overlapHits[i];
                }
                closest = math.normalize(closestHit.Position - position);
            }

            return closest;
        }
        
         // Rotate vector by degrees using trigonometry.
         public static float3 RotateVector2DTrig(float3 vector, float degrees)
         {
             var radians = degrees * math.TORADIANS;
             var sin = math.sin(radians);
             var cos = math.cos(radians);
    
             var x = vector.x * cos - vector.y * sin;
             var y = vector.x * sin + vector.y * cos;
    
             return new float3(x, y, 0);
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