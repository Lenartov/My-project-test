using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Voxelization.Editor
{
    public sealed class VoxelizationWindow : EditorWindow
    {
        private enum SourceMode
        {
            Objects,
            Hierarchy
        }

        [SerializeField] private SourceMode mode;
        [SerializeField] private List<GameObject> sourceObjects = new List<GameObject> { null };
        [SerializeField] private GameObject sourceRoot;
        [SerializeField] private float voxelSize = 0.1f;
        [SerializeField] private float voxelGap;
        [SerializeField] private float surfaceAccuracy = 1f;
        [SerializeField] private Color fallbackColor = Color.white;

        private string outputFolder = "Assets";
        private string outputName = string.Empty;
        private bool overwrite;
        private Vector2 scrollPosition;
        private bool showModels;
        private VoxelizationBatch preview;
        private string status;
        private MessageType statusType;

        private string PreferencesKey => "Voxelization." + Application.dataPath + ".";

        [MenuItem("Tools/Voxelization/Surface Voxelizer")]
        public static void Open()
        {
            GetWindow<VoxelizationWindow>("Surface Voxelizer");
        }

        private void OnEnable()
        {
            outputFolder = EditorPrefs.GetString(PreferencesKey + "Folder", "Assets");
            outputName = EditorPrefs.GetString(PreferencesKey + "Name", string.Empty);
            overwrite = EditorPrefs.GetBool(PreferencesKey + "Overwrite", false);
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            SavePreferences();
            ClearPreview();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                ClearPreview();
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.LabelField("Surface Voxelization", EditorStyles.boldLabel);
            mode = (SourceMode)GUILayout.Toolbar((int)mode, new[] { "Objects", "Hierarchy" });
            if (mode == SourceMode.Objects)
            {
                DrawObjects();
            }
            else
            {
                sourceRoot = (GameObject)EditorGUILayout.ObjectField("Source Root", sourceRoot, typeof(GameObject), true);
                EditorGUILayout.HelpBox("Copies the full transform hierarchy, including inactive objects. Only MeshFilter + MeshRenderer visuals are generated; LOD groups use LOD0.", MessageType.Info);
            }

            EditorGUILayout.Space();
            voxelSize = Mathf.Max(0.0001f, EditorGUILayout.FloatField("Voxel Size", voxelSize));
            voxelGap = Mathf.Clamp(EditorGUILayout.FloatField("Voxel Gap", voxelGap), 0f, voxelSize - 0.00001f);
            surfaceAccuracy = EditorGUILayout.Slider("Surface Accuracy", surfaceAccuracy, 0.05f, 1f);
            fallbackColor = EditorGUILayout.ColorField("Fallback Color", fallbackColor);
            EditorGUILayout.HelpBox("Voxel Size uses each mesh's local units. Lower Surface Accuracy reduces over-thickening but can leave gaps.", MessageType.None);

            DrawOutput();
            List<GameObject> inputs = GetInputs();
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            List<VoxelizationSource> sources = VoxelizationBatch.Inspect(inputs, mode == SourceMode.Hierarchy, errors, warnings);
            foreach (string error in errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            foreach (string warning in warnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            if (sources.Count > 0)
            {
                DrawEstimate(sources);
            }

            string folder;
            string folderError;
            bool validFolder = VoxelizationExporter.TryGetFolder(outputFolder, out folder, out folderError);
            if (!validFolder)
            {
                EditorGUILayout.HelpBox(folderError, MessageType.Warning);
            }

            bool validSources = errors.Count == 0 && !EditorApplication.isPlayingOrWillChangePlaymode;
            using (new EditorGUI.DisabledScope(!validSources))
            {
                if (GUILayout.Button("Generate Preview"))
                {
                    RunGeneration(inputs, sources, null);
                }

                using (new EditorGUI.DisabledScope(!validFolder))
                {
                    string saveLabel = mode == SourceMode.Hierarchy ? "Generate Prefab" : "Generate Mesh Assets";
                    if (GUILayout.Button(saveLabel))
                    {
                        Save(inputs, sources, folder);
                    }
                }
            }

            if (GUILayout.Button("Clear Preview"))
            {
                ClearPreview();
            }

            if (!string.IsNullOrEmpty(status))
            {
                EditorGUILayout.HelpBox(status, statusType);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawObjects()
        {
            for (int index = 0; index < sourceObjects.Count; index++)
            {
                EditorGUILayout.BeginHorizontal();
                sourceObjects[index] = (GameObject)EditorGUILayout.ObjectField(
                    "Source " + (index + 1), sourceObjects[index], typeof(GameObject), true);
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    sourceObjects.RemoveAt(index);
                    index--;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Object"))
            {
                sourceObjects.Add(null);
            }

            if (GUILayout.Button("Add Selected"))
            {
                foreach (GameObject selected in Selection.gameObjects)
                {
                    if (!sourceObjects.Contains(selected) && !IsPreviewObject(selected))
                    {
                        sourceObjects.RemoveAll(item => item == null);
                        sourceObjects.Add(selected);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawOutput()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            if (GUILayout.Button("Browse...", GUILayout.Width(80f)))
            {
                string selectedFolder = EditorUtility.OpenFolderPanel("Voxel Output Folder", Application.dataPath, string.Empty);
                if (!string.IsNullOrEmpty(selectedFolder))
                {
                    string normalized;
                    string error;
                    outputFolder = VoxelizationExporter.TryGetFolder(selectedFolder, out normalized, out error)
                        ? normalized
                        : selectedFolder;
                    SavePreferences();
                }
            }

            EditorGUILayout.EndHorizontal();

            string folderPath;
            string folderError;
            DefaultAsset currentFolder = null;
            if (VoxelizationExporter.TryGetFolder(outputFolder, out folderPath, out folderError))
            {
                currentFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
            }

            DefaultAsset selectedAsset = (DefaultAsset)EditorGUILayout.ObjectField("Folder Asset", currentFolder, typeof(DefaultAsset), false);
            if (selectedAsset != null && selectedAsset != currentFolder)
            {
                string selectedPath = AssetDatabase.GetAssetPath(selectedAsset);
                if (AssetDatabase.IsValidFolder(selectedPath))
                {
                    outputFolder = selectedPath;
                }
            }

            outputName = EditorGUILayout.TextField(new GUIContent("Output Name",
                "Leave empty to use source names. For several Objects, a custom name is a prefix."), outputName);
            overwrite = EditorGUILayout.Toggle("Overwrite Existing", overwrite);
            if (EditorGUI.EndChangeCheck())
            {
                SavePreferences();
            }

            if (overwrite)
            {
                EditorGUILayout.HelpBox("Matching output assets are updated in place. Existing references see the new meshes. Unused old mesh files are retained.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Name collisions create a new name with a numeric suffix: Name, Name 1, Name 2.", MessageType.None);
            }
        }

        private List<GameObject> GetInputs()
        {
            if (mode == SourceMode.Hierarchy)
            {
                return new List<GameObject> { sourceRoot };
            }

            List<GameObject> inputs = new List<GameObject>();
            foreach (GameObject source in sourceObjects)
            {
                if (source == null || !inputs.Contains(source))
                {
                    inputs.Add(source);
                }
            }

            return inputs;
        }

        private void DrawEstimate(IReadOnlyList<VoxelizationSource> sources)
        {
            double cells = 0;
            foreach (VoxelizationSource source in sources)
            {
                Vector3Int size = VoxelMeshGenerator.GetGridSize(source.filter.sharedMesh.bounds, voxelSize);
                cells += (double)size.x * size.y * size.z;
            }

            EditorGUILayout.LabelField("Models", sources.Count.ToString());
            EditorGUILayout.LabelField("Potential Voxels", cells.ToString("N0") + " maximum");
            EditorGUILayout.LabelField("Potential Vertices", (cells * 24).ToString("N0") + " maximum");
            showModels = EditorGUILayout.Foldout(showModels, "Detected Models", true);
            if (showModels)
            {
                foreach (VoxelizationSource source in sources)
                {
                    string path = mode == SourceMode.Hierarchy
                        ? AnimationUtility.CalculateTransformPath(source.gameObject.transform, sourceRoot.transform)
                        : source.gameObject.name;
                    EditorGUILayout.LabelField(string.IsNullOrEmpty(path) ? source.gameObject.name : path);
                }
            }

            if (cells > 100000)
            {
                EditorGUILayout.HelpBox("Large voxel grid. Generation may take time and use substantial memory; increase Voxel Size if needed.", MessageType.Warning);
            }
        }

        private void Save(List<GameObject> inputs, List<VoxelizationSource> sources, string folder)
        {
            try
            {
                ValidatePreviewSources(inputs);
                VoxelizationExportPlan plan = VoxelizationExporter.Plan(folder, outputName, overwrite,
                    mode == SourceMode.Hierarchy, inputs, sources);
                RunGeneration(inputs, sources, plan);
            }
            catch (InvalidOperationException exception)
            {
                SetStatus(exception.Message, MessageType.Error);
            }
        }

        private void RunGeneration(List<GameObject> inputs, List<VoxelizationSource> sources, VoxelizationExportPlan plan)
        {
            VoxelizationBatch batch = null;
            try
            {
                ValidatePreviewSources(inputs);
                batch = new VoxelizationBatch();
                VoxelMeshGenerator generator = new VoxelMeshGenerator(voxelSize, voxelGap, surfaceAccuracy, fallbackColor);
                batch.Generate(inputs, sources, mode == SourceMode.Hierarchy, generator);
                if (plan == null)
                {
                    batch.ShowPreview(inputs, mode == SourceMode.Hierarchy);
                    ClearPreview();
                    preview = batch;
                    batch = null;
                    SetStatus("Preview generated. Clear Preview removes all temporary objects and meshes.", MessageType.Info);
                }
                else
                {
                    UnityEngine.Object result = VoxelizationExporter.Save(batch, plan);
                    Selection.activeObject = result;
                    EditorGUIUtility.PingObject(result);
                    SetStatus("Saved " + sources.Count + " mesh assets to " + plan.folder +
                        (string.IsNullOrEmpty(plan.prefabPath) ? "." : " and prefab: " + plan.prefabPath), MessageType.Info);
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("Generation cancelled. Temporary objects were cleaned up.", MessageType.Info);
            }
            catch (InvalidOperationException exception)
            {
                SetStatus(exception.Message, MessageType.Error);
            }
            catch (IOException exception)
            {
                SetStatus("Saving failed: " + exception.Message + " Already saved assets are retained.", MessageType.Error);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                batch?.Dispose();
            }
        }

        private void ValidatePreviewSources(IReadOnlyList<GameObject> inputs)
        {
            foreach (GameObject input in inputs)
            {
                if (IsPreviewObject(input))
                {
                    throw new InvalidOperationException("Select the original source, not a temporary voxel preview.");
                }
            }
        }

        private bool IsPreviewObject(GameObject candidate)
        {
            if (preview == null || candidate == null)
            {
                return false;
            }

            foreach (GameObject root in preview.Roots)
            {
                if (root != null && (candidate == root || candidate.transform.IsChildOf(root.transform)))
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearPreview()
        {
            preview?.Dispose();
            preview = null;
            SceneView.RepaintAll();
        }

        private void SavePreferences()
        {
            EditorPrefs.SetString(PreferencesKey + "Folder", outputFolder);
            EditorPrefs.SetString(PreferencesKey + "Name", outputName);
            EditorPrefs.SetBool(PreferencesKey + "Overwrite", overwrite);
        }

        private void SetStatus(string message, MessageType type)
        {
            status = message;
            statusType = type;
            Repaint();
        }
    }
}
