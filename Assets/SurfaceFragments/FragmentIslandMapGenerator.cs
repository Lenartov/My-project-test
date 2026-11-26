using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SurfaceFragments
{
    [DisallowMultipleComponent]
    public sealed class FragmentIslandMapGenerator : MonoBehaviour
    {
        [Serializable]
        public sealed class IslandTemplate
        {
            public FragmentRevealGroup prefab;
        }

        private sealed class TemplateInfo
        {
            public IslandTemplate template;
            public FragmentIslandLayout.Template shape;
        }

        [SerializeField] private int seed = 12345;
        [SerializeField, Min(1)] private int islandCount = 7;
        [SerializeField, Range(0f, 1f)] private float extraConnectionChance = 0.35f;
        [SerializeField, Min(0f), Tooltip("Distance between connected docking points along their outward normal. Zero joins the points exactly.")]
        private float islandGap = 0f;
        [SerializeField] private bool openFirstIsland = true;
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private List<IslandTemplate> templates = new List<IslandTemplate>();

        private GameObject generatedRoot;
        public FragmentIslandMap GeneratedMap { get; private set; }
        public FragmentIslandLayout GeneratedLayout { get; private set; }
        public event Action<FragmentIslandMap> MapGenerated;

        private void Start()
        {
            if (generateOnStart && GeneratedMap == null) GenerateMap();
        }

        private void OnDestroy()
        {
            if (Application.isPlaying && generatedRoot != null)
            {
                generatedRoot.SetActive(false);
                Destroy(generatedRoot);
            }
        }

        public FragmentIslandLayout CreateLayout()
        {
            return CreateLayout(ReadTemplates());
        }

        private FragmentIslandLayout CreateLayout(List<TemplateInfo> available)
        {
            List<FragmentIslandLayout.Template> shapes = new List<FragmentIslandLayout.Template>(available.Count);
            foreach (TemplateInfo template in available) shapes.Add(template.shape);
            return FragmentIslandLayout.Generate(shapes, seed, islandCount, islandGap, extraConnectionChance);
        }

        public void GenerateMap()
        {
            RequirePlayMode();
            List<TemplateInfo> available = ReadTemplates();
            FragmentIslandLayout layout = CreateLayout(available);
            List<FragmentIslandMap.Node> nodes = new List<FragmentIslandMap.Node>(layout.Placements.Count);
            List<FragmentMapConnection> connections = new List<FragmentMapConnection>(layout.Links.Count);
            GameObject staging = new GameObject("Generated Fragment Map");
            staging.SetActive(false);
            staging.transform.SetParent(transform, false);
            try
            {
                for (int index = 0; index < layout.Placements.Count; index++)
                {
                    FragmentIslandLayout.Placement placement = layout.Placements[index];
                    TemplateInfo chosen = available[placement.templateIndex];
                    FragmentRevealGroup instance = Instantiate(chosen.template.prefab, staging.transform, false);
                    string id = "Island_" + index.ToString("D3", CultureInfo.InvariantCulture);
                    instance.name = id + "_" + chosen.template.prefab.name;
                    instance.transform.localPosition = placement.position;
                    instance.transform.localRotation = Quaternion.Euler(0f, placement.yaw, 0f);
                    nodes.Add(new FragmentIslandMap.Node { id = id, island = instance });
                }

                foreach (FragmentIslandLayout.Link link in layout.Links)
                {
                    connections.Add(new FragmentMapConnection
                    {
                        from = nodes[link.from].id,
                        fromPort = layout.Templates[layout.Placements[link.from].templateIndex].Sockets[link.fromPort].id,
                        to = nodes[link.to].id,
                        toPort = layout.Templates[layout.Placements[link.to].templateIndex].Sockets[link.toPort].id
                    });
                }

                FragmentIslandMap map = staging.AddComponent<FragmentIslandMap>();
                map.Configure(nodes, connections, openFirstIsland ? nodes[0].id : null);
                // Initialize synchronously after activation, before the first rendered frame.
                staging.SetActive(true);
                map.InitializeMap();
                if (generatedRoot != null)
                {
                    generatedRoot.SetActive(false);
                    Destroy(generatedRoot);
                }

                generatedRoot = staging;
                GeneratedMap = map;
                GeneratedLayout = layout;
                staging = null;
            }
            finally
            {
                if (staging != null)
                {
                    staging.SetActive(false);
                    Destroy(staging);
                }
            }

            MapGenerated?.Invoke(GeneratedMap);
            if (!layout.IsComplete) Debug.LogWarning(layout.Diagnostic, this);
        }

        public void ClearGeneratedMap()
        {
            RequirePlayMode();
            if (generatedRoot != null)
            {
                generatedRoot.SetActive(false);
                Destroy(generatedRoot);
            }

            generatedRoot = null;
            GeneratedMap = null;
            GeneratedLayout = null;
        }

        private List<TemplateInfo> ReadTemplates()
        {
            if (!float.IsFinite(islandGap) || islandGap < 0f)
                throw new ArgumentException("Island Gap must be finite and nonnegative.");
            if (templates.Count == 0) throw new ArgumentException("Assign at least one baked island prefab in Templates.");
            List<TemplateInfo> result = new List<TemplateInfo>(templates.Count);
            foreach (IslandTemplate template in templates)
            {
                if (template == null || template.prefab == null || template.prefab.gameObject.scene.IsValid())
                    throw new ArgumentException("Assign saved baked prefab assets, not scene instances, in Templates.");
                FragmentRevealGroup prefab = template.prefab;
                if (!prefab.enabled || !prefab.gameObject.activeSelf)
                    throw new ArgumentException("The island prefab root and its reveal group must be enabled: " + prefab.name);
                if (prefab.GetComponentsInChildren<FragmentRevealGroup>(true).Length != 1)
                    throw new ArgumentException("A generated island template must have one root reveal group: " + prefab.name);
                FragmentIslandPorts ports = prefab.GetComponent<FragmentIslandPorts>();
                if (ports == null) throw new ArgumentException("The island prefab needs named FragmentIslandPorts: " + prefab.name);
                ports.ValidatePorts();
                Vector3 scale = prefab.transform.localScale;
                if (!float.IsFinite(scale.x) || !float.IsFinite(scale.y) || !float.IsFinite(scale.z) || scale.x == 0f || scale.y == 0f || scale.z == 0f)
                    throw new ArgumentException("The island prefab root needs finite, nonzero scale: " + prefab.name);

                FragmentRevealRenderer[] renderers = prefab.GetComponentsInChildren<FragmentRevealRenderer>(true);
                if (renderers.Length == 0) throw new ArgumentException("The prefab has no baked fragment renderers: " + prefab.name);
                Bounds bounds = default;
                bool found = false;
                Matrix4x4 rootMatrix = Matrix4x4.Scale(prefab.transform.localScale) * prefab.transform.worldToLocalMatrix;
                foreach (FragmentRevealRenderer renderer in renderers)
                {
                    if (renderer.Data == null || renderer.Data.Mesh == null)
                        throw new ArgumentException("Missing baked fragment data in " + prefab.name);
                    Bounds mesh = renderer.Data.Mesh.bounds;
                    Matrix4x4 matrix = rootMatrix * renderer.transform.localToWorldMatrix;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 point = matrix.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents,
                            new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                        if (!found) bounds = new Bounds(point, Vector3.zero);
                        else bounds.Encapsulate(point);
                        found = true;
                    }
                }

                List<Vector2> polygon = new List<Vector2>();
                if (ports.Footprint.Count > 0)
                    foreach (Vector2 point in ports.Footprint) polygon.Add(new Vector2(point.x * scale.x, point.y * scale.z));
                else
                    polygon.AddRange(new[] { new Vector2(bounds.min.x, bounds.min.z), new Vector2(bounds.max.x, bounds.min.z),
                        new Vector2(bounds.max.x, bounds.max.z), new Vector2(bounds.min.x, bounds.max.z) });
                Vector2[] outline = FragmentIslandFootprint.Prepare(polygon, out _);
                List<FragmentIslandLayout.Socket> sockets = new List<FragmentIslandLayout.Socket>();
                foreach (FragmentIslandPort port in ports.Ports)
                {
                    Vector3 anchor = Vector3.Scale(port.GetAttachmentPosition(), scale);
                    Vector2 normal = port.useCustomAttachment
                        ? new Vector2(port.attachmentNormal.x / scale.x, port.attachmentNormal.z / scale.z)
                        : FragmentIslandFootprint.OutwardNormal(new Vector2(anchor.x, anchor.z), outline);
                    sockets.Add(new FragmentIslandLayout.Socket(port.id, anchor, normal));
                }
                result.Add(new TemplateInfo { template = template, shape = new FragmentIslandLayout.Template(prefab.name, outline, sockets) });
            }
            return result;
        }

        private void RequirePlayMode()
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled)
                throw new InvalidOperationException("Generate or clear maps on an enabled generator in Play Mode.");
        }
    }
}
