using UnityEngine;

namespace RealtimeCardPrototype
{
    internal sealed class EnemyHealthState
    {
        public EnemyHealthState(float maxHealth)
        {
            Configure(maxHealth);
        }

        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }
        public bool IsDepleted => CurrentHealth <= 0f;

        public void Configure(float maxHealth)
        {
            MaxHealth = Mathf.Max(1f, maxHealth);
            CurrentHealth = MaxHealth;
        }

        public void ApplyDamage(int damage)
        {
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        }

        public float Restore(float amount)
        {
            float restoredAmount = Mathf.Min(Mathf.Max(0f, amount), MaxHealth - CurrentHealth);
            CurrentHealth += restoredAmount;
            return restoredAmount;
        }
    }

    internal sealed class EnemyBalanceState
    {
        private float damageFloor;
        private float breakTriggerDamage;
        private float gainAtStrongHit;
        private float gainExponent;

        public EnemyBalanceState(float maxBalance, float damageFloor, float breakTriggerDamage, float gainAtStrongHit, float gainExponent)
        {
            Configure(maxBalance, damageFloor, breakTriggerDamage, gainAtStrongHit, gainExponent);
        }

        public float MaxBalance { get; private set; }
        public float CurrentBalance { get; private set; }

        public void Configure(float maxBalance, float newDamageFloor, float newBreakTriggerDamage, float newGainAtStrongHit, float newGainExponent)
        {
            MaxBalance = Mathf.Max(1f, maxBalance);
            damageFloor = newDamageFloor;
            breakTriggerDamage = newBreakTriggerDamage;
            gainAtStrongHit = newGainAtStrongHit;
            gainExponent = newGainExponent;
            CurrentBalance = 0f;
        }

        public bool AddDamage(float damage, bool canTriggerBreak)
        {
            float gain = CalculateGain(damage);
            if (gain <= 0f)
            {
                return false;
            }

            bool reachedLimit = CurrentBalance + gain >= MaxBalance;
            CurrentBalance = Mathf.Min(MaxBalance, CurrentBalance + gain);
            return canTriggerBreak && damage >= breakTriggerDamage && reachedLimit;
        }

        public void Reset()
        {
            CurrentBalance = 0f;
        }

        private float CalculateGain(float damage)
        {
            if (damage <= damageFloor)
            {
                return 0f;
            }

            float strongWindow = Mathf.Max(0.01f, breakTriggerDamage - damageFloor);
            float normalizedPower = Mathf.Max(0f, (damage - damageFloor) / strongWindow);
            return Mathf.Pow(normalizedPower, gainExponent) * gainAtStrongHit;
        }
    }

    internal sealed class EnemySideState
    {
        private const int SideCount = 4;

        private readonly EnemyEdgeRole[] sideRoles = new EnemyEdgeRole[SideCount];
        private int exposedSideIndex = -1;
        private float exposedSideRemaining;

        public EnemyEdgeRole[] Roles => sideRoles;
        public int ExposedSideIndex => exposedSideIndex;
        public float ExposedSideRemaining => exposedSideRemaining;

        public void Generate(float blockingSideChance)
        {
            for (int i = 0; i < SideCount; i++)
            {
                sideRoles[i] = EnemyEdgeRole.Normal;
            }

            if (Random.value < blockingSideChance)
            {
                sideRoles[Random.Range(0, SideCount)] = EnemyEdgeRole.Blocking;
            }
        }

        public EnemyEdgeRole GetEffectiveRole(EnemyCombatSide side)
        {
            int sideIndex = (int)side;
            return sideIndex == exposedSideIndex && exposedSideRemaining > 0f
                ? EnemyEdgeRole.Exposed
                : sideRoles[sideIndex];
        }

        public void ActivateExposedSide(float duration)
        {
            int selected = PickSideWithRole(EnemyEdgeRole.Normal);
            if (selected < 0)
            {
                selected = PickSideWithRole(EnemyEdgeRole.Blocking);
            }

            exposedSideIndex = selected;
            exposedSideRemaining = selected >= 0 ? Mathf.Max(0f, duration) : 0f;
        }

        public void Tick(float deltaTime)
        {
            if (exposedSideIndex < 0)
            {
                return;
            }

            exposedSideRemaining = Mathf.Max(0f, exposedSideRemaining - deltaTime);
            if (exposedSideRemaining <= 0f)
            {
                exposedSideIndex = -1;
            }
        }

        private int PickSideWithRole(EnemyEdgeRole role)
        {
            int matches = 0;
            for (int i = 0; i < SideCount; i++)
            {
                if (sideRoles[i] == role)
                {
                    matches++;
                }
            }

            if (matches == 0)
            {
                return -1;
            }

            int picked = Random.Range(0, matches);
            for (int i = 0; i < SideCount; i++)
            {
                if (sideRoles[i] == role && picked-- == 0)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
