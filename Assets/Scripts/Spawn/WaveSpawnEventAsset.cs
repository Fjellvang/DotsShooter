using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DotsShooter
{
    /// <summary>
    /// Asset Describing a spawn event, which is a determinictic event that spawns a wave of enemies at a specific time.
    /// </summary>
    [CreateAssetMenu(fileName = "WaveSpawnEventData", menuName = "DotsShooter/WaveSpawnData")]
    public class WaveSpawnEventAsset : ScriptableObject
    {
        public SpawnFormation Formation;
        [Tooltip("Number of enemies to spawn.")]
        public int Count = 5;
        [Tooltip("Spacing between enemies.")]
        public float Spacing = 1;
        public float InitialSpawnX;
        public float InitialSpawnY;
        [SerializeReference]
        public SpawnFormationData FormationData = new CircleFormationData();
    }

    [Serializable]
    public class SpawnFormationData { }
    
    [Serializable]
    public class LineFormationData : SpawnFormationData
    {
        public Direction MovementDirection; 
        public Direction AlignmentDirection;
    }
    [Serializable]
    public class CircleFormationData : SpawnFormationData
    {
        public AngularDirection MovementDirection;
        public float Width = 5f;
        public float Height = 5f;
    }
    public enum SpawnFormation
    {
        Circle,
        Line,
    }
    
    public enum AngularDirection
    {
        Inwards,
        Outwards,
    }
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right,
    }
}