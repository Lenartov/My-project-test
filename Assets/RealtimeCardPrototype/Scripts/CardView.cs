using UnityEngine;

namespace RealtimeCardPrototype
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CardView : MonoBehaviour
    {
        [SerializeField] private CardTeam team;
        [SerializeField] private TextMesh titleText;
        [SerializeField] private TextMesh bodyText;
        [SerializeField] private Renderer bodyRenderer;

        private readonly Renderer[] powerOutlineRenderers = new Renderer[4];
        private Color baseBodyColor = Color.white;
        private Color baseTextColor = Color.white;

        public CardTeam Team => team;
        public Rigidbody Body { get; private set; }
        public BoxCollider Collider { get; private set; }

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Collider = GetComponent<BoxCollider>();
        }

        public void Configure(CardTeam cardTeam, string title, string body, Color bodyColor, Color textColor)
        {
            team = cardTeam;

            if (bodyRenderer != null)
            {
                bodyRenderer.sharedMaterial = PrototypeMaterials.Create(bodyColor, 0.52f);
                baseBodyColor = bodyColor;
            }

            baseTextColor = textColor;

            if (titleText != null)
            {
                titleText.text = title;
                titleText.color = textColor;
            }

            if (bodyText != null)
            {
                bodyText.text = body;
                bodyText.color = textColor;
            }
        }

        public void SetAvailable(bool isAvailable)
        {
            Color bodyColor = isAvailable ? baseBodyColor : Color.Lerp(baseBodyColor, Color.gray, 0.78f);
            Color textColor = isAvailable ? baseTextColor : new Color(0.62f, 0.62f, 0.62f, 1f);

            if (bodyRenderer != null)
            {
                bodyRenderer.material.color = bodyColor;

                if (bodyRenderer.material.HasProperty("_BaseColor"))
                {
                    bodyRenderer.material.SetColor("_BaseColor", bodyColor);
                }
            }

            if (titleText != null)
            {
                titleText.color = textColor;
            }

            if (bodyText != null)
            {
                bodyText.color = textColor;
            }
        }

        public void SetPowerOutline(int stacks)
        {
            bool isActive = stacks > 0;
            Color color = GetPowerOutlineColor(stacks);

            for (int i = 0; i < powerOutlineRenderers.Length; i++)
            {
                Renderer outline = powerOutlineRenderers[i];
                if (outline == null)
                {
                    continue;
                }

                outline.gameObject.SetActive(isActive);
                if (isActive)
                {
                    DisableAndDestroyCollider(outline.GetComponent<Collider>());
                    outline.material.color = color;

                    if (outline.material.HasProperty("_BaseColor"))
                    {
                        outline.material.SetColor("_BaseColor", color);
                    }
                }
            }
        }

        public static CardView Create(string name, Vector3 position, Vector2 size, Color color, CardTeam team)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;

            CardView view = root.AddComponent<CardView>();
            BoxCollider collider = root.GetComponent<BoxCollider>();
            collider.size = new Vector3(size.x, 0.18f, size.y);

            Rigidbody body = root.GetComponent<Rigidbody>();
            body.mass = team == CardTeam.Player ? 0.85f : 2.2f;
            body.linearDamping = 5.5f;
            body.angularDamping = 8f;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Body";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = new Vector3(size.x, 0.14f, size.y);
            DisableAndDestroyCollider(visual.GetComponent<Collider>());

            Renderer renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = PrototypeMaterials.Create(color, 0.52f);

            float textScale = team == CardTeam.Enemy ? 0.8f : 1f;
            TextMesh title = CreateText("Title", root.transform, new Vector3(0f, 0.105f, size.y * 0.22f), 0.073f * textScale, FontStyle.Bold);
            TextMesh bodyText = CreateText("BodyText", root.transform, new Vector3(0f, 0.108f, -size.y * 0.2f), 0.043f * textScale, FontStyle.Normal);

            view.bodyRenderer = renderer;
            view.titleText = title;
            view.bodyText = bodyText;
            view.Configure(team, name, string.Empty, color, Color.white);

            if (team == CardTeam.Player)
            {
                view.CreatePowerOutline(size);
                AddMotionTrail(root, color);
                root.AddComponent<CardActivityReporter>();
            }

            return view;
        }

        private static void AddMotionTrail(GameObject root, Color color)
        {
            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.minVertexDistance = 0.035f;
            trail.startWidth = 0.42f;
            trail.endWidth = 0.02f;
            trail.numCapVertices = 4;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.emitting = true;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.55f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            trail.colorGradient = gradient;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            trail.sharedMaterial = new Material(shader)
            {
                color = color
            };
        }

        private void CreatePowerOutline(Vector2 size)
        {
            Color color = GetPowerOutlineColor(1);
            powerOutlineRenderers[0] = CreateOutlineSegment("Power Outline Top", new Vector3(0f, 0.121f, size.y * 0.5f), new Vector3(size.x + 0.1f, 0.026f, 0.045f), color);
            powerOutlineRenderers[1] = CreateOutlineSegment("Power Outline Bottom", new Vector3(0f, 0.121f, -size.y * 0.5f), new Vector3(size.x + 0.1f, 0.026f, 0.045f), color);
            powerOutlineRenderers[2] = CreateOutlineSegment("Power Outline Left", new Vector3(-size.x * 0.5f, 0.121f, 0f), new Vector3(0.045f, 0.026f, size.y + 0.1f), color);
            powerOutlineRenderers[3] = CreateOutlineSegment("Power Outline Right", new Vector3(size.x * 0.5f, 0.121f, 0f), new Vector3(0.045f, 0.026f, size.y + 0.1f), color);
            SetPowerOutline(0);
        }

        private Renderer CreateOutlineSegment(string segmentName, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = segmentName;
            segment.transform.SetParent(transform, false);
            segment.transform.localPosition = localPosition;
            segment.transform.localScale = localScale;
            DisableAndDestroyCollider(segment.GetComponent<Collider>());

            Renderer renderer = segment.GetComponent<Renderer>();
            renderer.sharedMaterial = PrototypeMaterials.Create(color, 0.2f);
            return renderer;
        }

        private Color GetPowerOutlineColor(int stacks)
        {
            switch (Mathf.Clamp(stacks, 1, 3))
            {
                case 1:
                    return new Color(1f, 0.92f, 0.08f);
                case 2:
                    return new Color(1f, 0.48f, 0.08f);
                default:
                    return new Color(1f, 0.05f, 0.02f);
            }
        }

        private static TextMesh CreateText(string name, Transform parent, Vector3 localPosition, float characterSize, FontStyle style)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            TextMesh text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = characterSize;
            text.fontSize = 64;
            text.fontStyle = style;
            text.color = Color.white;

            MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
            renderer.sortingOrder = 10;

            return text;
        }

        private static void DisableAndDestroyCollider(Collider collider)
        {
            if (collider == null)
            {
                return;
            }

            collider.enabled = false;
            Destroy(collider);
        }
    }
}
