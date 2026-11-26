using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFragments.Editor
{
    public sealed class FragmentMeshCacheLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string sourceKey;
            public float cellSize;
            public string fingerprint;
            public Mesh mesh;
            public FragmentMeshData data;
        }

        [SerializeField, HideInInspector] private List<Entry> entries = new List<Entry>();
        public List<Entry> Entries => entries;
    }
}
