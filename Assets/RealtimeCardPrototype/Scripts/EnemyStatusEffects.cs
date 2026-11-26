using System;
using UnityEngine;

namespace RealtimeCardPrototype
{
    internal sealed class EnemyStatusEffects
    {
        private readonly float poisonDuration;
        private readonly float poisonStackStepSeconds;
        private readonly float poisonDecaySpeedMultiplier;
        private readonly float poisonLevelOneDamage;
        private readonly float poisonLevelTwoDamage;
        private readonly float poisonLevelThreeDamage;
        private readonly float poisonTickInterval;
        private readonly float poisonPartialTickThreshold;
        private readonly float shockMarkDuration;
        private readonly float shockChainDamageFraction;
        private readonly float shockChainPopupInterval;

        private int poisonLevel;
        private float poisonRemaining;
        private float poisonStackProgress;
        private float poisonDamageMultiplier = 1f;
        private float lastPoisonHoverTime = -999f;
        private float poisonTickTimer;
        private float shockRemaining;
        private float shockChainPopupTimer;
        private int shockChainPopupDamage;

        public EnemyStatusEffects(
            float poisonDuration,
            float poisonStackStepSeconds,
            float poisonDecaySpeedMultiplier,
            float poisonLevelOneDamage,
            float poisonLevelTwoDamage,
            float poisonLevelThreeDamage,
            float poisonTickInterval,
            float poisonPartialTickThreshold,
            float shockMarkDuration,
            float shockChainDamageFraction,
            float shockChainPopupInterval)
        {
            this.poisonDuration = poisonDuration;
            this.poisonStackStepSeconds = poisonStackStepSeconds;
            this.poisonDecaySpeedMultiplier = poisonDecaySpeedMultiplier;
            this.poisonLevelOneDamage = poisonLevelOneDamage;
            this.poisonLevelTwoDamage = poisonLevelTwoDamage;
            this.poisonLevelThreeDamage = poisonLevelThreeDamage;
            this.poisonTickInterval = poisonTickInterval;
            this.poisonPartialTickThreshold = poisonPartialTickThreshold;
            this.shockMarkDuration = shockMarkDuration;
            this.shockChainDamageFraction = shockChainDamageFraction;
            this.shockChainPopupInterval = shockChainPopupInterval;
        }

        public int PoisonLevel => poisonLevel;
        public float PoisonStackProgress => poisonStackProgress;
        public float PoisonStackStepSeconds => poisonStackStepSeconds;
        public float ShockRemaining => shockRemaining;
        public float ShockDuration => shockMarkDuration;
        public bool HasShockMark => shockRemaining > 0f;

        public void ApplyPoisonHover(float deltaTime, float damageMultiplier, float currentTime)
        {
            poisonStackProgress = Mathf.Min(poisonStackProgress + deltaTime, poisonStackStepSeconds * 2f);
            poisonLevel = Mathf.Max(poisonLevel, GetPoisonLevelFromProgress());
            poisonDamageMultiplier = Mathf.Max(poisonDamageMultiplier, damageMultiplier);
            poisonRemaining = poisonDuration;
            lastPoisonHoverTime = currentTime;
        }

        public void ApplyShockMark()
        {
            shockRemaining = shockMarkDuration;
        }

        public float GetShockChainDamage(int sourceDamage)
        {
            return sourceDamage * shockChainDamageFraction;
        }

        public void AccumulateShockPopup(int damage)
        {
            shockChainPopupDamage += damage;
        }

        public void Tick(float deltaTime, float currentTime, Action<float> applyPoisonDamage, Action<int> showShockPopup)
        {
            TickPoison(deltaTime, currentTime, applyPoisonDamage);
            TickShockMark(deltaTime);
            TickShockPopup(deltaTime, showShockPopup);
        }

        private void TickPoison(float deltaTime, float currentTime, Action<float> applyPoisonDamage)
        {
            if (poisonStackProgress <= 0f)
            {
                ClearPoison();
                return;
            }

            poisonLevel = GetPoisonLevelFromProgress();
            poisonTickTimer += deltaTime;
            while (poisonTickTimer >= poisonTickInterval)
            {
                applyPoisonDamage(GetPoisonDamage(poisonLevel));
                poisonTickTimer -= poisonTickInterval;
            }

            if (currentTime - lastPoisonHoverTime <= deltaTime + 0.01f)
            {
                poisonRemaining = poisonDuration;
            }
            else if (poisonRemaining > 0f)
            {
                poisonRemaining -= deltaTime;
            }
            else
            {
                int previousLevel = poisonLevel;
                poisonStackProgress = Mathf.Max(0f, poisonStackProgress - deltaTime * poisonDecaySpeedMultiplier);
                int nextLevel = poisonStackProgress > 0f ? GetPoisonLevelFromProgress() : 0;
                if (nextLevel < previousLevel)
                {
                    if (poisonTickTimer >= poisonPartialTickThreshold)
                    {
                        applyPoisonDamage(GetPoisonDamage(previousLevel));
                    }

                    poisonTickTimer = 0f;
                }

                poisonLevel = nextLevel;
            }

            if (poisonLevel <= 0)
            {
                ClearPoison();
            }
        }

        private void TickShockMark(float deltaTime)
        {
            shockRemaining = Mathf.Max(0f, shockRemaining - deltaTime);
        }

        private void TickShockPopup(float deltaTime, Action<int> showShockPopup)
        {
            if (shockChainPopupDamage <= 0)
            {
                shockChainPopupTimer = 0f;
                return;
            }

            shockChainPopupTimer += deltaTime;
            if (shockChainPopupTimer >= shockChainPopupInterval)
            {
                showShockPopup(shockChainPopupDamage);
                shockChainPopupTimer = 0f;
                shockChainPopupDamage = 0;
            }
        }

        private float GetPoisonDamage(int level)
        {
            float baseDamage;
            switch (Mathf.Clamp(level, 1, 3))
            {
                case 1:
                    baseDamage = poisonLevelOneDamage;
                    break;
                case 2:
                    baseDamage = poisonLevelTwoDamage;
                    break;
                default:
                    baseDamage = poisonLevelThreeDamage;
                    break;
            }

            return baseDamage * poisonDamageMultiplier;
        }

        private int GetPoisonLevelFromProgress()
        {
            if (poisonStackProgress >= poisonStackStepSeconds * 2f)
            {
                return 3;
            }

            return poisonStackProgress >= poisonStackStepSeconds ? 2 : 1;
        }

        private void ClearPoison()
        {
            poisonLevel = 0;
            poisonRemaining = 0f;
            poisonStackProgress = 0f;
            poisonDamageMultiplier = 1f;
            poisonTickTimer = 0f;
            lastPoisonHoverTime = -999f;
        }
    }
}
