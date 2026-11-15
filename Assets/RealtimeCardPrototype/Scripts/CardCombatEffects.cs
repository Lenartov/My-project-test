using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealtimeCardPrototype
{
    internal sealed class CardCombatEffects : MonoBehaviour
    {
        private enum Effect
        {
            Strike,
            Heavy,
            Blast,
            Ward,
            BlockImpact
        }

        private struct Spark
        {
            public Vector3 Velocity;
            public float Length;
            public float Lifetime;
            public Color Color;
        }

        private static Material effectMaterial;
        private static Material ribbonMaterial;

        private readonly List<Vector3> vertices = new List<Vector3>(672);
        private readonly List<Color> colors = new List<Color>(672);
        private readonly List<int> indices = new List<int>(2304);
        private Mesh mesh;
        private Spark[] sparks;
        private Effect effect;
        private Color color;
        private float age;
        private float duration;
        private float delay;
        private float heading;
        private float radius;

        internal static Material RibbonMaterial
        {
            get
            {
                if (ribbonMaterial == null)
                {
                    ribbonMaterial = new Material(EffectMaterial) { name = "Card Ribbon", hideFlags = HideFlags.DontSave };
                    ribbonMaterial.SetFloat("_Ribbon", 1f);
                }

                return ribbonMaterial;
            }
        }

        private static Material EffectMaterial
        {
            get
            {
                if (effectMaterial == null)
                {
                    Shader shader = Resources.Load<Shader>("CardEffects");
                    effectMaterial = new Material(shader) { name = "Card Combat Effects", hideFlags = HideFlags.DontSave };
                }

                return effectMaterial;
            }
        }

        internal static Color GetColor(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Strike: return new Color(1f, 0.73f, 0.26f);
                case CardKind.Heavy: return new Color(1f, 0.27f, 0.4f);
                case CardKind.Blast: return new Color(1f, 0.4f, 0.07f);
                default: return new Color(0.24f, 0.83f, 1f);
            }
        }

        internal static void Strike(Vector3 point, Vector3 velocity, bool heavy)
        {
            CardCombatEffects instance = Create(point, heavy ? Effect.Heavy : Effect.Strike,
                GetColor(heavy ? CardKind.Heavy : CardKind.Strike), heavy ? 0.53f : 0.34f);
            instance.heading = GetHeading(velocity);
            instance.CreateSparks(heavy ? 22 : 11, 68f, heavy ? 5.5f : 4.2f);
            instance.Render();
        }

        internal static void Blast(Vector3 point, float splashRadius)
        {
            CardCombatEffects instance = Create(point, Effect.Blast, GetColor(CardKind.Blast), 0.68f);
            instance.radius = splashRadius;
            instance.CreateSparks(38, 180f, 5.8f);
            instance.Render();
        }

        internal static CardCombatEffects RaiseWard(Transform card, float height)
        {
            CardCombatEffects ward = Create(card.position + Vector3.up * height, Effect.Ward, GetColor(CardKind.Block), float.PositiveInfinity);
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
            CardCombatEffects instance = Create(point, Effect.BlockImpact, GetColor(CardKind.Block), 0.48f);
            instance.delay = travelTime;
            instance.heading = GetHeading(incoming);
            instance.CreateSparks(19, 83f, 4.2f);
        }

        private static CardCombatEffects Create(Vector3 point, Effect effect, Color color, float duration)
        {
            GameObject root = new GameObject(effect + " VFX");
            root.transform.position = point;
            CardCombatEffects instance = root.AddComponent<CardCombatEffects>();
            instance.effect = effect;
            instance.color = color;
            instance.duration = duration;
            instance.mesh = new Mesh { name = root.name };
            instance.mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh = instance.mesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = EffectMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 9;
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
            }

            age += deltaTime;
            if (age >= duration)
            {
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

            if (effect == Effect.Ward)
            {
                DrawWard();
            }
            else
            {
                float progress = age / duration;
                float fade = 1f - progress;
                float expansion = 1f - Mathf.Pow(fade, 2.2f);
                Color whiteHot = Color.Lerp(color, Color.white, 0.8f);
                switch (effect)
                {
                    case Effect.Strike:
                        Arc(heading - 82f, 164f, 0.24f + 0.64f * expansion, 0.18f * fade, 0f, 0f, whiteHot, fade);
                        Arc(heading - 67f, 134f, 0.1f + 0.55f * expansion, 0.15f * fade, -0.025f, 0f, color, fade * 0.6f);
                        break;
                    case Effect.Heavy:
                        Arc(heading - 103f, 206f, 0.38f + 1.2f * expansion, 0.26f * fade, 0f, 0f, whiteHot, fade);
                        Arc(heading - 126f, 252f, 0.26f + 1.57f * expansion, 0.48f * fade, -0.03f, 0f, color, fade * 0.88f);
                        Arc(0f, 360f, 0.12f + 0.84f * expansion, 0.13f, 0.04f, 0f, color, fade * 0.55f);
                        break;
                    case Effect.Blast:
                        float wave = Mathf.Lerp(0.25f, radius, expansion);
                        Arc(0f, 360f, wave, 0.32f + 0.2f * progress, 0f, 0f, color, fade * 0.87f);
                        Arc(0f, 360f, wave * 0.91f, 0.12f, 0.035f, 0f, whiteHot, fade * 0.92f);
                        Arc(0f, 360f, wave * 0.57f, 0.22f, 0.09f, 0f, color, fade * fade * 0.8f);
                        break;
                    case Effect.BlockImpact:
                        Arc(heading - 72f, 144f, 0.39f + 0.54f * expansion, 0.24f, 0f, 0.47f, color, fade * 0.92f);
                        Arc(0f, 360f, 0.23f + 1.03f * expansion, 0.18f, -0.02f, 0f, whiteHot, fade * 0.83f);
                        break;
                }

                float flash = Mathf.Clamp01(1f - progress * 4f);
                float flashRadius = effect == Effect.Blast ? 0.68f : 0.37f;
                Arc(0f, 360f, flashRadius * 0.5f, flashRadius, 0.07f, 0f, whiteHot, flash);
                DrawSparks();
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
        }

        private void DrawWard()
        {
            float fadeIn = Mathf.Clamp01(age / 0.26f);
            float pulse = 0.73f + 0.18f * Mathf.Sin(age * 8f);
            float wardRadius = Mathf.Lerp(0.35f, 0.83f, fadeIn);
            Arc(0f, 360f, wardRadius, 0.11f, -0.015f, 0f, color, pulse * 0.78f);
            for (int segment = 0; segment < 3; segment++)
            {
                float angle = segment * 120f + age * 26f;
                Arc(angle, 79f, wardRadius * 0.96f, 0.14f, 0.03f, 0.32f * fadeIn, color, pulse * 0.47f);
                Arc(angle, 79f, wardRadius * 0.96f, 0.07f, 0.17f * fadeIn, 0f, Color.Lerp(color, Color.white, 0.55f), pulse);
            }

            float activation = 1f - fadeIn;
            Arc(0f, 360f, 0.4f + fadeIn, 0.18f, 0.04f, 0f, color, activation * 0.85f);
        }

        private void CreateSparks(int count, float spread, float speed)
        {
            System.Random random = new System.Random(GetInstanceID());
            sparks = new Spark[count];
            for (int i = 0; i < count; i++)
            {
                float angle = heading + Mathf.Lerp(-spread, spread, (float)random.NextDouble());
                float velocity = Mathf.Lerp(0.28f, 1f, (float)random.NextDouble()) * speed;
                sparks[i] = new Spark
                {
                    Velocity = Direction(angle) * velocity + Vector3.up * Mathf.Lerp(0.2f, 1.45f, (float)random.NextDouble()),
                    Length = Mathf.Lerp(0.12f, 0.44f, (float)random.NextDouble()),
                    Lifetime = duration * Mathf.Lerp(0.43f, 1f, (float)random.NextDouble()),
                    Color = Color.Lerp(color, Color.white, (float)random.NextDouble() * 0.82f)
                };
            }
        }

        private void DrawSparks()
        {
            foreach (Spark spark in sparks)
            {
                float fade = 1f - age / spark.Lifetime;
                if (fade <= 0f)
                {
                    continue;
                }

                Vector3 position = spark.Velocity * (age / (1f + age)) + Vector3.up * (0.045f - age * age * 0.65f);
                Vector3 length = spark.Velocity.normalized * spark.Length * (0.52f + fade * 0.48f);
                Vector3 width = Vector3.Cross(Vector3.up, length).normalized * (0.014f + fade * 0.018f);
                int start = vertices.Count;
                AddVertex(position + length * 0.42f, spark.Color, fade * 0.7f);
                AddVertex(position - width, spark.Color, fade);
                AddVertex(position - length, spark.Color, 0f);
                AddVertex(position + width, spark.Color, fade);
                AddQuad(start, start + 1, start + 2, start + 3);
            }
        }

        private void Arc(float angle, float sweep, float arcRadius, float thickness, float height, float wallHeight, Color tint, float opacity)
        {
            if (opacity <= 0f)
            {
                return;
            }

            int segments = Mathf.CeilToInt(sweep / 9f);
            bool tapered = sweep < 360f;
            int start = vertices.Count;
            for (int index = 0; index <= segments; index++)
            {
                float fraction = (float)index / segments;
                Vector3 direction = Direction(angle + fraction * sweep);
                float taper = tapered ? Mathf.Max(0f, Mathf.Sin(Mathf.PI * fraction)) : 1f;
                Vector3 center = direction * arcRadius + Vector3.up * height;
                Vector3 edge = direction * (thickness * 0.5f * taper) + Vector3.up * (wallHeight * 0.5f * taper);
                AddVertex(center - edge, tint, 0f);
                AddVertex(center, tint, opacity * taper);
                AddVertex(center + edge, tint, 0f);

                if (index > 0)
                {
                    int current = start + index * 3;
                    AddQuad(current - 3, current - 2, current + 1, current);
                    AddQuad(current - 2, current - 1, current + 2, current + 1);
                }
            }
        }

        private void AddVertex(Vector3 position, Color tint, float opacity)
        {
            tint.a = opacity;
            vertices.Add(position);
            colors.Add(tint);
        }

        private void AddQuad(int a, int b, int c, int d)
        {
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
            indices.Add(a);
            indices.Add(c);
            indices.Add(d);
        }

        private static float GetHeading(Vector3 direction)
        {
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        private static Vector3 Direction(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }
    }
}
