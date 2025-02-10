using System;
using System.Collections;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DotsShooter.SceneManagement
{
    public class SceneManager : MonoBehaviour
    {
        [SerializeField]
        SubScene DotsScene;

        IEnumerator Start()
        {
            if (!DotsScene.IsLoaded)
            {
                Debug.Log("Dots Scene is not loaded, loading...");
                yield return null;
            }
            Debug.Log("Dots Scene is loaded");
        }
    }
}