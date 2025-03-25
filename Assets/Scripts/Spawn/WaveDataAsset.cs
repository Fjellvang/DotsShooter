using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DotsShooter
{
    [CreateAssetMenu(fileName = "WaveData", menuName = "DotsShooter/WaveData")]
    public class WaveDataAsset : ScriptableObject
    {
        public EnemyData[] Enemies;
        [FormerlySerializedAs("SpawnTime")] 
        public float TimeBetweenWaves;
        public SpawnEvent[] WaveSpawnEvents;
    }
    
    [Serializable]
    public class SpawnEvent
    {
        public GameObject Prefab;
        [Tooltip("Time in seconds after which the enemy will spawn")]
        public float SpawnAfterElapsedSeconds;
        public SpawnFormationAsset Formation;
    }
    
    [Serializable]
    public class EnemyData
    {
        public GameObject Prefab;
        public int Weight; 
    }
}