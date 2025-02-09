using System.Diagnostics.CodeAnalysis;
using DotsShooter.Events;
using JetBrains.Annotations;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Audio
{
    public abstract class AudioClipPlayer : MonoBehaviour
    {
        private AudioSource[] _audioSources;
        [SerializeField]
        AudioClip clip;
        
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
            audioSource.clip = clip;
            audioSource.pitch = originalPitch + variance;
            audioSource.Play();
        } 
        

    }
}