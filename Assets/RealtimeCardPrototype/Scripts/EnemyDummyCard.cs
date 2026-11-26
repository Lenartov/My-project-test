using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    [RequireComponent(typeof(CardView))]
    public sealed class EnemyDummyCard : MonoBehaviour
    {
        [SerializeField] private EnemyConfiguration configuration = new EnemyConfiguration();
        [SerializeField] private TextMesh statusText;

        public event Action<EnemyDummyCard> Died;

        private CardView cardView;
        private Rigidbody body;
        private Vector3 initialScale;
        private EnemyHealthState healthState;
        private EnemyBalanceState balanceState;
        private EnemySideState sideState;
        private EnemyMovementController movementController;
        private EnemyStatusEffects statusEffects;
        private EnemyCardStatusView statusView;
        private CardCombatEffects shockMark;
        private CardCombatEffects poisonMark;
        private float deathTimer;
        private bool isDying;
        private EnemyActionController actionController;
        private EnemyAttackExecutor attackExecutor;
        private EnemyAttackSequence pendingAttackSequence;
        private readonly List<EnemyAttackPlan> runningAttackPlans = new List<EnemyAttackPlan>();
        private const float DeathAnimationSeconds = 0.28f;
        private static readonly Collider[] ShockHits = new Collider[16];

        public bool IsDead => isDying || healthState != null && healthState.IsDepleted;

        private void Awake()
        {
            cardView = GetComponent<CardView>();
            body = cardView.Body;
            initialScale = transform.localScale;
            healthState = new EnemyHealthState(configuration.vitals.maxHealth);
            balanceState = new EnemyBalanceState(
                configuration.vitals.maxBalance,
                configuration.balance.damageFloor,
                configuration.balance.breakTriggerDamage,
                configuration.balance.gainAtStrongHit,
                configuration.balance.gainExponent);
            sideState = new EnemySideState();
            sideState.Generate(configuration.defense.blockingSideChance);
            movementController = new EnemyMovementController(
                body,
                transform.position,
                transform.rotation,
                configuration.movement.returnDamping,
                configuration.movement.maxKnockbackOffset,
                configuration.movement.limitRelaxDistanceFraction,
                configuration.movement.limitReturnSpring,
                configuration.movement.maxPlanarSpeed,
                configuration.movement.knockbackMinDamage,
                configuration.movement.knockbackMaxDamage,
                configuration.movement.maxKnockbackImpulse);
            statusEffects = new EnemyStatusEffects(
                configuration.statusEffects.poisonDuration,
                configuration.statusEffects.poisonStackStepSeconds,
                configuration.statusEffects.poisonDecaySpeedMultiplier,
                configuration.statusEffects.poisonLevelOneDamage,
                configuration.statusEffects.poisonLevelTwoDamage,
                configuration.statusEffects.poisonLevelThreeDamage,
                configuration.statusEffects.poisonTickInterval,
                configuration.statusEffects.poisonPartialTickThreshold,
                configuration.statusEffects.shockMarkDuration,
                configuration.statusEffects.shockChainDamageFraction,
                configuration.statusEffects.shockChainPopupInterval);
            statusView = new EnemyCardStatusView(transform, cardView.Collider);
            statusView.Create();
            RefreshStatusView();
            attackExecutor = new EnemyAttackExecutor(transform, () => isDying);
            actionController = new EnemyActionController(transform);
            actionController.Configure(this, configuration.actions);
        }

        private void FixedUpdate()
        {
            if (isDying)
            {
                return;
            }

            movementController.TickFixed();
        }

        private void Update()
        {
            if (isDying)
            {
                TickDeathAnimation(Time.deltaTime);
                return;
            }

            statusEffects.Tick(Time.deltaTime, Time.time, ReceivePoisonDamage, ShowAccumulatedShockDamage);
            if (shockMark != null && !statusEffects.HasShockMark)
            {
                Destroy(shockMark.gameObject);
                shockMark = null;
            }

            if (poisonMark != null && statusEffects.PoisonLevel == 0)
            {
                Destroy(poisonMark.gameObject);
                poisonMark = null;
            }

            sideState.Tick(Time.deltaTime);
            TickAttackSequences(Time.deltaTime);
            actionController.Tick(this, Time.deltaTime);
            RefreshStatusView();
        }

        public void ConfigureStatusText(TextMesh text)
        {
            statusText = text;
            RefreshHealthStatus();
        }

        public void ConfigureVitals(float newMaxHealth, float newMaxPosture)
        {
            configuration.vitals.maxHealth = Mathf.Max(1f, newMaxHealth);
            configuration.vitals.maxBalance = Mathf.Max(1f, newMaxPosture);
            if (healthState != null)
            {
                healthState.Configure(configuration.vitals.maxHealth);
            }

            if (balanceState != null)
            {
                balanceState.Configure(
                    configuration.vitals.maxBalance,
                    configuration.balance.damageFloor,
                    configuration.balance.breakTriggerDamage,
                    configuration.balance.gainAtStrongHit,
                    configuration.balance.gainExponent);
            }
            RefreshHealthStatus();
            RefreshStatusView();
        }

        public void ConfigureActionTimings(float initialWaitSeconds, float attackChargeSeconds, float waitSeconds, float attackDamage)
        {
            configuration.actions.initialSleepSeconds = Mathf.Max(0.01f, initialWaitSeconds);
            configuration.actions.attackChargeSeconds = Mathf.Max(0.01f, attackChargeSeconds);
            configuration.actions.waitSeconds = Mathf.Max(0.01f, waitSeconds);
            configuration.actions.attackDamage = Mathf.Max(0f, attackDamage);
            if (actionController != null)
            {
                actionController.Configure(this, configuration.actions);
            }
        }

        public void ConfigureActionCycle(params EnemyActionStepConfiguration[] steps)
        {
            if (steps == null || steps.Length == 0)
            {
                throw new ArgumentException("An enemy action cycle must contain at least one step.", nameof(steps));
            }

            configuration.actions.cycle = new List<EnemyActionStepConfiguration>(steps);
            if (actionController != null)
            {
                actionController.Configure(this, configuration.actions);
            }
        }

        public DamageResult ReceiveHit(CardHitProfile profile, Vector3 attackerPosition, Vector3 attackerVelocity, Vector3 contactPoint)
        {
            if (isDying)
            {
                return default;
            }

            float speed = attackerVelocity.magnitude;
            EnemyCombatSide hitSide = EvaluateHitSide(attackerPosition);
            EnemyEdgeRole hitRole = GetEffectiveSideRole(hitSide);
            float facingMultiplier = GetSideMultiplier(hitRole);
            float speedMultiplier = profile.GetSpeedMultiplier(speed);
            float totalMultiplier = facingMultiplier * speedMultiplier;
            float postureDamage = profile.postureDamage * totalMultiplier;

            Vector3 impulseDirection = attackerVelocity.sqrMagnitude > 0.01f
                ? attackerVelocity.normalized
                : (transform.position - attackerPosition).normalized;
            impulseDirection.y = 0f;

            string facingLabel = GetRoleLabel(hitRole);
            int damage = ApplyDamage(new EnemyDamageEvent(
                EnemyDamageKind.Direct,
                profile.damage * totalMultiplier,
                postureDamage,
                true,
                true,
                impulseDirection,
                true,
                true,
                Vector3.zero,
                new Color(1f, 0.12f, 0.08f),
                facingLabel));
            Debug.Log($"{name} hit: {hitSide} {facingLabel}, damage {damage}, balance {balanceState.CurrentBalance:0.0}/{balanceState.MaxBalance:0.0}, speed {speed:0.0}");
            return new DamageResult(damage, postureDamage);
        }

        public void ReceiveSplashHit(float damage, float postureDamage, Vector3 blastPoint)
        {
            if (isDying || damage <= 0f)
            {
                return;
            }

            Vector3 impulseDirection = transform.position - blastPoint;
            impulseDirection.y = 0f;
            int appliedDamage = ApplyDamage(new EnemyDamageEvent(
                EnemyDamageKind.Splash,
                damage,
                postureDamage,
                true,
                true,
                impulseDirection,
                true,
                true,
                Vector3.right * 0.18f,
                new Color(1f, 0.44f, 0.12f),
                "SPLASH"));
            Debug.Log($"{name} splash hit: damage {appliedDamage}, balance {balanceState.CurrentBalance:0.0}/{balanceState.MaxBalance:0.0}");
        }

        public void ApplyGravitonPull(Vector3 center, float acceleration)
        {
            if (isDying || acceleration <= 0f)
            {
                return;
            }

            movementController.ApplyGravitonPull(center, acceleration);
        }

        public void ReceiveGravitonTick(float damage)
        {
            if (isDying || damage <= 0f)
            {
                return;
            }

            ApplyDamage(new EnemyDamageEvent(
                EnemyDamageKind.Graviton,
                damage,
                0f,
                false,
                false,
                Vector3.zero,
                true,
                true,
                Vector3.forward * 0.18f,
                new Color(0.62f, 0.3f, 1f),
                "GRAV"));
        }

        internal void ReceiveTurretShot(float damage, Color color)
        {
            ApplyDamage(new EnemyDamageEvent(
                EnemyDamageKind.TurretShot,
                damage,
                0f,
                false,
                false,
                Vector3.zero,
                true,
                true,
                Vector3.zero,
                color,
                "TURRET"));
        }

        public void ApplyPoisonHover(float deltaTime, float damageMultiplier = 1f)
        {
            if (isDying)
            {
                return;
            }

            statusEffects.ApplyPoisonHover(deltaTime, damageMultiplier, Time.time);
            if (poisonMark == null && statusEffects.PoisonLevel > 0)
            {
                Vector3 point = transform.TransformPoint(new Vector3(-cardView.Collider.size.x * 0.46f, 0.20f, 0f));
                poisonMark = CardCombatEffects.MarkPoison(transform, point);
            }
        }

        public void ApplyShockMark()
        {
            if (isDying)
            {
                return;
            }

            statusEffects.ApplyShockMark();
            Vector3 point = transform.TransformPoint(new Vector3(cardView.Collider.size.x * 0.43f, 0.23f, 0.23f));
            shockMark = CardCombatEffects.MarkShock(
                transform, point, statusEffects.ShockDuration, configuration.statusEffects.shockChainRadius, shockMark);
            RefreshStatus($"SHOCK\nHP {healthState.CurrentHealth:0}");
        }

        public void ReceiveHeal(float amount)
        {
            if (isDying || amount <= 0f)
            {
                return;
            }

            float restoredAmount = healthState.Restore(amount);
            if (restoredAmount <= 0f)
            {
                return;
            }

            DamagePopup.SpawnHealing(restoredAmount, transform.position + Vector3.up * 0.18f, new Color(0.1f, 1f, 0.62f));
            RefreshStatus($"HEAL\nHP {healthState.CurrentHealth:0}");
            RefreshStatusView();
        }

        public EnemyAttackPlan CreateEnemyAttackPlan(EnemyAttackTargetMode mode, int hitCount, float hitInterval, int damage)
        {
            if (damage <= 0)
            {
                return default;
            }

            EnemyAttackPlan plan = attackExecutor.CreatePlan(mode, hitCount == 1);
            if (!plan.HasPlayerTarget)
            {
                return plan;
            }

            EnemyAttackSequence sequence = new EnemyAttackSequence(hitCount, hitInterval, damage);
            pendingAttackSequence = sequence;
            return new EnemyAttackPlan(plan.Mode, null, plan.PlayerTarget, plan.TargetPoint, sequence);
        }

        public void ShowEnemyAttackTelegraph(EnemyAttackPlan plan, int damage)
        {
            attackExecutor.ShowTelegraph(plan, damage);
        }

        public void HideEnemyAttackTelegraph()
        {
            attackExecutor.HideTelegraph();
        }

        public void ExecuteEnemyAttack(EnemyAttackPlan plan, int damage)
        {
            if (plan.Sequence == null)
            {
                attackExecutor.Execute(plan, damage);
                return;
            }

            if (ReferenceEquals(pendingAttackSequence, plan.Sequence))
            {
                pendingAttackSequence = null;
            }

            plan.Sequence.Start(damage, this, plan);
            if (!plan.Sequence.IsComplete)
            {
                runningAttackPlans.Add(plan);
            }
        }

        public void ExecuteEnemyAttackHit(EnemyAttackPlan plan, int hitIndex, List<ShieldBlockerCard> blockChain, int damage)
        {
            attackExecutor.ExecuteBlockChain(plan, hitIndex, blockChain, damage);
        }

        public bool TryAddBlockToPendingAttack(ShieldBlockerCard block)
        {
            return !isDying && pendingAttackSequence != null && pendingAttackSequence.TryAddRootBlock(block);
        }

        public void PerformBasicEnemyAttack(float damage)
        {
            attackExecutor.Execute(attackExecutor.CreatePlan(EnemyAttackTargetMode.Structure), CombatDamage.ToHitPoints(damage));
        }

        private void AddBalanceFromDamage(float damage, bool canTriggerBreak)
        {
            if (isDying || actionController.IsInRecovery)
            {
                return;
            }

            if (balanceState.AddDamage(damage, canTriggerBreak))
            {
                StartBalanceBreak();
            }
        }

        private int ApplyDamage(EnemyDamageEvent damageEvent)
        {
            int damage = damageEvent.Damage;
            if (isDying || damage <= 0)
            {
                return 0;
            }

            healthState.ApplyDamage(damage);
            DamageWindowTracker.Record(damage);
            AddBalanceFromDamage(damage, damageEvent.CanTriggerBalanceBreak);

            if (damageEvent.ApplyKnockback)
            {
                movementController.ApplyDamageKnockback(damage, damageEvent.KnockbackDirection);
            }

            if (damageEvent.ShowPopup)
            {
                DamagePopup.Spawn(damage, transform.position + damageEvent.PopupOffset, damageEvent.PopupColor);
            }

            if (damageEvent.TriggerShockChain)
            {
                TriggerShockChain(damage);
            }

            RefreshStatus($"{damageEvent.StatusLabel}\nHP {healthState.CurrentHealth:0}");
            TryStartDeath();
            return damage;
        }

        private void StartBalanceBreak()
        {
            balanceState.Reset();
            sideState.ActivateExposedSide(configuration.balance.exposedSideSeconds);
            HideEnemyAttackTelegraph();

            actionController.InterruptForRecovery(this, configuration.balance.recoverySeconds);
            RefreshStatus($"BROKEN\nHP {healthState.CurrentHealth:0}");
            RefreshStatusView();
        }


        private int ApplyShockChainDamage(float damage, bool showImmediatePopup)
        {
            int appliedDamage = ApplyDamage(new EnemyDamageEvent(
                EnemyDamageKind.ShockChain,
                damage,
                0f,
                false,
                false,
                Vector3.zero,
                false,
                showImmediatePopup,
                Vector3.left * 0.18f,
                new Color(0.18f, 0.58f, 1f),
                "CHAIN"));

            if (!showImmediatePopup)
            {
                statusEffects.AccumulateShockPopup(appliedDamage);
            }

            return appliedDamage;
        }

        private void TriggerShockChain(int sourceDamage)
        {
            TriggerShockChain(sourceDamage, true);
        }

        private void TriggerShockChain(int sourceDamage, bool showImmediatePopup)
        {
            if (!statusEffects.HasShockMark || sourceDamage <= 0)
            {
                return;
            }

            float chainDamage = statusEffects.GetShockChainDamage(sourceDamage);
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, configuration.statusEffects.shockChainRadius, ShockHits);
            HashSet<EnemyDummyCard> chainedEnemies = new HashSet<EnemyDummyCard>();
            bool hasChain = false;

            for (int i = 0; i < hitCount; i++)
            {
                EnemyDummyCard enemy = ShockHits[i].GetComponentInParent<EnemyDummyCard>();
                if (enemy == null || ReferenceEquals(enemy, this) || chainedEnemies.Contains(enemy))
                {
                    continue;
                }

                chainedEnemies.Add(enemy);
                Vector3 position = enemy.transform.position;
                if (enemy.ApplyShockChainDamage(chainDamage, showImmediatePopup) > 0)
                {
                    CardCombatEffects.ShockArc(transform.position, position);
                    hasChain = true;
                }
            }

            if (hasChain)
            {
                CardCombatAudio.PlayShockChain();
            }
        }

        private void ReceivePoisonDamage(float damage)
        {
            int appliedDamage = ApplyDamage(new EnemyDamageEvent(
                EnemyDamageKind.Poison,
                damage,
                0f,
                false,
                false,
                Vector3.zero,
                true,
                true,
                Vector3.right * 0.18f,
                new Color(0.74f, 0.96f, 0.08f),
                "POISON"));

            if (appliedDamage > 0)
            {
                CardCombatEffects.PoisonTick(transform.position - Vector3.right * 0.34f);
            }
        }

        private void ShowAccumulatedShockDamage(int damage)
        {
            DamagePopup.Spawn(damage, transform.position + Vector3.left * 0.18f, new Color(0.18f, 0.58f, 1f));
        }

        private void TryStartDeath()
        {
            if (isDying || !healthState.IsDepleted)
            {
                return;
            }

            isDying = true;
            if (shockMark != null)
            {
                Destroy(shockMark.gameObject);
            }

            if (poisonMark != null)
            {
                Destroy(poisonMark.gameObject);
                poisonMark = null;
            }

            deathTimer = 0f;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            cardView.Collider.enabled = false;
            CancelAttackSequences();
            actionController.Hide();
            RefreshStatus("DEFEATED");
            Died?.Invoke(this);
        }

        private void TickDeathAnimation(float deltaTime)
        {
            deathTimer += deltaTime;
            float amount = Mathf.Clamp01(deathTimer / DeathAnimationSeconds);
            float scale = Mathf.SmoothStep(1f, 0.05f, amount);
            transform.localScale = initialScale * scale;

            if (deathTimer >= DeathAnimationSeconds)
            {
                Destroy(gameObject);
            }
        }

        private void RefreshStatusView()
        {
            if (statusView == null)
            {
                return;
            }

            statusView.Refresh(
                sideState.Roles,
                sideState.ExposedSideIndex,
                sideState.ExposedSideRemaining,
                balanceState.CurrentBalance,
                balanceState.MaxBalance,
                statusEffects.PoisonLevel,
                statusEffects.PoisonStackProgress,
                statusEffects.PoisonStackStepSeconds,
                statusEffects.ShockRemaining,
                statusEffects.ShockDuration);
        }

        private EnemyEdgeRole GetEffectiveSideRole(EnemyCombatSide side)
        {
            return sideState.GetEffectiveRole(side);
        }

        private void TickAttackSequences(float deltaTime)
        {
            for (int i = runningAttackPlans.Count - 1; i >= 0; i--)
            {
                EnemyAttackPlan plan = runningAttackPlans[i];
                plan.Sequence.Tick(deltaTime, this, plan);
                if (plan.Sequence.IsComplete)
                {
                    runningAttackPlans.RemoveAt(i);
                }
            }
        }

        private void CancelAttackSequences()
        {
            if (pendingAttackSequence != null)
            {
                pendingAttackSequence.Cancel();
                pendingAttackSequence = null;
            }

            for (int i = 0; i < runningAttackPlans.Count; i++)
            {
                runningAttackPlans[i].Sequence.Cancel();
            }

            runningAttackPlans.Clear();
        }

        private EnemyCombatSide EvaluateHitSide(Vector3 attackerPosition)
        {
            Vector3 incomingWorld = (attackerPosition - transform.position).normalized;
            Vector3 incomingLocal = transform.InverseTransformDirection(incomingWorld);

            if (Mathf.Abs(incomingLocal.x) > Mathf.Abs(incomingLocal.z))
            {
                return incomingLocal.x < 0f ? EnemyCombatSide.Left : EnemyCombatSide.Right;
            }

            return incomingLocal.z < 0f ? EnemyCombatSide.Front : EnemyCombatSide.Back;
        }

        private float GetSideMultiplier(EnemyEdgeRole role)
        {
            switch (role)
            {
                case EnemyEdgeRole.Exposed:
                    return configuration.defense.criticalSideMultiplier;
                case EnemyEdgeRole.Blocking:
                    return configuration.defense.blockingSideMultiplier;
                default:
                    return 1f;
            }
        }

        private string GetRoleLabel(EnemyEdgeRole role)
        {
            switch (role)
            {
                case EnemyEdgeRole.Blocking:
                    return "BLOCK";
                case EnemyEdgeRole.Exposed:
                    return "CRIT x2";
                default:
                    return "NORMAL";
            }
        }

        private void RefreshHealthStatus()
        {
            float currentHealth = healthState != null ? healthState.CurrentHealth : configuration.vitals.maxHealth;
            RefreshStatus($"HP {currentHealth:0}");
        }

        private void RefreshStatus(string status)
        {
            if (statusText != null)
            {
                statusText.text = status;
            }
        }

    }

}
