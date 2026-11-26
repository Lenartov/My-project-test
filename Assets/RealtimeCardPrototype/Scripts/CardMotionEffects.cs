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
            float edge = width * 0.37f;

            switch (kind)
            {
                case CardKind.Strike:
                    trails = new[]
                    {
                        CreateTrail(new Vector3(-edge, height, 0f), 0.26f, 0.17f, CardCombatEffects.Gold, CardCombatEffects.Cream),
                        CreateTrail(new Vector3(edge, height, 0f), 0.09f, 0.13f, CardCombatEffects.Cream, CardCombatEffects.Cream)
                    };
                    break;
                case CardKind.Heavy:
                    trails = new[]
                    {
                        CreateTrail(new Vector3(0f, height - 0.12f, 0f), 0.64f, 0.24f, CardCombatEffects.Coral, CardCombatEffects.Amber),
                        CreateTrail(new Vector3(edge * 1.25f, height - 0.05f, 0f), 0.12f, 0.18f, CardCombatEffects.Coral, CardCombatEffects.Coral)
                    };
                    break;
                case CardKind.Blast:
                    trails = new[]
                    {
                        CreateTrail(new Vector3(-edge * 0.64f, height - 0.04f, 0f), 0.28f, 0.20f, CardCombatEffects.Amber, CardCombatEffects.Gold),
                        CreateTrail(new Vector3(edge * 0.83f, height + 0.02f, 0f), 0.18f, 0.15f, CardCombatEffects.Coral, CardCombatEffects.Amber)
                    };
                    break;
                case CardKind.Shock:
                    trails = new[]
                    {
                        CreateTrail(new Vector3(-edge, height, 0f), 0.21f, 0.15f, CardCombatEffects.ElectricBlue, CardCombatEffects.Cream),
                        CreateTrail(new Vector3(edge, height, 0f), 0.10f, 0.11f, CardCombatEffects.Ice, CardCombatEffects.Ice)
                    };
                    break;
                case CardKind.Poison:
                    trails = new[]
                    {
                        CreateTrail(new Vector3(-edge * 0.6f, height - 0.12f, 0f), 0.25f, 0.19f, CardCombatEffects.PoisonGreen, CardCombatEffects.Lime),
                        CreateTrail(new Vector3(edge, height - 0.05f, 0f), 0.13f, 0.13f, CardCombatEffects.PoisonGreen, CardCombatEffects.PoisonGreen)
                    };
                    break;
                case CardKind.Focus:
                    trails = new[]
                    {
                        CreateTrail(new Vector3(-edge, height, 0f), 0.16f, 0.23f, CardCombatEffects.Turquoise, CardCombatEffects.Ice),
                        CreateTrail(new Vector3(edge * 0.76f, height + 0.08f, 0f), 0.09f, 0.17f, CardCombatEffects.ElectricBlue, CardCombatEffects.Ice)
                    };
                    break;
                case CardKind.Turret:
                    trails = new[]
                    {
                        CreateTrail(new Vector3(-edge, height, 0f), 0.14f, 0.16f, CardCombatEffects.Amber, CardCombatEffects.Gold),
                        CreateTrail(new Vector3(edge, height, 0f), 0.14f, 0.16f, CardCombatEffects.Amber, CardCombatEffects.Gold)
                    };
                    break;
                case CardKind.Pierce:
                    trails = new[]
                    {
                        CreateTrail(new Vector3(-edge, height + 0.06f, 0f), 0.13f, 0.19f, CardCombatEffects.PierceMint, CardCombatEffects.Ice),
                        CreateTrail(new Vector3(edge, height + 0.06f, 0f), 0.13f, 0.19f, CardCombatEffects.PierceMint, CardCombatEffects.Ice)
                    };
                    break;
                case CardKind.Power:
                    trails = new[]
                    {
                        CreateTrail(new Vector3(-edge, height, 0f), 0.17f, 0.17f, CardCombatEffects.Gold, CardCombatEffects.Cream),
                        CreateTrail(new Vector3(edge, height + 0.05f, 0f), 0.09f, 0.12f, CardCombatEffects.Amber, CardCombatEffects.Gold)
                    };
                    break;
                default:
                    trails = new[]
                    {
                        CreateTrail(new Vector3(-edge, height - 0.04f, 0f), 0.18f, 0.16f, CardCombatEffects.Turquoise, CardCombatEffects.Ice),
                        CreateTrail(new Vector3(edge, height - 0.04f, 0f), 0.18f, 0.16f, CardCombatEffects.Turquoise, CardCombatEffects.Ice)
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

        private TrailRenderer CreateTrail(Vector3 offset, float width, float lifetime, Color tail, Color tip)
        {
            GameObject trailObject = new GameObject("Card Motion Ribbon");
            trailObject.transform.SetParent(transform, false);
            trailObject.transform.localPosition = offset;

            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = CardCombatEffects.Material;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.time = lifetime;
            trail.minVertexDistance = 0.08f;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.widthMultiplier = width;
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.52f), new Keyframe(0.22f, 1f), new Keyframe(0.55f, 0.68f), new Keyframe(1f, 0f));
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(tip, 0f), new GradientColorKey(tail, 0.64f), new GradientColorKey(tail, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            };
            trail.emitting = false;
            return trail;
        }
    }
}
