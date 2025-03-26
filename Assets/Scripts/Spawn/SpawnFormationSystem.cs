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
            
            foreach (var (formationBase, lineData, entity) in SystemAPI
                         .Query<RefRO<FormationBaseData>, RefRO<LineFormationComponent>>()
                         .WithAll<SpawnFormationFlag>()
                         .WithEntityAccess())
             {
                var formationBaseData = formationBase.ValueRO;
                for(int i =0; i < formationBase.ValueRO.Count; i++)
                {
                    var direction = lineData.ValueRO.AlignmentDirection switch 
                    {
                        Direction.Up =>    new float3( 0, 1, 0),
                        Direction.Down =>  new float3( 0,-1, 0),
                        Direction.Left =>  new float3(-1, 0, 0),
                        Direction.Right => new float3( 1, 0, 0),
                        _ => 0
                    };
                    var position = direction * i * formationBaseData.Spacing;
                    var transform = LocalTransform.FromPosition(new float3(formationBaseData.InitialPosition,0) + position);
                    var spawned = ecb.Instantiate(formationBaseData.Prefab);
                    ecb.SetComponent(spawned, transform);
                    
                    var angle = lineData.ValueRO.MovementDirection switch 
                    {
                        Direction.Up =>    -math.PI/2,
                        Direction.Right => 0,
                        Direction.Down =>  math.PI/2,
                        Direction.Left =>  math.PI,
                        _ => 0
                    };
                    
                    //TODO: this is the weakest part of the code, we dont enforce in the baker that the entity has the component
                    ecb.SetComponent(spawned, new LinearMovementComponent() { Angle = angle });
                }
                
                // Set the Spawn flag to false
                SystemAPI.SetComponentEnabled<SpawnFormationFlag>(entity,false);
            }
        }
    }
}