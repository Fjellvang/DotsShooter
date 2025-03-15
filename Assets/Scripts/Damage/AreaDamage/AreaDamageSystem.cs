using DotsShooter.Destruction;
using DotsShooter.Health;
using DotsShooter.SimpleCollision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace DotsShooter.Damage.AreaDamage
{
    [BurstCompile]
    [UpdateAfter(typeof(SimpleCollisionSystem))]
    [UpdateBefore(typeof(HealthSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct AreaDamageSystem : ISystem
    {
       private BufferLookup<DamageData> _bufferLookup;
       private ComponentLookup<LocalToWorld> _localToWorldLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            _bufferLookup = state.GetBufferLookup<DamageData>();
            _localToWorldLookup = state.GetComponentLookup<LocalToWorld>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _localToWorldLookup.Update(ref state);

            var markedForDestructionLookup = SystemAPI.GetComponentLookup<MarkedForDestruction>();
            _bufferLookup.Update(ref state);
            foreach (var (damage, localTransform, entity) in SystemAPI
                         .Query<RefRO<AreaDamage>, RefRO<LocalTransform>>()
                         .WithNone<MarkedForDestruction>()
                         .WithEntityAccess())
            {
                if (!state.EntityManager.HasComponent<SimpleCollisionEvent>(entity))
                {
                    continue;
                }

                var physics = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
                var simpleCollisionBuffer = state.EntityManager.GetBuffer<SimpleCollisionEvent>(entity);
                for (int i = 0; i < simpleCollisionBuffer.Length; i++)
                {
                    var location = localTransform.ValueRO.Position;
                    // var entities = grid.GetEntitiesInRadius(location, damage.ValueRO.Radius, localToWorld);
                    
                    var overlapHits = new NativeList<DistanceHit>(state.WorldUpdateAllocator);
                    if (physics.OverlapSphere(location, damage.ValueRO.Radius, ref overlapHits,
                            damage.ValueRO.CollisionFilter))
                    {
                        for(int j = 0; j < overlapHits.Length; j++)
                        {
                            var other = overlapHits[j].Entity;
                            if (_bufferLookup.HasBuffer(other))
                            {
                                _bufferLookup[other].Add(new DamageData() { Damage = damage.ValueRO.Damage });
                            }
                        }
                    }
                        
                    // Destroy the bullet
                    markedForDestructionLookup.SetComponentEnabled(entity, true);
                }
            }
        }
    }
}