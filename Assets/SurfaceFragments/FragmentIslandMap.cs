using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFragments
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class FragmentIslandMap : MonoBehaviour
    {
        [Serializable]
        public sealed class Node
        {
            public string id;
            public FragmentRevealGroup island;
        }

        private sealed class BoundarySource
        {
            public string sourceId;
            public FragmentRevealGroup source;
            public float[] coverage;
            public Vector3[] sourcePoints;
        }

        private sealed class PreviewBoundary
        {
            public FragmentRevealGroup target;
            public readonly List<string> portIds = new List<string>();
            public readonly List<float[]> profiles = new List<float[]>();
            public readonly List<float[]> pendingProfiles = new List<float[]>();
            public readonly List<BoundarySource> sources = new List<BoundarySource>();
        }

        private sealed class RevealAnimation
        {
            public FragmentRevealGroup group;
            public float startProgress;
            public float targetProgress = 1f;
            public float initialInfluence;
            public bool closing;
            public PreviewBoundary boundary;
            public readonly List<float[]> initialProfiles = new List<float[]>();

            public float Fraction => Mathf.Abs(targetProgress - startProgress) <= 0.000001f ? 1f : Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((group.RevealProgress - startProgress) / (targetProgress - startProgress)));
            public float Influence => Mathf.Lerp(initialInfluence, closing ? 0f : 1f, Fraction);
        }

        [SerializeField] private List<Node> nodes = new List<Node>();
        [SerializeField] private List<FragmentMapConnection> connections = new List<FragmentMapConnection>();
        [SerializeField] private string startingNodeId;
        [SerializeField] private bool initializeOnStart = true;
        [SerializeField, Range(0f, 1f), Tooltip("Front progress, not an exact fraction of visible fragments.")]
        private float previewProgress = 0.2f;
        [SerializeField, Tooltip("Animate manual Preview Progress changes. Neighbor boundaries always follow the assembling island.")]
        private bool animatePreviews = true;

        private readonly Dictionary<string, Node> runtimeNodes = new Dictionary<string, Node>(StringComparer.Ordinal);
        private readonly Dictionary<string, FragmentIslandPorts> runtimePorts = new Dictionary<string, FragmentIslandPorts>(StringComparer.Ordinal);
        private readonly Dictionary<string, PreviewBoundary> previewBoundaries = new Dictionary<string, PreviewBoundary>(StringComparer.Ordinal);
        private readonly Dictionary<string, RevealAnimation> animations = new Dictionary<string, RevealAnimation>(StringComparer.Ordinal);
        private readonly List<PreviewBoundary> boundaryUpdates = new List<PreviewBoundary>();
        private readonly List<string> completed = new List<string>();
        private FragmentMapState state;

        public IReadOnlyList<Node> Nodes => nodes;
        public IReadOnlyList<FragmentMapConnection> Connections => connections;
        public bool IsInitialized => state != null;
        public int OpeningIslandCount
        {
            get
            {
                int count = 0;
                foreach (RevealAnimation animation in animations.Values) if (!animation.closing) count++;
                return count;
            }
        }
        public int ClosingIslandCount => animations.Count - OpeningIslandCount;
        public event Action<string> IslandOpened;
        public event Action<string> IslandClosed;

        private void Start()
        {
            if (initializeOnStart && !IsInitialized) InitializeMap();
        }

        public void Configure(IReadOnlyList<Node> nodeBindings, IReadOnlyList<FragmentMapConnection> links, string start = null)
        {
            if (IsInitialized) throw new InvalidOperationException("Configure map bindings before InitializeMap.");
            nodes = new List<Node>();
            foreach (Node node in nodeBindings) nodes.Add(new Node { id = node.id, island = node.island });
            connections = new List<FragmentMapConnection>();
            foreach (FragmentMapConnection link in links) connections.Add(link.Copy());
            startingNodeId = start;
        }

        public void ValidateConfiguration()
        {
            CreateState(true);
        }

        public void InitializeMap()
        {
            RequirePlayMode();
            FragmentMapState replacement = CreateState(true);
            runtimeNodes.Clear();
            runtimePorts.Clear();
            previewBoundaries.Clear();
            animations.Clear();
            boundaryUpdates.Clear();
            completed.Clear();
            foreach (Node node in nodes)
            {
                runtimeNodes.Add(node.id, new Node { id = node.id, island = node.island });
                runtimePorts.Add(node.id, node.island.GetComponent<FragmentIslandPorts>());
                List<FragmentBoundaryPath> portPaths = new List<FragmentBoundaryPath>();
                foreach (FragmentIslandPort port in runtimePorts[node.id].Ports) portPaths.Add(port.GetRevealBoundary());
                List<Vector4> support = new List<Vector4>();
                FragmentBoundaryPath.Pack(portPaths, Matrix4x4.identity, support);
                node.island.SetRevealRangeSupport(support);
                node.island.RefreshChildren();
                node.island.HideImmediate();
                node.island.SetBoundaryPaths(Array.Empty<FragmentBoundaryPath>());
            }

            state = replacement;
            if (!string.IsNullOrEmpty(startingNodeId)) runtimeNodes[startingNodeId].island.ShowImmediate();
            RebuildPreviews(false);
            if (!string.IsNullOrEmpty(startingNodeId)) IslandOpened?.Invoke(startingNodeId);
        }

        public FragmentMapVisibility GetVisibility(string id)
        {
            RequireInitialized();
            return state.GetVisibility(id);
        }

        public List<string> GetIslandIds(FragmentMapVisibility visibility)
        {
            RequireInitialized();
            return state.GetIslandIds(visibility);
        }

        public List<string> GetConnectedIslandIds(string id)
        {
            RequireInitialized();
            return state.GetConnectedIslandIds(id);
        }

        public bool CanOpenIsland(string id)
        {
            return IsInitialized && state.CanOpen(id);
        }

        public bool CanCloseIsland(string id)
        {
            return IsInitialized && state.CanClose(id);
        }

        public bool OpenIsland(string id, bool immediate = false)
        {
            RequirePlayMode();
            RequireInitialized();
            FragmentMapVisibility visibility = state.GetVisibility(id);
            if (visibility == FragmentMapVisibility.Revealed || visibility == FragmentMapVisibility.Revealing && !immediate) return false;
            FragmentRevealGroup group = runtimeNodes[id].island;
            if (group == null || !group.isActiveAndEnabled)
                throw new InvalidOperationException("The target island must remain active with its reveal group enabled: " + id);
            if (visibility != FragmentMapVisibility.Revealing) state.TryBeginOpen(id);
            if (immediate)
            {
                group.ShowImmediate();
                animations.Remove(id);
                state.CompleteOpen(id);
                RebuildPreviews(false);
                IslandOpened?.Invoke(id);
            }
            else
            {
                previewBoundaries.TryGetValue(id, out PreviewBoundary boundary);
                float initialInfluence = 0f;
                if (animations.TryGetValue(id, out RevealAnimation previous))
                {
                    boundary = previous.boundary;
                    initialInfluence = previous.Influence;
                    animations.Remove(id);
                }
                RevealAnimation animation = new RevealAnimation { group = group, startProgress = group.RevealProgress,
                    initialInfluence = initialInfluence, boundary = boundary };
                if (boundary != null)
                    foreach (float[] profile in boundary.profiles) animation.initialProfiles.Add((float[])profile.Clone());
                animations.Add(id, animation);
                group.PlayReveal();
                RebuildPreviews(false);
            }

            return true;
        }

        public bool CloseIsland(string id, bool immediate = false)
        {
            RequirePlayMode();
            RequireInitialized();
            FragmentMapVisibility visibility = state.GetVisibility(id);
            if (!state.CanClose(id) && !(immediate && visibility == FragmentMapVisibility.Hiding)) return false;
            FragmentRevealGroup group = runtimeNodes[id].island;
            if (group == null || !group.isActiveAndEnabled)
                throw new InvalidOperationException("The target island must remain active with its reveal group enabled: " + id);
            if (visibility != FragmentMapVisibility.Hiding) state.TryBeginClose(id);
            List<string> ports = state.GetIncomingPortIds(id);
            // Full visibility allows changing the entry geometry without changing the displayed surface.
            group.SetBoundaryPaths(GetPaths(id, ports));
            PreviewBoundary boundary = ports.Count > 0 ? BuildPreviewBoundary(id, ports) : null;
            float target = boundary != null ? previewProgress : 0f;
            if (immediate)
            {
                group.SetProgress(target);
                animations.Remove(id);
                state.CompleteClose(id);
                RebuildPreviews(false);
                IslandClosed?.Invoke(id);
                return true;
            }

            RevealAnimation animation = new RevealAnimation { group = group, startProgress = group.RevealProgress,
                targetProgress = target, initialInfluence = 1f, closing = true, boundary = boundary };
            if (boundary != null)
            {
                foreach (float[] profile in boundary.profiles)
                {
                    for (int sample = 0; sample < profile.Length; sample++) profile[sample] = 1f;
                    animation.initialProfiles.Add((float[])profile.Clone());
                }
                group.SetBoundaryCoverage(boundary.profiles);
            }
            animations.Add(id, animation);
            group.PlayToProgress(target);
            RebuildPreviews(false);
            return true;
        }

        public void RefreshPreviews()
        {
            RequirePlayMode();
            RequireInitialized();
            RebuildPreviews(animatePreviews);
        }

        private void LateUpdate()
        {
            if (state == null || animations.Count == 0) return;
            foreach (RevealAnimation animation in animations.Values)
            {
                if (animation.group == null || !animation.group.isActiveAndEnabled)
                {
                    enabled = false;
                    Debug.LogError("Island reveal interrupted: its group was removed or disabled. Restore it and reinitialize the map.", this);
                    return;
                }
            }

            boundaryUpdates.Clear();
            foreach (PreviewBoundary preview in previewBoundaries.Values)
            {
                bool affected = false;
                foreach (BoundarySource source in preview.sources)
                    affected |= animations.ContainsKey(source.sourceId);
                if (!affected) continue;
                CalculateCoverage(preview, null);
                boundaryUpdates.Add(preview);
            }

            foreach (RevealAnimation animation in animations.Values)
            {
                if (animation.boundary == null) continue;
                CalculateCoverage(animation.boundary, animation);
                boundaryUpdates.Add(animation.boundary);
            }

            // Read every donor's previous profile before publishing any new profile, including in graph cycles.
            foreach (PreviewBoundary boundary in boundaryUpdates)
            {
                CopyCoverage(boundary);
                boundary.target.RefreshBoundaryCoverage();
            }
            boundaryUpdates.Clear();

            completed.Clear();
            foreach (KeyValuePair<string, RevealAnimation> item in animations)
                if (Mathf.Approximately(item.Value.group.RevealProgress, item.Value.targetProgress)) completed.Add(item.Key);
            List<string> closed = null;
            foreach (string id in completed)
            {
                RevealAnimation animation = animations[id];
                animations.Remove(id);
                if (animation.closing)
                {
                    state.CompleteClose(id);
                    if (closed == null) closed = new List<string>();
                    closed.Add(id);
                    if (animation.boundary != null) previewBoundaries[id] = animation.boundary;
                }
                else state.CompleteOpen(id);
            }

            if (closed != null) RebuildPreviews(true);
            if (completed.Count > 0)
                foreach (string id in completed.ToArray())
                {
                    if (closed != null && closed.Contains(id)) IslandClosed?.Invoke(id);
                    else IslandOpened?.Invoke(id);
                }
        }

        private void RebuildPreviews(bool animateProgress)
        {
            Dictionary<string, PreviewBoundary> previous = new Dictionary<string, PreviewBoundary>(previewBoundaries, StringComparer.Ordinal);
            previewBoundaries.Clear();
            foreach (KeyValuePair<string, Node> item in runtimeNodes)
            {
                if (state.GetVisibility(item.Key) == FragmentMapVisibility.Hidden)
                {
                    item.Value.island.HideImmediate();
                    item.Value.island.SetBoundaryPaths(Array.Empty<FragmentBoundaryPath>());
                    continue;
                }
                if (state.GetVisibility(item.Key) != FragmentMapVisibility.Preview) continue;
                List<string> ids = state.GetIncomingPortIds(item.Key);
                if (previous.TryGetValue(item.Key, out PreviewBoundary old))
                    foreach (string port in old.portIds) if (!ids.Contains(port)) ids.Add(port);
                FragmentRevealGroup group = item.Value.island;
                group.SetBoundaryPathsPreservingReveal(GetPaths(item.Key, ids));
                PreviewBoundary preview = BuildPreviewBoundary(item.Key, ids);
                CalculateCoverage(preview, null);
                CopyCoverage(preview);
                group.SetBoundaryCoverage(preview.profiles);
                if (animateProgress) group.PlayToProgress(previewProgress);
                else group.SetProgress(previewProgress);
                previewBoundaries.Add(item.Key, preview);
            }
        }

        private PreviewBoundary BuildPreviewBoundary(string id, List<string> portIds)
        {
            PreviewBoundary preview = new PreviewBoundary { target = runtimeNodes[id].island };
            preview.portIds.AddRange(portIds);
            Dictionary<string, int> offsets = new Dictionary<string, int>(StringComparer.Ordinal);
            List<Vector4> targetSegments = new List<Vector4>();
            foreach (string portId in portIds)
            {
                offsets.Add(portId, preview.profiles.Count);
                runtimePorts[id].TryGetPort(portId, out FragmentBoundaryPath path);
                FragmentBoundaryPath.Pack(new[] { path }, preview.target.transform.localToWorldMatrix, targetSegments);
                foreach (Vector4 segment in targetSegments)
                {
                    preview.profiles.Add(new float[FragmentBoundaryCoverage.SamplesPerSegment]);
                    preview.pendingProfiles.Add(new float[FragmentBoundaryCoverage.SamplesPerSegment]);
                }
            }

            foreach (FragmentMapConnection link in state.Connections)
            {
                string sourceId;
                string sourcePort;
                string targetPort;
                if (link.to == id)
                {
                    sourceId = link.from;
                    sourcePort = link.fromPort;
                    targetPort = link.toPort;
                }
                else if (link.from == id)
                {
                    sourceId = link.to;
                    sourcePort = link.toPort;
                    targetPort = link.fromPort;
                }
                else continue;
                FragmentMapVisibility visibility = state.GetVisibility(sourceId);
                if (visibility != FragmentMapVisibility.Revealed && visibility != FragmentMapVisibility.Revealing && visibility != FragmentMapVisibility.Hiding) continue;
                FragmentRevealGroup source = runtimeNodes[sourceId].island;
                runtimePorts[sourceId].TryGetPort(sourcePort, out FragmentBoundaryPath sourcePath);
                runtimePorts[id].TryGetPort(targetPort, out FragmentBoundaryPath targetPath);
                List<Vector4> sourceSegments = new List<Vector4>();
                FragmentBoundaryPath.Pack(new[] { sourcePath }, source.transform.localToWorldMatrix, sourceSegments);
                FragmentBoundaryPath.Pack(new[] { targetPath }, preview.target.transform.localToWorldMatrix, targetSegments);
                bool reverse = FragmentBoundaryCoverage.ShouldReverse(sourceSegments, targetSegments);
                float total = FragmentBoundaryCoverage.Length(targetSegments);
                float traversed = 0f;
                Matrix4x4 inverse = source.transform.worldToLocalMatrix;
                for (int segmentIndex = 0; segmentIndex < targetSegments.Count; segmentIndex++)
                {
                    Vector4 segment = targetSegments[segmentIndex];
                    float length = Vector2.Distance(FragmentBoundaryCoverage.Point(segment, 0f), FragmentBoundaryCoverage.Point(segment, 1f));
                    float[] profile = preview.profiles[offsets[targetPort] + segmentIndex];
                    Vector3[] samples = new Vector3[profile.Length];
                    for (int sample = 0; sample < samples.Length; sample++)
                    {
                        float t = (traversed + length * sample / (samples.Length - 1)) / total;
                        Vector2 point = FragmentBoundaryCoverage.Sample(sourceSegments, reverse ? 1f - t : t);
                        samples[sample] = inverse.MultiplyPoint3x4(new Vector3(point.x, source.transform.position.y, point.y));
                    }

                    preview.sources.Add(new BoundarySource { sourceId = sourceId, source = source,
                        coverage = preview.pendingProfiles[offsets[targetPort] + segmentIndex], sourcePoints = samples });
                    traversed += length;
                }
            }

            return preview;
        }

        private void CalculateCoverage(PreviewBoundary preview, RevealAnimation opening)
        {
            foreach (float[] profile in preview.pendingProfiles) Array.Clear(profile, 0, profile.Length);
            foreach (BoundarySource source in preview.sources)
            {
                FragmentMapVisibility visibility = state.GetVisibility(source.sourceId);
                bool complete = visibility == FragmentMapVisibility.Revealed;
                if (!complete && visibility != FragmentMapVisibility.Revealing && visibility != FragmentMapVisibility.Hiding) continue;
                float entrance = complete ? 1f : animations[source.sourceId].Influence;
                Matrix4x4 matrix = source.source.transform.localToWorldMatrix;
                for (int index = 0; index < source.coverage.Length; index++)
                {
                    float coverage = complete ? 1f : entrance * source.source.GetRevealAtWorldPoint(matrix.MultiplyPoint3x4(source.sourcePoints[index]));
                    source.coverage[index] = Mathf.Max(source.coverage[index], coverage);
                }
            }

            if (opening != null)
            {
                float fraction = opening.Fraction;
                for (int segment = 0; segment < preview.pendingProfiles.Count; segment++)
                    for (int sample = 0; sample < preview.pendingProfiles[segment].Length; sample++)
                        preview.pendingProfiles[segment][sample] = opening.closing
                            ? Mathf.Lerp(opening.initialProfiles[segment][sample], preview.pendingProfiles[segment][sample], fraction)
                            : Mathf.Max(preview.pendingProfiles[segment][sample], Mathf.Lerp(opening.initialProfiles[segment][sample], 1f, fraction));
            }
        }

        private static void CopyCoverage(PreviewBoundary preview)
        {
            for (int index = 0; index < preview.profiles.Count; index++)
                Array.Copy(preview.pendingProfiles[index], preview.profiles[index], preview.profiles[index].Length);
        }

        private List<FragmentBoundaryPath> GetPaths(string id, IReadOnlyList<string> portIds)
        {
            List<FragmentBoundaryPath> paths = new List<FragmentBoundaryPath>();
            foreach (string portId in portIds)
            {
                if (!runtimePorts[id].TryGetPort(portId, out FragmentBoundaryPath path))
                    throw new InvalidOperationException("Port was removed from island " + id + ": " + portId);
                paths.Add(path);
            }

            return paths;
        }

        private FragmentMapState CreateState(bool requireActive)
        {
            List<string> ids = new List<string>();
            Dictionary<string, FragmentIslandPorts> ports = new Dictionary<string, FragmentIslandPorts>(StringComparer.Ordinal);
            HashSet<FragmentRevealGroup> groups = new HashSet<FragmentRevealGroup>();
            foreach (Node node in nodes)
            {
                if (node == null || node.island == null || string.IsNullOrWhiteSpace(node.id) || ports.ContainsKey(node.id) || !groups.Add(node.island))
                    throw new ArgumentException("Assign unique node IDs and a different baked island group to each node.");
                if (requireActive && !node.island.isActiveAndEnabled)
                    throw new ArgumentException("Enable the island object and its FragmentRevealGroup: " + node.id);
                if (requireActive)
                {
                    node.island.RefreshChildren();
                    if (node.island.Renderers.Count == 0)
                        throw new ArgumentException("Node " + node.id + " has no baked FragmentRevealRenderers. Use the generated island prefab.");
                    foreach (FragmentRevealRenderer renderer in node.island.Renderers)
                    {
                        if (renderer.Data == null || renderer.Data.Mesh == null)
                            throw new ArgumentException("Node " + node.id + " contains missing baked mesh data.");
                    }
                }
                FragmentIslandPorts component = node.island.GetComponent<FragmentIslandPorts>();
                if (component == null) throw new ArgumentException("Add/configure FragmentIslandPorts, or regenerate the island with named ports: " + node.id);
                component.ValidatePorts();
                ports.Add(node.id, component);
                ids.Add(node.id);
            }

            FragmentMapState result = new FragmentMapState(ids, connections, startingNodeId);
            foreach (FragmentMapConnection link in result.Connections)
            {
                if (!ports[link.from].TryGetPort(link.fromPort, out _) || !ports[link.to].TryGetPort(link.toPort, out _))
                    throw new ArgumentException("Connection references a missing port: " + link.from + "/" + link.fromPort + " -> " + link.to + "/" + link.toPort);
            }

            return result;
        }

        private void RequireInitialized()
        {
            if (!IsInitialized) throw new InvalidOperationException("Call InitializeMap before controlling the map.");
        }

        private void RequirePlayMode()
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled)
                throw new InvalidOperationException("Map controls require an enabled component in Play Mode.");
        }
    }
}
