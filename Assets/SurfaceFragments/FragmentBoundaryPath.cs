using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFragments
{
    [Serializable]
    public sealed class FragmentBoundaryPath
    {
        public bool enabled = true;
        public bool closed;
        public List<Vector3> points = new List<Vector3>();

        public FragmentBoundaryPath Copy()
        {
            return new FragmentBoundaryPath { enabled = enabled, closed = closed, points = new List<Vector3>(points) };
        }

        public static void Pack(IReadOnlyList<FragmentBoundaryPath> paths, Matrix4x4 matrix, List<Vector4> segments)
        {
            segments.Clear();
            foreach (FragmentBoundaryPath path in paths)
            {
                if (!path.enabled || path.points.Count < 2)
                {
                    continue;
                }

                int count = path.points.Count - 1 + (path.closed && path.points.Count > 2 ? 1 : 0);
                for (int index = 0; index < count; index++)
                {
                    Vector3 start = matrix.MultiplyPoint3x4(path.points[index]);
                    Vector3 end = matrix.MultiplyPoint3x4(path.points[(index + 1) % path.points.Count]);
                    if (!float.IsFinite(start.x) || !float.IsFinite(start.z) || !float.IsFinite(end.x) || !float.IsFinite(end.z))
                    {
                        throw new ArgumentException("Boundary points must have finite XZ coordinates.");
                    }

                    if (start.x != end.x || start.z != end.z)
                    {
                        segments.Add(new Vector4(start.x, start.z, end.x, end.z));
                    }
                }
            }
        }

        public static float Distance(Vector3 position, IReadOnlyList<Vector4> segments)
        {
            float minimum = float.PositiveInfinity;
            Vector2 location = new Vector2(position.x, position.z);
            foreach (Vector4 segment in segments)
            {
                Vector2 start = new Vector2(segment.x, segment.y);
                Vector2 direction = new Vector2(segment.z - segment.x, segment.w - segment.y);
                float t = Mathf.Clamp01(Vector2.Dot(location - start, direction) / direction.sqrMagnitude);
                minimum = Mathf.Min(minimum, (location - start - direction * t).sqrMagnitude);
            }

            return segments.Count == 0 ? 0f : Mathf.Sqrt(minimum);
        }
    }
}
