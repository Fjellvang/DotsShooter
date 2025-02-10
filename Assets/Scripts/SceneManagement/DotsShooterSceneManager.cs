using System.Collections;
using Unity.Scenes;
using UnityAtoms.BaseAtoms;
using UnityEngine;

namespace DotsShooter.SceneManagement
{
    public class DotsShooterSceneManager : MonoBehaviour
    {
        [SerializeField]
        SubScene DotsScene;
        
        [SerializeField]
        VoidEvent OnSceneLoaded;

        IEnumerator Start()
        {
            if (!DotsScene.IsLoaded)
            {
                Debug.Log("Dots Scene is not loaded, loading...");
                yield return null;
            }
            OnSceneLoaded.Raise();
        }
    }
}