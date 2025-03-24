using System;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DotsShooter
{
    /// <summary>
    /// Asset Describing a spawn event, which is a determinictic event that spawns a wave of enemies at a specific time.
    /// </summary>
    [CreateAssetMenu(fileName = "WaveData", menuName = "DotsShooter/WaveSpawnData")]
    public class WaveSpawnEventAsset : ScriptableObject
    {
        public GameObject Prefab;
        public SpawnFormation Formation;
        [Tooltip("Time in seconds, when the wave should spawn.")]
        public float SpawnTime;
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
#if UNITY_EDITOR
    [CustomEditor(typeof(WaveSpawnEventAsset))]
    public class WaveSpawnEventAssetEditor : Editor
    {
        private SerializedProperty formationProperty;
        private SerializedProperty eventTypeProperty;
        
        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawMyGizmos;
            
            // Cache serialized properties
            formationProperty = serializedObject.FindProperty("Formation");
            eventTypeProperty = serializedObject.FindProperty("EventType");
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawMyGizmos;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // Cache the current formation before drawing the default inspector
            SpawnFormation previousFormation = (SpawnFormation)formationProperty.enumValueIndex;
            
            // Draw the default inspector
            DrawDefaultInspector();
            
            // Check if formation has changed
            if (previousFormation != (SpawnFormation)formationProperty.enumValueIndex)
            {
                // Switch the event type based on the new formation
                SpawnFormation currentFormation = (SpawnFormation)formationProperty.enumValueIndex;
                SwitchEventTypeBasedOnFormation(currentFormation);
                
                // Apply changes
                serializedObject.ApplyModifiedProperties();
                
                // Mark the asset as dirty to ensure changes are saved
                EditorUtility.SetDirty(target);
            }
        }
        
        private void SwitchEventTypeBasedOnFormation(SpawnFormation formation)
        {
            var waveData = (WaveSpawnEventAsset)target;
            
            // Create the appropriate event type based on formation
            if (formation == SpawnFormation.Circle)
            {
                // If it's already the correct type, no need to change
                if (waveData.FormationData is CircleFormationData)
                    return;
                
                // Otherwise, replace with the correct type
                var newEventType = new CircleFormationData();
                waveData.FormationData = newEventType;
            }
            else if (formation == SpawnFormation.Line)
            {
                // If it's already the correct type, no need to change
                if (waveData.FormationData is LineFormationData)
                    return;
                
                // Otherwise, replace with the correct type
                var newEventType = new LineFormationData();
                waveData.FormationData = newEventType;
            }
        }
        private void DrawMyGizmos(SceneView sceneView)
        {
            var waveData = (WaveSpawnEventAsset)target;
            if (waveData.Formation == SpawnFormation.Circle)
            {
                DrawCircleFormation(waveData); 
            }
            else if (waveData.Formation == SpawnFormation.Line)
            {
                DrawLineFormation(waveData);
            }
            Handles.DrawWireCube(Vector3.zero, Vector3.one * 2f);
        }
        
        private void DrawCircleFormation(WaveSpawnEventAsset waveData)
        {
            if (waveData.FormationData is not CircleFormationData circleData)
            {
                Debug.Log("Circle formation data is null");
                return;
            }
            // Draw a circle formation with enemies arranged in a circle
            Handles.color = Color.green;
            // Draw each enemy position
            for (int i = 0; i < waveData.Count; i++)
            {
                float angle = (i * 2 * Mathf.PI) / waveData.Count;
                Vector3 position = new Vector3(
                    waveData.InitialSpawnX + circleData.Width * Mathf.Cos(angle),
                    waveData.InitialSpawnY + circleData.Height * Mathf.Sin(angle),
                    0
                );
                Handles.DrawWireCube(position, Vector3.one);
            }
        }
        private void DrawLineFormation(WaveSpawnEventAsset waveData)
        {
            if (waveData.FormationData is not LineFormationData lineData)
            {
                Debug.Log("Line formation data is null");
                return;
            }
            for (int i = 0; i < waveData.Count; i++)
            {
                var position = lineData.AlignmentDirection switch
                {
                    Direction.Up => new Vector3(waveData.InitialSpawnX, waveData.InitialSpawnY + i * waveData.Spacing,
                        0),
                    Direction.Down => new Vector3(waveData.InitialSpawnX, waveData.InitialSpawnY - i * waveData.Spacing,
                        0),
                    Direction.Left => new Vector3(waveData.InitialSpawnX - i * waveData.Spacing, waveData.InitialSpawnY,
                        0),
                    Direction.Right => new Vector3(waveData.InitialSpawnX + i * waveData.Spacing,
                        waveData.InitialSpawnY, 0),
                    _ => Vector3.zero
                };
                Handles.DrawWireCube(position, Vector3.one);
            }
        }
    }
#endif
}