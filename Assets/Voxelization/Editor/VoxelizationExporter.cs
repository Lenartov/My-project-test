using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Voxelization.Editor
{
    internal sealed class VoxelizationExportPlan
    {
        public string folder;
        public string prefabPath;
        public string materialPath;
        public readonly List<string> meshPaths = new List<string>();
    }

    internal static class VoxelizationExporter
    {
        public static bool TryGetFolder(string input, out string folder, out string error)
        {
            folder = string.Empty;
            error = "Output Folder must be inside this project's Assets folder.";
            string normalized = input.Trim().Replace('\\', '/').TrimEnd('/');
            string assets = Application.dataPath.Replace('\\', '/');
            if (normalized.Equals(assets, StringComparison.OrdinalIgnoreCase))
            {
                normalized = "Assets";
            }
            else if (normalized.StartsWith(assets + "/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "Assets" + normalized.Substring(assets.Length);
            }

            if (normalized != "Assets" && !normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return false;
            }

            string[] segments = normalized.Split('/');
            foreach (string segment in segments)
            {
                if (segment == "." || segment == ".." || string.IsNullOrWhiteSpace(segment) ||
                    segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    segment.EndsWith(".", StringComparison.Ordinal) || segment.EndsWith(" ", StringComparison.Ordinal))
                {
                    error = "Output Folder contains an invalid folder name.";
                    return false;
                }
            }

            folder = normalized;
            error = string.Empty;
            return true;
        }

        public static VoxelizationExportPlan Plan(string folder, string outputName, bool overwrite,
            bool hierarchy, IReadOnlyList<GameObject> inputs, IReadOnlyList<VoxelizationSource> sources)
        {
            VoxelizationExportPlan plan = new VoxelizationExportPlan { folder = folder };
            HashSet<string> reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (hierarchy)
            {
                string name = SanitizeName(string.IsNullOrWhiteSpace(outputName) ? inputs[0].name + "_Voxel" : outputName);
                plan.folder = GetPath(folder, name, string.Empty, overwrite, reserved);
                plan.prefabPath = plan.folder + "/" + Path.GetFileName(plan.folder) + ".prefab";
                plan.materialPath = plan.folder + "/VoxelMaterial.mat";
            }

            for (int index = 0; index < sources.Count; index++)
            {
                string meshName;
                if (hierarchy)
                {
                    meshName = sources[index].meshName;
                }
                else if (string.IsNullOrWhiteSpace(outputName))
                {
                    meshName = sources[index].gameObject.name + "_VoxelMesh";
                }
                else
                {
                    meshName = sources.Count == 1 ? outputName : outputName + "_" + sources[index].gameObject.name;
                }

                string meshFolder = hierarchy ? plan.folder + "/Meshes" : folder;
                plan.meshPaths.Add(GetPath(meshFolder, SanitizeName(meshName), ".asset", overwrite, reserved));
            }

            ValidateFolder(plan.folder);
            if (hierarchy)
            {
                ValidateFolder(plan.folder + "/Meshes");
            }

            ValidateTargets(plan, sources, inputs);
            return plan;
        }

        public static UnityEngine.Object Save(VoxelizationBatch batch, VoxelizationExportPlan plan)
        {
            for (int index = 0; index < plan.meshPaths.Count; index++)
            {
                string path = plan.meshPaths[index];
                EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                Mesh generated = UnityEngine.Object.Instantiate(batch.Meshes[index]);
                generated.name = Path.GetFileNameWithoutExtension(path);
                generated.hideFlags = HideFlags.None;
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Mesh saved;
                try
                {
                    if (existing == null)
                    {
                        AssetDatabase.CreateAsset(generated, path);
                        if (!EditorUtility.IsPersistent(generated))
                        {
                            throw new IOException("Could not save mesh: " + path);
                        }

                        saved = generated;
                    }
                    else
                    {
                        EditorUtility.CopySerialized(generated, existing);
                        EditorUtility.SetDirty(existing);
                        saved = existing;
                    }
                }
                finally
                {
                    if (!EditorUtility.IsPersistent(generated))
                    {
                        UnityEngine.Object.DestroyImmediate(generated);
                    }
                }

                AssetDatabase.SaveAssetIfDirty(saved);
                batch.ReplaceMesh(index, saved);
            }

            if (string.IsNullOrEmpty(plan.prefabPath))
            {
                return AssetDatabase.LoadAssetAtPath<Mesh>(plan.meshPaths[0]);
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(plan.materialPath);
            if (material == null)
            {
                material = new Material(batch.Material) { name = "VoxelMaterial", hideFlags = HideFlags.None };
                try
                {
                    AssetDatabase.CreateAsset(material, plan.materialPath);
                    if (!EditorUtility.IsPersistent(material))
                    {
                        throw new IOException("Could not save material: " + plan.materialPath);
                    }
                }
                finally
                {
                    if (!EditorUtility.IsPersistent(material))
                    {
                        UnityEngine.Object.DestroyImmediate(material);
                    }
                }
            }

            batch.ReplaceMaterial(material);
            GameObject root = batch.Roots[0];
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.hideFlags = HideFlags.None;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, plan.prefabPath, out bool success);
            if (!success)
            {
                throw new IOException("Could not save prefab: " + plan.prefabPath);
            }

            return prefab;
        }

        private static void ValidateTargets(VoxelizationExportPlan plan, IReadOnlyList<VoxelizationSource> sources,
            IReadOnlyList<GameObject> inputs)
        {
            HashSet<string> sourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GameObject input in inputs)
            {
                AddSourcePath(sourcePaths, input);
                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(input);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    sourcePaths.Add(prefabPath);
                }
            }

            foreach (VoxelizationSource source in sources)
            {
                string sourcePrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source.gameObject);
                if (!string.IsNullOrEmpty(sourcePrefabPath))
                {
                    sourcePaths.Add(sourcePrefabPath);
                }

                AddSourcePath(sourcePaths, source.filter.sharedMesh);
                foreach (Material material in source.renderer.sharedMaterials)
                {
                    AddSourcePath(sourcePaths, material);
                }
            }

            foreach (string path in plan.meshPaths)
            {
                ValidateTarget(path, typeof(Mesh), sourcePaths);
            }

            if (!string.IsNullOrEmpty(plan.prefabPath))
            {
                ValidateTarget(plan.prefabPath, typeof(GameObject), sourcePaths);
                ValidateTarget(plan.materialPath, typeof(Material), sourcePaths);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(plan.materialPath);
                if (material != null && material.shader != Shader.Find("Voxelization/URP Voxel Reveal"))
                {
                    throw new InvalidOperationException("The existing VoxelMaterial uses a different shader. Choose another output name.");
                }
            }
        }

        private static void AddSourcePath(HashSet<string> paths, UnityEngine.Object source)
        {
            if (source == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(path))
            {
                paths.Add(path);
            }
        }

        private static void ValidateTarget(string path, Type expectedType, HashSet<string> sourcePaths)
        {
            if (sourcePaths.Contains(path))
            {
                throw new InvalidOperationException("Output would overwrite a source asset: " + path);
            }

            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if ((existing != null && !expectedType.IsInstanceOfType(existing)) || (existing == null && Exists(path)))
            {
                throw new InvalidOperationException("Output path is occupied by a different asset type: " + path);
            }
        }

        private static void ValidateFolder(string folder)
        {
            string current = folder;
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(current) || (AssetDatabase.LoadMainAssetAtPath(current) != null &&
                    !AssetDatabase.IsValidFolder(current)))
                {
                    throw new InvalidOperationException("Output folder path is occupied by a file: " + current);
                }

                current = Path.GetDirectoryName(current)?.Replace('\\', '/');
            }
        }

        private static string GetPath(string folder, string name, string extension, bool overwrite, HashSet<string> reserved)
        {
            string path = folder + "/" + name + extension;
            int suffix = 1;
            while (reserved.Contains(path) || (!overwrite && Exists(path)))
            {
                path = folder + "/" + name + " " + suffix + extension;
                suffix++;
            }

            reserved.Add(path);
            return path;
        }

        private static bool Exists(string path)
        {
            return !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)) || File.Exists(path) || Directory.Exists(path);
        }

        private static string SanitizeName(string name)
        {
            string clean = name.Trim();
            foreach (char character in Path.GetInvalidFileNameChars())
            {
                clean = clean.Replace(character, '_');
            }

            clean = clean.TrimEnd('.', ' ');
            return string.IsNullOrEmpty(clean) ? "VoxelModel" : clean;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            string guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            if (string.IsNullOrEmpty(guid))
            {
                throw new IOException("Could not create output folder: " + folder);
            }
        }
    }
}
