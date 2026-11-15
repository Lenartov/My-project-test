using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voxelization
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class VoxelRevealGroup : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float revealProgress = 1f;
        [SerializeField] private Transform originTransform;
        [SerializeField, Tooltip("World-space origin used when Origin Transform is not assigned.")]
        private Vector3 revealOrigin;
        [SerializeField, Min(0.001f), Tooltip("Thickness of the assembling front, in world units.")]
        private float transitionWidth = 0.15f;
        [SerializeField, Min(0f), Tooltip("Random assembly delay and extra flight duration, in world units. Zero keeps a uniform radial front.")]
        private float revealChaos = 0.75f;
        [SerializeField, Min(0.001f)] private float revealDuration = 2f;

        private readonly List<VoxelRevealRenderer> revealRenderers = new List<VoxelRevealRenderer>();
        private bool initialized;
        private bool isRevealing;
        private float elapsed;
        private float appliedProgress;
        private float appliedWidth;
        private float appliedChaos;
        private Vector3 appliedOrigin;
        private float appliedMaximumDistance;

        public IReadOnlyList<VoxelRevealRenderer> RevealRenderers => revealRenderers;
        public float RevealProgress => revealProgress;

        private void OnEnable()
        {
            RefreshChildren();
        }

        private void OnDisable()
        {
            isRevealing = false;
        }

        private void OnValidate()
        {
            revealProgress = Mathf.Clamp01(revealProgress);
            transitionWidth = Mathf.Max(0.001f, transitionWidth);
            revealChaos = Mathf.Max(0f, revealChaos);
            revealDuration = Mathf.Max(0.001f, revealDuration);
            if (isActiveAndEnabled)
            {
                if (revealProgress != appliedProgress)
                {
                    isRevealing = false;
                }

                Synchronize(true);
            }
        }

        private void OnDidApplyAnimationProperties()
        {
            isRevealing = false;
            Synchronize(true);
        }

        private void Update()
        {
            if (isRevealing && Application.IsPlaying(gameObject))
            {
                elapsed += Time.deltaTime;
                revealProgress = Mathf.Clamp01(elapsed / revealDuration);
                if (revealProgress >= 1f)
                {
                    isRevealing = false;
                }
            }

            Synchronize(false);
        }

        [ContextMenu("Refresh Children")]
        public void RefreshChildren()
        {
            GetComponentsInChildren(true, revealRenderers);
            for (int index = revealRenderers.Count - 1; index >= 0; index--)
            {
                VoxelRevealRenderer renderer = revealRenderers[index];
                if (!renderer.IsControlledBy(this))
                {
                    revealRenderers.RemoveAt(index);
                }
                else
                {
                    renderer.RefreshGeometry();
                }
            }

            initialized = true;
            Synchronize(true);
        }

        public void SetOrigin(Vector3 worldOrigin)
        {
            originTransform = null;
            revealOrigin = worldOrigin;
            Synchronize(true);
        }

        public void SetOrigin(Transform worldOrigin)
        {
            if (worldOrigin == null)
            {
                throw new ArgumentNullException(nameof(worldOrigin));
            }

            originTransform = worldOrigin;
            revealOrigin = worldOrigin.position;
            Synchronize(true);
        }

        public void SetProgress(float progress)
        {
            isRevealing = false;
            revealProgress = Mathf.Clamp01(progress);
            Synchronize(true);
        }

        [ContextMenu("Play Reveal")]
        public void PlayReveal()
        {
            SetProgress(0f);
            elapsed = 0f;
            isRevealing = true;
        }

        [ContextMenu("Play Reveal", true)]
        private bool CanPlayReveal()
        {
            return Application.IsPlaying(gameObject) && isActiveAndEnabled;
        }

        public void PlayReveal(Vector3 worldOrigin)
        {
            SetOrigin(worldOrigin);
            PlayReveal();
        }

        public void PlayReveal(Transform worldOrigin)
        {
            SetOrigin(worldOrigin);
            PlayReveal();
        }

        [ContextMenu("Show Immediate")]
        public void ShowImmediate()
        {
            SetProgress(1f);
        }

        [ContextMenu("Hide Immediate")]
        public void HideImmediate()
        {
            SetProgress(0f);
        }

        private void Synchronize(bool force)
        {
            if (!initialized)
            {
                RefreshChildren();
                return;
            }

            Vector3 origin = originTransform != null ? originTransform.position : revealOrigin;
            float maximum = 0f;
            foreach (VoxelRevealRenderer renderer in revealRenderers)
            {
                // Destroyed children can remain in the cache until RefreshChildren is requested.
                if (renderer != null && renderer.TryGetMaximumDistance(origin, out float distance))
                {
                    maximum = Mathf.Max(maximum, distance);
                }
            }

            if (!force && appliedOrigin == origin && appliedMaximumDistance == maximum &&
                appliedWidth == transitionWidth && appliedChaos == revealChaos && appliedProgress == revealProgress)
            {
                return;
            }

            foreach (VoxelRevealRenderer renderer in revealRenderers)
            {
                if (renderer != null)
                {
                    renderer.ApplyGroupState(origin, maximum, transitionWidth, revealChaos, revealProgress);
                }
            }

            appliedOrigin = origin;
            appliedMaximumDistance = maximum;
            appliedWidth = transitionWidth;
            appliedChaos = revealChaos;
            appliedProgress = revealProgress;
        }
    }
}
