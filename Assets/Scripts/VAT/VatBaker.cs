using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DotsShooter.VAT
{
    #if UNITY_EDITOR
    public class VatBaker : EditorWindow
    {
        private GameObject animatedModel;
        private AnimationClip idleAnimation;
        private AnimationClip runAnimation;

        private int frameRate = 30;
        private int textureWidth = 512;
        private float uvScale = 1.0f;

        [MenuItem("Tools/VAT Baker")]
        public static void ShowWindow()
        {
            GetWindow<VatBaker>("VAT Animation Baker");
        }

        private void OnGUI()
        {
            GUILayout.Label("Skeleton to VAT Converter", EditorStyles.boldLabel);

            animatedModel =
                EditorGUILayout.ObjectField("Animated Model", animatedModel, typeof(GameObject), true) as GameObject;
            idleAnimation =
                EditorGUILayout.ObjectField("Idle Animation", idleAnimation, typeof(AnimationClip), false) as
                    AnimationClip;
            runAnimation =
                EditorGUILayout.ObjectField("Run Animation", runAnimation, typeof(AnimationClip), false) as
                    AnimationClip;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bake Settings", EditorStyles.boldLabel);
            frameRate = EditorGUILayout.IntField("Frame Rate", frameRate);
            textureWidth = EditorGUILayout.IntField("Texture Width", textureWidth);

            EditorGUILayout.Space();

            if (GUILayout.Button("Bake VAT Textures"))
            {
                if (animatedModel != null && idleAnimation != null && runAnimation != null)
                {
                    BakeAnimationsToVAT();
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Please assign all required fields.", "OK");
                }
            }
        }

        private void BakeAnimationsToVAT()
        {
            // Create a temporary instance for baking
            GameObject instance = Instantiate(animatedModel);
            Animator animator = instance.GetComponent<Animator>();

            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }

            // Bake both animations
            BakeAnimationToVAT(instance, idleAnimation, "Idle");
            BakeAnimationToVAT(instance, runAnimation, "Run");

            // Clean up
            DestroyImmediate(instance);

            EditorUtility.DisplayDialog("Baking Complete", "VAT textures have been generated successfully!", "OK");
        }

        private void BakeAnimationToVAT(GameObject target, AnimationClip clip, string animName)
        {
            // Get the SkinnedMeshRenderer
            SkinnedMeshRenderer smr = target.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null)
            {
                Debug.LogError("No SkinnedMeshRenderer found in the model.");
                return;
            }

            // Create a Playable Graph to play the animation
            PlayableGraph graph = PlayableGraph.Create("VATBaking");
            var output = AnimationPlayableOutput.Create(graph, "Animation", target.GetComponent<Animator>());
            var clipPlayable = AnimationClipPlayable.Create(graph, clip);
            output.SetSourcePlayable(clipPlayable);

            // Get source mesh data
            Mesh originalMesh = smr.sharedMesh;
            int vertexCount = originalMesh.vertexCount;

            // Calculate frames
            int frameCount = Mathf.CeilToInt(clip.length * frameRate);
            int textureHeight = Mathf.CeilToInt((float)vertexCount / textureWidth);

            // Create position and normal textures
            Texture2D positionTexture =
                new Texture2D(textureWidth, textureHeight * frameCount, TextureFormat.RGBAHalf, false);
            Texture2D normalTexture =
                new Texture2D(textureWidth, textureHeight * frameCount, TextureFormat.RGBAHalf, false);

            // Temporary mesh to capture deformed state
            Mesh bakedMesh = new Mesh();

            // Sample animation at each frame
            for (int frame = 0; frame < frameCount; frame++)
            {
                // Set animation time
                float normalizedTime = (float)frame / (float)(frameCount - 1);
                clipPlayable.SetTime(normalizedTime * clip.length);
                graph.Evaluate();

                // Bake the current pose to the mesh
                smr.BakeMesh(bakedMesh);

                Vector3[] vertices = bakedMesh.vertices;
                Vector3[] normals = bakedMesh.normals;

                // Write to textures
                for (int v = 0; v < vertexCount; v++)
                {
                    int x = v % textureWidth;
                    int y = (v / textureWidth) + (frame * textureHeight);

                    // Position map (RGB = position, A = 1)
                    positionTexture.SetPixel(x, y, new Color(vertices[v].x, vertices[v].y, vertices[v].z, 1));

                    // Normal map (RGB = normal, A = 1)
                    normalTexture.SetPixel(x, y, new Color(normals[v].x, normals[v].y, normals[v].z, 1));
                }
            }

            // Apply changes to textures
            positionTexture.Apply();
            normalTexture.Apply();

            // Save textures as assets
            string folderPath = "Assets/VAT_Textures";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            AssetDatabase.CreateAsset(bakedMesh, $"{folderPath}/{animName}_mesh.asset");
            File.WriteAllBytes($"{folderPath}/{animName}_pos.exr",
                positionTexture.EncodeToEXR(Texture2D.EXRFlags.CompressZIP));
            File.WriteAllBytes($"{folderPath}/{animName}_norm.exr",
                normalTexture.EncodeToEXR(Texture2D.EXRFlags.CompressZIP));

            // Create metadata asset with animation info
            CreateVATMetadata(animName, frameCount, textureWidth, textureHeight, vertexCount);

            // Clean up
            graph.Destroy();
            DestroyImmediate(positionTexture);
            DestroyImmediate(normalTexture);

            AssetDatabase.Refresh();
        }

        private void CreateVATMetadata(string animName, int frameCount, int textureWidth, int textureHeight,
            int vertexCount)
        {
            VATMetadata metadata = ScriptableObject.CreateInstance<VATMetadata>();
            metadata.animationName = animName;
            metadata.frameCount = frameCount;
            metadata.textureWidth = textureWidth;
            metadata.textureHeight = textureHeight;
            metadata.vertexCount = vertexCount;

            AssetDatabase.CreateAsset(metadata, $"Assets/VAT_Textures/{animName}_metadata.asset");
        }
    }

    #endif
    // Metadata ScriptableObject to store VAT information
    public class VATMetadata : ScriptableObject
    {
        public string animationName;
        public int frameCount;
        public int textureWidth;
        public int textureHeight;
        public int vertexCount;
        public float animationLength;
    }
}