using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DotsShooter
{
    public partial struct SpawnFormationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<FormationBaseData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged); 
            var parallelWriter = ecb.AsParallelWriter();
            
            new SpawnLineFormationJob
            {
                Ecb = parallelWriter
            }.ScheduleParallel();
            new SpawnCircleFormationJob
            {
                Ecb = parallelWriter
            }.ScheduleParallel();
            
            // state.Dependency = job.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct SpawnCircleFormationJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter Ecb;

        public void Execute([EntityIndexInQuery] int entityIndex,
            in FormationBaseData formationBase,
            in CircleFormationComponent circleData,
            EnabledRefRW<SpawnFormationFlag> enabledFlag)
        {
            var formationBaseData = formationBase;
            var direction = circleData.MovementDirection switch
            {
                AngularDirection.Inwards => math.PI,
                AngularDirection.Outwards => 0,
                _ => throw new ArgumentOutOfRangeException()
            };
            var spawnCount = formationBaseData.Count;
            for (int i = 0; i < spawnCount; i++)
            {
                var angle = (i * 2 * math.PI) / spawnCount;
                var position = new float3(
                    formationBaseData.InitialPosition.x + math.cos(angle) * circleData.Width,
                    formationBaseData.InitialPosition.y + math.sin(angle) * circleData.Height
                    , 0);
                    
                var transform = LocalTransform.FromPosition(position);
                var spawned = Ecb.Instantiate(entityIndex, formationBaseData.Prefab);
                Ecb.SetComponent(entityIndex, spawned, transform);
                Ecb.SetComponent(entityIndex, spawned, new LinearMovementComponent() { Angle = angle + direction });
            }
            
            enabledFlag.ValueRW = false;
        }
    }

    [BurstCompile]
    public partial struct SpawnLineFormationJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter Ecb;

        public void Execute([EntityIndexInQuery] int entityIndex, 
            in FormationBaseData formationBase,
            in LineFormationComponent lineData,
            EnabledRefRW<SpawnFormationFlag> enabledFlag)
        {
            var formationBaseData = formationBase;
            for (int i = 0; i < formationBaseData.Count; i++)
            {
                var direction = lineData.AlignmentDirection switch
                {
                    Direction.Up => new float3(0, 1, 0),
                    Direction.Down => new float3(0, -1, 0),
                    Direction.Left => new float3(-1, 0, 0),
                    Direction.Right => new float3(1, 0, 0),
                    _ => 0
                };
                var position = direction * i * formationBaseData.Spacing;
                var transform = LocalTransform.FromPosition(new float3(formationBaseData.InitialPosition, 0) + position);
                var spawned = Ecb.Instantiate(entityIndex, formationBaseData.Prefab);
                Ecb.SetComponent(entityIndex, spawned, transform);

                var angle = lineData.MovementDirection switch
                {
                    Direction.Up => -math.PI / 2,
                    Direction.Right => 0,
                    Direction.Down => math.PI / 2,
                    Direction.Left => math.PI,
                    _ => 0
                };

                // TODO: this is the weakest part of the code, we don't enforce in the baker that the entity has the component
                Ecb.SetComponent(entityIndex, spawned, new LinearMovementComponent() { Angle = angle });
            }

            // Set the Spawn flag to false
            enabledFlag.ValueRW = false;
        }
    }
}