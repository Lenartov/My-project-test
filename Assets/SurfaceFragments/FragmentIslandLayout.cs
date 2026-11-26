using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFragments
{
    public sealed class FragmentIslandLayout
    {
        public readonly struct Socket
        {
            public readonly string id;
            public readonly Vector3 position;
            public readonly Vector2 outward;

            public Socket(string id, Vector3 position, Vector2 outward)
            {
                if (string.IsNullOrWhiteSpace(id) || !float.IsFinite(position.x) || !float.IsFinite(position.y) ||
                    !float.IsFinite(position.z) || !float.IsFinite(outward.sqrMagnitude) || outward.sqrMagnitude <= 0f)
                    throw new ArgumentException("Docking sockets need a name, finite position and nonzero XZ outward normal.");
                this.id = id;
                this.position = position;
                this.outward = outward / Mathf.Sqrt(outward.sqrMagnitude);
            }
        }

        public sealed class Template
        {
            public readonly string name;
            internal readonly Vector2[] polygon;
            internal readonly int[] triangles;
            private readonly Socket[] sockets;
            public IReadOnlyList<Vector2> Footprint => polygon;
            public IReadOnlyList<Socket> Sockets => sockets;

            public Template(string name, IReadOnlyList<Vector2> footprint, IReadOnlyList<Socket> sockets)
            {
                this.name = name;
                polygon = FragmentIslandFootprint.Prepare(footprint, out triangles);
                if (sockets.Count == 0) throw new ArgumentException(name + ": define at least one docking port.");
                this.sockets = new Socket[sockets.Count];
                HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < sockets.Count; index++)
                {
                    if (!ids.Add(sockets[index].id)) throw new ArgumentException(name + ": docking port names must be unique.");
                    this.sockets[index] = sockets[index];
                }
            }
        }

        public readonly struct Placement
        {
            public readonly int templateIndex;
            public readonly Vector3 position;
            public readonly float yaw;

            public Placement(int templateIndex, Vector3 position, float yaw)
            {
                this.templateIndex = templateIndex;
                this.position = position;
                this.yaw = yaw;
            }
        }

        public readonly struct Link
        {
            public readonly int from, fromPort, to, toPort;

            public Link(int from, int fromPort, int to, int toPort)
            {
                this.from = from;
                this.fromPort = fromPort;
                this.to = to;
                this.toPort = toPort;
            }
        }

        private readonly List<Template> templates;
        private readonly List<Placement> placements = new List<Placement>();
        private readonly List<Vector2[]> footprints = new List<Vector2[]>();
        private readonly List<Link> links = new List<Link>();
        public IReadOnlyList<Template> Templates => templates;
        public IReadOnlyList<Placement> Placements => placements;
        public IReadOnlyList<Link> Links => links;
        public int RequestedCount { get; }
        public bool IsComplete => placements.Count == RequestedCount;
        public string Diagnostic => IsComplete ? string.Empty : "Placed " + placements.Count + " of " + RequestedCount +
            " islands. No unused port could accept another template without footprint overlap. Add ports/templates or try another seed.";

        private FragmentIslandLayout(IReadOnlyList<Template> templates, int count)
        {
            this.templates = new List<Template>(templates);
            RequestedCount = count;
        }

        public IReadOnlyList<Vector2> GetFootprint(int index) => footprints[index];

        public Vector3 GetSocketPosition(int node, int port)
        {
            Placement placement = placements[node];
            return placement.position + FragmentIslandFootprint.Rotate(templates[placement.templateIndex].Sockets[port].position, placement.yaw);
        }

        public Vector2 GetSocketNormal(int node, int port)
        {
            Placement placement = placements[node];
            return FragmentIslandFootprint.Rotate(templates[placement.templateIndex].Sockets[port].outward, placement.yaw);
        }

        public static FragmentIslandLayout Generate(IReadOnlyList<Template> templates, int seed, int count, float gap, float extraConnectionChance)
        {
            if (templates.Count == 0) throw new ArgumentException("Assign at least one island template.");
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            if (!float.IsFinite(gap) || gap < 0f) throw new ArgumentOutOfRangeException(nameof(gap));
            if (!float.IsFinite(extraConnectionChance) || extraConnectionChance < 0f || extraConnectionChance > 1f)
                throw new ArgumentOutOfRangeException(nameof(extraConnectionChance));
            float minimumExtent = float.PositiveInfinity;
            List<(int template, int port)> candidates = new List<(int, int)>();
            for (int index = 0; index < templates.Count; index++)
            {
                Rect bounds = FragmentIslandFootprint.Bounds(templates[index].polygon);
                minimumExtent = Mathf.Min(minimumExtent, bounds.width, bounds.height);
                for (int port = 0; port < templates[index].Sockets.Count; port++) candidates.Add((index, port));
            }
            float tolerance = Mathf.Max(0.000000000001f, minimumExtent * 0.00001f);
            System.Random random = new System.Random(seed);
            FragmentIslandLayout result = new FragmentIslandLayout(templates, count);
            List<bool[]> occupiedPorts = new List<bool[]>();
            List<(int node, int port)> frontier = new List<(int, int)>();
            int initial = random.Next(templates.Count);
            result.Add(new Placement(initial, Vector3.zero, 0f));
            occupiedPorts.Add(new bool[templates[initial].Sockets.Count]);
            for (int port = 0; port < templates[initial].Sockets.Count; port++) frontier.Add((0, port));

            while (result.placements.Count < count && frontier.Count > 0)
            {
                int selected = random.Next(frontier.Count);
                (int node, int port) source = frontier[selected];
                frontier[selected] = frontier[frontier.Count - 1];
                frontier.RemoveAt(frontier.Count - 1);
                if (occupiedPorts[source.node][source.port]) continue;
                Vector3 anchor = result.GetSocketPosition(source.node, source.port);
                Vector2 outward = result.GetSocketNormal(source.node, source.port);
                int firstCandidate = random.Next(candidates.Count);
                for (int attempt = 0; attempt < candidates.Count; attempt++)
                {
                    (int template, int port) candidate = candidates[(firstCandidate + attempt) % candidates.Count];
                    Socket socket = templates[candidate.template].Sockets[candidate.port];
                    float yaw = (Mathf.Atan2(-outward.x, -outward.y) - Mathf.Atan2(socket.outward.x, socket.outward.y)) * Mathf.Rad2Deg;
                    Vector3 position = anchor + new Vector3(outward.x, 0f, outward.y) * gap - FragmentIslandFootprint.Rotate(socket.position, yaw);
                    Placement placement = new Placement(candidate.template, position, yaw);
                    Vector2[] polygon = Transform(templates[candidate.template].polygon, placement);
                    bool overlap = false;
                    for (int existing = 0; existing < result.placements.Count; existing++)
                    {
                        if (!FragmentIslandFootprint.Overlaps(polygon, templates[candidate.template].triangles,
                            result.footprints[existing], templates[result.placements[existing].templateIndex].triangles, tolerance)) continue;
                        overlap = true;
                        break;
                    }
                    if (overlap) continue;
                    int node = result.placements.Count;
                    result.placements.Add(placement);
                    result.footprints.Add(polygon);
                    result.links.Add(new Link(source.node, source.port, node, candidate.port));
                    occupiedPorts[source.node][source.port] = true;
                    bool[] used = new bool[templates[candidate.template].Sockets.Count];
                    used[candidate.port] = true;
                    occupiedPorts.Add(used);
                    for (int port = 0; port < used.Length; port++) if (!used[port]) frontier.Add((node, port));
                    break;
                }
            }

            // Extra graph edges require matching free anchors, not neighboring grid cells.
            for (int first = 0; first < result.placements.Count; first++)
                for (int second = first + 1; second < result.placements.Count; second++)
                    for (int a = 0; a < occupiedPorts[first].Length; a++)
                    {
                        if (occupiedPorts[first][a]) continue;
                        for (int b = 0; b < occupiedPorts[second].Length; b++)
                        {
                            if (occupiedPorts[second][b]) continue;
                            Vector2 normal = result.GetSocketNormal(first, a);
                            if (Vector2.Dot(normal, result.GetSocketNormal(second, b)) > -0.9999f) continue;
                            Vector3 expected = result.GetSocketPosition(first, a) + new Vector3(normal.x, 0f, normal.y) * gap;
                            if (Vector3.Distance(expected, result.GetSocketPosition(second, b)) > tolerance * 10f) continue;
                            if (random.NextDouble() >= extraConnectionChance) continue;
                            result.links.Add(new Link(first, a, second, b));
                            occupiedPorts[first][a] = true;
                            occupiedPorts[second][b] = true;
                            break;
                        }
                    }
            return result;
        }

        private void Add(Placement placement)
        {
            placements.Add(placement);
            footprints.Add(Transform(templates[placement.templateIndex].polygon, placement));
        }

        private static Vector2[] Transform(Vector2[] polygon, Placement placement)
        {
            Vector2[] result = new Vector2[polygon.Length];
            for (int index = 0; index < result.Length; index++)
                result[index] = FragmentIslandFootprint.Rotate(polygon[index], placement.yaw) + new Vector2(placement.position.x, placement.position.z);
            return result;
        }
    }
}
