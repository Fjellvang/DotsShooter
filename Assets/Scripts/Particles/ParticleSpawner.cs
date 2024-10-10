using UnityEngine;

namespace DotsShooter.Particles
{
    public class ParticleSpawner : MonoBehaviour
    {
        public ParticleSystem particleSystem;
        
        public void Spawn(Vector3 position)
        {
            particleSystem.transform.position = position;
            particleSystem.Play();
        }
    }
}