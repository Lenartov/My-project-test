using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFragments
{
    public enum FragmentMapVisibility
    {
        Hidden,
        Preview,
        Revealing,
        Revealed,
        Hiding
    }

    [Serializable]
    public sealed class FragmentMapConnection
    {
        public string from;
        public string fromPort;
        public string to;
        public string toPort;
        // Retained for existing serialized connections; adjacency no longer restricts opening.
        [HideInInspector] public bool bidirectional;

        public FragmentMapConnection Copy()
        {
            return new FragmentMapConnection { from = from, fromPort = fromPort, to = to, toPort = toPort, bidirectional = bidirectional };
        }
    }

    // Topology and discovery rules are independent of scene objects and rendering.
    public sealed class FragmentMapState
    {
        private readonly Dictionary<string, FragmentMapVisibility> visibility = new Dictionary<string, FragmentMapVisibility>(StringComparer.Ordinal);
        private readonly List<FragmentMapConnection> connections = new List<FragmentMapConnection>();
        private readonly List<string> nodeIds = new List<string>();
        public IReadOnlyList<FragmentMapConnection> Connections => connections;

        public FragmentMapState(IReadOnlyList<string> nodeIds, IReadOnlyList<FragmentMapConnection> links, string initiallyOpenId = null)
        {
            if (nodeIds.Count == 0) throw new ArgumentException("Configure at least one island.");
            foreach (string id in nodeIds)
            {
                if (string.IsNullOrWhiteSpace(id) || visibility.ContainsKey(id))
                    throw new ArgumentException("Map node IDs must be nonempty and unique.");
                visibility.Add(id, FragmentMapVisibility.Hidden);
                this.nodeIds.Add(id);
            }

            if (!string.IsNullOrEmpty(initiallyOpenId)) RequireNode(initiallyOpenId);
            HashSet<(string, string, string, string)> unique = new HashSet<(string, string, string, string)>();
            foreach (FragmentMapConnection link in links)
            {
                if (link == null) throw new ArgumentException("Map connections cannot be null.");
                RequireNode(link.from);
                RequireNode(link.to);
                if (link.from == link.to || string.IsNullOrWhiteSpace(link.fromPort) || string.IsNullOrWhiteSpace(link.toPort))
                    throw new ArgumentException("Connect different nodes and specify a port on both ends.");
                if (!unique.Add((link.from, link.fromPort, link.to, link.toPort)))
                    throw new ArgumentException("Duplicate map connection: " + link.from + " -> " + link.to);
                connections.Add(link.Copy());
            }

            if (!string.IsNullOrEmpty(initiallyOpenId)) visibility[initiallyOpenId] = FragmentMapVisibility.Revealed;
            RefreshDiscovery();
        }

        public FragmentMapVisibility GetVisibility(string id)
        {
            RequireNode(id);
            return visibility[id];
        }

        public List<string> GetIslandIds(FragmentMapVisibility value)
        {
            List<string> result = new List<string>();
            foreach (string id in nodeIds) if (visibility[id] == value) result.Add(id);
            return result;
        }

        public List<string> GetConnectedIslandIds(string id)
        {
            RequireNode(id);
            List<string> result = new List<string>();
            foreach (FragmentMapConnection link in connections)
            {
                string neighbor = link.from == id ? link.to : link.to == id ? link.from : null;
                if (neighbor != null && !result.Contains(neighbor)) result.Add(neighbor);
            }

            return result;
        }

        public bool CanOpen(string id)
        {
            FragmentMapVisibility value = GetVisibility(id);
            return value == FragmentMapVisibility.Hidden || value == FragmentMapVisibility.Preview || value == FragmentMapVisibility.Hiding;
        }

        public bool TryBeginOpen(string id)
        {
            if (!CanOpen(id)) return false;
            visibility[id] = FragmentMapVisibility.Revealing;
            RefreshDiscovery();
            return true;
        }

        public void CompleteOpen(string id)
        {
            if (GetVisibility(id) != FragmentMapVisibility.Revealing)
                throw new InvalidOperationException("The island is not currently opening: " + id);
            visibility[id] = FragmentMapVisibility.Revealed;
            RefreshDiscovery();
        }

        public bool CanClose(string id)
        {
            return GetVisibility(id) == FragmentMapVisibility.Revealed;
        }

        public bool TryBeginClose(string id)
        {
            if (!CanClose(id)) return false;
            visibility[id] = FragmentMapVisibility.Hiding;
            RefreshDiscovery();
            return true;
        }

        public void CompleteClose(string id)
        {
            if (GetVisibility(id) != FragmentMapVisibility.Hiding)
                throw new InvalidOperationException("The island is not currently closing: " + id);
            visibility[id] = FragmentMapVisibility.Hidden;
            RefreshDiscovery();
        }

        public List<string> GetIncomingPortIds(string id)
        {
            RequireNode(id);
            List<string> ports = new List<string>();
            foreach (FragmentMapConnection link in connections)
            {
                string port = null;
                if (link.to == id && CanInfluence(visibility[link.from])) port = link.toPort;
                if (link.from == id && CanInfluence(visibility[link.to])) port = link.fromPort;
                if (port != null && !ports.Contains(port)) ports.Add(port);
            }

            return ports;
        }

        private static bool CanInfluence(FragmentMapVisibility value)
        {
            return value == FragmentMapVisibility.Revealed || value == FragmentMapVisibility.Revealing || value == FragmentMapVisibility.Hiding;
        }

        private void RefreshDiscovery()
        {
            foreach (string id in nodeIds)
            {
                if (CanInfluence(visibility[id])) continue;
                visibility[id] = GetIncomingPortIds(id).Count > 0 ? FragmentMapVisibility.Preview : FragmentMapVisibility.Hidden;
            }
        }

        private void RequireNode(string id)
        {
            if (id == null || !visibility.ContainsKey(id)) throw new ArgumentException("Unknown map node ID: " + id);
        }
    }
}
