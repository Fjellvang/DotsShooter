using UnityEditor;
using UnityEngine;

namespace DotsShooter
{
    [CustomEditor(typeof(WaveSpawnEventAsset))]
    public class WaveSpawnEventAssetEditor : Editor
    {
        private SerializedProperty formationProperty;
        
        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawMyGizmos;
            
            // Cache serialized properties
            formationProperty = serializedObject.FindProperty("Formation");
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

            switch (formation)
            {
                // Create the appropriate event type based on formation
                // If it's already the correct type, no need to change
                case SpawnFormation.Circle when waveData.FormationData is CircleFormationData:
                    return;
                // Otherwise, replace with the correct type
                case SpawnFormation.Circle:
                {
                    var newEventType = new CircleFormationData();
                    waveData.FormationData = newEventType;
                    break;
                }
                // If it's already the correct type, no need to change
                case SpawnFormation.Line when waveData.FormationData is LineFormationData:
                    return;
                // Otherwise, replace with the correct type
                case SpawnFormation.Line:
                {
                    var newEventType = new LineFormationData();
                    waveData.FormationData = newEventType;
                    break;
                }
            }
        }
        private void DrawMyGizmos(SceneView sceneView)
        {
            var waveData = (WaveSpawnEventAsset)target;
            switch (waveData.Formation)
            {
                case SpawnFormation.Circle:
                    DrawCircleFormation(waveData);
                    break;
                case SpawnFormation.Line:
                    DrawLineFormation(waveData);
                    break;
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
            // Draw a line to show the direction the enemies will move
            var direction = circleData.MovementDirection == AngularDirection.Inwards ? -1 : 1;
            
            Handles.color = Color.red;
            Handles.DrawLine(new Vector3(waveData.InitialSpawnX, waveData.InitialSpawnY + circleData.Height, 0),
                new Vector3(waveData.InitialSpawnX, waveData.InitialSpawnY + circleData.Height + direction * 5f, 0));
            Handles.color = Color.green;
            // Draw each enemy position
            for (int i = 0; i < waveData.Count; i++)
            {
                var angle = (i * 2 * Mathf.PI) / waveData.Count;
                var position = new Vector3(
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
            
            // Draw a line representing the movement direction from the initial spawn
            Handles.color = Color.red;
            var movementDirection = lineData.MovementDirection switch
            {
                Direction.Up => new Vector3(waveData.InitialSpawnX, waveData.InitialSpawnY, 0),
                Direction.Down => new Vector3(waveData.InitialSpawnX, waveData.InitialSpawnY - 5f, 0),
                Direction.Left => new Vector3(waveData.InitialSpawnX - 5f, waveData.InitialSpawnY, 0),
                Direction.Right => new Vector3(waveData.InitialSpawnX + 5f, waveData.InitialSpawnY, 0),
                _ => Vector3.zero
            };
            Handles.DrawLine(new Vector3(waveData.InitialSpawnX, waveData.InitialSpawnY, 0), movementDirection);
            
            Handles.color = Color.green;
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
}