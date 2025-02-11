// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Client;
using Newtonsoft.Json;
using System;
using UnityEditor;
using UnityEngine;

namespace Metaplay.Unity
{
    public class EnvironmentConfigEditorConfigWrapper : ScriptableObject
    {
        [SerializeReference] public EnvironmentConfig[] Environments;
    }

    [CustomEditor(typeof(EnvironmentConfigEditorConfigWrapper))]
    public class EnvironmentConfigEditorConfigWrapperEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            // Hide the "Script:" field
            DrawPropertiesExcluding(serializedObject, new string[] {"m_Script"});

            // Handle duplicated array elements, because array elements are duplicated by reference by default because of the SerializeReference attribute
            EnvironmentConfigEditorConfigWrapper targetObj = (EnvironmentConfigEditorConfigWrapper)serializedObject.targetObject;
            int currentIndex = 0;
            foreach (EnvironmentConfig env in targetObj.Environments)
            {
                for (int i = 0; i < targetObj.Environments.Length; i++)
                {
                    if (currentIndex == i) continue;

                    if (ReferenceEquals(env, targetObj.Environments[i]))
                    {
                        string jsonObj               = JsonConvert.SerializeObject(env);
                        Type   environmentConfigType = IntegrationRegistry.GetSingleIntegrationType(typeof(EnvironmentConfig));
                        targetObj.Environments[i]    = (EnvironmentConfig)JsonConvert.DeserializeObject(jsonObj, environmentConfigType);
                    }
                }

                currentIndex++;
            }

            // Ignore any changes to fields that are not backed by a field/property (e.g. expanders)
            GUI.changed = serializedObject.ApplyModifiedProperties();
        }
    }
}
