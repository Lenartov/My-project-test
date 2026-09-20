using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    public static class EnemyActionIconLibrary
    {
        private static readonly Dictionary<EnemyActionKind, Texture2D> GeneratedIcons = new Dictionary<EnemyActionKind, Texture2D>();

        public static Texture2D LoadIcon(EnemyActionKind kind)
        {
            Texture2D resourceIcon = Resources.Load<Texture2D>(GetResourcePath(kind));
            return resourceIcon != null ? resourceIcon : GetGeneratedIcon(kind);
        }

        public static Color GetColor(EnemyActionKind kind)
        {
            switch (kind)
            {
                case EnemyActionKind.Attack:
                    return new Color(1f, 0.08f, 0.04f);
                case EnemyActionKind.Heal:
                    return new Color(0.2f, 1f, 0.3f);
                case EnemyActionKind.Sleep:
                    return new Color(0.28f, 0.7f, 1f);
                case EnemyActionKind.Recovery:
                    return new Color(0.35f, 1f, 0.22f);
                default:
                    return new Color(1f, 0.82f, 0.12f);
            }
        }

        private static string GetResourcePath(EnemyActionKind kind)
        {
            switch (kind)
            {
                case EnemyActionKind.Attack:
                    return "EnemyActionIcons/attack_sword";
                case EnemyActionKind.Heal:
                    return "EnemyActionIcons/heal_cross";
                case EnemyActionKind.Sleep:
                    return "EnemyActionIcons/sleep_zzz";
                case EnemyActionKind.Recovery:
                    return "EnemyActionIcons/recovery_star";
                default:
                    return "EnemyActionIcons/wait_pause";
            }
        }

        private static Texture2D GetGeneratedIcon(EnemyActionKind kind)
        {
            if (GeneratedIcons.TryGetValue(kind, out Texture2D icon))
            {
                return icon;
            }

            icon = kind == EnemyActionKind.Recovery ? CreateStarIcon() : null;
            GeneratedIcons[kind] = icon;
            return icon;
        }

        private static Texture2D CreateStarIcon()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "GeneratedRecoveryStar";

            Color transparent = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, transparent);
                }
            }

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            Vector2[] points = new Vector2[10];
            for (int i = 0; i < points.Length; i++)
            {
                float radius = i % 2 == 0 ? 27f : 11f;
                float angle = Mathf.PI * 0.5f + i * Mathf.PI * 2f / points.Length;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            Color white = Color.white;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                    if (IsPointInPolygon(point, points))
                    {
                        texture.SetPixel(x, y, white);
                    }
                }
            }

            texture.Apply(false, true);
            return texture;
        }

        private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                bool crosses = (a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y) / Mathf.Max(0.0001f, b.y - a.y) + a.x;
                if (crosses)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }

    public sealed class EnemyActionIndicator
    {
        private readonly Transform root;
        private readonly ActionIcon currentIcon;
        private readonly ActionIcon nextIcon;
        private readonly LineRenderer ringBackground;
        private readonly LineRenderer ringProgress;
        private const int RingSegments = 48;
        private const float RingRadius = 0.28f;

        public EnemyActionIndicator(Transform parent)
        {
            GameObject rootObject = new GameObject("Enemy Action Indicator");
            rootObject.transform.SetParent(parent, false);
            rootObject.transform.localPosition = new Vector3(0f, 0.32f, 1.02f);
            root = rootObject.transform;

            currentIcon = new ActionIcon("Current Action Icon", root, Vector3.zero, 1f);
            nextIcon = new ActionIcon("Next Action Icon", root, new Vector3(0.42f, 0.01f, 0.03f), 0.58f);
            ringBackground = CreateRing("Action Ring Background", root, new Color(0.015f, 0.014f, 0.014f), 0.045f);
            ringProgress = CreateRing("Action Ring Progress", root, Color.white, 0.062f);
            SetRingProgress(0f, Color.white);
        }

        public void Set(EnemyActionKind current, float progress, EnemyActionKind next)
        {
            currentIcon.SetKind(current, false);
            nextIcon.SetKind(next, true);
            SetRingProgress(progress, EnemyActionIconLibrary.GetColor(current));
        }

        public void Hide()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        private static LineRenderer CreateRing(string name, Transform parent, Color color, float width)
        {
            GameObject ringObject = new GameObject(name);
            ringObject.transform.SetParent(parent, false);
            ringObject.transform.localPosition = new Vector3(0f, 0.025f, 0f);

            LineRenderer line = ringObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = RingSegments + 1;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 4;
            line.material = CreateLineMaterial(color);
            line.startColor = color;
            line.endColor = color;

            for (int i = 0; i <= RingSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / RingSegments;
                line.SetPosition(i, new Vector3(Mathf.Sin(angle) * RingRadius, 0f, Mathf.Cos(angle) * RingRadius));
            }

            return line;
        }

        private static Material CreateLineMaterial(Color color)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.name = $"EnemyActionLine_{ColorUtility.ToHtmlStringRGBA(color)}";
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }

        private void SetRingProgress(float progress, Color color)
        {
            if (ringProgress == null)
            {
                return;
            }

            if (progress <= 0f)
            {
                ringProgress.positionCount = 0;
                return;
            }

            int activeSegments = Mathf.Clamp(Mathf.CeilToInt(RingSegments * Mathf.Clamp01(progress)), 1, RingSegments);
            ringProgress.positionCount = activeSegments + 1;
            ringProgress.startColor = color;
            ringProgress.endColor = color;
            ringProgress.material.color = color;
            if (ringProgress.material.HasProperty("_BaseColor"))
            {
                ringProgress.material.SetColor("_BaseColor", color);
            }

            for (int i = 0; i <= activeSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / RingSegments;
                ringProgress.SetPosition(i, new Vector3(Mathf.Sin(angle) * RingRadius, 0f, Mathf.Cos(angle) * RingRadius));
            }
        }

        private sealed class ActionIcon
        {
            private readonly Renderer renderer;
            private EnemyActionKind currentKind;
            private bool currentMuted;
            private bool hasKind;

            public ActionIcon(string name, Transform parent, Vector3 localPosition, float scale)
            {
                GameObject rootObject = new GameObject(name);
                rootObject.transform.SetParent(parent, false);
                rootObject.transform.localPosition = localPosition;
                rootObject.transform.localScale = Vector3.one * scale;

                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Icon Texture";
                quad.transform.SetParent(rootObject.transform, false);
                quad.transform.localPosition = new Vector3(0f, 0.018f, 0f);
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                quad.transform.localScale = new Vector3(0.46f, 0.46f, 1f);
                renderer = quad.GetComponent<Renderer>();
                DisableAndDestroyCollider(quad.GetComponent<Collider>());
            }

            public void SetKind(EnemyActionKind kind, bool muted)
            {
                if (hasKind && currentKind == kind && currentMuted == muted)
                {
                    return;
                }

                hasKind = true;
                currentKind = kind;
                currentMuted = muted;

                Color color = EnemyActionIconLibrary.GetColor(kind);
                if (muted)
                {
                    color = Color.Lerp(color, new Color(0.12f, 0.12f, 0.12f), 0.52f);
                    color.a = 0.58f;
                }

                renderer.sharedMaterial = CreateIconMaterial(EnemyActionIconLibrary.LoadIcon(kind), color);
            }
        }

        private static Material CreateIconMaterial(Texture2D texture, Color tint)
        {
            Material material = CreateLineMaterial(tint);
            material.mainTexture = texture;
            return material;
        }

        private static void DisableAndDestroyCollider(Collider collider)
        {
            if (collider == null)
            {
                return;
            }

            collider.enabled = false;
            Object.Destroy(collider);
        }
    }

    public sealed class EnemyAttackProjectile : MonoBehaviour
    {
        private Transform projectile;
        private Vector3 start;
        private Vector3 end;
        private TextMesh damageText;
        private Camera mainCamera;
        private float age;
        private const float Lifetime = 0.3f;

        public static void Spawn(Vector3 start, Vector3 end, float damage)
        {
            GameObject projectileObject = new GameObject("Enemy Attack Projectile");
            EnemyAttackProjectile projectile = projectileObject.AddComponent<EnemyAttackProjectile>();
            projectile.start = start;
            projectile.end = end;
            projectile.mainCamera = Camera.main;

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Projectile Sphere";
            sphere.transform.SetParent(projectileObject.transform, false);
            sphere.transform.localScale = Vector3.one * 0.17f;
            sphere.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Create(new Color(1f, 0.02f, 0.01f), 0.16f);
            Collider collider = sphere.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);

            projectile.projectile = sphere.transform;
            projectile.damageText = CreateDamageText(projectileObject.transform, damage);
            projectile.UpdateProjectile(0f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float amount = Mathf.Clamp01(age / Lifetime);
            UpdateProjectile(amount);

            if (age >= Lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void UpdateProjectile(float amount)
        {
            if (projectile == null)
            {
                return;
            }

            projectile.position = Vector3.Lerp(start, end, amount);

            if (damageText != null)
            {
                damageText.transform.position = projectile.position + Vector3.up * 0.18f;
                if (mainCamera != null)
                {
                    damageText.transform.rotation = Quaternion.LookRotation(damageText.transform.position - mainCamera.transform.position);
                }
            }
        }

        private static TextMesh CreateDamageText(Transform parent, float damage)
        {
            GameObject textObject = new GameObject("Attack Damage");
            textObject.transform.SetParent(parent, false);

            TextMesh text = textObject.AddComponent<TextMesh>();
            text.text = damage.ToString("0");
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.1f;
            text.fontSize = 64;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(1f, 0.18f, 0.1f);
            textObject.GetComponent<MeshRenderer>().sortingOrder = 51;
            return text;
        }
    }
}
