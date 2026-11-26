using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurfaceFragments.Editor
{
    internal static class FragmentIslandBaker
    {
        private sealed class Visual
        {
            public MeshRenderer source;
            public FragmentMeshCache.PreparedGeometry geometry;
            public GameObject target;
        }

        public static void Bake(FragmentIslandAuthoring authoring, FragmentIslandBakeRecord linked = null,
            string savedSourcePath = null, Transform sourceContentsRoot = null)
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Bake or regenerate islands in Edit Mode.");
            }

            if (!float.IsFinite(authoring.CellSize) || authoring.CellSize <= 0f)
            {
                throw new ArgumentException("Cell Size must be finite and greater than zero.");
            }

            PrefabStage stage = PrefabStageUtility.GetPrefabStage(authoring.gameObject);
            if (stage != null && stage.scene.isDirty)
            {
                throw new InvalidOperationException("Save the source prefab edits before generating its linked island.");
            }

            string sourcePath = savedSourcePath ?? GetSourcePath(authoring.gameObject);
            Transform identityRoot = sourceContentsRoot != null ? sourceContentsRoot : stage != null ? stage.prefabContentsRoot.transform : null;
            Func<Transform, string> nodeKey = node => GetNodeKey(node, sourcePath, identityRoot);
            IReadOnlyList<FragmentIslandPort> sourcePorts = authoring.Ports.Count > 0 ? authoring.Ports : FragmentIslandPort.FromBoundaries(authoring.BoundaryPaths);
            FragmentIslandPorts.ValidateDefinitions(sourcePorts, authoring.name);
            if (authoring.Footprint.Count > 0) FragmentIslandFootprint.Prepare(authoring.Footprint, out _);
            string sourceKey = nodeKey(authoring.transform);
            FragmentIslandBakeRecord record = linked != null ? linked : authoring.BakeRecord;
            if (record != null && record.SourceKey != sourceKey)
            {
                throw new InvalidOperationException("This bake record belongs to another source. Clear the link to generate a separate output.");
            }

            string folder = record != null ? GetRecordFolder(record) : ValidateFolder(authoring.OutputFolder);
            Shader shader = Shader.Find("Surface Fragments/URP Reveal");
            if (shader == null)
            {
                throw new InvalidOperationException("Surface Fragments/URP Reveal shader is missing. Check shader compilation errors.");
            }

            using FragmentMeshCache cache = new FragmentMeshCache();
            Dictionary<Mesh, FragmentMeshCache.PreparedGeometry> generated = new Dictionary<Mesh, FragmentMeshCache.PreparedGeometry>();
            List<Visual> visuals = new List<Visual>();
            List<Transform> sourceNodes = new List<Transform>();
            CollectNodes(authoring.transform, sourceNodes);
            HashSet<Renderer> excludedLods = GetExcludedLods(authoring.gameObject);
            Dictionary<string, Material> stagedMaterials = new Dictionary<string, Material>();
            HashSet<string> sourceAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(sourcePath)) sourceAssetPaths.Add(sourcePath);
            GameObject workingRoot = null;
            Scene previewScene = default;
            bool loadedExisting = false;
            bool commitStarted = false;
            int disabledVisuals = 0;
            int unsupportedRenderers = 0;

            try
            {
                foreach (Transform node in sourceNodes)
                {
                    foreach (Renderer visualRenderer in node.GetComponents<Renderer>())
                    {
                        if (!(visualRenderer is MeshRenderer)) unsupportedRenderers++;
                    }

                    if (!node.TryGetComponent(out MeshFilter filter) || !node.TryGetComponent(out MeshRenderer renderer) ||
                        filter.sharedMesh == null || excludedLods.Contains(renderer))
                    {
                        continue;
                    }

                    Mesh mesh = filter.sharedMesh;
                    if (!renderer.enabled) disabledVisuals++;
                    AddAssetPath(sourceAssetPaths, mesh);
                    if (!generated.TryGetValue(mesh, out FragmentMeshCache.PreparedGeometry geometry))
                    {
                        if (EditorUtility.DisplayCancelableProgressBar("Bake Fragment Island", "Checking shared cache: " + mesh.name, 0f))
                            throw new OperationCanceledException();
                        geometry = cache.Prepare(mesh, authoring.CellSize,
                            progress => EditorUtility.DisplayCancelableProgressBar("Bake Fragment Island", node.name, progress));
                        generated.Add(mesh, geometry);
                        if (geometry.result != null && geometry.result.openCuts > 0)
                        {
                            Debug.LogWarning(node.name + ": generated with " + geometry.result.openCuts + " incomplete cut contours. Inspect the saved result.", authoring);
                        }
                    }

                    visuals.Add(new Visual { source = renderer, geometry = geometry });
                    Material[] materials = renderer.sharedMaterials;
                    for (int slot = 0; slot < mesh.subMeshCount; slot++)
                    {
                        Material material = slot < materials.Length ? materials[slot] : null;
                        AddAssetPath(sourceAssetPaths, material);
                        string key = MaterialKey(material, nodeKey(node), slot);
                        if (!stagedMaterials.ContainsKey(key))
                        {
                            stagedMaterials.Add(key, CreateMaterial(shader, material, authoring.FallbackColor));
                        }
                    }
                }

                if (visuals.Count == 0)
                {
                    throw new InvalidOperationException("No MeshFilter + MeshRenderer geometry was found under this island.");
                }

                if (disabledVisuals > 0)
                {
                    Debug.LogWarning(disabledVisuals + " source MeshRenderers are disabled and will stay disabled in the output. " +
                        "Re-enable source renderers if they were disabled only to inspect a temporary preview.", authoring);
                }

                if (unsupportedRenderers > 0)
                {
                    Debug.LogWarning(unsupportedRenderers + " non-mesh renderers are not baked (for example sprites, particles, or skinned meshes). Their transforms are retained.", authoring);
                }

                if (record != null)
                {
                    ValidateRecordAssets(record, sourceAssetPaths, cache);
                }
                cache.ValidateBeforeSave(sourceAssetPaths);

                // No assets are written until all geometry has been generated successfully.
                if (record != null && record.GeneratedPrefab != null)
                {
                    workingRoot = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(record.GeneratedPrefab));
                    loadedExisting = true;
                }
                else
                {
                    previewScene = EditorSceneManager.NewPreviewScene();
                    workingRoot = new GameObject(authoring.OutputName);
                    SceneManager.MoveGameObjectToScene(workingRoot, previewScene);
                }

                workingRoot.SetActive(false);
                Dictionary<string, GameObject> previous = new Dictionary<string, GameObject>();
                foreach (FragmentBakeNode node in workingRoot.GetComponentsInChildren<FragmentBakeNode>(true))
                {
                    previous.Add(node.SourceKey, node.gameObject);
                }

                Dictionary<Transform, GameObject> copies = new Dictionary<Transform, GameObject>();
                HashSet<string> currentKeys = new HashSet<string>();
                foreach (Transform node in sourceNodes)
                {
                    string key = nodeKey(node);
                    if (!currentKeys.Add(key))
                    {
                        throw new InvalidOperationException("Duplicate source identity. Save the source prefab or scene and retry.");
                    }

                    GameObject copy;
                    if (node == authoring.transform)
                    {
                        copy = workingRoot;
                    }
                    else if (!previous.TryGetValue(key, out copy))
                    {
                        copy = new GameObject(node.name);
                        SceneManager.MoveGameObjectToScene(copy, workingRoot.scene);
                    }

                    copies.Add(node, copy);
                    if (node != authoring.transform)
                    {
                        copy.transform.SetParent(copies[node.parent].transform, false);
                        copy.transform.localPosition = node.localPosition;
                        copy.transform.localRotation = node.localRotation;
                        copy.transform.localScale = node.localScale;
                        copy.name = node.name;
                        copy.SetActive(node.gameObject.activeSelf);
                        copy.transform.SetSiblingIndex(node.GetSiblingIndex());
                    }

                    copy.layer = node.gameObject.layer;
                    FragmentBakeNode marker = copy.GetComponent<FragmentBakeNode>();
                    if (marker == null) marker = copy.AddComponent<FragmentBakeNode>();
                    marker.SetSourceKey(key);
                }

                foreach (KeyValuePair<string, GameObject> old in previous)
                {
                    if (!currentKeys.Contains(old.Key) && old.Value != null && old.Value != workingRoot)
                    {
                        UnityEngine.Object.DestroyImmediate(old.Value);
                    }
                }

                HashSet<GameObject> visualNodes = new HashSet<GameObject>();
                foreach (Visual visual in visuals)
                {
                    visual.target = copies[visual.source.transform];
                    visualNodes.Add(visual.target);
                }

                foreach (GameObject copy in copies.Values)
                {
                    if (!visualNodes.Contains(copy))
                    {
                        RemoveVisual(copy);
                    }
                }

                workingRoot.transform.localPosition = Vector3.zero;
                workingRoot.transform.localRotation = Quaternion.identity;
                workingRoot.transform.localScale = authoring.transform.localScale;
                if (EditorUtility.DisplayCancelableProgressBar("Bake Fragment Island", "Ready to save. Shared meshes reused: " +
                    cache.ReusedCount + ", new/rebuilt: " + cache.RebuiltCount, 1f))
                {
                    throw new OperationCanceledException();
                }

                cache.Commit();
                commitStarted = true;
                if (record == null)
                {
                    EnsureFolder(folder);
                    folder = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeName(authoring.OutputName));
                    EnsureFolder(folder);
                    record = ScriptableObject.CreateInstance<FragmentIslandBakeRecord>();
                    GlobalObjectId.TryParse(sourceKey, out GlobalObjectId sourceId);
                    string guid = string.IsNullOrEmpty(sourcePath) ? sourceId.assetGUID.ToString() : AssetDatabase.AssetPathToGUID(sourcePath);
                    record.Initialize(sourceKey, guid, folder);
                    AssetDatabase.CreateAsset(record, folder + "/BakeRecord.asset");
                }
                else
                {
                    record.Initialize(record.SourceKey, record.SourceGuid, folder);
                }

                folder = record.OutputFolder;
                EnsureFolder(folder + "/Materials");
                record.SharedGeometry.Clear();
                foreach (FragmentMeshCache.PreparedGeometry geometry in generated.Values)
                {
                    record.SharedGeometry.Add(new FragmentIslandBakeRecord.GeometryBinding
                    {
                        key = geometry.key, mesh = geometry.Data.Mesh, data = geometry.Data
                    });
                }

                Dictionary<string, Material> savedMaterials = new Dictionary<string, Material>();
                foreach (KeyValuePair<string, Material> item in stagedMaterials)
                {
                    FragmentIslandBakeRecord.MaterialBinding binding = record.Materials.Find(value => value.key == item.Key);
                    if (binding == null)
                    {
                        binding = new FragmentIslandBakeRecord.MaterialBinding { key = item.Key };
                        record.Materials.Add(binding);
                    }

                    binding.material = Persist(item.Value, binding.material,
                        folder + "/Materials/" + SafeName(item.Value.name) + "_" + Hash128.Compute(item.Key) + ".mat");
                    savedMaterials.Add(item.Key, binding.material);
                }

                foreach (Visual visual in visuals)
                {
                    Material[] original = visual.source.sharedMaterials;
                    Material[] materials = new Material[visual.geometry.SubMeshCount];
                    for (int slot = 0; slot < materials.Length; slot++)
                    {
                        Material material = slot < original.Length ? original[slot] : null;
                        materials[slot] = savedMaterials[MaterialKey(material, nodeKey(visual.source.transform), slot)];
                    }

                    FragmentRevealRenderer reveal = visual.target.GetComponent<FragmentRevealRenderer>();
                    if (reveal == null) reveal = visual.target.AddComponent<FragmentRevealRenderer>();
                    reveal.Initialize(visual.geometry.Data, materials);
                    MeshRenderer renderer = visual.target.GetComponent<MeshRenderer>();
                    renderer.enabled = visual.source.enabled;
                    renderer.shadowCastingMode = visual.source.shadowCastingMode;
                    renderer.receiveShadows = visual.source.receiveShadows;
                    renderer.renderingLayerMask = visual.source.renderingLayerMask;
                }

                FragmentRevealGroup group = workingRoot.GetComponent<FragmentRevealGroup>();
                if (group == null) group = workingRoot.AddComponent<FragmentRevealGroup>();
                group.Configure(authoring.BoundaryPaths, authoring.RevealSettings);
                FragmentIslandPorts ports = workingRoot.GetComponent<FragmentIslandPorts>();
                if (ports == null) ports = workingRoot.AddComponent<FragmentIslandPorts>();
                ports.Configure(sourcePorts, authoring.Footprint);
                group.SetProgress(1f);
                group.RefreshChildren();
                // Preserve the root's active state; the working hierarchy remains in its isolated scene.
                workingRoot.SetActive(authoring.gameObject.activeSelf);
                string prefabPath = record.GeneratedPrefab != null ? AssetDatabase.GetAssetPath(record.GeneratedPrefab) : folder + "/Island.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(workingRoot, prefabPath, out bool success);
                if (!success) throw new IOException("Could not save generated island prefab: " + prefabPath);
                record.SetGeneratedPrefab(prefab);
                EditorUtility.SetDirty(record);
                AssetDatabase.SaveAssetIfDirty(record);
                if (savedSourcePath == null)
                {
                    if (!EditorUtility.IsPersistent(authoring)) Undo.RecordObject(authoring, "Link fragment island output");
                    authoring.SetBakeRecord(record);
                    EditorUtility.SetDirty(authoring);
                    if (PrefabUtility.IsPartOfPrefabInstance(authoring)) PrefabUtility.RecordPrefabInstancePropertyModifications(authoring);
                    if (EditorUtility.IsPersistent(authoring)) AssetDatabase.SaveAssetIfDirty(authoring);
                }
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("Saved fragment island: " + prefabPath + ". " + visuals.Count + " renderers, " + generated.Count +
                    " unique source meshes. Shared meshes reused: " + cache.ReusedCount + ", new/rebuilt: " + cache.RebuiltCount +
                    ". Save/apply the source's bake link if prompted.", authoring);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Island generation canceled before saving. Existing output assets were not changed.", authoring);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (loadedExisting && workingRoot != null) PrefabUtility.UnloadPrefabContents(workingRoot);
                if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
                foreach (Material material in stagedMaterials.Values)
                {
                    if (!EditorUtility.IsPersistent(material)) UnityEngine.Object.DestroyImmediate(material);
                }

                if (commitStarted && record != null)
                {
                    EditorUtility.SetDirty(record);
                    AssetDatabase.SaveAssetIfDirty(record);
                }
            }
        }

        public static void RegenerateSavedSource(FragmentIslandBakeRecord record)
        {
            string path = AssetDatabase.GUIDToAssetPath(record.SourceGuid);
            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Open the saved source scene and regenerate using its authoring component.");
            }

            GameObject source = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (FragmentIslandAuthoring authoring in source.GetComponentsInChildren<FragmentIslandAuthoring>(true))
                {
                    if (GetNodeKey(authoring.transform, path, source.transform) == record.SourceKey)
                    {
                        Bake(authoring, record, path, source.transform);
                        return;
                    }
                }

                throw new InvalidOperationException("The linked source authoring root no longer exists.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(source);
            }
        }

        private static string GetSourcePath(GameObject root)
        {
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(root);
            if (stage != null) return stage.assetPath;
            string path = AssetDatabase.GetAssetPath(root);
            if (!string.IsNullOrEmpty(path)) return path;
            return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
        }

        private static string GetNodeKey(Transform node, string sourcePath, Transform contentsRoot = null)
        {
            UnityEngine.Object identity = node.gameObject;
            if (!string.IsNullOrEmpty(sourcePath))
            {
                if (contentsRoot != null)
                {
                    List<int> indices = new List<int>();
                    for (Transform current = node; current != contentsRoot; current = current.parent)
                    {
                        int index = 0;
                        foreach (Transform sibling in current.parent)
                        {
                            if (sibling == current) break;
                            if ((sibling.gameObject.hideFlags & HideFlags.DontSave) == 0) index++;
                        }

                        indices.Add(index);
                    }

                    Transform assetNode = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath).transform;
                    for (int index = indices.Count - 1; index >= 0; index--) assetNode = assetNode.GetChild(indices[index]);
                    identity = assetNode.gameObject;
                }
                else
                {
                    GameObject asset = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(node.gameObject, sourcePath);
                    if (asset != null) identity = asset;
                }
            }

            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(identity);
            if (id.assetGUID == default || id.targetObjectId == 0)
            {
                throw new InvalidOperationException("Save the source prefab or scene first so island objects have persistent identities.");
            }

            return id.ToString();
        }

        private static string GetAssetKey(UnityEngine.Object asset, string fallback)
        {
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId)
                ? guid + ":" + localId : fallback;
        }

        private static string MaterialKey(Material material, string nodeKey, int slot)
        {
            return material == null ? "fallback" : GetAssetKey(material, nodeKey + "/material/" + slot);
        }

        private static void CollectNodes(Transform root, List<Transform> nodes)
        {
            if ((root.gameObject.hideFlags & HideFlags.DontSave) != 0) return;
            nodes.Add(root);
            foreach (Transform child in root) CollectNodes(child, nodes);
        }

        private static HashSet<Renderer> GetExcludedLods(GameObject root)
        {
            HashSet<Renderer> excluded = new HashSet<Renderer>();
            HashSet<Renderer> first = new HashSet<Renderer>();
            foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = group.GetLODs();
                for (int index = 0; index < lods.Length; index++)
                {
                    foreach (Renderer renderer in lods[index].renderers)
                    {
                        if (index == 0) first.Add(renderer);
                        else excluded.Add(renderer);
                    }
                }
            }

            excluded.ExceptWith(first);
            return excluded;
        }

        private static void RemoveVisual(GameObject target)
        {
            FragmentRevealRenderer reveal = target.GetComponent<FragmentRevealRenderer>();
            if (reveal != null) UnityEngine.Object.DestroyImmediate(reveal);
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null) UnityEngine.Object.DestroyImmediate(renderer);
            MeshFilter filter = target.GetComponent<MeshFilter>();
            if (filter != null) UnityEngine.Object.DestroyImmediate(filter);
        }

        private static void AddAssetPath(HashSet<string> paths, UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path)) paths.Add(path);
        }

        private static void ValidateRecordAssets(FragmentIslandBakeRecord record, HashSet<string> sources, FragmentMeshCache cache)
        {
            string folder = GetRecordFolder(record);
            List<UnityEngine.Object> assets = new List<UnityEngine.Object> { record, record.GeneratedPrefab };
            foreach (FragmentIslandBakeRecord.GeometryBinding binding in record.Geometry)
            {
                assets.Add(binding.mesh);
                assets.Add(binding.data);
                if (binding.data != null) assets.Add(binding.data.Mesh);
            }

            foreach (FragmentIslandBakeRecord.MaterialBinding binding in record.Materials) assets.Add(binding.material);
            foreach (FragmentIslandBakeRecord.GeometryBinding binding in record.SharedGeometry) cache.ValidateReference(binding);
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset == null) continue;
                string path = AssetDatabase.GetAssetPath(asset);
                if (!path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase) || sources.Contains(path))
                {
                    throw new InvalidOperationException("Bake record would overwrite a source or non-owned asset: " + path);
                }
            }
        }

        private static T Persist<T>(T generated, T existing, string path) where T : UnityEngine.Object
        {
            generated.hideFlags = HideFlags.None;
            if (existing == null)
            {
                if (File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    throw new IOException("Output path is occupied by an unlinked asset: " + path);
                }

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

        internal static string ValidateFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("Choose an output folder inside Assets.");
            string project = Path.GetDirectoryName(Application.dataPath);
            string full = Path.GetFullPath(Path.IsPathRooted(folder) ? folder : Path.Combine(project, folder))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string assets = Path.GetFullPath(Application.dataPath);
            if (!full.Equals(assets, StringComparison.OrdinalIgnoreCase) &&
                !full.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Output Folder must be inside this project's Assets folder.");
            }

            return "Assets" + full.Substring(assets.Length).Replace('\\', '/');
        }

        internal static string GetRecordFolder(FragmentIslandBakeRecord record)
        {
            string path = AssetDatabase.GetAssetPath(record);
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("The linked bake record must be a saved asset.");
            return ValidateFolder(Path.GetDirectoryName(path));
        }

        private static string SafeName(string name)
        {
            foreach (char character in Path.GetInvalidFileNameChars()) name = name.Replace(character, '_');
            name = name.Trim().TrimEnd('.', ' ');
            return string.IsNullOrEmpty(name) ? "FragmentIsland" : name;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            if (File.Exists(folder)) throw new IOException("Output folder path is occupied by a file: " + folder);
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(folder))))
            {
                throw new IOException("Could not create output folder: " + folder);
            }
        }

        private static Material CreateMaterial(Shader shader, Material original, Color fallback)
        {
            Material material = new Material(shader) { name = original != null ? original.name + "_Fragments" : "FragmentFallback" };
            Color tint = fallback;
            if (original != null)
            {
                if (original.HasProperty("_BaseColor")) tint = original.GetColor("_BaseColor");
                else if (original.HasProperty("_Color")) tint = original.GetColor("_Color");
                string property = original.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                if (original.HasProperty(property))
                {
                    material.SetTexture("_BaseMap", original.GetTexture(property));
                    material.SetTextureScale("_BaseMap", original.GetTextureScale(property));
                    material.SetTextureOffset("_BaseMap", original.GetTextureOffset(property));
                }

                bool clip = original.HasProperty("_AlphaClip") && original.GetFloat("_AlphaClip") > 0.5f;
                clip |= original.GetTag("RenderType", false) == "TransparentCutout";
                if (clip) material.SetFloat("_Cutoff", original.HasProperty("_Cutoff") ? original.GetFloat("_Cutoff") : 0.5f);
            }

            material.SetColor("_BaseColor", tint);
            return material;
        }
    }
}
