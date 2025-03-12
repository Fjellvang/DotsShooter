using UnityEditor;
using UnityEngine.SceneManagement;

namespace DotsShooter.SceneManagement
{
    public class SceneLoader : Singleton<SceneLoader>
    {
        public SceneAsset MetaplayConnectionScene;

        private void Start()
        {
            SceneManager.LoadScene(MetaplayConnectionScene.name, LoadSceneMode.Additive);
        }
    }
}