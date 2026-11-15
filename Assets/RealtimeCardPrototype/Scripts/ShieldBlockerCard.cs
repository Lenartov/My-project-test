using UnityEngine;

namespace RealtimeCardPrototype
{
    public sealed class ShieldBlockerCard : FieldStructureCard
    {
        private EnemyAttackSequence sequence;
        private CardCombatEffects ward;
        private bool isReturnPending;
        private float returnDeadline;

        protected override float InitialMaxHealth => 3f;
        protected override Color HealthBarFillColor => new Color(0.58f, 0.82f, 1f);
        protected override Color HealthBarBackgroundColor => new Color(0.025f, 0.04f, 0.07f);
        protected override bool ReservesHandSlotUntilCommitted => true;
        public override bool CanBeDirectTarget => false;
        public override bool CanInterceptAttacks => false;

        internal bool TryAssignToSequence(EnemyAttackSequence newSequence)
        {
            if (newSequence == null || sequence != null || !IsTargetable)
            {
                return false;
            }

            sequence = newSequence;
            return true;
        }

        public bool TryAppendBlock(ShieldBlockerCard nextBlock)
        {
            return sequence != null && sequence.TryAppendBlock(this, nextBlock);
        }

        internal float AbsorbEnemyAttack(float damage, Vector3 source)
        {
            sequence = null;
            Vector3 point = TargetPoint;
            ward.ReleaseWard(AttackProjectile.TravelTime);
            CardCombatEffects.BlockImpact(point, source - point, AttackProjectile.TravelTime);
            return AbsorbStructureDamage(damage, true);
        }

        internal float GetPreviewOverflowDamage(float damage)
        {
            return Mathf.Max(0f, damage - CurrentHealth);
        }

        internal void ConsumeUnusedSequenceBlock(EnemyAttackSequence owner)
        {
            if (!ReferenceEquals(sequence, owner))
            {
                return;
            }

            sequence = null;
            FinishPlayed();
        }

        protected override void OnMouseUp()
        {
            base.OnMouseUp();
            if (sequence != null || HasLeftHand)
            {
                return;
            }

            isReturnPending = true;
            returnDeadline = Time.unscaledTime + 0.08f;
        }

        private void LateUpdate()
        {
            if (!isReturnPending)
            {
                return;
            }

            if (sequence != null)
            {
                isReturnPending = false;
                return;
            }

            if (Time.unscaledTime < returnDeadline)
            {
                return;
            }

            isReturnPending = false;
            ReturnUncommittedCardToHand();
        }

        private void OnTriggerEnter(Collider other)
        {
            ShieldBlockerCard previousBlock = other.GetComponentInParent<ShieldBlockerCard>();
            if (previousBlock != null && !ReferenceEquals(previousBlock, this) && previousBlock.TryAppendBlock(this))
            {
                CommitToAttackSequence();
                return;
            }

            EnemyDummyCard enemy = other.GetComponentInParent<EnemyDummyCard>();
            if (enemy != null && enemy.TryAddBlockToPendingAttack(this))
            {
                CommitToAttackSequence();
            }
        }

        private void CommitToAttackSequence()
        {
            isReturnPending = false;
            CardView view = GetComponent<CardView>();
            ward = CardCombatEffects.RaiseWard(transform, view.OverlayHeight);
            CommitHandCard();
        }
    }
}
