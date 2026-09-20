using UnityEngine;

namespace RealtimeCardPrototype
{
    [System.Serializable]
    public struct CardHitProfile
    {
        public float damage;
        public float postureDamage;
        public float impulse;
        public float minSpeedForHit;
        public float maxSpeedForDamage;
        public float speedDamageScale;

        public static CardHitProfile BasicAttack()
        {
            return new CardHitProfile
            {
                damage = 2f,
                postureDamage = 3f,
                impulse = 3.5f,
                minSpeedForHit = 1.25f,
                maxSpeedForDamage = 10f,
                speedDamageScale = 0.16f
            };
        }

        public float GetSpeedMultiplier(float speed)
        {
            float cappedSpeed = maxSpeedForDamage > minSpeedForHit ? Mathf.Min(speed, maxSpeedForDamage) : speed;
            return 1f + Mathf.Max(0f, cappedSpeed - minSpeedForHit) * speedDamageScale;
        }
    }
}
