using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Stateful;
using Unity.Transforms;
using UnityEngine;


namespace DotsShooter
{
	[RequireMatchingQueriesForUpdate]
	[UpdateBefore(typeof(TransformSystemGroup))]
	public partial struct TrackStatefulCollisionSystem : ISystem
	{
		private EntityQuery _trackCollisionQuery;
		
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<SimulationSingleton>();
			_trackCollisionQuery = state.GetEntityQuery(typeof(TrackCollision));
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			var trackers = _trackCollisionQuery.ToEntityArray(Allocator.Temp);
			for (int i = 0; i < trackers.Length; i++)
			{
				HandleCollisionEvents(ref state, trackers, i);
			}
		}

		private static void HandleCollisionEvents(ref SystemState state, NativeArray<Entity> trackers, int i)
		{
			var entity = trackers[i];
			var tracker = state.EntityManager.GetComponentData<TrackCollision>(entity);
			if (!state.EntityManager.HasComponent<StatefulCollisionEvent>(entity))
			{
				return;
			}
				
			var collisionEvents = state.EntityManager.GetBuffer<StatefulCollisionEvent>(entity);
			for (int j = 0; j < collisionEvents.Length; j++)
			{
				var collisionEvent = collisionEvents[j];
				// Debug.Log($"Entity A: {collisionEvent.EntityA}, Entity B: {collisionEvent.EntityB}, State: {GetStateName(collisionEvent.State)}");
			}
		}

		private static string GetStateName(StatefulEventState state) => state switch
		{
			StatefulEventState.Enter => "Enter",
			StatefulEventState.Stay => "Stay",
			StatefulEventState.Exit => "Exit",
			_ => "Unknown"
		};
	}
}

