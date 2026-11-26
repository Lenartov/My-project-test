using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFragments
{
    internal static class FragmentBoundaryCoverage
    {
        public const int SamplesPerSegment = 33;

        public static float Projection(Vector2 point, Vector4 segment)
        {
            Vector2 start = new Vector2(segment.x, segment.y);
            Vector2 direction = new Vector2(segment.z - segment.x, segment.w - segment.y);
            return Mathf.Clamp01(Vector2.Dot(point - start, direction) / direction.sqrMagnitude);
        }

        public static Vector2 Point(Vector4 segment, float t)
        {
            return Vector2.Lerp(new Vector2(segment.x, segment.y), new Vector2(segment.z, segment.w), t);
        }

        public static float Length(IReadOnlyList<Vector4> segments)
        {
            float length = 0f;
            foreach (Vector4 segment in segments) length += Vector2.Distance(Point(segment, 0f), Point(segment, 1f));
            return length;
        }

        public static Vector2 Sample(IReadOnlyList<Vector4> segments, float fraction)
        {
            float distance = Mathf.Clamp01(fraction) * Length(segments);
            for (int index = 0; index < segments.Count; index++)
            {
                Vector4 segment = segments[index];
                float length = Vector2.Distance(Point(segment, 0f), Point(segment, 1f));
                if (distance <= length || index == segments.Count - 1) return Point(segment, distance / length);
                distance -= length;
            }

            throw new System.ArgumentException("A port needs at least one nondegenerate XZ segment.");
        }

        public static bool ShouldReverse(IReadOnlyList<Vector4> source, IReadOnlyList<Vector4> target)
        {
            Vector2 a = Point(source[0], 0f);
            Vector2 b = Point(source[source.Count - 1], 1f);
            Vector2 c = Point(target[0], 0f);
            Vector2 d = Point(target[target.Count - 1], 1f);
            return (a - d).sqrMagnitude + (b - c).sqrMagnitude < (a - c).sqrMagnitude + (b - d).sqrMagnitude;
        }

        public static float Evaluate(Vector2 point, IReadOnlyList<Vector4> segments, IReadOnlyList<Vector2> heldFronts,
            IReadOnlyList<float[]> profiles, float progress, float maximumDistance, float width, float chaos)
        {
            if (progress <= 0f) return 0f;
            if (progress >= 1f) return 1f;
            if (segments.Count == 0) return Mathf.SmoothStep(0f, 1f, progress);
            width = Mathf.Max(0.001f, width);
            chaos = Mathf.Max(0f, chaos);
            float front = (maximumDistance + width + 2f * chaos) * progress;
            float margin = float.NegativeInfinity;
            for (int index = 0; index < segments.Count; index++)
            {
                Vector4 segment = segments[index];
                float t = Projection(point, segment);
                float coverage = 1f;
                if (profiles != null)
                {
                    float[] samples = profiles[index];
                    float sample = t * (samples.Length - 1);
                    int first = Mathf.Min((int)sample, samples.Length - 2);
                    coverage = Mathf.Lerp(samples[first], samples[first + 1], sample - first);
                }

                if (coverage <= 0f) continue;
                Vector2 held = heldFronts[index];
                float heldRadius = held.x * Mathf.Clamp01(progress / Mathf.Max(held.y, 0.000001f));
                float radius = Mathf.Max(front, heldRadius) * coverage;
                margin = Mathf.Max(margin, radius - Vector2.Distance(point, Point(segment, t)));
            }

            // The boundary follows the mean assembly envelope; per-fragment random offsets stay on the GPU.
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((margin - chaos * 0.5f) / (width + chaos * 0.75f)));
        }

        public static float SupportDistance(IReadOnlyList<Vector4> localSupport, Matrix4x4 matrix, IReadOnlyList<Vector4> sources)
        {
            if (sources.Count == 0) return 0f;
            float maximum = 0f;
            foreach (Vector4 support in localSupport)
            {
                Vector3 a = matrix.MultiplyPoint3x4(new Vector3(support.x, 0f, support.y));
                Vector3 b = matrix.MultiplyPoint3x4(new Vector3(support.z, 0f, support.w));
                Vector2 start = new Vector2(a.x, a.z);
                Vector2 end = new Vector2(b.x, b.z);
                float bound = 0f;
                for (int sample = 0; sample < SamplesPerSegment; sample++)
                {
                    Vector2 position = Vector2.Lerp(start, end, (float)sample / (SamplesPerSegment - 1));
                    float nearest = float.PositiveInfinity;
                    foreach (Vector4 source in sources)
                        nearest = Mathf.Min(nearest, (position - Point(source, Projection(position, source))).sqrMagnitude);
                    bound = Mathf.Max(bound, Mathf.Sqrt(nearest));
                }

                // The nearest-distance field is 1-Lipschitz; half a sample interval bounds any missed maximum.
                float padding = Vector2.Distance(start, end) / (2f * (SamplesPerSegment - 1));
                maximum = Mathf.Max(maximum, bound + padding);
            }

            return maximum;
        }
    }
}
