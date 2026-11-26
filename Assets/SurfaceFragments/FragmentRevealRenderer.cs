using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFragments
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FragmentRevealRenderer : MonoBehaviour
    {
        [SerializeField] private FragmentMeshData data;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock properties;
        private Matrix4x4 rangeMatrix;
        private FragmentMeshData rangeData;
        private int rangeRevision;

        public FragmentMeshData Data => data;
        internal bool SpatialStateChanged => data != rangeData || data == null || data.Revision != rangeRevision ||
            transform.localToWorldMatrix != rangeMatrix;

        public void Initialize(FragmentMeshData meshData, Material[] materials)
        {
            data = meshData;
            GetComponent<MeshFilter>().sharedMesh = data.Mesh;
            CacheRenderer();
            meshRenderer.sharedMaterials = materials;
            rangeData = null;
        }

        private void OnEnable()
        {
            CacheRenderer();
        }

        private void CacheRenderer()
        {
            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
                properties = new MaterialPropertyBlock();
            }
        }

        internal float GetMaximumDistance(IReadOnlyList<Vector4> segments)
        {
            if (data == null)
            {
                throw new InvalidOperationException("Assign baked FragmentMeshData to " + name + ".");
            }

            rangeMatrix = transform.localToWorldMatrix;
            rangeData = data;
            rangeRevision = data.Revision;
            if (segments.Count == 0)
            {
                return 0f;
            }

            float maximum = 0f;
            foreach (Vector3 center in data.Centers)
            {
                maximum = Mathf.Max(maximum, FragmentBoundaryPath.Distance(rangeMatrix.MultiplyPoint3x4(center), segments));
            }

            return maximum;
        }

        internal void Apply(GraphicsBuffer buffer, int count, float maximumDistance, float progress,
            float motionTime, FragmentRevealSettings settings, bool parametersChanged)
        {
            CacheRenderer();
            meshRenderer.GetPropertyBlock(properties);
            properties.SetFloat("_RevealProgress", progress);
            properties.SetFloat("_MotionTime", motionTime);
            if (parametersChanged)
            {
                properties.SetBuffer("_BoundarySegments", buffer);
                properties.SetInteger("_BoundaryCount", count);
                properties.SetFloat("_RevealMaxDistance", maximumDistance);
                properties.SetFloat("_TransitionWidth", Mathf.Max(0.001f, settings.transitionWidth));
                properties.SetFloat("_RevealChaos", Mathf.Max(0f, settings.revealChaos));
                properties.SetFloat("_ScatterDistance", settings.scatterDistance);
                properties.SetFloat("_ScatterRotation", settings.scatterRotation * Mathf.Deg2Rad);
                properties.SetFloat("_WaveStrength", settings.waveStrength);
                Bounds bounds = data.Mesh.bounds;
                bounds.Expand(2f * (settings.scatterDistance * 1.4f + settings.waveStrength + 2f * data.MaximumRadius));
                meshRenderer.localBounds = bounds;
            }

            meshRenderer.SetPropertyBlock(properties);
        }

        internal void ClearOverrides()
        {
            CacheRenderer();
            meshRenderer.SetPropertyBlock(null);
            meshRenderer.ResetLocalBounds();
        }
    }
}
