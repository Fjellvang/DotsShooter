using UnityEngine;

namespace DotsShooter
{
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;

        /// <summary>
        /// The Singleton instance
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    // If the instance is null, try to find an object of type T in the scene
                    _instance = FindFirstObjectByType<T>();

                    if (_instance == null)
                    {
                        // If no object of type T can be found in the scene, create a new instance
                        var singleton = new GameObject(typeof(T).Name);
                        _instance = singleton.AddComponent<T>();
                    }
                }

                return _instance;
            }
        }


        public virtual void Awake()
        {
            if (_instance == null)
            {
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }}