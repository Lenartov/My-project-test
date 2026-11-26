using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFragments
{
    public sealed class FragmentMeshData : ScriptableObject
    {
        [SerializeField] private Mesh mesh;
        [SerializeField] private Vector3[] centers = Array.Empty<Vector3>();
        [SerializeField] private float maximumRadius;
        [SerializeField] private int revision;

        public Mesh Mesh => mesh;
        public IReadOnlyList<Vector3> Centers => centers;
        public float MaximumRadius => maximumRadius;
        public int Revision => revision;

        public void Initialize(Mesh generatedMesh, Vector3[] fragmentCenters, float radius, int version)
        {
            mesh = generatedMesh;
            centers = fragmentCenters;
            maximumRadius = radius;
            revision = version;
        }
    }
}
