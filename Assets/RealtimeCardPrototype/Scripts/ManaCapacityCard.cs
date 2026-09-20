using System;
using UnityEngine;

namespace RealtimeCardPrototype
{
    public sealed class ManaCapacityCard : FieldStructureCard
    {
        [SerializeField] private int maxManaBonus = 1;

        public Action<int> MaxManaBonusStarted;
        public Action<int> MaxManaBonusEnded;

        private bool maxManaBuffApplied;

        protected override int DefaultManaCost => 3;
        protected override Color HealthBarFillColor => new Color(0.45f, 0.55f, 1f);

        protected override void OnStructureActivated()
        {
            if (maxManaBuffApplied)
            {
                return;
            }

            maxManaBuffApplied = true;
            MaxManaBonusStarted?.Invoke(maxManaBonus);
        }

        protected override void OnStructureDeactivated()
        {
            if (!maxManaBuffApplied)
            {
                return;
            }

            maxManaBuffApplied = false;
            MaxManaBonusEnded?.Invoke(maxManaBonus);
        }
    }
}
