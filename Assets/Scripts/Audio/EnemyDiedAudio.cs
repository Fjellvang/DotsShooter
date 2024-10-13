using System;
using DotsShooter.Events;
using DotsShooter.UI;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace DotsShooter.Audio
{
    public class EnemyDiedAudio : MonoBehaviour
    {
        private AudioSource[] _audioSources;
        [SerializeField]
        AudioClip deadSound;
        
        [SerializeField]
        float pitchVariance = 0.1f;
        [SerializeField]
        float originalPitch;

        private void Awake()
        {
            _audioSources = GetComponentsInChildren<AudioSource>();
        }

        private AudioSource GetNextAudioSource()
        {
            foreach (var audioSource in _audioSources)
            {
                if (!audioSource.isPlaying)
                {
                    return audioSource;
                }
            }
            return null;
        }   
        [ContextMenu("Play")]
        public void Play()
        {
            var audioSource = GetNextAudioSource();
            if (!audioSource)
            {
                return;
            }
            var variance = Random.Range(-pitchVariance, pitchVariance);
            audioSource.clip = deadSound;
            audioSource.pitch = originalPitch + variance;
            audioSource.Play();
        }

        private void OnEnable()
        {
            var eventSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<EventSystem>();
            eventSystem.OnEnemyDied += Play;
        }
        
        private void OnDisable()
        {
            if (!World.DefaultGameObjectInjectionWorld.IsCreated) return;
            var eventSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<EventSystem>();
            eventSystem.OnEnemyDied -= Play;
        }
    }
}