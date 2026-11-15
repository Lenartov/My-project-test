using UnityEditor;
using UnityEngine;

namespace SurfaceFragmentPrototype.Editor
{
    [CustomEditor(typeof(SurfaceFragmentRevealPrototype))]
    public sealed class SurfaceFragmentRevealPrototypeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            SurfaceFragmentRevealPrototype prototype = (SurfaceFragmentRevealPrototype)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Candidate grid cells (bounds estimate)", prototype.EstimatedCellCount.ToString("N0"));
            if (prototype.EstimatedCellCount > 500000)
            {
                EditorGUILayout.HelpBox("This can generate a very large mesh and use substantial time and memory. " +
                    "Generation is allowed; the progress dialog supports cancellation.", MessageType.Warning);
            }

            EditorGUILayout.LabelField("Preview fragments / vertices", prototype.FragmentCount + " / " + prototype.PreviewVertexCount);
            using (new EditorGUI.DisabledScope(!prototype.isActiveAndEnabled))
            {
                if (GUILayout.Button("Build Preview"))
                {
                    prototype.BuildPreview();
                    SceneView.RepaintAll();
                }
            }

            if (GUILayout.Button("Clear Preview"))
            {
                prototype.ClearPreview();
                SceneView.RepaintAll();
            }
        }
    }
}
