using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voxelization
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public sealed class VoxelRevealRenderer : MonoBehaviour
    {
        private static readonly int RevealProgressId = Shader.PropertyToID("_RevealProgress");
        private static readonly int RevealOriginId = Shader.PropertyToID("_RevealOrigin");
        private static readonly int ScatterDistanceId = Shader.PropertyToID("_ScatterDistance");
        private static readonly int WaveStrengthId = Shader.PropertyToID("_WaveStrength");
        private static readonly int TransitionWidthId = Shader.PropertyToID("_TransitionWidth");
        private static readonly int RevealChaosId = Shader.PropertyToID("_RevealChaos");
        private static readonly int ColorIntensityId = Shader.PropertyToID("_ColorIntensity");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int RevealMaxDistanceId = Shader.PropertyToID("_RevealMaxDistance");
        private static readonly int RevealBoundaryCountId = Shader.PropertyToID("_RevealBoundaryCount");
        private static readonly int RevealBoundariesId = Shader.PropertyToID("_RevealBoundaries");

        [SerializeField, Range(0f, 1f)] private float revealProgress = 1f;
        [SerializeField, Tooltip("World-space origin for standalone rendering. A group supplies its own origin.")]
        private Vector3 revealOrigin;
        [SerializeField, Min(0f)] private float scatterDistance = 1f;
        [SerializeField, Min(0f)] private float waveStrength = 0.1f;
        [SerializeField, Min(0.001f), Tooltip("Transition width in world units. A group supplies its own width.")]
        private float transitionWidth = 0.15f;
        [SerializeField, Min(0f), Tooltip("Assembly randomness in world units. A group supplies its own chaos setting.")]
        private float revealChaos = 0.75f;
        [SerializeField, Range(0f, 2f)] private float colorIntensity = 1f;
        [SerializeField, Range(0f, 2f)] private float saturation = 1f;

        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private MaterialPropertyBlock propertyBlock;
        private readonly List<Vector3> voxelCenters = new List<Vector3>();
        private Mesh cachedMesh;
        private Matrix4x4 cachedMatrix;
        private Vector3 cachedOrigin;
        private float cachedMaximumDistance;
        private bool distanceRangeValid;
        private bool useGroupRange;
        private Vector3 groupOrigin;
        private float groupMaximumDistance;
        private float groupTransitionWidth;
        private float groupRevealChaos;
        private Vector4[] groupBoundaries;
        private int groupBoundaryCount;
        private readonly Vector4[] cachedBoundarySegments = new Vector4[VoxelBoundaryRevealGroup.MaximumBoundaryCount];
        private int cachedBoundaryCount;
        private Matrix4x4 cachedBoundaryMatrix;
        private float cachedBoundaryMaximum;
        private bool boundaryDistanceValid;

        public float RevealProgress => revealProgress;

        public void SetProgress(float progress)
        {
            revealProgress = Mathf.Clamp01(progress);
            ApplyProperties();
        }

        public void SetOrigin(Vector3 worldOrigin)
        {
            revealOrigin = worldOrigin;
            useGroupRange = false;
            ApplyProperties();
        }

        public void ShowImmediate()
        {
            SetProgress(1f);
        }

        public void HideImmediate()
        {
            SetProgress(0f);
        }

        private void OnEnable()
        {
            ApplyProperties();
        }

        private void OnDisable()
        {
            if (meshRenderer != null)
            {
                meshRenderer.ResetLocalBounds();
            }
        }

        private void OnDidApplyAnimationProperties()
        {
            ApplyProperties();
        }

        private void OnValidate()
        {
            revealProgress = Mathf.Clamp01(revealProgress);
            scatterDistance = Mathf.Max(0f, scatterDistance);
            waveStrength = Mathf.Max(0f, waveStrength);
            transitionWidth = Mathf.Max(0.001f, transitionWidth);
            revealChaos = Mathf.Max(0f, revealChaos);
            colorIntensity = Mathf.Clamp(colorIntensity, 0f, 2f);
            saturation = Mathf.Clamp(saturation, 0f, 2f);
            ApplyProperties();
        }

        internal void RefreshGeometry()
        {
            cachedMesh = null;
            distanceRangeValid = false;
            boundaryDistanceValid = false;
        }

        private bool TryCacheVoxelCenters()
        {
            CacheComponents();
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                return false;
            }

            if (cachedMesh != mesh)
            {
                if (!mesh.isReadable)
                {
                    throw new InvalidOperationException("Voxel reveal requires a readable generated mesh: " + mesh.name);
                }

                mesh.GetUVs(1, voxelCenters);
                if (voxelCenters.Count == 0 || voxelCenters.Count != mesh.vertexCount || voxelCenters.Count % 24 != 0)
                {
                    throw new InvalidOperationException("Voxel reveal requires an Animation Ready mesh with voxel centers in UV1: " + mesh.name);
                }

                // The generator writes 24 vertices with identical metadata for each cube.
                int centerCount = voxelCenters.Count / 24;
                for (int index = 0; index < centerCount; index++)
                {
                    voxelCenters[index] = voxelCenters[index * 24];
                }

                voxelCenters.RemoveRange(centerCount, voxelCenters.Count - centerCount);
                voxelCenters.TrimExcess();
                cachedMesh = mesh;
                distanceRangeValid = false;
                boundaryDistanceValid = false;
            }

            return true;
        }

        internal bool TryGetMaximumDistance(Vector3 worldOrigin, out float maximumDistance)
        {
            if (!TryCacheVoxelCenters())
            {
                maximumDistance = 0f;
                return false;
            }

            Matrix4x4 matrix = transform.localToWorldMatrix;
            if (!distanceRangeValid || cachedOrigin != worldOrigin || cachedMatrix != matrix)
            {
                float maximumSqr = 0f;
                foreach (Vector3 center in voxelCenters)
                {
                    float distanceSqr = (matrix.MultiplyPoint3x4(center) - worldOrigin).sqrMagnitude;
                    maximumSqr = Mathf.Max(maximumSqr, distanceSqr);
                }

                cachedMaximumDistance = Mathf.Sqrt(maximumSqr);
                cachedOrigin = worldOrigin;
                cachedMatrix = matrix;
                distanceRangeValid = true;
            }

            maximumDistance = cachedMaximumDistance;
            return true;
        }

        internal bool TryGetMaximumBoundaryDistance(Vector4[] segments, int count, out float maximumDistance)
        {
            if (!TryCacheVoxelCenters())
            {
                maximumDistance = 0f;
                return false;
            }

            Matrix4x4 matrix = transform.localToWorldMatrix;
            bool changed = !boundaryDistanceValid || cachedBoundaryCount != count || cachedBoundaryMatrix != matrix;
            for (int index = 0; index < count; index++)
            {
                if (cachedBoundarySegments[index] != segments[index])
                {
                    changed = true;
                }
            }

            if (changed)
            {
                float maximumSqr = 0f;
                foreach (Vector3 center in voxelCenters)
                {
                    Vector3 worldCenter = matrix.MultiplyPoint3x4(center);
                    Vector2 point = new Vector2(worldCenter.x, worldCenter.z);
                    float minimumSqr = float.PositiveInfinity;
                    for (int index = 0; index < count; index++)
                    {
                        Vector4 segment = segments[index];
                        Vector2 start = new Vector2(segment.x, segment.y);
                        Vector2 direction = new Vector2(segment.z - segment.x, segment.w - segment.y);
                        float projection = Mathf.Clamp01(Vector2.Dot(point - start, direction) / direction.sqrMagnitude);
                        float distanceSqr = (point - start - direction * projection).sqrMagnitude;
                        minimumSqr = Mathf.Min(minimumSqr, distanceSqr);
                    }

                    maximumSqr = Mathf.Max(maximumSqr, minimumSqr);
                }

                cachedBoundaryMaximum = Mathf.Sqrt(maximumSqr);
                Array.Copy(segments, cachedBoundarySegments, count);
                cachedBoundaryCount = count;
                cachedBoundaryMatrix = matrix;
                boundaryDistanceValid = true;
            }

            maximumDistance = cachedBoundaryMaximum;
            return true;
        }

        internal bool IsControlledBy(Component group)
        {
            for (Transform parent = transform; parent != null; parent = parent.parent)
            {
                if (parent.TryGetComponent(out VoxelRevealGroup radialGroup))
                {
                    return parent != transform && radialGroup == group;
                }

                if (parent.TryGetComponent(out VoxelBoundaryRevealGroup boundaryGroup))
                {
                    return parent != transform && boundaryGroup == group;
                }
            }

            return false;
        }

        internal void ApplyBoundaryGroupState(Vector4[] segments, int count, float maximumDistance,
            float width, float chaos, float progress)
        {
            groupBoundaries = segments;
            groupBoundaryCount = count;
            useGroupRange = true;
            groupMaximumDistance = maximumDistance;
            groupTransitionWidth = width;
            groupRevealChaos = chaos;
            SetProgress(progress);
        }

        internal void ApplyGroupState(Vector3 worldOrigin, float maximumDistance, float width, float chaos, float progress)
        {
            groupBoundaryCount = 0;
            useGroupRange = true;
            groupOrigin = worldOrigin;
            groupMaximumDistance = maximumDistance;
            groupTransitionWidth = width;
            groupRevealChaos = chaos;
            SetProgress(progress);
        }

        private void CacheComponents()
        {
            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }

            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }
        }

        private void ApplyProperties()
        {
            CacheComponents();
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            Vector3 origin = useGroupRange ? groupOrigin : revealOrigin;
            float maximumDistance = groupMaximumDistance;
            if (!useGroupRange)
            {
                TryGetMaximumDistance(origin, out maximumDistance);
            }

            float width = useGroupRange ? groupTransitionWidth : transitionWidth;
            float chaos = useGroupRange ? groupRevealChaos : revealChaos;
            float chaosBlend = Mathf.Clamp01(chaos / Mathf.Max(width, 0.001f));
            Bounds animatedBounds = mesh.bounds;
            animatedBounds.Expand(2f * (scatterDistance * (1f + 0.4f * chaosBlend) + waveStrength));
            meshRenderer.localBounds = animatedBounds;

            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(RevealProgressId, revealProgress);
            propertyBlock.SetVector(RevealOriginId, origin);
            propertyBlock.SetFloat(ScatterDistanceId, scatterDistance);
            propertyBlock.SetFloat(WaveStrengthId, waveStrength);
            propertyBlock.SetFloat(TransitionWidthId, width);
            propertyBlock.SetFloat(RevealChaosId, chaos);
            propertyBlock.SetFloat(ColorIntensityId, colorIntensity);
            propertyBlock.SetFloat(SaturationId, saturation);
            propertyBlock.SetFloat(RevealMaxDistanceId, maximumDistance);
            int boundaryCount = useGroupRange ? groupBoundaryCount : 0;
            propertyBlock.SetInteger(RevealBoundaryCountId, boundaryCount);
            if (boundaryCount > 0)
            {
                propertyBlock.SetVectorArray(RevealBoundariesId, groupBoundaries);
            }

            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
