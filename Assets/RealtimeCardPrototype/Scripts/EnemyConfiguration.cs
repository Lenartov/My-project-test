using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    [Serializable]
    public sealed class EnemyConfiguration
    {
        public EnemyVitalsConfiguration vitals = new EnemyVitalsConfiguration();
        public EnemyMovementConfiguration movement = new EnemyMovementConfiguration();
        public EnemyDefenseConfiguration defense = new EnemyDefenseConfiguration();
        public EnemyBalanceConfiguration balance = new EnemyBalanceConfiguration();
        public EnemyStatusEffectsConfiguration statusEffects = new EnemyStatusEffectsConfiguration();
        public EnemyActionConfiguration actions = new EnemyActionConfiguration();
    }

    [Serializable]
    public sealed class EnemyVitalsConfiguration
    {
        public float maxHealth = 84f;
        public float maxBalance = 8f;
    }

    [Serializable]
    public sealed class EnemyMovementConfiguration
    {
        public float returnDamping = 5.5f;
        public float maxKnockbackOffset = 1f;
        public float limitRelaxDistanceFraction = 0.75f;
        public float limitReturnSpring = 34f;
        public float maxPlanarSpeed = 31.5f;
        public float knockbackMinDamage = 2f;
        public float knockbackMaxDamage = 17f;
        public float maxKnockbackImpulse = 52.5f;
    }

    [Serializable]
    public sealed class EnemyDefenseConfiguration
    {
        public float criticalSideMultiplier = 2f;
        public float blockingSideMultiplier = 0.5f;
        [Range(0f, 1f)] public float blockingSideChance = 0.2f;
    }

    [Serializable]
    public sealed class EnemyBalanceConfiguration
    {
        public float damageFloor = 0f;
        public float breakTriggerDamage = 1.5f;
        public float gainAtStrongHit = 1.3f;
        public float gainExponent = 1.25f;
        public float recoverySeconds = 10f;
        public float exposedSideSeconds = 10f;
    }

    [Serializable]
    public sealed class EnemyStatusEffectsConfiguration
    {
        public float poisonDuration = 2f;
        public float poisonStackStepSeconds = 0.25f;
        public float poisonDecaySpeedMultiplier = 0.2f;
        public float poisonLevelOneDamage = 1f;
        public float poisonLevelTwoDamage = 2f;
        public float poisonLevelThreeDamage = 3f;
        public float poisonTickInterval = 1.25f;
        public float poisonPartialTickThreshold = 0.1f;
        public float shockMarkDuration = 10f;
        public float shockChainRadius = 2.85f;
        [Range(0f, 1f)] public float shockChainDamageFraction = 0.25f;
        public float shockChainPopupInterval = 0.45f;
    }

    [Serializable]
    public sealed class EnemyActionConfiguration
    {
        public float initialSleepSeconds = 10f;
        public float attackChargeSeconds = 5f;
        public float waitSeconds = 10f;
        public float attackDamage = 2f;
        public List<EnemyActionStepConfiguration> cycle = new List<EnemyActionStepConfiguration>
        {
            new EnemyActionStepConfiguration(EnemyActionKind.Attack, EnemyAttackTargetMode.Player, 1f, 1f),
            new EnemyActionStepConfiguration(EnemyActionKind.Attack, EnemyAttackTargetMode.Structure, 1f, 1f),
            new EnemyActionStepConfiguration(EnemyActionKind.Heal, EnemyAttackTargetMode.Player, 1f, 2f)
        };
    }

    [Serializable]
    public sealed class EnemyActionStepConfiguration
    {
        public EnemyActionKind kind;
        public EnemyAttackTargetMode attackTargetMode;
        public float durationMultiplier;
        public float amountMultiplier;
        [Min(1)] public int hitCount = 1;
        [Min(0.01f)] public float hitInterval = 0.3f;

        public EnemyActionStepConfiguration()
            : this(EnemyActionKind.Attack, EnemyAttackTargetMode.Player, 1f, 1f)
        {
        }

        public EnemyActionStepConfiguration(
            EnemyActionKind kind,
            EnemyAttackTargetMode attackTargetMode,
            float durationMultiplier,
            float amountMultiplier,
            int hitCount = 1,
            float hitInterval = 0.3f)
        {
            this.kind = kind;
            this.attackTargetMode = attackTargetMode;
            this.durationMultiplier = durationMultiplier;
            this.amountMultiplier = amountMultiplier;
            this.hitCount = Mathf.Max(1, hitCount);
            this.hitInterval = Mathf.Max(0.01f, hitInterval);
        }
    }
}
