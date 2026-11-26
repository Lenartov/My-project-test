using UnityEngine;

namespace RealtimeCardPrototype
{
    internal static class CombatDamage
    {
        internal static int ToHitPoints(float amount)
        {
            return amount > 0f ? Mathf.Max(1, Mathf.FloorToInt(amount + 0.5f)) : 0;
        }
    }
}
