using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFragments
{
    internal static class FragmentRevealFrontMemory
    {
        // Endpoints are matched in island-local space; stored radii use world distance.
        public static List<Vector2> Remap(IReadOnlyList<Vector4> previous, IReadOnlyList<Vector2> memory,
            IReadOnlyList<Vector4> next, float currentFront, float progress)
        {
            List<Vector2> result = new List<Vector2>(next.Count);
            foreach (Vector4 segment in next)
            {
                Vector2 held = Vector2.zero;
                for (int index = 0; index < previous.Count; index++)
                {
                    Vector4 old = previous[index];
                    if (old != segment && new Vector4(old.z, old.w, old.x, old.y) != segment) continue;
                    float radius = index < memory.Count ? memory[index].x * Mathf.Clamp01(progress / Mathf.Max(memory[index].y, 0.000001f)) : 0f;
                    held = new Vector2(Mathf.Max(currentFront, radius), progress);
                    break;
                }

                result.Add(held);
            }

            return result;
        }
    }
}
