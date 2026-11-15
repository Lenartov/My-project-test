using UnityEngine;

namespace RealtimeCardPrototype
{
    public enum EnemyDamageKind
    {
        Direct,
        Splash,
        Poison,
        ShockChain,
        Graviton,
        TurretShot
    }

    public readonly struct EnemyDamageEvent
    {
        public EnemyDamageEvent(
            EnemyDamageKind kind,
            float damage,
            float postureDamage,
            bool canTriggerBalanceBreak,
            bool applyKnockback,
            Vector3 knockbackDirection,
            bool triggerShockChain,
            bool showPopup,
            Vector3 popupOffset,
            Color popupColor,
            string statusLabel)
        {
            Kind = kind;
            Damage = Mathf.Max(0f, damage);
            PostureDamage = Mathf.Max(0f, postureDamage);
            CanTriggerBalanceBreak = canTriggerBalanceBreak;
            ApplyKnockback = applyKnockback;
            KnockbackDirection = knockbackDirection;
            TriggerShockChain = triggerShockChain;
            ShowPopup = showPopup;
            PopupOffset = popupOffset;
            PopupColor = popupColor;
            StatusLabel = statusLabel;
        }

        public EnemyDamageKind Kind { get; }
        public float Damage { get; }
        public float PostureDamage { get; }
        public bool CanTriggerBalanceBreak { get; }
        public bool ApplyKnockback { get; }
        public Vector3 KnockbackDirection { get; }
        public bool TriggerShockChain { get; }
        public bool ShowPopup { get; }
        public Vector3 PopupOffset { get; }
        public Color PopupColor { get; }
        public string StatusLabel { get; }
    }
}
