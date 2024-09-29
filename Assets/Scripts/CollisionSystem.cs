using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;


namespace DotsShooter
{
	[UpdateInGroup(typeof(PhysicsSystemGroup))]
	[UpdateAfter(typeof(PhysicsSimulationGroup))]
	public partial struct CollisionSystem : ISystem
	{
		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<SimulationSingleton>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			state.Dependency = new CollisionJob()
			{
				PlayerLookup = SystemAPI.GetComponentLookup<PlayerTag>(true),
			}.Schedule(
				SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
		}

		[BurstCompile]
		private struct CollisionJob : ICollisionEventsJob
		{
			[ReadOnly] public ComponentLookup<PlayerTag> PlayerLookup;
			
			private bool IsPlayer(Entity entity) => PlayerLookup.HasComponent(entity);
			public void Execute(CollisionEvent collisionEvent)
			{
				if (IsPlayer(collisionEvent.EntityA))
				{
					Debug.Log("Entity A is a player");
				}
				else if (IsPlayer(collisionEvent.EntityB))
				{
					Debug.Log("Entity B is a player");
				}
				Debug.Log($"A: {collisionEvent.EntityA}, B: {collisionEvent.EntityB}");
			}
		}
	}
}