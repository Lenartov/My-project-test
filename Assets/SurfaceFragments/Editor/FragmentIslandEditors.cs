using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SurfaceFragments.Editor
{
    internal static class FragmentPathEditor
    {
        public static void DrawHandles(SerializedObject serialized, Transform root)
        {
            serialized.Update();
            SerializedProperty paths = serialized.FindProperty("boundaryPaths");
            Color previous = Handles.color;
            Handles.color = Color.cyan;
            if (paths != null)
            {
                for (int index = 0; index < paths.arraySize; index++)
                {
                    DrawPathHandles(paths.GetArrayElementAtIndex(index), root, index.ToString());
                }
            }

            SerializedProperty ports = serialized.FindProperty("ports");
            if (ports != null)
            {
                Handles.color = Color.yellow;
                for (int index = 0; index < ports.arraySize; index++)
                {
                    SerializedProperty port = ports.GetArrayElementAtIndex(index);
                    DrawPathHandles(port.FindPropertyRelative("boundary"), root, port.FindPropertyRelative("id").stringValue);
                    if (port.FindPropertyRelative("useCustomAttachment").boolValue) DrawAttachmentHandle(port, root);
                }
            }

            SerializedProperty footprint = serialized.FindProperty("footprint");
            if (footprint != null && footprint.arraySize > 0)
            {
                Handles.color = Color.green;
                Vector3[] line = new Vector3[footprint.arraySize + 1];
                for (int index = 0; index < footprint.arraySize; index++)
                {
                    SerializedProperty point = footprint.GetArrayElementAtIndex(index);
                    Vector2 value = point.vector2Value;
                    Vector3 world = root.TransformPoint(new Vector3(value.x, 0f, value.y));
                    EditorGUI.BeginChangeCheck();
                    Vector3 moved = Handles.PositionHandle(world, root.rotation);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Vector3 local = root.InverseTransformPoint(moved);
                        point.vector2Value = new Vector2(local.x, local.z);
                    }
                    line[index] = moved;
                    Handles.Label(moved, "Footprint:" + index);
                }
                line[line.Length - 1] = line[0];
                if (line.Length > 2) Handles.DrawAAPolyLine(3f, line);
            }

            Handles.color = previous;
            serialized.ApplyModifiedProperties();
        }

        private static void DrawAttachmentHandle(SerializedProperty port, Transform root)
        {
            SerializedProperty position = port.FindPropertyRelative("attachmentPosition");
            SerializedProperty normal = port.FindPropertyRelative("attachmentNormal");
            Vector3 world = root.TransformPoint(position.vector3Value);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(world, root.rotation);
            if (EditorGUI.EndChangeCheck()) position.vector3Value = root.InverseTransformPoint(moved);
            Handles.Label(moved, port.FindPropertyRelative("id").stringValue + " Dock");
            Vector3 direction = new Vector3(normal.vector3Value.x, 0f, normal.vector3Value.z);
            if (direction.sqrMagnitude <= 0f) return;
            Quaternion rotation = Quaternion.LookRotation(root.localToWorldMatrix.inverse.transpose.MultiplyVector(direction).normalized, root.up);
            EditorGUI.BeginChangeCheck();
            Quaternion turned = Handles.RotationHandle(rotation, moved);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 local = root.localToWorldMatrix.transpose.MultiplyVector(turned * Vector3.forward);
                normal.vector3Value = new Vector3(local.x, 0f, local.z).normalized;
            }
            Handles.ArrowHandleCap(0, moved, rotation, HandleUtility.GetHandleSize(moved) * 0.8f, EventType.Repaint);
        }

        private static void DrawPathHandles(SerializedProperty path, Transform root, string label)
        {
            if (!path.FindPropertyRelative("enabled").boolValue) return;
            SerializedProperty points = path.FindPropertyRelative("points");
            Vector3[] line = new Vector3[points.arraySize + (path.FindPropertyRelative("closed").boolValue && points.arraySize > 2 ? 1 : 0)];
            for (int index = 0; index < points.arraySize; index++)
            {
                SerializedProperty point = points.GetArrayElementAtIndex(index);
                Vector3 position = root.TransformPoint(point.vector3Value);
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck()) point.vector3Value = root.InverseTransformPoint(moved);
                line[index] = moved;
                Handles.Label(moved, label + ":" + index);
            }

            if (line.Length > points.arraySize) line[line.Length - 1] = line[0];
            if (line.Length > 1) Handles.DrawAAPolyLine(3f, line);
        }

        public static void DrawCreatePortsButton(SerializedObject serialized, IReadOnlyList<FragmentBoundaryPath> boundaries)
        {
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (!GUILayout.Button("Create Named Ports From Boundaries")) return;
                SerializedProperty ports = serialized.FindProperty("ports");
                if (ports.arraySize > 0 && !EditorUtility.DisplayDialog("Replace port definitions?",
                    "This replaces port IDs and shapes. Existing map connections may need updating.", "Replace", "Cancel")) return;
                List<FragmentIslandPort> definitions = FragmentIslandPort.FromBoundaries(boundaries);
                ports.arraySize = definitions.Count;
                for (int index = 0; index < definitions.Count; index++)
                {
                    SerializedProperty port = ports.GetArrayElementAtIndex(index);
                    port.FindPropertyRelative("id").stringValue = definitions[index].id;
                    port.FindPropertyRelative("useCustomAttachment").boolValue = false;
                    port.FindPropertyRelative("attachmentPosition").vector3Value = Vector3.zero;
                    port.FindPropertyRelative("attachmentNormal").vector3Value = Vector3.forward;
                    port.FindPropertyRelative("attachmentWidth").floatValue = 1f;
                    SerializedProperty path = port.FindPropertyRelative("boundary");
                    path.FindPropertyRelative("enabled").boolValue = true;
                    path.FindPropertyRelative("closed").boolValue = definitions[index].boundary.closed;
                    SerializedProperty points = path.FindPropertyRelative("points");
                    points.arraySize = definitions[index].boundary.points.Count;
                    for (int point = 0; point < points.arraySize; point++)
                        points.GetArrayElementAtIndex(point).vector3Value = definitions[index].boundary.points[point];
                }

                serialized.ApplyModifiedProperties();
                SceneView.RepaintAll();
            }
        }

        public static void DrawAddButtons(SerializedObject serialized, Transform root)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Left Edge")) AddPath(serialized, root, false);
            if (GUILayout.Button("Add Bounds Outline")) AddPath(serialized, root, true);
            EditorGUILayout.EndHorizontal();
        }

        public static void DrawFootprintButtons(SerializedObject serialized, Transform root, IReadOnlyList<FragmentBoundaryPath> paths)
        {
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Add Docking Point"))
                {
                    SerializedProperty ports = serialized.FindProperty("ports");
                    HashSet<string> ids = new HashSet<string>();
                    for (int index = 0; index < ports.arraySize; index++) ids.Add(ports.GetArrayElementAtIndex(index).FindPropertyRelative("id").stringValue);
                    int number = 1;
                    while (ids.Contains("Port_" + number)) number++;
                    ports.arraySize++;
                    SerializedProperty port = ports.GetArrayElementAtIndex(ports.arraySize - 1);
                    port.FindPropertyRelative("id").stringValue = "Port_" + number;
                    port.FindPropertyRelative("useCustomAttachment").boolValue = true;
                    Bounds bounds = GetLocalBounds(root);
                    port.FindPropertyRelative("attachmentPosition").vector3Value = new Vector3(bounds.center.x, 0f, bounds.max.z);
                    port.FindPropertyRelative("attachmentNormal").vector3Value = Vector3.forward;
                    port.FindPropertyRelative("attachmentWidth").floatValue = 1f;
                    SerializedProperty boundary = port.FindPropertyRelative("boundary");
                    boundary.FindPropertyRelative("enabled").boolValue = true;
                    boundary.FindPropertyRelative("closed").boolValue = false;
                    boundary.FindPropertyRelative("points").arraySize = 0;
                    serialized.ApplyModifiedProperties();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("Set Bounds Footprint"))
                {
                    Bounds bounds = GetLocalBounds(root);
                    SetFootprint(serialized, new[] { new Vector2(bounds.min.x, bounds.min.z), new Vector2(bounds.max.x, bounds.min.z),
                        new Vector2(bounds.max.x, bounds.max.z), new Vector2(bounds.min.x, bounds.max.z) });
                }
                FragmentBoundaryPath closed = null;
                if (paths != null)
                    foreach (FragmentBoundaryPath path in paths)
                        if (path.enabled && path.closed && path.points.Count >= 3) { closed = path; break; }
                using (new EditorGUI.DisabledScope(closed == null))
                {
                    if (GUILayout.Button("Copy Closed Boundary To Footprint"))
                    {
                        List<Vector2> points = new List<Vector2>();
                        foreach (Vector3 point in closed.points) points.Add(new Vector2(point.x, point.z));
                        SetFootprint(serialized, points);
                    }
                }
            }
        }

        private static void SetFootprint(SerializedObject serialized, IReadOnlyList<Vector2> points)
        {
            SerializedProperty footprint = serialized.FindProperty("footprint");
            footprint.arraySize = points.Count;
            for (int index = 0; index < points.Count; index++) footprint.GetArrayElementAtIndex(index).vector2Value = points[index];
            serialized.ApplyModifiedProperties();
            SceneView.RepaintAll();
        }

        private static void AddPath(SerializedObject serialized, Transform root, bool closed)
        {
            Bounds bounds = GetLocalBounds(root);
            SerializedProperty paths = serialized.FindProperty("boundaryPaths");
            paths.arraySize++;
            SerializedProperty path = paths.GetArrayElementAtIndex(paths.arraySize - 1);
            path.FindPropertyRelative("enabled").boolValue = true;
            path.FindPropertyRelative("closed").boolValue = closed;
            SerializedProperty points = path.FindPropertyRelative("points");
            points.arraySize = closed ? 4 : 2;
            points.GetArrayElementAtIndex(0).vector3Value = new Vector3(bounds.min.x, 0f, bounds.min.z);
            points.GetArrayElementAtIndex(1).vector3Value = new Vector3(bounds.min.x, 0f, bounds.max.z);
            if (closed)
            {
                points.GetArrayElementAtIndex(2).vector3Value = new Vector3(bounds.max.x, 0f, bounds.max.z);
                points.GetArrayElementAtIndex(3).vector3Value = new Vector3(bounds.max.x, 0f, bounds.min.z);
            }
            serialized.ApplyModifiedProperties();
            SceneView.RepaintAll();
        }

        private static Bounds GetLocalBounds(Transform root)
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            bool found = false;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || (filter.gameObject.hideFlags & HideFlags.DontSave) != 0) continue;
                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int index = 0; index < 8; index++)
                {
                    Vector3 corner = mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((index & 1) == 0 ? -1 : 1, (index & 2) == 0 ? -1 : 1, (index & 4) == 0 ? -1 : 1));
                    Vector3 point = matrix.MultiplyPoint3x4(corner);
                    if (!found) bounds = new Bounds(point, Vector3.zero);
                    else bounds.Encapsulate(point);
                    found = true;
                }
            }

            return bounds;
        }
    }

    [CustomEditor(typeof(FragmentIslandAuthoring))]
    public sealed class FragmentIslandAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "outputFolder", "outputName", "bakeRecord");
            FragmentIslandAuthoring authoring = (FragmentIslandAuthoring)target;
            FragmentPathEditor.DrawAddButtons(serializedObject, authoring.transform);
            serializedObject.ApplyModifiedProperties();
            FragmentPathEditor.DrawCreatePortsButton(serializedObject, authoring.BoundaryPaths);
            FragmentPathEditor.DrawFootprintButtons(serializedObject, authoring.transform, authoring.BoundaryPaths);
            SerializedProperty record = serializedObject.FindProperty("bakeRecord");
            using (new EditorGUI.DisabledScope(record.objectReferenceValue != null))
            {
                SerializedProperty folder = serializedObject.FindProperty("outputFolder");
                EditorGUILayout.BeginHorizontal();
                folder.stringValue = EditorGUILayout.TextField("Output Folder", folder.stringValue);
                if (GUILayout.Button("Browse", GUILayout.Width(65)))
                {
                    string selected = EditorUtility.OpenFolderPanel("Fragment island output", Application.dataPath, string.Empty);
                    if (!string.IsNullOrEmpty(selected)) folder.stringValue = FragmentIslandBaker.ValidateFolder(selected);
                }

                EditorGUILayout.EndHorizontal();
                DefaultAsset currentFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder.stringValue);
                DefaultAsset dragged = (DefaultAsset)EditorGUILayout.ObjectField("Folder Asset", currentFolder, typeof(DefaultAsset), false);
                if (dragged != null && dragged != currentFolder)
                {
                    string path = AssetDatabase.GetAssetPath(dragged);
                    if (AssetDatabase.IsValidFolder(path)) folder.stringValue = path;
                }

                SerializedProperty name = serializedObject.FindProperty("outputName");
                name.stringValue = EditorGUILayout.TextField("Output Name", name.stringValue);
            }

            EditorGUILayout.PropertyField(record, new GUIContent("Linked Bake Record"));
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Save source prefab edits before baking. Output is a visual-only clone; source scripts and colliders are not copied. " +
                "All saved models share one reveal range. With no paths, progress applies uniformly. " +
                "For generated maps, define an XZ Footprint and docking points with outward normals. " +
                "Green handles edit the footprint; yellow handles edit ports. Multiple ports can share a long side.", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button(authoring.BakeRecord != null ? "Regenerate Linked Island" : "Bake Island Prefab"))
                {
                    FragmentIslandBaker.Bake(authoring);
                    serializedObject.Update();
                }
            }

            if (authoring.BakeRecord != null)
            {
                EditorGUILayout.LabelField("Saved output", FragmentIslandBaker.GetRecordFolder(authoring.BakeRecord));
                if (GUILayout.Button("Open Generated Prefab") && authoring.BakeRecord.GeneratedPrefab != null)
                    AssetDatabase.OpenAsset(authoring.BakeRecord.GeneratedPrefab);
                if (GUILayout.Button("Unlink Output (Keep Files)") && EditorUtility.DisplayDialog("Unlink output?",
                    "Existing generated assets will remain. The next bake creates a separate output folder.", "Unlink", "Cancel"))
                {
                    serializedObject.Update();
                    serializedObject.FindProperty("bakeRecord").objectReferenceValue = null;
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }

        private void OnSceneGUI() => FragmentPathEditor.DrawHandles(serializedObject, ((FragmentIslandAuthoring)target).transform);
    }

    [CustomEditor(typeof(FragmentRevealGroup))]
    public sealed class FragmentRevealGroupEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            FragmentRevealGroup group = (FragmentRevealGroup)target;
            FragmentPathEditor.DrawAddButtons(serializedObject, group.transform);
            EditorGUILayout.LabelField("Boundary segments / renderers", group.BoundarySegmentCount + " / " + group.Renderers.Count);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Hide")) SetProgress(group, 0f);
            if (GUILayout.Button("Show")) SetProgress(group, 1f);
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Play Reveal")) group.PlayReveal();
                if (GUILayout.Button("Play Hide")) group.PlayHide();
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Refresh Children")) group.RefreshChildren();
        }

        private static void SetProgress(FragmentRevealGroup group, float progress)
        {
            Undo.RecordObject(group, "Set island reveal progress");
            group.SetProgress(progress);
            EditorUtility.SetDirty(group);
            SceneView.RepaintAll();
        }

        private void OnSceneGUI() => FragmentPathEditor.DrawHandles(serializedObject, ((FragmentRevealGroup)target).transform);
    }

    [CustomEditor(typeof(FragmentIslandBakeRecord))]
    public sealed class FragmentIslandBakeRecordEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            FragmentIslandBakeRecord record = (FragmentIslandBakeRecord)target;
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Generated Prefab", record.GeneratedPrefab, typeof(GameObject), false);
            EditorGUILayout.LabelField("Output", FragmentIslandBaker.GetRecordFolder(record));
            string path = AssetDatabase.GUIDToAssetPath(record.SourceGuid);
            EditorGUILayout.LabelField("Source", path);
            if (GUILayout.Button("Open Source") && !string.IsNullOrEmpty(path))
                AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(path));
            using (new EditorGUI.DisabledScope(Application.isPlaying || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)))
            {
                if (GUILayout.Button("Regenerate From Saved Source")) FragmentIslandBaker.RegenerateSavedSource(record);
            }
        }
    }
}
