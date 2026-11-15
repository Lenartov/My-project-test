using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Voxelization.Editor
{
    internal sealed class VoxelizationSource
    {
        public GameObject gameObject;
        public MeshFilter filter;
        public MeshRenderer renderer;
        public string meshName;
    }

    internal sealed class VoxelizationBatch : IDisposable
    {
        private readonly List<GameObject> roots = new List<GameObject>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private Material material;
        private Scene stagingScene;

        public IReadOnlyList<GameObject> Roots => roots;
        public IReadOnlyList<Mesh> Meshes => meshes;
        public Material Material => material;

        public static List<VoxelizationSource> Inspect(IReadOnlyList<GameObject> inputs, bool hierarchy,
            List<string> errors, List<string> warnings)
        {
            List<VoxelizationSource> sources = new List<VoxelizationSource>();
            HashSet<GameObject> visited = new HashSet<GameObject>();
            for (int inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
            {
                GameObject input = inputs[inputIndex];
                if (input == null)
                {
                    errors.Add("Assign every source field or remove empty entries.");
                    continue;
                }

                HashSet<Renderer> excludedLods = new HashSet<Renderer>();
                if (hierarchy)
                {
                    LODGroup[] groups = input.GetComponentsInChildren<LODGroup>(true);
                    foreach (LODGroup group in groups)
                    {
                        LOD[] lods = group.GetLODs();
                        HashSet<Renderer> firstLod = new HashSet<Renderer>();
                        if (lods.Length > 0)
                        {
                            firstLod.UnionWith(lods[0].renderers);
                        }

                        for (int lodIndex = 1; lodIndex < lods.Length; lodIndex++)
                        {
                            foreach (Renderer renderer in lods[lodIndex].renderers)
                            {
                                if (renderer != null && !firstLod.Contains(renderer))
                                {
                                    excludedLods.Add(renderer);
                                }
                            }
                        }
                    }

                    if (groups.Length > 0)
                    {
                        warnings.Add("LODGroup: only LOD0 geometry is generated; LOD components are not copied.");
                    }
                }

                Transform[] transforms = hierarchy
                    ? input.GetComponentsInChildren<Transform>(true)
                    : new[] { input.transform };
                HashSet<Transform> temporarySubtrees = new HashSet<Transform>();
                foreach (Transform transform in transforms)
                {
                    GameObject candidate = transform.gameObject;
                    if (IsTemporary(candidate) || temporarySubtrees.Contains(transform.parent))
                    {
                        temporarySubtrees.Add(transform);
                        if (!hierarchy || candidate == input)
                        {
                            errors.Add("Select a saved source object, not a temporary preview.");
                        }

                        continue;
                    }

                    if (!visited.Add(candidate))
                    {
                        continue;
                    }

                    string label = AnimationUtility.CalculateTransformPath(transform, input.transform);
                    label = string.IsNullOrEmpty(label) ? input.name : input.name + "/" + label;
                    MeshFilter filter = candidate.GetComponent<MeshFilter>();
                    MeshRenderer renderer = candidate.GetComponent<MeshRenderer>();
                    if (filter == null || renderer == null)
                    {
                        if (!hierarchy)
                        {
                            errors.Add(label + ": requires MeshFilter and MeshRenderer.");
                        }
                        else if (candidate.GetComponent<Renderer>() != null)
                        {
                            warnings.Add(label + ": unsupported visual component is omitted.");
                        }
                        else if (filter != null)
                        {
                            warnings.Add(label + ": MeshFilter without MeshRenderer is omitted.");
                        }

                        continue;
                    }

                    if (excludedLods.Contains(renderer))
                    {
                        continue;
                    }

                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null || !mesh.isReadable)
                    {
                        errors.Add(label + ": requires a readable source mesh.");
                        continue;
                    }

                    bool hasTriangles = false;
                    for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                    {
                        if (mesh.GetTopology(subMesh) == MeshTopology.Triangles && mesh.GetIndexCount(subMesh) > 0)
                        {
                            hasTriangles = true;
                            break;
                        }
                    }

                    if (!hasTriangles)
                    {
                        errors.Add(label + ": mesh has no triangles.");
                        continue;
                    }

                    sources.Add(new VoxelizationSource
                    {
                        gameObject = candidate,
                        filter = filter,
                        renderer = renderer,
                        meshName = GetMeshName(transform, input.transform)
                    });
                }
            }

            if (sources.Count == 0)
            {
                errors.Add("No supported mesh objects found.");
            }

            return sources;
        }

        public void Generate(IReadOnlyList<GameObject> inputs, IReadOnlyList<VoxelizationSource> sources,
            bool hierarchy, VoxelMeshGenerator generator)
        {
            Shader shader = Shader.Find("Voxelization/URP Voxel Reveal");
            if (shader == null)
            {
                throw new InvalidOperationException("Voxelization/URP Voxel Reveal shader was not found.");
            }

            material = new Material(shader) { name = "VoxelMaterial", hideFlags = HideFlags.HideAndDontSave };
            stagingScene = EditorSceneManager.NewPreviewScene();
            Dictionary<GameObject, Transform> copies = new Dictionary<GameObject, Transform>();
            if (hierarchy)
            {
                roots.Add(CopyHierarchy(inputs[0].transform, null, copies).gameObject);
            }

            for (int index = 0; index < sources.Count; index++)
            {
                VoxelizationSource source = sources[index];
                if (EditorUtility.DisplayCancelableProgressBar("Surface Voxelizer", source.gameObject.name,
                    (float)index / sources.Count))
                {
                    throw new OperationCanceledException();
                }

                Mesh mesh = generator.Generate(source.filter.sharedMesh, source.renderer.sharedMaterials);
                mesh.name = source.meshName;
                mesh.hideFlags = HideFlags.HideAndDontSave;
                meshes.Add(mesh);

                GameObject copy;
                if (hierarchy)
                {
                    copy = copies[source.gameObject].gameObject;
                }
                else
                {
                    copy = CreateObject(source.gameObject.name, null);
                    roots.Add(copy);
                    CopyTransform(source.gameObject.transform, copy.transform);
                    copy.SetActive(source.gameObject.activeSelf);
                }

                copy.layer = source.gameObject.layer;
                copy.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = copy.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.enabled = source.renderer.enabled;
                renderer.shadowCastingMode = source.renderer.shadowCastingMode;
                renderer.receiveShadows = source.renderer.receiveShadows;
                renderer.renderingLayerMask = source.renderer.renderingLayerMask;
                copy.AddComponent<VoxelRevealRenderer>().ShowImmediate();
            }
        }

        public void ShowPreview(IReadOnlyList<GameObject> inputs, bool hierarchy)
        {
            for (int index = 0; index < roots.Count; index++)
            {
                GameObject source = inputs[hierarchy ? 0 : index];
                GameObject root = roots[index];
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    transform.gameObject.hideFlags = HideFlags.DontSave;
                }

                if (source.scene.IsValid() && source.scene.isLoaded)
                {
                    SceneManager.MoveGameObjectToScene(root, source.scene);
                    root.transform.SetParent(source.transform.parent, false);
                }
                else
                {
                    SceneManager.MoveGameObjectToScene(root, SceneManager.GetActiveScene());
                }

                CopyTransform(source.transform, root.transform);
            }

            EditorSceneManager.ClosePreviewScene(stagingScene);
            stagingScene = default;
            Selection.activeGameObject = roots[0];
            SceneView.RepaintAll();
        }

        public void ReplaceMesh(int index, Mesh savedMesh)
        {
            Mesh temporaryMesh = meshes[index];
            foreach (GameObject root in roots)
            {
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == temporaryMesh)
                    {
                        filter.sharedMesh = savedMesh;
                    }
                }
            }
        }

        public void ReplaceMaterial(Material savedMaterial)
        {
            foreach (GameObject root in roots)
            {
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.sharedMaterial = savedMaterial;
                }
            }
        }

        public void Dispose()
        {
            foreach (GameObject root in roots)
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            if (stagingScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(stagingScene);
            }

            foreach (Mesh mesh in meshes)
            {
                if (mesh != null && !EditorUtility.IsPersistent(mesh))
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }

            if (material != null)
            {
                UnityEngine.Object.DestroyImmediate(material);
            }

            roots.Clear();
            meshes.Clear();
        }

        private Transform CopyHierarchy(Transform source, Transform parent, Dictionary<GameObject, Transform> copies)
        {
            GameObject copy = CreateObject(source.name, parent);
            copies.Add(source.gameObject, copy.transform);
            CopyTransform(source, copy.transform);
            copy.layer = source.gameObject.layer;
            for (int index = 0; index < source.childCount; index++)
            {
                Transform child = source.GetChild(index);
                if (!IsTemporary(child.gameObject))
                {
                    CopyHierarchy(child, copy.transform, copies);
                }
            }

            copy.SetActive(source.gameObject.activeSelf);
            return copy.transform;
        }

        private GameObject CreateObject(string name, Transform parent)
        {
            GameObject copy = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(copy, stagingScene);
            copy.transform.SetParent(parent, false);
            return copy;
        }

        private static void CopyTransform(Transform source, Transform target)
        {
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        private static string GetMeshName(Transform transform, Transform root)
        {
            string identifier = string.Empty;
            Transform current = transform;
            while (current != root)
            {
                identifier = current.GetSiblingIndex().ToString("D4") + "_" + identifier;
                current = current.parent;
            }

            return identifier + transform.name + "_VoxelMesh";
        }

        private static bool IsTemporary(GameObject source)
        {
            return (source.hideFlags & HideFlags.DontSaveInEditor) != 0;
        }
    }
}
