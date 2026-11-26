using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    internal sealed class PlayerManaState
    {
        private readonly int baseMaxMana;
        private readonly float baseRegenInterval;
        private float regenTimer;
        private float regenIntervalReduction;
        private int maxManaBonus;

        public PlayerManaState(int maxMana, int startingMana, float regenInterval)
        {
            baseMaxMana = Mathf.Max(0, maxMana);
            baseRegenInterval = regenInterval;
            CurrentMana = Mathf.Clamp(startingMana, 0, baseMaxMana);
        }

        public int CurrentMana { get; private set; }
        public int EffectiveMaxMana => Mathf.Max(0, baseMaxMana + maxManaBonus);
        public float EffectiveRegenInterval => Mathf.Max(0.2f, baseRegenInterval - regenIntervalReduction);

        public void Tick(float seconds)
        {
            if (CurrentMana >= EffectiveMaxMana)
            {
                regenTimer = 0f;
                return;
            }

            regenTimer += seconds;
            while (regenTimer >= EffectiveRegenInterval && CurrentMana < EffectiveMaxMana)
            {
                regenTimer -= EffectiveRegenInterval;
                CurrentMana = Mathf.Min(EffectiveMaxMana, CurrentMana + 1);
            }

            if (CurrentMana >= EffectiveMaxMana)
            {
                regenTimer = 0f;
            }
        }

        public bool TrySpend(int amount)
        {
            if (CurrentMana < amount)
            {
                return false;
            }

            CurrentMana -= amount;
            return true;
        }

        public void Restore(int amount)
        {
            CurrentMana = Mathf.Min(EffectiveMaxMana, CurrentMana + Mathf.Max(0, amount));
        }

        public void AddRegenIntervalReduction(float seconds)
        {
            regenIntervalReduction = Mathf.Max(0f, regenIntervalReduction + seconds);
        }

        public void RemoveRegenIntervalReduction(float seconds)
        {
            regenIntervalReduction = Mathf.Max(0f, regenIntervalReduction - seconds);
        }

        public void AddMaxManaBonus(int amount)
        {
            maxManaBonus = Mathf.Max(0, maxManaBonus + amount);
        }

        public void RemoveMaxManaBonus(int amount)
        {
            maxManaBonus = Mathf.Max(0, maxManaBonus - amount);
            CurrentMana = Mathf.Min(CurrentMana, EffectiveMaxMana);
        }
    }

    internal sealed class PlayerHealthState
    {
        public PlayerHealthState(float maxHealth)
        {
            MaxHealth = Mathf.Max(1f, maxHealth);
            CurrentHealth = MaxHealth;
        }

        public float MaxHealth { get; }
        public float CurrentHealth { get; private set; }

        public bool ApplyDamage(int damage)
        {
            CurrentHealth = Mathf.Max(0f, CurrentHealth - Mathf.Max(0f, damage));
            if (CurrentHealth > 0f)
            {
                return false;
            }

            CurrentHealth = MaxHealth;
            return true;
        }
    }

    internal sealed class PlayerHandLayout
    {
        private readonly Vector3 handCenter;
        private readonly float cardSpacing;

        public PlayerHandLayout(Vector3 handCenter, float cardSpacing)
        {
            this.handCenter = handCenter;
            this.cardSpacing = cardSpacing;
        }

        public Vector3 TargetPoint => handCenter + new Vector3(0f, 0.65f, 0.2f);

        public void Apply(IList<IHandCard> cards, bool snap)
        {
            float middle = (cards.Count - 1) * 0.5f;
            for (int i = 0; i < cards.Count; i++)
            {
                IHandCard card = cards[i];
                if (card == null || card.IsDragging)
                {
                    continue;
                }

                float offset = i - middle;
                Vector3 slot = handCenter + new Vector3(offset * cardSpacing, 0f, -Mathf.Abs(offset) * 0.06f);
                Quaternion rotation = Quaternion.Euler(0f, offset * -4f, 0f);
                card.SetHandSlot(slot, rotation);

                if (snap)
                {
                    card.Transform.SetPositionAndRotation(slot, rotation);
                }
            }
        }
    }
}
