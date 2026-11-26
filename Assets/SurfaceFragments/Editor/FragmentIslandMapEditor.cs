using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SurfaceFragments.Editor
{
    [CustomEditor(typeof(FragmentIslandMap))]
    public sealed class FragmentIslandMapEditor : UnityEditor.Editor
    {
        private readonly Dictionary<FragmentRevealGroup, FragmentIslandPorts> portCache = new Dictionary<FragmentRevealGroup, FragmentIslandPorts>();
        private string validation;
        private bool valid;

        private void OnEnable() => Validate();

        public override void OnInspectorGUI()
        {
            FragmentIslandMap map = (FragmentIslandMap)target;
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("nodes"), true);
                List<string> ids = GetNodeIds();
                DrawChoice("Initially Open", serializedObject.FindProperty("startingNodeId"), ids, true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("initializeOnStart"));
                SerializedProperty links = serializedObject.FindProperty("connections");
                EditorGUILayout.LabelField("Connections", EditorStyles.boldLabel);
                for (int index = 0; index < links.arraySize; index++)
                {
                    SerializedProperty link = links.GetArrayElementAtIndex(index);
                    EditorGUILayout.BeginVertical("box");
                    DrawChoice("Island A", link.FindPropertyRelative("from"), ids);
                    DrawChoice("Port A", link.FindPropertyRelative("fromPort"), GetPortIds(link.FindPropertyRelative("from").stringValue));
                    DrawChoice("Island B", link.FindPropertyRelative("to"), ids);
                    DrawChoice("Port B", link.FindPropertyRelative("toPort"), GetPortIds(link.FindPropertyRelative("to").stringValue));
                    bool remove = GUILayout.Button("Remove Connection");
                    EditorGUILayout.EndVertical();
                    if (remove)
                    {
                        links.DeleteArrayElementAtIndex(index);
                        break;
                    }
                }

                if (GUILayout.Button("Add Connection"))
                {
                    links.arraySize++;
                    SerializedProperty link = links.GetArrayElementAtIndex(links.arraySize - 1);
                    link.FindPropertyRelative("from").stringValue = ids.Count > 0 ? ids[0] : string.Empty;
                    link.FindPropertyRelative("to").stringValue = ids.Count > 1 ? ids[1] : string.Empty;
                    link.FindPropertyRelative("fromPort").stringValue = string.Empty;
                    link.FindPropertyRelative("toPort").stringValue = string.Empty;
                }

                if (GUILayout.Button("Collect Child Islands")) CollectChildren(map);
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("previewProgress"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("animatePreviews"));
            bool changed = EditorGUI.EndChangeCheck();
            bool modified = serializedObject.ApplyModifiedProperties();
            if (changed || modified) Validate();
            if (GUILayout.Button("Validate Graph"))
            {
                portCache.Clear();
                Validate();
            }

            EditorGUILayout.HelpBox(validation, valid ? MessageType.Info : MessageType.Warning);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !map.isActiveAndEnabled))
            {
                if (GUILayout.Button(map.IsInitialized ? "Reset Map" : "Initialize Map"))
                {
                    map.InitializeMap();
                    GUIUtility.ExitGUI();
                }
                if (map.IsInitialized)
                {
                    EditorGUILayout.LabelField("Opening Islands", map.OpeningIslandCount.ToString());
                    EditorGUILayout.LabelField("Closing Islands", map.ClosingIslandCount.ToString());
                    if (GUILayout.Button("Refresh Neighbor Previews")) map.RefreshPreviews();
                    foreach (FragmentIslandMap.Node node in map.Nodes)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(node.id + ": " + map.GetVisibility(node.id));
                            bool close = map.CanCloseIsland(node.id);
                            using (new EditorGUI.DisabledScope(!close && !map.CanOpenIsland(node.id)))
                            {
                                if (GUILayout.Button(close ? "Close" : "Open", GUILayout.Width(65)))
                                {
                                    if (close) map.CloseIsland(node.id);
                                    else map.OpenIsland(node.id);
                                    GUIUtility.ExitGUI();
                                }
                            }
                        }
                    }
                }
            }
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying && ((FragmentIslandMap)target).IsInitialized;

        private List<string> GetNodeIds()
        {
            List<string> ids = new List<string>();
            SerializedProperty nodes = serializedObject.FindProperty("nodes");
            for (int index = 0; index < nodes.arraySize; index++)
            {
                string id = nodes.GetArrayElementAtIndex(index).FindPropertyRelative("id").stringValue;
                if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
            }

            return ids;
        }

        private List<string> GetPortIds(string id)
        {
            List<string> ids = new List<string>();
            SerializedProperty nodes = serializedObject.FindProperty("nodes");
            for (int index = 0; index < nodes.arraySize; index++)
            {
                SerializedProperty node = nodes.GetArrayElementAtIndex(index);
                if (node.FindPropertyRelative("id").stringValue != id) continue;
                FragmentRevealGroup group = node.FindPropertyRelative("island").objectReferenceValue as FragmentRevealGroup;
                if (group == null) break;
                FragmentIslandPorts ports = GetPorts(group);
                if (ports != null)
                {
                    foreach (FragmentIslandPort port in ports.Ports) ids.Add(port.id);
                }

                break;
            }

            return ids;
        }

        private FragmentIslandPorts GetPorts(FragmentRevealGroup group)
        {
            if (!portCache.TryGetValue(group, out FragmentIslandPorts ports) || ports == null)
                portCache[group] = ports = group.GetComponent<FragmentIslandPorts>();
            return ports;
        }

        private static void DrawChoice(string label, SerializedProperty property, List<string> ids, bool optional = false)
        {
            List<string> values = new List<string>(ids);
            if (optional) values.Insert(0, string.Empty);
            int selected = values.IndexOf(property.stringValue);
            if (selected < 0)
            {
                values.Insert(0, string.IsNullOrEmpty(property.stringValue) ? "<Select>" : "<Missing: " + property.stringValue + ">");
                selected = 0;
            }

            EditorGUI.BeginChangeCheck();
            string[] labels = values.ToArray();
            if (optional)
                for (int index = 0; index < values.Count; index++)
                    if (string.IsNullOrEmpty(values[index])) labels[index] = "None (all hidden)";
            int next = EditorGUILayout.Popup(label, selected, labels);
            if (EditorGUI.EndChangeCheck()) property.stringValue = values[next];
        }

        private void CollectChildren(FragmentIslandMap map)
        {
            SerializedProperty nodes = serializedObject.FindProperty("nodes");
            HashSet<FragmentRevealGroup> existing = new HashSet<FragmentRevealGroup>();
            HashSet<string> ids = new HashSet<string>(GetNodeIds(), StringComparer.Ordinal);
            for (int index = 0; index < nodes.arraySize; index++)
                existing.Add(nodes.GetArrayElementAtIndex(index).FindPropertyRelative("island").objectReferenceValue as FragmentRevealGroup);
            foreach (FragmentRevealGroup group in map.GetComponentsInChildren<FragmentRevealGroup>(true))
            {
                FragmentRevealGroup parent = group.transform.parent != null ? group.transform.parent.GetComponentInParent<FragmentRevealGroup>(true) : null;
                if (existing.Contains(group) || parent != null) continue;
                string id = group.name;
                int suffix = 1;
                while (!ids.Add(id)) id = group.name + "_" + suffix++;
                nodes.arraySize++;
                SerializedProperty node = nodes.GetArrayElementAtIndex(nodes.arraySize - 1);
                node.FindPropertyRelative("id").stringValue = id;
                node.FindPropertyRelative("island").objectReferenceValue = group;
            }

        }

        private void Validate()
        {
            try
            {
                ((FragmentIslandMap)target).ValidateConfiguration();
                valid = true;
                validation = "Graph is valid. Open any island independently; connections carry neighbor reveal influence. Preview islands do not propagate discovery.";
            }
            catch (ArgumentException error)
            {
                valid = false;
                validation = error.Message;
            }
        }

        private void OnSceneGUI()
        {
            FragmentIslandMap map = (FragmentIslandMap)target;
            Color previous = Handles.color;
            foreach (FragmentMapConnection link in map.Connections)
            {
                FragmentIslandMap.Node from = null;
                FragmentIslandMap.Node to = null;
                foreach (FragmentIslandMap.Node node in map.Nodes)
                {
                    if (node.id == link.from) from = node;
                    if (node.id == link.to) to = node;
                }

                if (from == null || to == null || from.island == null || to.island == null) continue;
                Handles.color = map.IsInitialized && map.GetVisibility(to.id) == FragmentMapVisibility.Revealed ? Color.green : Color.cyan;
                Vector3 a = PortCenter(from, link.fromPort);
                Vector3 b = PortCenter(to, link.toPort);
                Handles.DrawAAPolyLine(3f, a, b);
                Handles.Label((a + b) * 0.5f, link.from + " -- " + link.to);
            }

            Handles.color = previous;
        }

        private Vector3 PortCenter(FragmentIslandMap.Node node, string id)
        {
            FragmentIslandPorts ports = GetPorts(node.island);
            if (ports == null || !ports.TryGetPort(id, out FragmentBoundaryPath path) || path.points.Count == 0)
                return node.island.transform.position;
            Vector3 center = Vector3.zero;
            foreach (Vector3 point in path.points) center += point / path.points.Count;
            return node.island.transform.TransformPoint(center);
        }
    }
}
