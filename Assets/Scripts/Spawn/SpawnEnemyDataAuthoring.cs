using System;
using DotsShooter.Common;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace DotsShooter
{
    public struct WaveData : IBufferElementData
    {
        public Entity PrefabsBufferEntity;
        public float SpawnTime;
    }
    public struct EnemyPrefabData : IBufferElementData
    {
        public Entity Prefab;
        public int Weight;
    }

    [Serializable]
    public class EnemyData
    {
        public GameObject Prefab;
        public int Weight; 
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
                Debug.Log("Baking spawn data");
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity,
                    new SpawnEnemyData
                    {
                        MaxX = authoring.MaxX,
                        MaxY = authoring.MaxY,
                        SpawnTimer = 0,
                    });
                var waveEntityBuffer = AddBuffer<WaveData>(entity);
                foreach (var waveData in authoring.WaveData)
                {
                    var waveEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                    waveEntityBuffer.Add(new WaveData
                    {
                        PrefabsBufferEntity = waveEntity,
                        SpawnTime = waveData.SpawnTime
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
        }
    }
}