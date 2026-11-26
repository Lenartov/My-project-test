using UnityEngine;
using UnityEngine.Rendering;

namespace RealtimeCardPrototype
{
    internal sealed partial class CardCombatEffects
    {
        private static readonly Color PoisonShadow = new Color(0.065f, 0.24f, 0.047f);
        private static readonly Color SparkWhite = new Color(0.93f, 1f, 0.67f);
        private static readonly Vector2[] LightningShape =
        {
            new Vector2(0.02f, 0.56f), new Vector2(-0.39f, -0.06f), new Vector2(-0.07f, -0.03f),
            new Vector2(-0.18f, -0.53f), new Vector2(0.39f, 0.16f), new Vector2(0.04f, 0.12f)
        };
        private static readonly int[] LightningTriangles = { 0, 1, 2, 0, 2, 5, 2, 3, 5, 3, 4, 5 };

        private static Material shockRadiusMaterial;
        private static Mesh shockRadiusMesh;
        private Vector3 boltEnd;
        private Vector3 poisonExtents;

        internal static void ShockImpact(Vector3 contact, Vector3 velocity)
        {
            Vector3 point = contact + Vector3.up * 0.19f;
            Vector3 direction = Vector3.ProjectOnPlane(velocity, Vector3.up).normalized;
            CardCombatEffects impact = Create(point - direction * 0.78f, Effect.ShockImpact, 0.30f);
            impact.boltEnd = direction * 1.12f + Vector3.up * 0.11f;
            impact.Render();
            CardCombatAudio.PlayShock();
        }

        internal static void ShockArc(Vector3 source, Vector3 target)
        {
            Vector3 height = Vector3.up * 0.35f;
            CardCombatEffects arc = Create(source + height, Effect.ShockArc, 0.23f);
            arc.boltEnd = target - source;
            arc.Render();
        }

        internal static CardCombatEffects MarkShock(Transform owner, Vector3 point, float seconds, float chainRadius, CardCombatEffects mark)
        {
            if (mark == null)
            {
                mark = Create(point, Effect.ShockMark, seconds);
                mark.transform.SetParent(owner, true);
                mark.CreateShockRadius(owner.position, chainRadius);
                mark.Render();
            }
            else
            {
                mark.duration = mark.age + seconds;
            }

            return mark;
        }

        private void CreateShockRadius(Vector3 center, float chainRadius)
        {
            if (shockRadiusMaterial == null)
            {
                shockRadiusMaterial = new Material(Resources.Load<Shader>("ShockRadius"))
                {
                    name = "Shock Chain Radius",
                    hideFlags = HideFlags.DontSave
                };
            }

            if (shockRadiusMesh == null)
            {
                shockRadiusMesh = new Mesh
                {
                    name = "Shock Radius Quad",
                    hideFlags = HideFlags.DontSave,
                    vertices = new[]
                    {
                        new Vector3(-1f, 0f, -1f), new Vector3(-1f, 0f, 1f),
                        new Vector3(1f, 0f, 1f), new Vector3(1f, 0f, -1f)
                    },
                    uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
                    triangles = new[] { 0, 1, 2, 0, 2, 3 }
                };
            }

            GameObject range = new GameObject("Shock Chain Radius");
            range.transform.position = center - Vector3.up * 0.11f;
            range.transform.localScale = new Vector3(chainRadius, 1f, chainRadius);
            range.transform.SetParent(transform, true);
            range.AddComponent<MeshFilter>().sharedMesh = shockRadiusMesh;
            MeshRenderer renderer = range.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = shockRadiusMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        internal static void PoisonAura(Transform owner, Vector3 halfExtents, float seconds)
        {
            CardCombatEffects aura = Create(owner.position, Effect.PoisonAura, seconds);
            aura.poisonExtents = halfExtents;
            aura.transform.SetParent(owner, true);
            aura.Render();
            CardCombatAudio.PlayPoison();
        }

        internal static CardCombatEffects MarkPoison(Transform owner, Vector3 point)
        {
            CardCombatEffects mark = Create(point, Effect.PoisonStatus, float.PositiveInfinity);
            mark.transform.SetParent(owner, true);
            mark.Render();
            return mark;
        }

        internal static void PoisonTick(Vector3 point)
        {
            CardCombatEffects tick = Create(point + Vector3.up * 0.31f, Effect.PoisonTick, 0.33f);
            tick.Render();
        }

        private void DrawShockBolt(float progress)
        {
            Vector3 side = Vector3.Cross(Vector3.up, boltEnd).normalized;
            Vector3 previous = Vector3.zero;
            float width = 0.12f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 0.84f, progress)));
            int pulse = Mathf.FloorToInt(progress * 3f);
            const int Segments = 5;
            for (int i = 1; i <= Segments; i++)
            {
                float along = (float)i / Segments;
                float zigzag = (i + pulse) % 2 == 0 ? -0.19f : 0.19f;
                Vector3 point = boltEnd * along;
                if (i < Segments)
                {
                    point += side * zigzag;
                }

                DrawStrip(previous, point, width, ElectricBlue, SparkWhite);
                if (i == 3)
                {
                    Vector3 fork = point + side * (zigzag * 2.2f) + boltEnd * 0.14f;
                    DrawStrip(point, fork, width * 0.53f, Turquoise, Ice);
                    DrawStrip(fork, fork + side * -zigzag * 0.55f + boltEnd * 0.095f, width * 0.29f, Turquoise, Ice);
                }

                previous = point;
            }

            float flare = (effect == Effect.ShockImpact ? 0.53f : 0.35f) * (1f - EaseOut(progress));
            Burst(boltEnd + Vector3.up * 0.03f, Vector3.right, Vector3.forward, 4, flare, flare * 0.36f, ElectricBlue);
            Burst(boltEnd + Vector3.up * 0.05f, Vector3.right, Vector3.forward, 4, flare * 0.72f, flare * 0.23f, SparkWhite);
        }

        private void DrawStrip(Vector3 start, Vector3 end, float width, Color edge, Color center)
        {
            Vector3 side = Vector3.Cross(Vector3.up, end - start).normalized * width;
            Triangle(start - side, start + side, end + side, edge);
            Triangle(start - side, end + side, end - side, edge);

            Vector3 lift = Vector3.up * 0.014f;
            side *= 0.49f;
            Triangle(start - side + lift, start + side + lift, end + side + lift, center);
            Triangle(start - side + lift, end + side + lift, end - side + lift, center);
        }

        private void DrawShockMark()
        {
            float settle = EaseOut(Mathf.Clamp01(age / 0.20f));
            float scale = (0.32f + 0.065f * Mathf.Sin(age * 8.5f)) * settle;
            scale *= Mathf.Clamp01((duration - age) / 0.32f);
            Vector3 center = Vector3.up * (0.11f + Mathf.Sin(age * 6f) * 0.041f);
            DrawLightningGlyph(center, scale * 1.22f, Navy);
            DrawLightningGlyph(center + Vector3.up * 0.016f, scale, ElectricBlue);
            DrawLightningGlyph(center + Vector3.up * 0.032f, scale * 0.64f, SparkWhite);
        }

        private void DrawLightningGlyph(Vector3 center, float scale, Color tint)
        {
            for (int i = 0; i < LightningTriangles.Length; i += 3)
            {
                Vector2 a = LightningShape[LightningTriangles[i]];
                Vector2 b = LightningShape[LightningTriangles[i + 1]];
                Vector2 c = LightningShape[LightningTriangles[i + 2]];
                Triangle(center + (Vector3.right * a.x + ShieldUp * a.y) * scale,
                    center + (Vector3.right * b.x + ShieldUp * b.y) * scale,
                    center + (Vector3.right * c.x + ShieldUp * c.y) * scale, tint);
            }
        }

        private void DrawPoisonAura(float progress)
        {
            Vector3 center = transform.parent.position;
            center.y = Mathf.Max(0.06f, center.y - poisonExtents.y + 0.02f);
            transform.SetPositionAndRotation(center, Quaternion.identity);

            float opening = 0.67f + 0.33f * EaseOut(Mathf.Clamp01(progress / 0.19f));
            float closing = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.83f, 1f, progress));
            float width = 0.047f * closing;
            float x = poisonExtents.x * opening;
            float z = poisonExtents.z * opening;
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                int sx = quadrant % 2 == 0 ? -1 : 1;
                int sz = quadrant < 2 ? -1 : 1;
                Vector3 corner = new Vector3(sx * (x - width), 0f, sz * (z - width));
                DrawStrip(corner - Vector3.right * (sx * x * 0.45f), corner, width, PoisonShadow, PoisonGreen);
                DrawStrip(corner, corner - Vector3.forward * (sz * z * 0.42f), width, PoisonShadow, PoisonGreen);

                float phase = Mathf.Repeat(age * 2.2f + quadrant * 0.27f, 1f);
                float lift = 0.12f + phase * 0.52f;
                float size = (0.10f + Mathf.Sin(phase * Mathf.PI) * 0.08f) * (1f - phase) * closing;
                Puff(corner * 0.88f + Vector3.up * lift, size, quadrant * 23f, Lime, PoisonGreen);
            }
        }

        private void DrawPoisonStatus()
        {
            for (int i = 0; i < 3; i++)
            {
                float phase = Mathf.Repeat(age * 1.45f + i * 0.33f, 1f);
                float size = (0.12f + 0.046f * Mathf.Sin(phase * Mathf.PI)) * (1f - phase);
                Vector3 center = new Vector3(Mathf.Sin(i * 3f) * 0.13f, phase * 0.29f, (i - 1) * 0.18f);
                Puff(center, size, i * 31f, Lime, PoisonGreen);
            }
        }

        private void DrawPoisonTick(float progress)
        {
            float size = (0.21f + EaseOut(progress) * 0.23f) * (1f - progress);
            Burst(Vector3.zero, Vector3.right, Vector3.forward, 5, size, size * 0.55f, PoisonGreen);
            Burst(Vector3.up * 0.018f, Vector3.right, Vector3.forward, 5, size * 0.64f, size * 0.35f, Lime);
            DrawDebris(4, 0f, 155f, progress, 0.12f, 0.66f, PoisonGreen, Lime);
        }
    }
}
