using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFragments
{
    [Serializable]
    public sealed class FragmentIslandPort
    {
        public string id = "Port";
        public FragmentBoundaryPath boundary = new FragmentBoundaryPath();
        [Tooltip("Set a docking point and outward XZ normal explicitly. Otherwise use the boundary midpoint and nearest footprint edge.")]
        public bool useCustomAttachment;
        public Vector3 attachmentPosition;
        public Vector3 attachmentNormal = Vector3.forward;
        [Tooltip("Positive reveal edge width when Boundary has no points. Existing authored boundary paths take precedence.")]
        public float attachmentWidth = 1f;

        public FragmentIslandPort Copy()
        {
            return new FragmentIslandPort { id = id, boundary = boundary != null ? boundary.Copy() : new FragmentBoundaryPath(),
                useCustomAttachment = useCustomAttachment, attachmentPosition = attachmentPosition,
                attachmentNormal = attachmentNormal, attachmentWidth = attachmentWidth };
        }

        public FragmentBoundaryPath GetRevealBoundary()
        {
            if (boundary != null && (boundary.points.Count >= 2 || !boundary.enabled)) return boundary;
            if (!useCustomAttachment) return boundary;
            Vector3 tangent = new Vector3(attachmentNormal.z, 0f, -attachmentNormal.x);
            tangent *= attachmentWidth * 0.5f / Mathf.Sqrt(tangent.sqrMagnitude);
            return new FragmentBoundaryPath { points = new List<Vector3> { attachmentPosition - tangent, attachmentPosition + tangent } };
        }

        public Vector3 GetAttachmentPosition()
        {
            if (useCustomAttachment) return attachmentPosition;
            List<Vector4> segments = new List<Vector4>();
            FragmentBoundaryPath.Pack(new[] { GetRevealBoundary() }, Matrix4x4.identity, segments);
            Vector2 middle = FragmentBoundaryCoverage.Sample(segments, 0.5f);
            return new Vector3(middle.x, boundary.points[0].y, middle.y);
        }

        public static List<FragmentIslandPort> FromBoundaries(IReadOnlyList<FragmentBoundaryPath> paths)
        {
            List<FragmentIslandPort> ports = new List<FragmentIslandPort>();
            foreach (FragmentBoundaryPath path in paths)
            {
                if (!path.enabled || path.points.Count < 2) continue;
                if (path.closed && path.points.Count > 2)
                {
                    for (int index = 0; index < path.points.Count; index++)
                    {
                        ports.Add(new FragmentIslandPort
                        {
                            id = "Edge_" + (ports.Count + 1),
                            boundary = new FragmentBoundaryPath
                            {
                                points = new List<Vector3> { path.points[index], path.points[(index + 1) % path.points.Count] }
                            }
                        });
                    }
                }
                else
                {
                    ports.Add(new FragmentIslandPort { id = "Edge_" + (ports.Count + 1), boundary = path.Copy() });
                }
            }

            return ports;
        }
    }

    [DisallowMultipleComponent]
    public sealed class FragmentIslandPorts : MonoBehaviour
    {
        [SerializeField, Tooltip("Named island-local entry/exit polylines. Names are referenced by map connections.")]
        private List<FragmentIslandPort> ports = new List<FragmentIslandPort>();
        [SerializeField, Tooltip("Simple closed footprint in root-local XZ (Vector2.y is Z). Empty uses baked mesh bounds.")]
        private List<Vector2> footprint = new List<Vector2>();
        public IReadOnlyList<FragmentIslandPort> Ports => ports;
        public IReadOnlyList<Vector2> Footprint => footprint;

        public void Configure(IReadOnlyList<FragmentIslandPort> definitions, IReadOnlyList<Vector2> outline = null)
        {
            ports = new List<FragmentIslandPort>();
            foreach (FragmentIslandPort definition in definitions) ports.Add(definition.Copy());
            footprint = outline != null ? new List<Vector2>(outline) : new List<Vector2>();
        }

        public bool TryGetPort(string id, out FragmentBoundaryPath boundary)
        {
            foreach (FragmentIslandPort port in ports)
            {
                if (port.id == id)
                {
                    boundary = port.GetRevealBoundary();
                    return true;
                }
            }

            boundary = null;
            return false;
        }

        public void ValidatePorts()
        {
            ValidateDefinitions(ports, name);
            if (footprint.Count > 0) FragmentIslandFootprint.Prepare(footprint, out _);
        }

        public static void ValidateDefinitions(IReadOnlyList<FragmentIslandPort> definitions, string context)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            List<Vector4> segments = new List<Vector4>();
            foreach (FragmentIslandPort port in definitions)
            {
                if (port == null || string.IsNullOrWhiteSpace(port.id) || !ids.Add(port.id))
                    throw new ArgumentException(context + ": port IDs must be nonempty and unique.");
                if (port.boundary == null && !port.useCustomAttachment)
                    throw new ArgumentException(context + ": port " + port.id + " has no boundary.");
                if (port.useCustomAttachment && (!float.IsFinite(port.attachmentPosition.x) || !float.IsFinite(port.attachmentPosition.y) ||
                    !float.IsFinite(port.attachmentPosition.z) || !float.IsFinite(port.attachmentNormal.x) || !float.IsFinite(port.attachmentNormal.z) ||
                    new Vector2(port.attachmentNormal.x, port.attachmentNormal.z).sqrMagnitude == 0f ||
                    !float.IsFinite(port.attachmentWidth) || port.attachmentWidth <= 0f))
                    throw new ArgumentException(context + ": port " + port.id + " needs a finite attachment, nonzero outward XZ normal and positive width.");
                FragmentBoundaryPath.Pack(new[] { port.GetRevealBoundary() }, Matrix4x4.identity, segments);
                if (segments.Count == 0)
                    throw new ArgumentException(context + ": port " + port.id + " needs an enabled path with distinct XZ points.");
            }
        }
    }
}
