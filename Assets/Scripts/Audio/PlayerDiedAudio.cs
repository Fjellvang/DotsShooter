using System;
using DotsShooter.Events;
using DotsShooter.UI;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace DotsShooter.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class PlayerDiedAudio : MonoBehaviour
    {
        private AudioSource _audioSource;
        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        [ContextMenu("Play")]
        public void Play()
        {
            _audioSource.Play();
        }

        private void OnEnable()
        {
            var eventSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<EventSystem>();
            eventSystem.OnPlayerDied += (x) => Play();
        }
    }
}