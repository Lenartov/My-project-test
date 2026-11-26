using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFragments
{
    public sealed class FragmentIslandBakeRecord : ScriptableObject
    {
        [Serializable]
        public sealed class GeometryBinding
        {
            public string key;
            public Mesh mesh;
            public FragmentMeshData data;
        }

        [Serializable]
        public sealed class MaterialBinding
        {
            public string key;
            public Material material;
        }

        [SerializeField] private string sourceKey;
        [SerializeField] private string sourceGuid;
        [SerializeField] private string outputFolder;
        [SerializeField] private GameObject generatedPrefab;
        // Legacy island-owned outputs stay linked; shared outputs have separate ownership.
        [SerializeField] private List<GeometryBinding> geometry = new List<GeometryBinding>();
        [SerializeField] private List<GeometryBinding> sharedGeometry = new List<GeometryBinding>();
        [SerializeField] private List<MaterialBinding> materials = new List<MaterialBinding>();

        public string SourceKey => sourceKey;
        public string SourceGuid => sourceGuid;
        public string OutputFolder => outputFolder;
        public GameObject GeneratedPrefab => generatedPrefab;
        public List<GeometryBinding> Geometry => geometry;
        public List<GeometryBinding> SharedGeometry => sharedGeometry;
        public List<MaterialBinding> Materials => materials;

        public void Initialize(string key, string guid, string folder)
        {
            sourceKey = key;
            sourceGuid = guid;
            outputFolder = folder;
        }

        public void SetGeneratedPrefab(GameObject prefab) => generatedPrefab = prefab;
    }
}
