using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFragments
{
    public static class FragmentIslandFootprint
    {
        public static Vector2 Rotate(Vector2 value, float yaw)
        {
            float angle = yaw * Mathf.Deg2Rad;
            float sine = Mathf.Sin(angle);
            float cosine = Mathf.Cos(angle);
            return new Vector2(cosine * value.x + sine * value.y, -sine * value.x + cosine * value.y);
        }

        public static Vector3 Rotate(Vector3 value, float yaw)
        {
            Vector2 xz = Rotate(new Vector2(value.x, value.z), yaw);
            return new Vector3(xz.x, value.y, xz.y);
        }

        public static Vector2[] Prepare(IReadOnlyList<Vector2> input, out int[] triangles)
        {
            if (input.Count < 3) throw new ArgumentException("A footprint needs at least three ordered XZ points.");
            Vector2 minimum = input[0];
            Vector2 maximum = input[0];
            foreach (Vector2 point in input)
            {
                if (!float.IsFinite(point.x) || !float.IsFinite(point.y))
                    throw new ArgumentException("Footprint points must be finite.");
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }

            float span = Mathf.Max(maximum.x - minimum.x, maximum.y - minimum.y);
            float epsilon = Mathf.Max(0.000000000001f, Mathf.Min(maximum.x - minimum.x, maximum.y - minimum.y) * 0.000001f);
            double areaTolerance = epsilon * span;
            List<Vector2> points = new List<Vector2>();
            foreach (Vector2 point in input)
                if (points.Count == 0 || Vector2.Distance(points[points.Count - 1], point) > epsilon) points.Add(point);
            if (points.Count > 1 && Vector2.Distance(points[0], points[points.Count - 1]) <= epsilon) points.RemoveAt(points.Count - 1);
            bool removed = true;
            while (removed && points.Count > 3)
            {
                removed = false;
                for (int index = 0; index < points.Count; index++)
                {
                    Vector2 a = points[(index + points.Count - 1) % points.Count];
                    Vector2 b = points[index];
                    Vector2 c = points[(index + 1) % points.Count];
                    if (Math.Abs(Cross(a, b, c)) > areaTolerance || Vector2.Dot(b - a, b - c) > 0f) continue;
                    points.RemoveAt(index);
                    removed = true;
                    break;
                }
            }

            double area = Area(points);
            if (points.Count < 3 || Math.Abs(area) <= areaTolerance)
                throw new ArgumentException("A footprint must enclose a nonzero area.");
            for (int first = 0; first < points.Count; first++)
                for (int second = first + 1; second < points.Count; second++)
                {
                    if (second == first + 1 || first == 0 && second == points.Count - 1) continue;
                    if (SegmentsIntersect(points[first], points[(first + 1) % points.Count], points[second],
                        points[(second + 1) % points.Count], epsilon, areaTolerance))
                        throw new ArgumentException("A footprint must be a simple polygon without self intersections or holes.");
                }

            List<int> remaining = new List<int>();
            for (int index = 0; index < points.Count; index++) remaining.Add(index);
            List<int> output = new List<int>();
            double sign = area > 0 ? 1 : -1;
            while (remaining.Count > 3)
            {
                bool found = false;
                for (int index = 0; index < remaining.Count; index++)
                {
                    int a = remaining[(index + remaining.Count - 1) % remaining.Count];
                    int b = remaining[index];
                    int c = remaining[(index + 1) % remaining.Count];
                    if (Cross(points[a], points[b], points[c]) * sign <= areaTolerance) continue;
                    bool contains = false;
                    foreach (int other in remaining)
                    {
                        if (other == a || other == b || other == c) continue;
                        if (Cross(points[a], points[b], points[other]) * sign >= -areaTolerance &&
                            Cross(points[b], points[c], points[other]) * sign >= -areaTolerance &&
                            Cross(points[c], points[a], points[other]) * sign >= -areaTolerance)
                        {
                            contains = true;
                            break;
                        }
                    }
                    if (contains) continue;
                    output.Add(a); output.Add(b); output.Add(c);
                    remaining.RemoveAt(index);
                    found = true;
                    break;
                }
                if (!found) throw new ArgumentException("Could not triangulate the footprint; check nearly overlapping edges.");
            }
            output.AddRange(remaining);
            triangles = output.ToArray();
            return points.ToArray();
        }

        public static Vector2 OutwardNormal(Vector2 anchor, IReadOnlyList<Vector2> polygon)
        {
            float nearest = float.PositiveInfinity;
            Vector2 normal = Vector2.zero;
            float sign = Area(polygon) > 0 ? 1f : -1f;
            for (int index = 0; index < polygon.Count; index++)
            {
                Vector2 start = polygon[index];
                Vector2 direction = polygon[(index + 1) % polygon.Count] - start;
                float t = Mathf.Clamp01(Vector2.Dot(anchor - start, direction) / direction.sqrMagnitude);
                float distance = (anchor - start - direction * t).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                normal = new Vector2(direction.y, -direction.x) * (sign / Mathf.Sqrt(direction.sqrMagnitude));
            }
            return normal;
        }

        public static Rect Bounds(IReadOnlyList<Vector2> polygon)
        {
            Vector2 minimum = polygon[0];
            Vector2 maximum = polygon[0];
            foreach (Vector2 point in polygon) { minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point); }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        public static bool Overlaps(Vector2[] first, int[] firstTriangles, Vector2[] second, int[] secondTriangles, float tolerance)
        {
            Rect a = Bounds(first);
            Rect b = Bounds(second);
            if (Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) <= tolerance ||
                Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) <= tolerance) return false;
            for (int one = 0; one < firstTriangles.Length; one += 3)
                for (int two = 0; two < secondTriangles.Length; two += 3)
                    if (!Separated(first, firstTriangles, one, second, secondTriangles, two, tolerance) &&
                        !Separated(second, secondTriangles, two, first, firstTriangles, one, tolerance)) return true;
            return false;
        }

        private static bool Separated(Vector2[] first, int[] firstIndices, int firstOffset,
            Vector2[] second, int[] secondIndices, int secondOffset, float tolerance)
        {
            for (int edge = 0; edge < 3; edge++)
            {
                Vector2 direction = first[firstIndices[firstOffset + (edge + 1) % 3]] - first[firstIndices[firstOffset + edge]];
                Vector2 axis = new Vector2(direction.y, -direction.x) / Mathf.Sqrt(direction.sqrMagnitude);
                float aMin = float.PositiveInfinity, aMax = float.NegativeInfinity;
                float bMin = float.PositiveInfinity, bMax = float.NegativeInfinity;
                for (int index = 0; index < 3; index++)
                {
                    float a = Vector2.Dot(axis, first[firstIndices[firstOffset + index]]);
                    float b = Vector2.Dot(axis, second[secondIndices[secondOffset + index]]);
                    aMin = Mathf.Min(aMin, a); aMax = Mathf.Max(aMax, a);
                    bMin = Mathf.Min(bMin, b); bMax = Mathf.Max(bMax, b);
                }
                if (Mathf.Min(aMax, bMax) - Mathf.Max(aMin, bMin) <= tolerance) return true;
            }
            return false;
        }

        private static double Area(IReadOnlyList<Vector2> points)
        {
            double area = 0;
            for (int index = 0; index < points.Count; index++)
            {
                Vector2 a = points[index], b = points[(index + 1) % points.Count];
                area += (double)a.x * b.y - (double)b.x * a.y;
            }
            return area * 0.5;
        }

        private static double Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return ((double)b.x - a.x) * ((double)c.y - a.y) - ((double)b.y - a.y) * ((double)c.x - a.x);
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float epsilon, double areaTolerance)
        {
            if (Mathf.Max(a.x, b.x) + epsilon < Mathf.Min(c.x, d.x) || Mathf.Max(c.x, d.x) + epsilon < Mathf.Min(a.x, b.x) ||
                Mathf.Max(a.y, b.y) + epsilon < Mathf.Min(c.y, d.y) || Mathf.Max(c.y, d.y) + epsilon < Mathf.Min(a.y, b.y)) return false;
            double first = Cross(a, b, c), second = Cross(a, b, d), third = Cross(c, d, a), fourth = Cross(c, d, b);
            return !((first > areaTolerance && second > areaTolerance) || (first < -areaTolerance && second < -areaTolerance) ||
                (third > areaTolerance && fourth > areaTolerance) || (third < -areaTolerance && fourth < -areaTolerance));
        }
    }
}
