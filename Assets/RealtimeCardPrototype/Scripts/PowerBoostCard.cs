using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    public sealed class PowerBoostCard : FieldStructureCard
    {
        private readonly HashSet<IPowerBoostTarget> cardsInside = new HashSet<IPowerBoostTarget>();

        protected override int DefaultManaCost => 3;
        protected override Color HealthBarFillColor => new Color(1f, 0.72f, 0.08f);

        protected override void OnMouseUp()
        {
            bool wasDragging = IsDragging;
            base.OnMouseUp();
            if (wasDragging)
            {
                CardCombatEffects.PlacePower(GetComponent<CardView>());
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsActiveOnField)
            {
                return;
            }

            IPowerBoostTarget card = FindPowerBoostTarget(other);
            if (card == null || !card.IsDragging || cardsInside.Contains(card))
            {
                return;
            }

            cardsInside.Add(card);
            card.AddPowerStack(GetInstanceID());
        }

        private void OnTriggerExit(Collider other)
        {
            IPowerBoostTarget card = FindPowerBoostTarget(other);
            if (card != null)
            {
                cardsInside.Remove(card);
            }
        }

        private IPowerBoostTarget FindPowerBoostTarget(Collider other)
        {
            MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPowerBoostTarget card)
                {
                    return card;
                }
            }

            return null;
        }
    }
}
