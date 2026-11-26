using UnityEngine;

namespace RealtimeCardPrototype
{
    internal sealed partial class CardCombatEffects
    {
        internal static readonly Color PierceMint = new Color(0.12f, 0.96f, 0.72f);

        private static readonly Color PierceEdge = new Color(0.055f, 0.35f, 0.31f);
        private Vector2 cardHalfSize;
        private Color powerTint;

        internal static void PiercePass(Vector3 contact, Vector3 velocity)
        {
            CardCombatEffects pass = Create(contact + Vector3.up * 0.23f, Effect.PiercePass, 0.31f);
            pass.heading = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
            pass.Render();
            CardCombatAudio.PlayPierce();
        }

        internal static void ArmPierce(CardView card, float seconds)
        {
            CardCombatEffects charge = CreateCardEffect(card, Effect.PierceCharge, seconds, card.OverlayHeight + 0.045f);
            charge.transform.SetParent(card.transform, true);
            charge.Render();
        }

        internal static void PlacePower(CardView card)
        {
            CardCombatEffects placement = CreateCardEffect(card, Effect.PowerPlacement, 0.43f, -0.10f);
            placement.Render();

            CardCombatEffects ready = CreateCardEffect(card, Effect.PowerReady, float.PositiveInfinity, 0.06f);
            ready.transform.SetParent(card.transform, true);
            ready.Render();
            CardCombatAudio.PlayPowerPlacement();
        }

        internal static void ConfirmPower(CardView card, Color stackColor)
        {
            CardCombatEffects charge = CreateCardEffect(card, Effect.PowerCharge, 0.44f, card.OverlayHeight + 0.10f);
            charge.powerTint = stackColor;
            charge.Render();
            CardCombatAudio.PlayPowerCharge();
        }

        private static CardCombatEffects CreateCardEffect(CardView card, Effect kind, float seconds, float height)
        {
            CardCombatEffects instance = Create(card.transform.position + Vector3.up * height, kind, seconds);
            instance.transform.rotation = card.transform.rotation;
            Vector3 size = card.Collider.size;
            instance.cardHalfSize = new Vector2(size.x, size.z) * 0.5f;
            return instance;
        }

        private void DrawPiercePass(float progress)
        {
            Vector3 forward = Direction(heading);
            Vector3 right = Direction(heading + 90f);
            float travel = EaseOut(progress);
            float cut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.14f, 0.74f, progress));

            Vector3 point = forward * (0.50f + travel * 0.68f);
            Vector3 tail = forward * (-0.94f + travel * 0.77f);
            Vector3 crest = Vector3.Lerp(tail, point, 0.68f);
            Triangle(tail, crest - right * (0.23f * cut), point, PierceEdge);
            Triangle(tail, point, crest + right * (0.23f * cut), PierceMint);
            Vector3 highlight = Vector3.up * 0.025f;
            Triangle(tail + highlight, point + highlight, crest + right * (0.082f * cut) + highlight, Ice);

            float chipSize = Mathf.Sin(progress * Mathf.PI) * 0.14f;
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Vector3 chip = right * (side * (0.20f + travel * 0.52f))
                    + forward * (travel * (0.22f + i * 0.18f) - 0.26f)
                    + Vector3.up * (0.06f + progress * 0.24f);
                Triangle(chip - forward * chipSize, chip + right * (side * chipSize * 0.70f),
                    chip + forward * (chipSize * 1.6f), i < 2 ? Ice : PierceMint);
            }
        }

        private void DrawPierceCharge(float progress)
        {
            float taper = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.70f, 1f, progress));
            float offset = Mathf.Lerp(-0.56f, 0.62f, EaseOut(progress)) * cardHalfSize.y;
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 center = new Vector3(i * (cardHalfSize.x + 0.11f), 0f, offset * i);
                Vector3 back = center - Vector3.forward * (cardHalfSize.y * 0.52f * taper);
                Vector3 front = center + Vector3.forward * (cardHalfSize.y * 0.52f * taper);
                Triangle(back, front, center + Vector3.right * (i * 0.12f * taper), PierceMint);
                Triangle(back + Vector3.up * 0.02f, front + Vector3.up * 0.02f,
                    center + new Vector3(i * 0.047f * taper, 0.02f, 0f), Ice);
            }
        }

        private void DrawPowerPlacement(float progress)
        {
            float spread = EaseOut(progress) * 0.49f;
            float size = 1f - Mathf.SmoothStep(0f, 1f, progress);
            for (int i = 0; i < 4; i++)
            {
                Vector3 direction = Direction(45f + i * 90f);
                Vector3 corner = new Vector3(Mathf.Sign(direction.x) * cardHalfSize.x, 0f, Mathf.Sign(direction.z) * cardHalfSize.y);
                Vector3 center = corner + direction * (0.11f + spread);
                PowerChevron(center, direction, 0.21f * size, Amber, Gold);
                PowerChevron(center + direction * 0.19f, direction, 0.13f * size, Gold, Cream);
            }
        }

        private void DrawPowerReady()
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(age * 3.8f);
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 outward = Vector3.right * i;
                Vector3 center = outward * (cardHalfSize.x + 0.19f);
                PowerChevron(center, outward, 0.16f * pulse, Amber, Gold);
                PowerChevron(center + outward * 0.16f, outward, 0.11f * pulse, Gold, Cream);
            }
        }

        private void DrawPowerCharge(float progress)
        {
            float rise = EaseOut(progress) * 0.54f;
            float taper = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 1f, progress));
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 center = new Vector3(i * (cardHalfSize.x + 0.14f), rise, 0.04f);
                PowerChevron(center, Vector3.forward, 0.26f * taper, powerTint, Cream);
                PowerChevron(center - Vector3.forward * 0.20f, Vector3.forward, 0.18f * taper, Amber, powerTint);
            }

            float flash = 1f - Mathf.SmoothStep(0f, 1f, progress * 2.3f);
            Burst(Vector3.up * (0.10f + rise), Vector3.right, Vector3.forward, 4,
                0.36f * flash, 0.10f * flash, powerTint);
            Burst(Vector3.up * (0.13f + rise), Vector3.right, Vector3.forward, 4,
                0.23f * flash, 0.062f * flash, Cream);
        }

        private void PowerChevron(Vector3 center, Vector3 forward, float size, Color shadow, Color light)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 tip = center + forward * size;
            Vector3 inset = center + forward * (size * 0.36f);
            Vector3 back = center - forward * (size * 0.50f);
            Triangle(tip, back - right * size, inset, shadow);
            Triangle(tip, inset, back + right * size, light);
        }
    }
}
