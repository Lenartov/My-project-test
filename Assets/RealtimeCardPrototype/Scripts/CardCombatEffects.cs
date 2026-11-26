using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealtimeCardPrototype
{
    internal sealed partial class CardCombatEffects : MonoBehaviour
    {
        internal static readonly Color Cream = new Color(1f, 0.96f, 0.64f);
        internal static readonly Color Gold = new Color(1f, 0.69f, 0.045f);
        internal static readonly Color Amber = new Color(1f, 0.33f, 0.025f);
        internal static readonly Color Coral = new Color(0.88f, 0.055f, 0.065f);
        internal static readonly Color Ice = new Color(0.66f, 0.99f, 1f);
        internal static readonly Color Turquoise = new Color(0.025f, 0.66f, 0.86f);
        internal static readonly Color ElectricBlue = new Color(0.10f, 0.48f, 1f);
        internal static readonly Color PoisonGreen = new Color(0.15f, 0.62f, 0.21f);
        internal static readonly Color Lime = new Color(0.78f, 1f, 0.19f);

        private static readonly Color OrangeEdge = new Color(0.42f, 0.065f, 0.02f);
        private static readonly Color Navy = new Color(0.018f, 0.10f, 0.22f);
        private static readonly Color Blue = new Color(0.02f, 0.29f, 0.66f);
        private static readonly Vector3 ShieldUp = new Vector3(0f, 0.66f, 0.75f);

        private static readonly Vector2[] ShieldShape =
        {
            new Vector2(0f, -0.65f), new Vector2(-0.39f, -0.39f), new Vector2(-0.57f, -0.03f),
            new Vector2(-0.60f, 0.46f), new Vector2(0f, 0.64f), new Vector2(0.60f, 0.46f),
            new Vector2(0.57f, -0.03f), new Vector2(0.39f, -0.39f)
        };

        private enum Effect
        {
            Strike,
            Heavy,
            Blast,
            Ward,
            BlockImpact,
            ShockImpact,
            ShockArc,
            ShockMark,
            PoisonAura,
            PoisonStatus,
            PoisonTick,
            FocusPlacement,
            FocusFlow,
            TurretPlacement,
            TurretMuzzle,
            TurretBolt,
            TurretImpact,
            PiercePass,
            PierceCharge,
            PowerPlacement,
            PowerReady,
            PowerCharge
        }

        private static Material material;

        private readonly List<Vector3> vertices = new List<Vector3>(648);
        private readonly List<Color> colors = new List<Color>(648);
        private readonly List<int> indices = new List<int>(648);
        private Mesh mesh;
        private Effect effect;
        private float age;
        private float duration;
        private float delay;
        private float heading;
        private float radius;

        internal static Material Material
        {
            get
            {
                if (material == null)
                {
                    Shader shader = Resources.Load<Shader>("CardEffects");
                    material = new Material(shader) { name = "Card Combat Effects", hideFlags = HideFlags.DontSave };
                }

                return material;
            }
        }

        internal static void Strike(Vector3 point, Vector3 velocity, bool heavy)
        {
            CardCombatEffects impact = Create(point, heavy ? Effect.Heavy : Effect.Strike, heavy ? 0.55f : 0.37f);
            impact.heading = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
            impact.Render();
            CardCombatAudio.PlayAttack(heavy);
        }

        internal static void Blast(Vector3 point, float splashRadius)
        {
            CardCombatEffects blast = Create(point, Effect.Blast, 0.66f);
            blast.radius = splashRadius;
            blast.Render();
            CardCombatAudio.PlayBlast();
        }

        internal static CardCombatEffects RaiseWard(Transform card, Vector3 point)
        {
            CardCombatEffects ward = Create(point, Effect.Ward, float.PositiveInfinity);
            ward.transform.SetParent(card, true);
            ward.Render();
            return ward;
        }

        internal void ReleaseWard(float seconds)
        {
            transform.SetParent(null, true);
            duration = age + seconds;
        }

        internal static void BlockImpact(Vector3 point, Vector3 incoming, float travelTime)
        {
            CardCombatEffects impact = Create(point, Effect.BlockImpact, 0.52f);
            impact.delay = travelTime;
            impact.heading = Mathf.Atan2(incoming.x, incoming.z) * Mathf.Rad2Deg;
        }

        private static CardCombatEffects Create(Vector3 point, Effect effect, float duration)
        {
            GameObject root = new GameObject(effect + " VFX");
            root.transform.position = point;
            CardCombatEffects instance = root.AddComponent<CardCombatEffects>();
            instance.effect = effect;
            instance.duration = duration;
            instance.mesh = new Mesh { name = root.name };
            instance.mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh = instance.mesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return instance;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            if (delay > 0f)
            {
                delay -= deltaTime;
                if (delay > 0f)
                {
                    return;
                }

                deltaTime = -delay;
                delay = 0f;
                CardCombatAudio.PlayBlock();
            }

            age += deltaTime;
            if (effect == Effect.FocusFlow)
            {
                if (age >= FocusBubbleInterval)
                {
                    age = Mathf.Repeat(age, FocusBubbleInterval);
                    CardCombatAudio.PlayFocusBubble();
                }
                else if (age >= FocusDropLifetime + 2 * FocusDropStagger && vertices.Count == 0)
                {
                    return;
                }
            }

            if (effect == Effect.TurretBolt && boltTarget != null)
            {
                boltEnd = boltTarget.position + Vector3.up * TurretTargetHeight - transform.position;
            }

            if (age >= duration && effect != Effect.ShockMark)
            {
                if (effect == Effect.TurretBolt)
                {
                    CardCombatEffects impact = Create(transform.position + boltEnd, Effect.TurretImpact, 0.26f);
                    impact.heading = heading;
                    impact.Render();
                }

                Destroy(gameObject);
                return;
            }

            Render();
        }

        private void OnDestroy()
        {
            Destroy(mesh);
        }

        private void Render()
        {
            vertices.Clear();
            colors.Clear();
            indices.Clear();

            float progress = age / duration;
            switch (effect)
            {
                case Effect.Strike: DrawStrike(progress); break;
                case Effect.Heavy: DrawHeavy(progress); break;
                case Effect.Blast: DrawBlast(progress); break;
                case Effect.Ward: DrawWard(); break;
                case Effect.BlockImpact: DrawBlockImpact(progress); break;
                case Effect.ShockImpact:
                case Effect.ShockArc: DrawShockBolt(progress); break;
                case Effect.ShockMark: DrawShockMark(); break;
                case Effect.PoisonAura: DrawPoisonAura(progress); break;
                case Effect.PoisonStatus: DrawPoisonStatus(); break;
                case Effect.PoisonTick: DrawPoisonTick(progress); break;
                case Effect.FocusPlacement: DrawFocusPlacement(progress); break;
                case Effect.FocusFlow: DrawFocusFlow(); break;
                case Effect.TurretPlacement: DrawTurretPlacement(progress); break;
                case Effect.TurretMuzzle: DrawTurretMuzzle(progress); break;
                case Effect.TurretBolt: DrawTurretBolt(progress); break;
                case Effect.TurretImpact: DrawTurretImpact(progress); break;
                case Effect.PiercePass: DrawPiercePass(progress); break;
                case Effect.PierceCharge: DrawPierceCharge(progress); break;
                case Effect.PowerPlacement: DrawPowerPlacement(progress); break;
                case Effect.PowerReady: DrawPowerReady(); break;
                case Effect.PowerCharge: DrawPowerCharge(progress); break;
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
        }

        private void DrawStrike(float progress)
        {
            Vector3 forward = Direction(heading);
            Vector3 right = Direction(heading + 90f);
            float sweep = EaseOut(progress);
            float size = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0.88f, progress));

            if (size > 0f)
            {
                Vector3 center = forward * (0.12f + 0.76f * sweep) + Vector3.up * 0.09f;
                Vector3 leftTip = center - right * (0.90f * size) - forward * (0.37f * size);
                Vector3 rightTip = center + right * (0.90f * size) - forward * (0.04f * size);
                Vector3 crest = center + right * (0.22f * size) + forward * (0.22f * size);
                Vector3 notch = center - right * (0.21f * size) - forward * (0.23f * size);
                Triangle(leftTip, crest, notch, Amber);
                Triangle(notch, crest, rightTip, Gold);
                Triangle(leftTip, Vector3.Lerp(crest, notch, 0.30f) + Vector3.up * 0.018f,
                    Vector3.Lerp(rightTip, crest, 0.10f) + Vector3.up * 0.018f, Cream);
            }

            float flash = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.19f, 0.56f, progress));
            Burst(Vector3.up * 0.23f, right, forward, 4, 0.46f * flash, 0.13f * flash, Gold);
            Burst(Vector3.up * 0.25f, right, forward, 4, 0.31f * flash, 0.09f * flash, Cream);
            DrawDebris(4, heading, 55f, progress, 0.19f, 1.28f, Gold, Cream);
        }

        private void DrawHeavy(float progress)
        {
            float spread = EaseOut(progress);
            float size = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 0.84f, progress));
            Vector3 forward = Direction(heading);
            Vector3 right = Direction(heading + 90f);
            Vector3 center = forward * (0.10f + 0.44f * spread);

            // Wide points are held through the first part of the impact so the shape reads at game speed.
            float reach = (0.87f + spread * 0.57f) * size;
            Burst(center + Vector3.up * 0.055f, right, forward, 7, reach, reach * 0.52f, Coral);
            Burst(center + Vector3.up * 0.080f, right, forward, 7, reach * 0.80f, reach * 0.41f, Amber);
            Burst(center + Vector3.up * 0.105f, right, forward, 5, reach * 0.52f, reach * 0.23f, Cream);
            DrawDebris(8, heading, 150f, progress, 0.29f, 1.72f, Coral, Gold);
        }

        private void DrawBlast(float progress)
        {
            float growth = EaseOut(Mathf.Clamp01(progress / 0.48f));
            float collapse = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 0.88f, progress));
            float reach = Mathf.Lerp(radius * 0.28f, radius, EaseOut(Mathf.Clamp01(progress / 0.79f)));
            float rimWidth = 0.24f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 1f, progress)));
            PolygonRing(reach, rimWidth, Amber);

            if (collapse > 0f)
            {
                Puff(Vector3.up * (0.42f + growth * 0.37f), (0.40f + growth * 0.55f) * collapse, 0.1f, Gold, Amber);
                for (int i = 0; i < 5; i++)
                {
                    float angle = 11f + i * 72f;
                    float distance = (0.26f + growth * 0.96f) * (i % 2 == 0 ? 1f : 0.87f);
                    Vector3 center = Direction(angle) * distance + Vector3.up * (0.14f + growth * 0.4f);
                    Puff(center, (0.28f + growth * 0.35f) * collapse, angle, Amber, OrangeEdge);
                }
            }

            DrawDebris(7, 32f, 180f, progress, 0.17f, radius * 0.88f, Amber, Gold);
        }

        private void DrawWard()
        {
            float entrance = Mathf.Clamp01(age / 0.28f);
            float size = Mathf.Lerp(0.22f, 1f, EaseOut(entrance)) + Mathf.Sin(entrance * Mathf.PI) * 0.16f;
            Vector3 center = Vector3.up * (0.08f + 0.15f * entrance + 0.035f * Mathf.Sin(age * 5f));
            Vector3 right = Vector3.right * size;
            Vector3 up = ShieldUp * size;
            Vector3 front = Vector3.Cross(up, right).normalized * 0.016f;

            Shield(center, right, up, Navy, Navy);
            Shield(center + front, right * 0.88f, up * 0.88f, Ice, Turquoise);
            Shield(center + front * 2f, right * 0.70f, up * 0.70f, Turquoise, Blue);
            Burst(center + front * 3f, right, up, 4, 0.25f, 0.084f, Ice);
        }

        private void DrawBlockImpact(float progress)
        {
            float flash = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, 0.61f, progress));
            Vector3 center = Vector3.up * 0.18f;
            Burst(center, Vector3.right, ShieldUp, 4, 0.65f * flash, 0.19f * flash, Blue);
            Burst(center + Vector3.up * 0.05f, Vector3.right, ShieldUp, 4, 0.50f * flash, 0.12f * flash, Ice);
            DrawDebris(6, heading, 119f, progress, 0.27f, 1.50f, Blue, Ice);
        }

        private void Shield(Vector3 center, Vector3 right, Vector3 up, Color left, Color rightColor)
        {
            for (int i = 0; i < ShieldShape.Length; i++)
            {
                Vector2 first = ShieldShape[i];
                Vector2 second = ShieldShape[(i + 1) % ShieldShape.Length];
                Triangle(center, center + right * first.x + up * first.y,
                    center + right * second.x + up * second.y, i < 4 ? left : rightColor);
            }
        }

        private void DrawDebris(int count, float direction, float spread, float progress, float size, float distance, Color shade, Color light)
        {
            for (int i = 0; i < count; i++)
            {
                float fraction = (float)i / (count - 1);
                float angle = direction + Mathf.Lerp(-spread, spread, fraction) + (i % 2 == 0 ? -9f : 9f);
                float life = Mathf.Clamp01(progress / Mathf.Lerp(0.72f, 1f, i % 3 * 0.5f));
                float shrink = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.50f, 1f, life));
                float travel = (0.18f + EaseOut(life) * distance) * (i % 2 == 0 ? 1f : 0.69f);
                float lift = 0.06f + Mathf.Sin(life * Mathf.PI) * (0.45f + i % 3 * 0.16f);
                Vector3 center = Direction(angle) * travel + Vector3.up * lift;
                float width = size * shrink * (i % 2 == 0 ? 1f : 0.72f);
                Vector3 axis = Direction(angle + i * 27f + life * 95f) * width;
                Vector3 side = Vector3.Cross(Vector3.up, axis) * 0.58f;
                Vector3 tip = center + axis;
                Vector3 back = center - axis * 0.62f;
                Vector3 crest = center + Vector3.up * (width * 0.68f);
                Triangle(tip, back - side, crest, shade);
                Triangle(tip, crest, back + side, light);
                Triangle(back - side, back + side, crest, Color.Lerp(shade, light, 0.5f));
            }
        }

        private void Burst(Vector3 center, Vector3 right, Vector3 up, int points, float outer, float inner, Color tint)
        {
            if (outer <= 0f)
            {
                return;
            }

            int corners = points * 2;
            for (int i = 0; i < corners; i++)
            {
                float angle = i * Mathf.PI / points;
                float next = (i + 1) * Mathf.PI / points;
                float tip = i % 2 == 0 ? outer : inner;
                float nextTip = i % 2 == 0 ? inner : outer;
                Triangle(center, center + (right * Mathf.Sin(angle) + up * Mathf.Cos(angle)) * tip,
                    center + (right * Mathf.Sin(next) + up * Mathf.Cos(next)) * nextTip, tint);
            }
        }

        private void PolygonRing(float reach, float width, Color tint)
        {
            const int Sides = 12;
            for (int i = 0; i < Sides; i++)
            {
                Vector3 first = Direction(i * 360f / Sides);
                Vector3 second = Direction((i + 1) * 360f / Sides);
                Vector3 outer = first * reach;
                Vector3 nextOuter = second * reach;
                Vector3 inner = first * (reach - width);
                Vector3 nextInner = second * (reach - width);
                Triangle(outer, nextOuter, inner, tint);
                Triangle(inner, nextOuter, nextInner, tint);
            }
        }

        private void Puff(Vector3 center, float size, float yaw, Color light, Color shadow)
        {
            const int Sides = 7;
            for (int i = 0; i < Sides; i++)
            {
                Vector3 first = Direction(yaw + i * 360f / Sides) * size;
                Vector3 second = Direction(yaw + (i + 1) * 360f / Sides) * size;
                Vector3 firstSide = center + first;
                Vector3 secondSide = center + second;
                Vector3 top = center + Vector3.up * (size * 0.74f);
                Vector3 firstCap = top + first * 0.46f;
                Vector3 secondCap = top + second * 0.46f;
                Color side = Color.Lerp(shadow, light, 0.32f + i % 3 * 0.18f);
                Triangle(firstSide, firstCap, secondSide, side);
                Triangle(firstCap, secondCap, secondSide, side);
                Triangle(top, firstCap, secondCap, light);
                Triangle(center - Vector3.up * (size * 0.45f), firstSide, secondSide, shadow);
            }
        }

        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Color tint)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            colors.Add(tint);
            colors.Add(tint);
            colors.Add(tint);
            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 2);
        }

        private static float EaseOut(float value)
        {
            float remaining = 1f - value;
            return 1f - remaining * remaining * remaining;
        }

        private static Vector3 Direction(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }
    }
}
