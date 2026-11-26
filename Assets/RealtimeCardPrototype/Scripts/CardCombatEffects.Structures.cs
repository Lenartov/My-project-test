using UnityEngine;

namespace RealtimeCardPrototype
{
    internal sealed partial class CardCombatEffects
    {
        private const float FocusBubbleInterval = 3.6f;
        private const float FocusDropLifetime = 0.94f;
        private const float FocusDropStagger = 0.17f;
        private const float TurretTargetHeight = 0.26f;
        private static readonly Color ManaBlue = new Color(0.09f, 0.73f, 1f);
        private static readonly Color Copper = new Color(0.55f, 0.19f, 0.045f);

        private Transform boltTarget;

        internal static void PlaceFocus(Transform card, float halfWidth)
        {
            CardCombatEffects placement = Create(card.position - Vector3.up * 0.11f, Effect.FocusPlacement, 0.52f);
            placement.radius = halfWidth;
            placement.Render();

            CardCombatEffects flow = Create(card.position + Vector3.up * 0.24f, Effect.FocusFlow, float.PositiveInfinity);
            flow.transform.SetParent(card, true);
            flow.radius = halfWidth;
            CardCombatAudio.PlayFocusBubble();
        }

        internal static void PlaceTurret(Vector3 position, float halfWidth)
        {
            CardCombatEffects placement = Create(position - Vector3.up * 0.10f, Effect.TurretPlacement, 0.38f);
            placement.radius = halfWidth;
            placement.Render();
        }

        internal static void FireTurret(Vector3 origin, Transform target)
        {
            Vector3 destination = target.position + Vector3.up * TurretTargetHeight;
            Vector3 offset = destination - origin;
            float angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            Vector3 muzzlePoint = origin + Direction(angle) * Mathf.Min(0.29f, offset.magnitude * 0.25f);

            CardCombatEffects muzzle = Create(muzzlePoint, Effect.TurretMuzzle, 0.17f);
            muzzle.heading = angle;
            muzzle.Render();

            CardCombatEffects bolt = Create(muzzlePoint, Effect.TurretBolt, 0.16f);
            bolt.boltTarget = target;
            bolt.boltEnd = destination - muzzlePoint;
            bolt.heading = angle;
            bolt.Render();
            CardCombatAudio.PlayTurretShot();
        }

        private void DrawFocusPlacement(float progress)
        {
            float advance = EaseOut(progress);
            float width = Mathf.Lerp(0.09f, 0f, progress);
            PolygonRing(radius * (1.08f + advance * 1.17f), width, ManaBlue);
            for (int i = 0; i < 5; i++)
            {
                Vector3 outward = Direction(36f + i * 72f);
                Vector3 center = outward * (radius + advance * 0.52f) + Vector3.up * (0.06f + Mathf.Sin(progress * Mathf.PI) * 0.34f);
                ManaDrop(center, i % 2 == 0 ? 0.09f : 0.06f, 1f - progress);
            }
        }

        private void DrawFocusFlow()
        {
            for (int i = 0; i < 3; i++)
            {
                float progress = (age - i * FocusDropStagger) / FocusDropLifetime;
                if (progress <= 0f || progress >= 1f)
                {
                    continue;
                }

                Vector3 side = Direction(70f + i * 105f);
                Vector3 center = side * (radius * 0.86f + progress * 0.24f) + Vector3.up * (0.04f + progress * 0.48f);
                float size = Mathf.SmoothStep(0f, 1f, progress * 5f) * Mathf.SmoothStep(0f, 1f, (1f - progress) * 2.2f);
                ManaDrop(center, i == 1 ? 0.11f : 0.08f, size);
            }
        }

        private void ManaDrop(Vector3 center, float width, float scale)
        {
            width *= scale;
            Vector3 top = center + Vector3.up * (width * 1.65f);
            Vector3 bottom = center - Vector3.up * (width * 0.75f);
            const int Sides = 5;
            for (int i = 0; i < Sides; i++)
            {
                Vector3 first = center + Direction(i * 360f / Sides) * width;
                Vector3 next = center + Direction((i + 1) * 360f / Sides) * width;
                Triangle(top, first, next, i < 2 ? Ice : ManaBlue);
                Triangle(bottom, next, first, Turquoise);
            }
        }

        private void DrawTurretPlacement(float progress)
        {
            float spread = EaseOut(progress);
            float size = 1f - Mathf.SmoothStep(0f, 1f, progress);
            for (int i = 0; i < 4; i++)
            {
                Vector3 outward = Direction(45f + i * 90f);
                Vector3 across = Vector3.Cross(Vector3.up, outward);
                Vector3 point = outward * (radius * 1.26f + spread * 0.44f);
                Vector3 tip = point + outward * (0.17f * size);
                Vector3 notch = point - outward * (0.045f * size);
                Triangle(tip, point - across * (0.15f * size), notch, Amber);
                Triangle(tip, notch, point + across * (0.15f * size), Gold);
                Puff(point + outward * 0.12f + Vector3.up * (spread * 0.14f), 0.092f * size, i * 54f, Cream, Copper);
            }
        }

        private void DrawTurretMuzzle(float progress)
        {
            float size = 1f - Mathf.SmoothStep(0f, 1f, progress);
            Vector3 forward = Direction(heading);
            Vector3 right = Direction(heading + 90f);
            Vector3 center = forward * (0.10f + progress * 0.28f);
            Burst(center, right, forward, 4, 0.29f * size, 0.09f * size, Amber);
            Burst(center + Vector3.up * 0.022f, right, forward, 4, 0.20f * size, 0.064f * size, Cream);
        }

        private void DrawTurretBolt(float progress)
        {
            Vector3 forward = boltEnd.normalized;
            Vector3 right = Direction(heading + 90f);
            Vector3 nose = boltEnd * progress;
            Vector3 trailEnd = nose - forward * Mathf.Min(1.12f, nose.magnitude);
            Vector3 neck = nose - forward * 0.21f;
            Vector3 left = neck - right * 0.09f;
            Vector3 rightEdge = neck + right * 0.09f;
            Vector3 ridge = neck + Vector3.up * 0.07f;
            Triangle(trailEnd, left, rightEdge, Amber);
            Triangle(trailEnd + Vector3.up * 0.015f, ridge, rightEdge, Gold);
            Triangle(trailEnd + Vector3.up * 0.015f, left, ridge, Cream);
            Triangle(nose, ridge, rightEdge, Gold);
            Triangle(nose, left, ridge, Cream);
            Triangle(nose, rightEdge, left, Copper);

            Vector3 fletching = nose - forward * 0.32f;
            Triangle(fletching + right * 0.15f, fletching - forward * 0.17f, ridge, Amber);
            Triangle(fletching - right * 0.15f, ridge, fletching - forward * 0.17f, Gold);
        }

        private void DrawTurretImpact(float progress)
        {
            float flash = 1f - Mathf.SmoothStep(0f, 1f, progress * 1.9f);
            Burst(Vector3.zero, Vector3.right, Vector3.forward, 4, 0.32f * flash, 0.095f * flash, Amber);
            Burst(Vector3.up * 0.032f, Vector3.right, Vector3.forward, 4, 0.23f * flash, 0.065f * flash, Cream);
            DrawDebris(4, heading, 72f, progress, 0.085f, 0.43f, Copper, Gold);
        }
    }
}
