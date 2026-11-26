using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    public sealed class TurretCard : FieldStructureCard, IPowerBoostTarget
    {
        internal const int Cost = 2;
        private const int BaseShotDamage = 1;
        private const int MaxPowerStacks = 3;
        private const float PowerPerStack = 0.5f;
        private static readonly Color ShotColor = new Color(0.18f, 1f, 0.74f);
        private static readonly Collider[] ShotCandidates = new Collider[32];

        [SerializeField, Min(0.01f)] private float shotInterval = 2.5f;
        [SerializeField, Min(0.01f)] private float attackRange = 6f;
        [SerializeField, Min(1f)] private float maxHealth = 3f;

        private readonly HashSet<int> powerSources = new HashSet<int>();
        private CardView cardView;
        private float shotTimer;

        public override bool CanBeDirectTarget => !IsDragging;
        protected override int InitialMaxHealth => CombatDamage.ToHitPoints(Mathf.Max(1f, maxHealth));
        protected override int DefaultManaCost => Cost;
        protected override Color HealthBarFillColor => ShotColor;

        protected override void Awake()
        {
            base.Awake();
            cardView = GetComponent<CardView>();
        }

        protected override void OnStructureActivated()
        {
            SetPlayable(true);
        }

        protected override void OnMouseUp()
        {
            bool wasDragging = IsDragging;
            base.OnMouseUp();
            if (wasDragging)
            {
                CardCombatEffects.PlaceTurret(transform.position, cardView.Collider.size.x * 0.5f);
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!IsActiveOnField || IsDragging)
            {
                return;
            }

            shotTimer += Time.deltaTime;
            if (shotTimer < shotInterval)
            {
                return;
            }

            shotTimer -= shotInterval;
            EnemyDummyCard enemy = FindNearestEnemy();
            if (enemy != null)
            {
                float damage = BaseShotDamage * (1f + powerSources.Count * PowerPerStack);
                CardCombatEffects.FireTurret(transform.position + Vector3.up * cardView.OverlayHeight, enemy.transform);
                enemy.ReceiveTurretShot(damage, ShotColor);
            }
        }

        public void AddPowerStack(int sourceId)
        {
            if (powerSources.Count < MaxPowerStacks && powerSources.Add(sourceId))
            {
                cardView.SetPowerOutline(powerSources.Count);
            }
        }

        private EnemyDummyCard FindNearestEnemy()
        {
            Vector3 origin = transform.position;
            int hitCount = Physics.OverlapSphereNonAlloc(origin, attackRange, ShotCandidates, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            EnemyDummyCard nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                EnemyDummyCard enemy = ShotCandidates[i].GetComponentInParent<EnemyDummyCard>();
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                float distance = (enemy.transform.position - origin).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = enemy;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }
    }
}
