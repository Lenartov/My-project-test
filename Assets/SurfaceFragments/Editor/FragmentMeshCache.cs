using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SurfaceFragmentPrototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SurfaceFragments.Editor
{
    internal sealed class FragmentMeshCache : IDisposable
    {
        internal sealed class PreparedGeometry
        {
            public string key;
            public FragmentMeshCacheLibrary.Entry entry;
            public string fingerprint;
            public CellFragmentBuilder.Result result;
            public FragmentMeshData Data => entry.data;
            public int SubMeshCount => result != null ? result.mesh.subMeshCount : Data.Mesh.subMeshCount;
        }

        internal const string Root = "Assets/Generated/FragmentLibrary";
        private const string LibraryPath = Root + "/Editor/Cache.asset";
        // Increment when CellFragmentBuilder's output algorithm changes.
        private const int AlgorithmVersion = 1;
        private FragmentMeshCacheLibrary library;
        private readonly Dictionary<string, FragmentMeshCacheLibrary.Entry> entries = new Dictionary<string, FragmentMeshCacheLibrary.Entry>(StringComparer.Ordinal);
        private readonly List<PreparedGeometry> prepared = new List<PreparedGeometry>();
        public int ReusedCount { get; private set; }
        public int RebuiltCount => prepared.Count - ReusedCount;

        public FragmentMeshCache()
        {
            library = AssetDatabase.LoadAssetAtPath<FragmentMeshCacheLibrary>(LibraryPath);
            if (library == null)
            {
                if (Occupied(LibraryPath)) throw new IOException("Shared mesh cache path is occupied by an unrelated asset: " + LibraryPath);
                return;
            }

            foreach (FragmentMeshCacheLibrary.Entry entry in library.Entries)
            {
                string key = SlotKey(entry.sourceKey, entry.cellSize);
                ValidateEntry(entry, key);
                if (!entries.TryAdd(key, entry)) throw new InvalidOperationException("Duplicate shared mesh cache slot: " + key);
            }
        }

        public PreparedGeometry Prepare(Mesh source, float cellSize, Func<float, bool> reportProgress)
        {
            if (!float.IsFinite(cellSize) || cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId) || string.IsNullOrEmpty(guid))
                throw new ArgumentException("Save the source Mesh as an asset before using the shared fragment cache: " + source.name);
            string sourceKey = guid + ":" + localId.ToString(CultureInfo.InvariantCulture);
            string key = SlotKey(sourceKey, cellSize);
            string fingerprint = Fingerprint(source);
            entries.TryGetValue(key, out FragmentMeshCacheLibrary.Entry entry);
            PreparedGeometry item = new PreparedGeometry { key = key, fingerprint = fingerprint,
                entry = entry ?? new FragmentMeshCacheLibrary.Entry { sourceKey = sourceKey, cellSize = cellSize } };
            if (entry != null && entry.fingerprint == fingerprint)
            {
                ReusedCount++;
            }
            else
            {
                item.result = CellFragmentBuilder.Build(source, cellSize, Matrix4x4.identity, reportProgress);
                item.result.mesh.name = source.name + "_Fragments";
            }

            prepared.Add(item);
            return item;
        }

        public void ValidateBeforeSave(HashSet<string> sources)
        {
            foreach (string path in sources)
                if (path.StartsWith(Root + "/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Shared cache output cannot also be a source asset: " + path);
            foreach (PreparedGeometry item in prepared)
            {
                if (entries.ContainsKey(item.key)) continue;
                if (Occupied(MeshPath(item.key)) || Occupied(DataPath(item.key)))
                    throw new IOException("Shared mesh cache slot contains unlinked assets: " + SlotFolder(item.key));
            }
        }

        public void ValidateReference(FragmentIslandBakeRecord.GeometryBinding binding)
        {
            if (!entries.TryGetValue(binding.key, out FragmentMeshCacheLibrary.Entry entry) ||
                binding.mesh != entry.mesh || binding.data != entry.data)
                throw new InvalidOperationException("Bake record references a missing or mismatched shared mesh cache slot: " + binding.key);
        }

        public void Commit()
        {
            if (RebuiltCount == 0) return;
            if (library == null)
            {
                EnsureFolder(Root + "/Editor");
                library = ScriptableObject.CreateInstance<FragmentMeshCacheLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            foreach (PreparedGeometry item in prepared)
            {
                if (item.result == null) continue;
                EnsureFolder(SlotFolder(item.key));
                FragmentMeshCacheLibrary.Entry entry = item.entry;
                entry.mesh = Persist(item.result.mesh, entry.mesh, MeshPath(item.key));
                FragmentMeshData data = ScriptableObject.CreateInstance<FragmentMeshData>();
                try
                {
                    data.name = entry.mesh.name + "_Data";
                    data.Initialize(entry.mesh, item.result.centers.ToArray(), item.result.maximumRadius,
                        entry.data != null ? entry.data.Revision + 1 : 1);
                    entry.data = Persist(data, entry.data, DataPath(item.key));
                }
                finally
                {
                    if (!EditorUtility.IsPersistent(data)) UnityEngine.Object.DestroyImmediate(data);
                }

                // Publish freshness only after both outputs have been saved successfully.
                entry.fingerprint = item.fingerprint;
                if (!entries.ContainsKey(item.key))
                {
                    library.Entries.Add(entry);
                    entries.Add(item.key, entry);
                }

                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssetIfDirty(library);
            }
        }

        public void Dispose()
        {
            foreach (PreparedGeometry item in prepared)
                if (item.result != null && !EditorUtility.IsPersistent(item.result.mesh))
                    UnityEngine.Object.DestroyImmediate(item.result.mesh);
            if (library != null && !EditorUtility.IsPersistent(library)) UnityEngine.Object.DestroyImmediate(library);
        }

        private static string SlotKey(string sourceKey, float cellSize)
        {
            return sourceKey + ":" + BitConverter.SingleToInt32Bits(cellSize).ToString("x8", CultureInfo.InvariantCulture);
        }

        private static string SlotFolder(string key) => Root + "/" + Hash128.Compute(key);
        private static string MeshPath(string key) => SlotFolder(key) + "/Mesh.asset";
        private static string DataPath(string key) => SlotFolder(key) + "/Data.asset";
        private static bool Occupied(string path) => File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null;

        private static void ValidateEntry(FragmentMeshCacheLibrary.Entry entry, string key)
        {
            if (string.IsNullOrEmpty(entry.sourceKey) || !float.IsFinite(entry.cellSize) || entry.cellSize <= 0f ||
                entry.mesh == null || entry.data == null || entry.data.Mesh != entry.mesh ||
                AssetDatabase.GetAssetPath(entry.mesh) != MeshPath(key) || AssetDatabase.GetAssetPath(entry.data) != DataPath(key))
                throw new InvalidOperationException("Shared mesh cache entry is missing or its owned outputs were moved/changed: " + key +
                    ". Restore the cache assets; do not delete resources referenced by baked islands.");
        }

        private static string Fingerprint(Mesh mesh)
        {
            using (SHA256 hash = SHA256.Create())
            {
                using (CryptoStream stream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write))
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                using (Mesh.MeshDataArray snapshot = MeshUtility.AcquireReadOnlyMeshData(mesh))
                {
                    Mesh.MeshData data = snapshot[0];
                    writer.Write(AlgorithmVersion);
                    writer.Write(data.vertexCount);
                    writer.Write(data.vertexBufferCount);
                    VertexAttributeDescriptor[] attributes = mesh.GetVertexAttributes();
                    writer.Write(attributes.Length);
                    foreach (VertexAttributeDescriptor attribute in attributes)
                    {
                        writer.Write((int)attribute.attribute);
                        writer.Write((int)attribute.format);
                        writer.Write(attribute.dimension);
                        writer.Write(attribute.stream);
                        writer.Write(data.GetVertexAttributeOffset(attribute.attribute));
                    }

                    for (int index = 0; index < data.vertexBufferCount; index++)
                    {
                        byte[] bytes = data.GetVertexData<byte>(index).ToArray();
                        writer.Write(data.GetVertexBufferStride(index));
                        writer.Write(bytes.Length);
                        writer.Write(bytes);
                    }

                    writer.Write((int)data.indexFormat);
                    Array indices = data.indexFormat == IndexFormat.UInt16
                        ? (Array)data.GetIndexData<ushort>().ToArray() : data.GetIndexData<uint>().ToArray();
                    byte[] indexBytes = new byte[Buffer.ByteLength(indices)];
                    Buffer.BlockCopy(indices, 0, indexBytes, 0, indexBytes.Length);
                    writer.Write(indexBytes.Length);
                    writer.Write(indexBytes);
                    writer.Write(data.subMeshCount);
                    for (int index = 0; index < data.subMeshCount; index++)
                    {
                        SubMeshDescriptor subMesh = data.GetSubMesh(index);
                        writer.Write(subMesh.indexStart);
                        writer.Write(subMesh.indexCount);
                        writer.Write(subMesh.baseVertex);
                        writer.Write((int)subMesh.topology);
                    }
                }

                return BitConverter.ToString(hash.Hash).Replace("-", string.Empty);
            }
        }

        private static T Persist<T>(T generated, T existing, string path) where T : UnityEngine.Object
        {
            generated.hideFlags = HideFlags.None;
            if (existing == null)
            {
                if (Occupied(path)) throw new IOException("Shared mesh cache path is occupied by an unlinked asset: " + path);
                AssetDatabase.CreateAsset(generated, path);
                existing = generated;
            }
            else
            {
                EditorUtility.CopySerialized(generated, existing);
                EditorUtility.SetDirty(existing);
            }

            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            if (File.Exists(folder)) throw new IOException("Shared cache folder path is occupied by a file: " + folder);
            EnsureFolder(Path.GetDirectoryName(folder).Replace('\\', '/'));
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder))))
                throw new IOException("Could not create shared mesh cache folder: " + folder);
        }
    }
}
