using System;
using UnityEngine;

namespace RealtimeCardPrototype
{
    public sealed class ManaGeneratorCard : FieldStructureCard
    {
        [SerializeField] private float manaIntervalReduction = 0.25f;

        public Action<float> ManaRegenReductionStarted;
        public Action<float> ManaRegenReductionEnded;

        private CardView cardView;
        private bool manaBuffApplied;

        protected override int DefaultManaCost => 2;
        protected override Color HealthBarFillColor => new Color(0.25f, 0.92f, 1f);

        protected override void Awake()
        {
            base.Awake();
            cardView = GetComponent<CardView>();
        }

        protected override void OnMouseUp()
        {
            bool wasDragging = IsDragging;
            base.OnMouseUp();
            if (wasDragging)
            {
                CardCombatEffects.PlaceFocus(transform, cardView.Collider.size.x * 0.5f);
            }
        }

        protected override void OnStructureActivated()
        {
            if (manaBuffApplied)
            {
                return;
            }

            manaBuffApplied = true;
            ManaRegenReductionStarted?.Invoke(manaIntervalReduction);
        }

        protected override void OnStructureDeactivated()
        {
            if (!manaBuffApplied)
            {
                return;
            }

            manaBuffApplied = false;
            ManaRegenReductionEnded?.Invoke(manaIntervalReduction);
        }
    }
}
