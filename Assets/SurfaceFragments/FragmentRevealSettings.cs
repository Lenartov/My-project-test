using System;
using UnityEngine;

namespace SurfaceFragments
{
    [Serializable]
    public sealed class FragmentRevealSettings
    {
        [Min(0.001f)] public float transitionWidth = 0.4f;
        [Min(0f)] public float revealChaos = 0.8f;
        [Min(0f)] public float scatterDistance = 2f;
        [Range(0f, 180f)] public float scatterRotation = 120f;
        [Min(0f)] public float waveStrength = 0.2f;
        [Min(0.001f)] public float revealDuration = 2f;

        public FragmentRevealSettings Copy()
        {
            return new FragmentRevealSettings
            {
                transitionWidth = transitionWidth, revealChaos = revealChaos, scatterDistance = scatterDistance,
                scatterRotation = scatterRotation, waveStrength = waveStrength, revealDuration = revealDuration
            };
        }
    }
}
