using UnityEditor;

namespace SurfaceFragments.Editor
{
    [CustomEditor(typeof(FragmentIslandPorts))]
    public sealed class FragmentIslandPortsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            FragmentIslandPorts ports = (FragmentIslandPorts)target;
            FragmentRevealGroup group = ports.GetComponent<FragmentRevealGroup>();
            if (group != null) FragmentPathEditor.DrawCreatePortsButton(serializedObject, group.BoundaryPaths);
            FragmentPathEditor.DrawFootprintButtons(serializedObject, ports.transform, group != null ? group.BoundaryPaths : null);
            EditorGUILayout.HelpBox("Footprint is an ordered XZ outline; green handles edit it. Docking points can use any names and outward directions. " +
                "Add multiple points along a long side for multiple neighbors. Configure the source before baking to retain changes.", MessageType.Info);
        }

        private void OnSceneGUI() => FragmentPathEditor.DrawHandles(serializedObject, ((FragmentIslandPorts)target).transform);
    }
}
