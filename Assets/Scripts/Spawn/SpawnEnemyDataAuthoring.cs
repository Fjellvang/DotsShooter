using System;
using DotsShooter.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace DotsShooter
{
    public struct WaveData : IBufferElementData
    {
        public Entity PrefabsBufferEntity;
        public float TimeBetweenWaves;
    }
    public struct EnemyPrefabData : IBufferElementData
    {
        public Entity Prefab;
        public int Weight;
    }
    
    public struct SpawnEventData : IBufferElementData
    {
        public Entity FormationEntity;
        public float SpawnAfterSeconds;
        public bool IsSpawned;
    }

    public struct SpawnFormationFlag : IComponentData, IEnableableComponent { }

    public struct FormationBaseData : IComponentData 
    {
        public Entity Prefab;
        public int Count;
        public float Spacing;
        public float2 InitialPosition;
    }
    public struct LineFormationComponent : IComponentData
    {
        public Direction MovementDirection;
        public Direction AlignmentDirection;
    }

    public struct CircleFormationComponent : IComponentData
    {
        public AngularDirection MovementDirection;
        public float Width;
        public float Height;
    }

    public struct SpawnEnemyData : IComponentData
    {
        public int MaxX;
        public int MaxY;
        public float SpawnTimer;
    }


    
    [RequireComponent(typeof(EntityRandomAuthoring))]
    public class SpawnEnemyDataAuthoring : MonoBehaviour
    {
        [FormerlySerializedAs("Enemies")] public WaveDataAsset[] WaveData;
        public int MaxX = 20;
        public int MaxY = 20;

        public class SpawnEnemyDataBaker : Baker<SpawnEnemyDataAuthoring>
        {
            public override void Bake(SpawnEnemyDataAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity,
                    new SpawnEnemyData
                    {
                        MaxX = authoring.MaxX,
                        MaxY = authoring.MaxY,
                        SpawnTimer = 0,
                    });
                BakeWaveData(authoring, entity);
            }

            private void BakeWaveData(SpawnEnemyDataAuthoring authoring, Entity entity)
            {
                var waveEntityBuffer = AddBuffer<WaveData>(entity);
                foreach (var waveData in authoring.WaveData)
                {
                    var waveEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                    BakeSpawnEvents(waveData?.WaveSpawnEvents, waveEntity);
                    waveEntityBuffer.Add(new WaveData
                    {
                        PrefabsBufferEntity = waveEntity,
                        TimeBetweenWaves = waveData.TimeBetweenWaves
                    });
                    var enemyPrefabsBuffer = AddBuffer<EnemyPrefabData>(waveEntity);
                    foreach (var enemyPrefab in waveData.Enemies)
                    {
                        enemyPrefabsBuffer.Add(new EnemyPrefabData
                        {
                            Prefab = GetEntity(enemyPrefab.Prefab, TransformUsageFlags.Dynamic),
                            Weight = enemyPrefab.Weight
                        });
                    }
                }
            }
            
            private void BakeSpawnEvents(SpawnEvent[] spawnEvents, Entity waveEntity)
            {
                var spawnEventBuffer = AddBuffer<SpawnEventData>(waveEntity);
                foreach (var spawnEvent in spawnEvents ?? Array.Empty<SpawnEvent>())
                {
                    var spawnEventEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                    var formation = spawnEvent.Formation;
                    // Some of this base formation data might be useful to pack in a blob asset?
                    var formationData = new FormationBaseData()
                    {
                        Prefab = GetEntity(spawnEvent.Prefab, TransformUsageFlags.Dynamic),
                        Count = formation.Count,
                        Spacing = formation.Spacing,
                        InitialPosition = new float2(formation.InitialSpawnX, formation.InitialSpawnY),
                    };
                    AddComponent(spawnEventEntity, formationData);
                    AddComponent(spawnEventEntity, new SpawnFormationFlag());
                    SetComponentEnabled<SpawnFormationFlag>(spawnEventEntity, false);
                    switch (formation.formationAssetData)
                    {
                        case LineFormationAssetData lineFormationData:
                            AddComponent(spawnEventEntity, new LineFormationComponent
                            {
                                MovementDirection = lineFormationData.MovementDirection,
                                AlignmentDirection = lineFormationData.AlignmentDirection
                            });
                            break;
                        case CircleFormationAssetData circleFormationData:
                            AddComponent(spawnEventEntity, new CircleFormationComponent
                            {
                                MovementDirection = circleFormationData.MovementDirection,
                                Width = circleFormationData.Width,
                                Height = circleFormationData.Height
                            });
                            break;
                    }
                    spawnEventBuffer.Add(new SpawnEventData
                    {
                        FormationEntity = spawnEventEntity,
                        SpawnAfterSeconds = spawnEvent.SpawnAfterElapsedSeconds,
                        IsSpawned = false
                    });
                }
            }
        }
        
        private void OnDrawGizmos()
        {
            // Set the gizmo color 
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f); // Semi-transparent green
    
            // Center position at origin (0,0,0)
            Vector3 center = Vector3.zero;
    
            // Draw a wire cube to show the boundaries
            // The size needs to be doubled since the range is from -MaxX to +MaxX, -MaxY to +MaxY
            Gizmos.DrawWireCube(center, new Vector3(MaxX * 2, MaxY * 2, 0.1f));
    
            // Draw a solid cube with transparency to visualize the area
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.1f); // More transparent for the solid part
            Gizmos.DrawCube(center, new Vector3(MaxX * 2, MaxY * 2, 0.1f));
        }
    }
}