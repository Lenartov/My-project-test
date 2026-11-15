using UnityEngine;

namespace RealtimeCardPrototype
{
    public sealed class CardActivityReporter : MonoBehaviour
    {
        private IHandCard card;
        private bool wasInteracting;

        private void Awake()
        {
            card = GetComponent<IHandCard>();
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
        }

        private void OnDisable()
        {
            if (wasInteracting)
            {
                wasInteracting = false;
                PlayerActivityTimeController.ReportCardInteraction(this, false);
            }
        }
    }
}
