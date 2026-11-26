using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFragments
{
    [DisallowMultipleComponent]
    public sealed class FragmentIslandAuthoring : MonoBehaviour
    {
        [SerializeField, Tooltip("Any positive size in each source mesh's local units.")]
        private float cellSize = 0.5f;
        [SerializeField] private Color fallbackColor = Color.white;
        [SerializeField] private FragmentRevealSettings revealSettings = new FragmentRevealSettings();
        [SerializeField, Tooltip("Ordered island-local points. Several paths remain independent; Closed joins the last point to the first.")]
        private List<FragmentBoundaryPath> boundaryPaths = new List<FragmentBoundaryPath>();
        [SerializeField, Tooltip("Named map connection ports. If empty, baking derives ports from the boundary paths.")]
        private List<FragmentIslandPort> ports = new List<FragmentIslandPort>();
        [SerializeField, Tooltip("Closed root-local XZ footprint for placement. Empty uses mesh bounds; concave simple polygons are supported.")]
        private List<Vector2> footprint = new List<Vector2>();
        [SerializeField, HideInInspector] private string outputFolder = "Assets/Generated/FragmentIslands";
        [SerializeField, HideInInspector] private string outputName;
        [SerializeField, HideInInspector] private FragmentIslandBakeRecord bakeRecord;

        public float CellSize => cellSize;
        public Color FallbackColor => fallbackColor;
        public FragmentRevealSettings RevealSettings => revealSettings;
        public IReadOnlyList<FragmentBoundaryPath> BoundaryPaths => boundaryPaths;
        public IReadOnlyList<FragmentIslandPort> Ports => ports;
        public IReadOnlyList<Vector2> Footprint => footprint;
        public string OutputFolder => outputFolder;
        public string OutputName => string.IsNullOrWhiteSpace(outputName) ? name + "_Fragments" : outputName;
        public FragmentIslandBakeRecord BakeRecord => bakeRecord;
        public void SetBakeRecord(FragmentIslandBakeRecord record) => bakeRecord = record;
    }
}
