using System;
using DotsShooter.Events;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace DotsShooter.Particles
{
    public class GameObjectSpawner : MonoBehaviour
    {
        [FormerlySerializedAs("gameObject")] 
        public GameObject objectToSpawn;
        
        public void Spawn(Vector3 position)
        {
            Instantiate(objectToSpawn, position, Quaternion.identity);
        }
        
        public void Spawn(float3 position)
        {
            Spawn(new Vector3(position.x, position.y, position.z));
        }

        public void OnEnable()
        {
            var eventSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<EventSystem>();
            eventSystem.OnPlayerDied += Spawn;
        }
    }
}