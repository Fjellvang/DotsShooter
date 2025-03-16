using DotsShooter.Destruction;
using DotsShooter.SimpleCollision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace DotsShooter.Damage
{
    [BurstCompile]
    [UpdateAfter(typeof(SimpleCollisionSystem))]
    public partial struct DamageOnCollisionSystem : ISystem
    {
        private BufferLookup<DamageData> _bufferLookup;
        // private EntityQuery _damageQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            _bufferLookup = state.GetBufferLookup<DamageData>();

            var builder = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllRW<DamageOnCollision>()
                    .WithAll<SimpleCollisionEvent>() // Add this line
                ;

            // _damageQuery = state.GetEntityQuery(builder);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _bufferLookup.Update(ref state);
            var markedForDestructionLookup = SystemAPI.GetComponentLookup<DestroyNextFrameTag>();
            var enemyTagLookup = SystemAPI.GetComponentLookup<EnemyTag>();

            foreach (var (damage, simpleCollisionBuffer, entity) in 
                     SystemAPI.Query<RefRO<DamageOnCollision>, DynamicBuffer<SimpleCollisionEvent>>()
                         .WithEntityAccess())
            {
                for (int i = 0; i < simpleCollisionBuffer.Length; i++)
                {
                    var simpleCollisionEvent = simpleCollisionBuffer[i];
                    var other = simpleCollisionEvent.GetOtherEntity(entity);
                    
                    // HACK: This is a hack to prevent enemies from damaging each other
                    if (enemyTagLookup.HasComponent(other) && enemyTagLookup.HasComponent(entity))
                    {
                        continue;
                    }
                    
                    if (_bufferLookup.HasBuffer(other)) {
                        _bufferLookup[other].Add(new DamageData() { Damage = damage.ValueRO.Damage });
                    }

                    if (damage.ValueRO.DestroyOnCollision)
                    {
                        markedForDestructionLookup.SetComponentEnabled(entity, true);
                    }
                }
            }
        }


        [BurstCompile]
        public partial struct HandleDamageJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;

            [ReadOnly, NativeDisableParallelForRestriction]
            public BufferLookup<DamageData> BufferLookup;

            void Execute(
                Entity entity,
                [ReadOnly] in DamageOnCollision damage,
                [ReadOnly] in DynamicBuffer<SimpleCollisionEvent> collisionBuffer,
                [ChunkIndexInQuery] int sortKey)
            {
                for (int i = 0; i < collisionBuffer.Length; i++)
                {
                    var simpleCollisionEvent = collisionBuffer[i];
                    var other = simpleCollisionEvent.GetOtherEntity(entity);

                    if (BufferLookup.HasBuffer(other))
                    {
                        ECB.AppendToBuffer(sortKey, other, new DamageData() { Damage = damage.Damage });
                    }

                    if (damage.DestroyOnCollision)
                    {
                        ECB.DestroyEntity(sortKey, entity);
                        break;
                    }
                }
            }
        }
    }
}