﻿using System;
using DotsShooter.UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace DotsShooter.Audio
{
    public class EnemyDiedAudio : AudioClipPlayer
    {
        private void OnEnable()
        {
            if (TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnEnemyDied += Play;
            }
        }
        
        private void OnDisable()
        {
            if (TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnEnemyDied -= Play;
            }
        }
    }
}