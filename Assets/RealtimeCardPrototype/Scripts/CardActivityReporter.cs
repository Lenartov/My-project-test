using UnityEngine;

namespace RealtimeCardPrototype
{
    public sealed class CardActivityReporter : MonoBehaviour
    {
        private IHandCard card;
        private bool wasInteracting;
        private bool hasLeftHand;

        private void Awake()
        {
            card = GetComponent<IHandCard>();
        }

        private void OnEnable()
        {
            card.LeftHand += HandleLeftHand;
            card.Played += HandlePlayed;
        }

        private void Update()
        {
            bool isInteracting = card.IsDragging;
            if (isInteracting == wasInteracting)
            {
                return;
            }

            wasInteracting = isInteracting;
            PlayerActivityTimeController.ReportCardInteraction(this, isInteracting);
            if (!isInteracting && hasLeftHand)
            {
                PlayerActivityTimeController.ReportCardUse();
            }
        }

        private void OnDisable()
        {
            card.LeftHand -= HandleLeftHand;
            card.Played -= HandlePlayed;
            if (wasInteracting)
            {
                wasInteracting = false;
                PlayerActivityTimeController.ReportCardInteraction(this, false);
            }
        }

        private void HandleLeftHand(IHandCard playedCard)
        {
            hasLeftHand = true;
            PlayerActivityTimeController.ReportCardUse();
        }

        private void HandlePlayed(IHandCard playedCard)
        {
            if (!hasLeftHand)
            {
                PlayerActivityTimeController.ReportCardUse();
            }
        }
    }
}
