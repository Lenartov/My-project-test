using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voxelization
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class VoxelBoundaryRevealGroup : MonoBehaviour
    {
        public const int MaximumBoundaryCount = 16;

        [Serializable]
        public sealed class Boundary
        {
            public Transform start;
            public Transform end;
        }

        [SerializeField, Range(0f, 1f)] private float revealProgress = 1f;
        [SerializeField, Tooltip("One to sixteen world-space line segments. Height is ignored; each segment needs distinct XZ endpoints.")]
        private List<Boundary> boundaries = new List<Boundary>();
        [SerializeField, Min(0.001f), Tooltip("Thickness of the assembling front, in world units.")]
        private float transitionWidth = 0.15f;
        [SerializeField, Min(0f), Tooltip("Random assembly delay and extra flight duration, in world units. Zero keeps uniform fronts from the boundaries.")]
        private float revealChaos = 0.75f;
        [SerializeField, Min(0.001f)] private float revealDuration = 2f;

        private readonly List<VoxelRevealRenderer> revealRenderers = new List<VoxelRevealRenderer>();
        private bool initialized;
        private bool isRevealing;
        private float elapsed;
        private float appliedProgress;
        private float appliedWidth;
        private float appliedChaos;
        private readonly Vector4[] boundarySegments = new Vector4[MaximumBoundaryCount];
        private readonly Vector4[] pendingSegments = new Vector4[MaximumBoundaryCount];
        private int boundaryCount;
        private string configurationError;
        private float appliedMaximumDistance;

        public IReadOnlyList<VoxelRevealRenderer> RevealRenderers => revealRenderers;
        public float RevealProgress => revealProgress;
        public float RevealDuration => revealDuration;
        public string ConfigurationError => configurationError;

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

        public void SetBoundaries(IReadOnlyList<Boundary> sources)
        {
            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            List<Boundary> replacement = new List<Boundary>(sources);
            if (!TryPackBoundaries(replacement, pendingSegments, out string error))
            {
                throw new ArgumentException(error, nameof(sources));
            }

            boundaries = replacement;
            Synchronize(true);
        }

        public void SetProgress(float progress)
        {
            if (!TryPackBoundaries(boundaries, pendingSegments, out string error))
            {
                throw new InvalidOperationException(error);
            }

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

            if (!TryPackBoundaries(boundaries, pendingSegments, out configurationError))
            {
                // Incomplete endpoints are expected while setting up the component in the Inspector.
                if (isRevealing)
                {
                    isRevealing = false;
                    Debug.LogError(configurationError, this);
                }

                return;
            }

            bool boundariesChanged = boundaryCount != boundaries.Count;
            for (int index = 0; index < boundaries.Count; index++)
            {
                if (boundarySegments[index] != pendingSegments[index])
                {
                    boundariesChanged = true;
                }
            }

            if (boundariesChanged)
            {
                Array.Copy(pendingSegments, boundarySegments, MaximumBoundaryCount);
                boundaryCount = boundaries.Count;
            }

            float maximum = 0f;
            foreach (VoxelRevealRenderer renderer in revealRenderers)
            {
                if (renderer != null && renderer.TryGetMaximumBoundaryDistance(boundarySegments, boundaryCount, out float distance))
                {
                    maximum = Mathf.Max(maximum, distance);
                }
            }

            if (!force && !boundariesChanged && appliedMaximumDistance == maximum &&
                appliedWidth == transitionWidth && appliedChaos == revealChaos && appliedProgress == revealProgress)
            {
                return;
            }

            foreach (VoxelRevealRenderer renderer in revealRenderers)
            {
                if (renderer != null)
                {
                    renderer.ApplyBoundaryGroupState(boundarySegments, boundaryCount, maximum,
                        transitionWidth, revealChaos, revealProgress);
                }
            }

            appliedMaximumDistance = maximum;
            appliedWidth = transitionWidth;
            appliedChaos = revealChaos;
            appliedProgress = revealProgress;
        }

        private bool TryPackBoundaries(IReadOnlyList<Boundary> sources, Vector4[] segments, out string error)
        {
            if (GetComponent<VoxelRevealGroup>() != null)
            {
                error = "Use either VoxelRevealGroup or VoxelBoundaryRevealGroup on this parent, not both.";
                return false;
            }

            if (sources.Count == 0 || sources.Count > MaximumBoundaryCount)
            {
                error = "Assign between 1 and " + MaximumBoundaryCount + " boundaries.";
                return false;
            }

            for (int index = 0; index < sources.Count; index++)
            {
                Boundary boundary = sources[index];
                if (boundary == null || boundary.start == null || boundary.end == null)
                {
                    error = "Boundary " + (index + 1) + " requires Start and End transforms.";
                    return false;
                }

                Vector3 start = boundary.start.position;
                Vector3 end = boundary.end.position;
                Vector2 direction = new Vector2(end.x - start.x, end.z - start.z);
                if (direction.sqrMagnitude <= 0.00000001f)
                {
                    error = "Boundary " + (index + 1) + " must have distinct endpoints in the XZ plane.";
                    return false;
                }

                segments[index] = new Vector4(start.x, start.z, end.x, end.z);
            }

            error = null;
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Color previousColor = Gizmos.color;
            Gizmos.color = Color.cyan;
            foreach (Boundary boundary in boundaries)
            {
                if (boundary != null && boundary.start != null && boundary.end != null)
                {
                    Vector3 start = boundary.start.position;
                    Vector3 end = boundary.end.position;
                    start.y = transform.position.y;
                    end.y = transform.position.y;
                    Gizmos.DrawLine(start, end);
                    Gizmos.DrawWireSphere(start, 0.1f);
                    Gizmos.DrawWireSphere(end, 0.1f);
                }
            }

            Gizmos.color = previousColor;
        }
    }
}
