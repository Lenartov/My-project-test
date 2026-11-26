using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFragments
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FragmentRevealGroup : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float revealProgress = 1f;
        [SerializeField] private FragmentRevealSettings settings = new FragmentRevealSettings();
        [SerializeField, Tooltip("Ordered points in this island's local space. Each path is independent; height is ignored.")]
        private List<FragmentBoundaryPath> boundaryPaths = new List<FragmentBoundaryPath>();

        private readonly List<FragmentRevealRenderer> renderers = new List<FragmentRevealRenderer>();
        private readonly List<Vector4> segments = new List<Vector4>();
        private readonly List<Vector4> pendingSegments = new List<Vector4>();
        private readonly List<Vector4> bufferData = new List<Vector4>();
        private readonly List<Vector4> coverageData = new List<Vector4>();
        private readonly List<Vector4> rangeSupport = new List<Vector4>();
        private IReadOnlyList<float[]> coverageProfiles;
        private List<Vector2> heldFronts = new List<Vector2>();
        private bool bufferDirty = true;
        private GraphicsBuffer boundaryBuffer;
        private float maximumDistance;
        private bool dirty = true;
        private bool rangeDirty = true;
        private float appliedProgress;
        private bool playing;
        private float animationStart;
        private float animationTarget;
        private float elapsed;
        #if UNITY_EDITOR
        private double lastPreviewTick;
        #endif

        public float RevealProgress => revealProgress;
        public float RevealDuration => settings.revealDuration;
        public int BoundarySegmentCount => segments.Count;
        public IReadOnlyList<FragmentRevealRenderer> Renderers => renderers;
        public IReadOnlyList<FragmentBoundaryPath> BoundaryPaths => boundaryPaths;

        public void Configure(IReadOnlyList<FragmentBoundaryPath> paths, FragmentRevealSettings revealSettings)
        {
            settings = revealSettings.Copy();
            SetBoundaryPaths(paths);
        }

        public void SetBoundaryPaths(IReadOnlyList<FragmentBoundaryPath> paths)
        {
            ReplacePaths(paths, false);
        }

        public void SetBoundaryPathsPreservingReveal(IReadOnlyList<FragmentBoundaryPath> paths)
        {
            if (isActiveAndEnabled) Synchronize();
            ReplacePaths(paths, true);
        }

        private void ReplacePaths(IReadOnlyList<FragmentBoundaryPath> paths, bool preserve)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            List<FragmentBoundaryPath> replacement = new List<FragmentBoundaryPath>();
            foreach (FragmentBoundaryPath path in paths)
            {
                replacement.Add(path.Copy());
            }

            List<Vector4> oldLocal = new List<Vector4>();
            List<Vector4> nextLocal = new List<Vector4>();
            FragmentBoundaryPath.Pack(boundaryPaths, Matrix4x4.identity, oldLocal);
            FragmentBoundaryPath.Pack(replacement, Matrix4x4.identity, nextLocal);
            float front = (maximumDistance + Mathf.Max(0.001f, settings.transitionWidth) + 2f * Mathf.Max(0f, settings.revealChaos)) * appliedProgress;
            heldFronts = preserve ? FragmentRevealFrontMemory.Remap(oldLocal, heldFronts, nextLocal, front, appliedProgress) : new List<Vector2>();
            boundaryPaths = replacement;
            coverageProfiles = null;
            bufferDirty = true;

            dirty = true;
            rangeDirty = true;
        }

        internal void SetRevealRangeSupport(IReadOnlyList<Vector4> localSegments)
        {
            rangeSupport.Clear();
            rangeSupport.AddRange(localSegments);
            rangeDirty = true;
        }

        internal void SetBoundaryCoverage(IReadOnlyList<float[]> profiles)
        {
            FragmentBoundaryPath.Pack(boundaryPaths, transform.localToWorldMatrix, pendingSegments);
            if (profiles.Count != pendingSegments.Count)
                throw new ArgumentException("Supply one coverage profile per boundary segment.");
            foreach (float[] profile in profiles)
                if (profile == null || profile.Length < 2) throw new ArgumentException("Coverage profiles need at least two samples.");
            coverageProfiles = profiles;
            bufferDirty = true;
            if (isActiveAndEnabled) Synchronize();
        }

        internal void RefreshBoundaryCoverage()
        {
            coverageData.Clear();
            foreach (float[] profile in coverageProfiles)
                foreach (float value in profile) coverageData.Add(new Vector4(value, 0f, 0f, 0f));
            boundaryBuffer.SetData(coverageData, 0, segments.Count * 3, coverageData.Count);
        }

        internal float GetRevealAtWorldPoint(Vector3 point)
        {
            return FragmentBoundaryCoverage.Evaluate(new Vector2(point.x, point.z), segments, heldFronts,
                coverageProfiles, revealProgress, maximumDistance, settings.transitionWidth, settings.revealChaos);
        }

        public void SetProgress(float progress)
        {
            playing = false;
            revealProgress = Mathf.Clamp01(progress);
            if (isActiveAndEnabled)
            {
                Synchronize();
            }
        }

        [ContextMenu("Show Immediate")]
        public void ShowImmediate() => SetProgress(1f);

        [ContextMenu("Hide Immediate")]
        public void HideImmediate() => SetProgress(0f);

        [ContextMenu("Play Reveal")]
        public void PlayReveal() => PlayToProgress(1f);

        [ContextMenu("Play Hide")]
        public void PlayHide() => PlayToProgress(0f);

        public void PlayToProgress(float target)
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled)
            {
                throw new InvalidOperationException("Playback requires an enabled group in Play Mode. Use the slider for Edit Mode preview.");
            }

            animationStart = revealProgress;
            animationTarget = Mathf.Clamp01(target);
            elapsed = 0f;
            playing = true;
        }

        [ContextMenu("Refresh Children")]
        public void RefreshChildren()
        {
            GetComponentsInChildren(true, renderers);
            for (int index = renderers.Count - 1; index >= 0; index--)
            {
                FragmentRevealGroup owner = renderers[index].GetComponentInParent<FragmentRevealGroup>(true);
                if (owner != this)
                {
                    renderers.RemoveAt(index);
                }
            }

            rangeDirty = true;
            dirty = true;
        }

        private void OnEnable()
        {
            RefreshChildren();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.update += EditorTick;
            #endif
        }

        private void OnDisable()
        {
            playing = false;
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
            #endif
            foreach (FragmentRevealRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.ClearOverrides();
                }
            }

            boundaryBuffer?.Dispose();
            boundaryBuffer = null;
            dirty = true;
            rangeDirty = true;
        }

        private void OnValidate()
        {
            revealProgress = Mathf.Clamp01(revealProgress);
            if (revealProgress != appliedProgress)
            {
                playing = false;
            }

            dirty = true;
            heldFronts.Clear();
            bufferDirty = true;
        }

        private void OnDidApplyAnimationProperties()
        {
            playing = false;
            dirty = true;
        }

        private void LateUpdate()
        {
            if (playing)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, settings.revealDuration));
                revealProgress = Mathf.Lerp(animationStart, animationTarget, t);
                playing = t < 1f;
            }

            Synchronize();
        }

        #if UNITY_EDITOR
        private void EditorTick()
        {
            if (Application.IsPlaying(gameObject) || (revealProgress <= 0f || revealProgress >= 1f) && !dirty)
            {
                return;
            }

            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now - lastPreviewTick < 1.0 / 30.0)
            {
                return;
            }

            lastPreviewTick = now;
            Synchronize();
            UnityEditor.SceneView.RepaintAll();
        }
        #endif

        private void Synchronize()
        {
            FragmentBoundaryPath.Pack(boundaryPaths, transform.localToWorldMatrix, pendingSegments);
            bool changed = segments.Count != pendingSegments.Count;
            if (!changed)
            {
                for (int index = 0; index < segments.Count; index++)
                {
                    changed |= segments[index] != pendingSegments[index];
                }
            }

            if (changed || boundaryBuffer == null || bufferDirty)
            {
                segments.Clear();
                segments.AddRange(pendingSegments);
                if (heldFronts.Count != segments.Count)
                {
                    heldFronts = new List<Vector2>(segments.Count);
                    for (int index = 0; index < segments.Count; index++) heldFronts.Add(Vector2.zero);
                }

                bufferData.Clear();
                int profileOffset = segments.Count * 3;
                for (int index = 0; index < segments.Count; index++)
                {
                    bufferData.Add(segments[index]);
                    bufferData.Add(new Vector4(heldFronts[index].x, heldFronts[index].y, 0f, 0f));
                    int length = coverageProfiles != null ? coverageProfiles[index].Length : 0;
                    bufferData.Add(new Vector4(profileOffset, length, 0f, 0f));
                    profileOffset += length;
                }

                if (coverageProfiles != null)
                    foreach (float[] profile in coverageProfiles)
                        foreach (float value in profile) bufferData.Add(new Vector4(value, 0f, 0f, 0f));

                int count = Mathf.Max(1, bufferData.Count);
                if (boundaryBuffer == null || boundaryBuffer.count < count)
                {
                    boundaryBuffer?.Dispose();
                    boundaryBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, 16);
                }

                if (segments.Count > 0)
                {
                    boundaryBuffer.SetData(bufferData);
                }

                bufferDirty = false;
                rangeDirty = true;
            }

            foreach (FragmentRevealRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    rangeDirty |= renderer.SpatialStateChanged;
                }
            }

            if (rangeDirty)
            {
                maximumDistance = 0f;
                foreach (FragmentRevealRenderer renderer in renderers)
                {
                    if (renderer != null)
                    {
                        maximumDistance = Mathf.Max(maximumDistance, renderer.GetMaximumDistance(segments));
                    }
                }

                maximumDistance = Mathf.Max(maximumDistance,
                    FragmentBoundaryCoverage.SupportDistance(rangeSupport, transform.localToWorldMatrix, segments));

                dirty = true;
                rangeDirty = false;
            }

            bool partial = revealProgress > 0f && revealProgress < 1f;
            if (!dirty && appliedProgress == revealProgress && !partial)
            {
                return;
            }

            #if UNITY_EDITOR
            float time = Application.IsPlaying(gameObject) ? Time.time : (float)UnityEditor.EditorApplication.timeSinceStartup;
            #else
            float time = Time.time;
            #endif
            foreach (FragmentRevealRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.Apply(boundaryBuffer, segments.Count, maximumDistance, revealProgress, time, settings, dirty);
                }
            }

            appliedProgress = revealProgress;
            dirty = false;
        }

        private void OnDrawGizmosSelected()
        {
            List<Vector4> lines = new List<Vector4>();
            FragmentBoundaryPath.Pack(boundaryPaths, transform.localToWorldMatrix, lines);
            Color previous = Gizmos.color;
            Gizmos.color = Color.cyan;
            foreach (Vector4 segment in lines)
            {
                Gizmos.DrawLine(new Vector3(segment.x, transform.position.y, segment.y),
                    new Vector3(segment.z, transform.position.y, segment.w));
            }

            Gizmos.color = previous;
        }
    }
}
