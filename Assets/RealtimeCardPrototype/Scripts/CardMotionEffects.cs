using UnityEngine;
using UnityEngine.Rendering;

namespace RealtimeCardPrototype
{
    internal sealed class CardMotionEffects : MonoBehaviour
    {
        private TrailRenderer[] trails;
        private IHandCard card;
        private bool isEmitting;

        internal void Configure(CardKind kind, IHandCard handCard, float width, float height)
        {
            card = handCard;
            Color color = CardCombatEffects.GetColor(kind);
            float edge = width * 0.37f;

            switch (kind)
            {
                case CardKind.Strike:
                    trails = new[]
                    {
                        CreateTrail(new Vector3(-edge, height, 0f), 0.18f, 0.21f, color, 0.65f),
                        CreateTrail(new Vector3(edge, height, 0f), 0.11f, 0.14f, color, 0.8f)
                    };
                    break;
                case CardKind.Heavy:
                    trails = new[]
                    {
                        CreateTrail(new Vector3(0f, height - 0.12f, 0f), 0.78f, 0.37f, color, 0.37f),
                        CreateTrail(new Vector3(-edge, height, 0f), 0.19f, 0.29f, color, 0.9f),
                        CreateTrail(new Vector3(edge, height, 0f), 0.19f, 0.29f, color, 0.9f)
                    };
                    break;
                case CardKind.Blast:
                    trails = new[]
                    {
                        CreateTrail(new Vector3(0f, height - 0.06f, 0f), 0.64f, 0.31f, color, 0.48f),
                        CreateTrail(new Vector3(-edge * 0.65f, height, 0f), 0.14f, 0.22f, Color.yellow, 0.8f),
                        CreateTrail(new Vector3(edge * 0.65f, height, 0f), 0.14f, 0.16f, color, 0.85f)
                    };
                    break;
                default:
                    trails = new[]
                    {
                        CreateTrail(new Vector3(0f, height - 0.06f, 0f), 0.55f, 0.26f, color, 0.33f),
                        CreateTrail(new Vector3(-edge, height, 0f), 0.12f, 0.26f, color, 0.77f),
                        CreateTrail(new Vector3(edge, height, 0f), 0.12f, 0.26f, color, 0.77f)
                    };
                    break;
            }
        }

        private void LateUpdate()
        {
            bool emitting = card.IsDragging && Time.deltaTime > 0f;
            if (isEmitting == emitting)
            {
                return;
            }

            isEmitting = emitting;
            foreach (TrailRenderer trail in trails)
            {
                trail.emitting = emitting;
            }
        }

        internal void Release()
        {
            enabled = false;
            foreach (TrailRenderer trail in trails)
            {
                trail.emitting = false;
                trail.transform.SetParent(null, true);
                Destroy(trail.gameObject, trail.time);
            }
        }

        private TrailRenderer CreateTrail(Vector3 offset, float width, float lifetime, Color color, float opacity)
        {
            GameObject trailObject = new GameObject("Card Motion Ribbon");
            trailObject.transform.SetParent(transform, false);
            trailObject.transform.localPosition = offset;

            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = CardCombatEffects.RibbonMaterial;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.time = lifetime;
            trail.minVertexDistance = 0.035f;
            trail.numCornerVertices = 3;
            trail.numCapVertices = 3;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.widthMultiplier = width;
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.4f, 0.62f), new Keyframe(1f, 0f));
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.62f), 0f), new GradientColorKey(color, 0.52f), new GradientColorKey(color, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(opacity, 0f), new GradientAlphaKey(opacity * 0.55f, 0.42f), new GradientAlphaKey(0f, 1f) }
            };
            trail.emitting = false;
            return trail;
        }
    }
}
