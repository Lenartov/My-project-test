using System;
using UnityEditor;
using UnityEngine;

namespace SurfaceFragments.Editor
{
    [CustomEditor(typeof(FragmentIslandMapGenerator))]
    public sealed class FragmentIslandMapGeneratorEditor : UnityEditor.Editor
    {
        private FragmentIslandLayout preview;
        private string error;

        public override void OnInspectorGUI()
        {
            FragmentIslandMapGenerator generator = (FragmentIslandMapGenerator)target;
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            if (serializedObject.ApplyModifiedProperties())
            {
                preview = null;
                error = null;
                SceneView.RepaintAll();
            }

            EditorGUILayout.HelpBox("Assign baked prefabs with named docking ports and an optional XZ footprint. " +
                "Ports determine placement and rotation. Preview Layout draws the actual footprints without creating objects.", MessageType.Info);
            if (GUILayout.Button("Preview Layout"))
            {
                try
                {
                    preview = generator.CreateLayout();
                    error = null;
                    SceneView.RepaintAll();
                }
                catch (ArgumentException exception)
                {
                    preview = null;
                    error = exception.Message;
                }
            }

            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !generator.isActiveAndEnabled))
            {
                if (GUILayout.Button(generator.GeneratedMap == null ? "Generate Map" : "Regenerate Map"))
                {
                    generator.GenerateMap();
                    GUIUtility.ExitGUI();
                }

                using (new EditorGUI.DisabledScope(generator.GeneratedMap == null))
                {
                    if (GUILayout.Button("Clear Generated Map"))
                    {
                        generator.ClearGeneratedMap();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            FragmentIslandMap map = generator.GeneratedMap;
            if (map != null && map.IsInitialized)
            {
                EditorGUILayout.LabelField("Opening Islands", map.OpeningIslandCount.ToString());
                EditorGUILayout.LabelField("Closing Islands", map.ClosingIslandCount.ToString());
                foreach (FragmentIslandMap.Node node in map.Nodes)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(node.id + ": " + map.GetVisibility(node.id));
                        bool close = map.CanCloseIsland(node.id);
                        using (new EditorGUI.DisabledScope(!map.isActiveAndEnabled || !close && !map.CanOpenIsland(node.id)))
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

                if (GUILayout.Button("Select Runtime Map")) Selection.activeGameObject = map.gameObject;
            }

            FragmentIslandLayout layout = map != null ? generator.GeneratedLayout : preview;
            if (layout != null)
            {
                EditorGUILayout.LabelField("Placed / Requested / Connections", layout.Placements.Count + " / " + layout.RequestedCount + " / " + layout.Links.Count);
                if (!layout.IsComplete) EditorGUILayout.HelpBox(layout.Diagnostic, MessageType.Warning);
            }
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying && ((FragmentIslandMapGenerator)target).GeneratedMap != null;

        private void OnSceneGUI()
        {
            FragmentIslandMapGenerator generator = (FragmentIslandMapGenerator)target;
            FragmentIslandLayout layout = generator.GeneratedMap != null ? generator.GeneratedLayout : preview;
            if (layout == null) return;
            Color previous = Handles.color;
            Handles.color = Color.cyan;
            foreach (FragmentIslandLayout.Link link in layout.Links)
            {
                Handles.DrawDottedLine(generator.transform.TransformPoint(layout.Placements[link.from].position),
                    generator.transform.TransformPoint(layout.Placements[link.to].position), 4f);
                Vector3 a = generator.transform.TransformPoint(layout.GetSocketPosition(link.from, link.fromPort));
                Vector3 b = generator.transform.TransformPoint(layout.GetSocketPosition(link.to, link.toPort));
                Handles.DrawAAPolyLine(3f, a, b);
                Handles.DrawWireDisc((a + b) * 0.5f, generator.transform.up, HandleUtility.GetHandleSize(a) * 0.08f);
            }
            for (int index = 0; index < layout.Placements.Count; index++)
            {
                FragmentIslandLayout.Placement placement = layout.Placements[index];
                Vector3 position = generator.transform.TransformPoint(placement.position);
                Handles.Label(position, "Island_" + index.ToString("D3") + " " + layout.Templates[placement.templateIndex].name);
                System.Collections.Generic.IReadOnlyList<Vector2> footprint = layout.GetFootprint(index);
                Vector3[] outline = new Vector3[footprint.Count + 1];
                for (int point = 0; point < footprint.Count; point++)
                    outline[point] = generator.transform.TransformPoint(new Vector3(footprint[point].x, placement.position.y, footprint[point].y));
                outline[outline.Length - 1] = outline[0];
                Handles.DrawAAPolyLine(2f, outline);
            }

            Handles.color = previous;
        }
    }
}
