using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    public enum EnemyActionKind
    {
        Attack,
        Heal,
        Wait,
        Sleep,
        Recovery
    }

    public interface IEnemyAction
    {
        EnemyActionKind Kind { get; }
        float Progress01 { get; }
        void Begin(EnemyDummyCard enemy);
        bool Tick(EnemyDummyCard enemy, float deltaTime);
    }

    internal static class EnemyActionFactory
    {
        public static IEnemyAction[] CreateCycle(EnemyActionConfiguration configuration)
        {
            List<IEnemyAction> actions = new List<IEnemyAction>(configuration.cycle.Count);
            for (int i = 0; i < configuration.cycle.Count; i++)
            {
                EnemyActionStepConfiguration step = configuration.cycle[i];
                float duration = configuration.attackChargeSeconds * Mathf.Max(0.01f, step.durationMultiplier);
                float amount = configuration.attackDamage * Mathf.Max(0f, step.amountMultiplier);

                switch (step.kind)
                {
                    case EnemyActionKind.Attack:
                        actions.Add(new EnemyAttackAction(duration, amount, step.attackTargetMode, step.hitCount, step.hitInterval));
                        break;
                    case EnemyActionKind.Heal:
                        actions.Add(new EnemyHealAction(duration, amount));
                        break;
                    case EnemyActionKind.Wait:
                        actions.Add(new EnemyWaitAction(duration));
                        break;
                    default:
                        throw new InvalidOperationException($"{step.kind} cannot be part of a regular enemy action cycle.");
                }
            }

            if (actions.Count == 0)
            {
                throw new InvalidOperationException("Enemy action cycle must contain at least one action.");
            }

            return actions.ToArray();
        }
    }

    public sealed class EnemyAttackAction : IEnemyAction
    {
        private readonly float chargeSeconds;
        private readonly float damage;
        private readonly EnemyAttackTargetMode targetMode;
        private readonly int hitCount;
        private readonly float hitInterval;
        private EnemyAttackPlan attackPlan;
        private float timer;

        public EnemyActionKind Kind => EnemyActionKind.Attack;
        public float Progress01 => Mathf.Clamp01(timer / chargeSeconds);

        public EnemyAttackAction(float chargeSeconds, float damage, EnemyAttackTargetMode targetMode, int hitCount, float hitInterval)
        {
            this.chargeSeconds = Mathf.Max(0.01f, chargeSeconds);
            this.damage = Mathf.Max(0f, damage);
            this.targetMode = targetMode;
            this.hitCount = Mathf.Max(1, hitCount);
            this.hitInterval = Mathf.Max(0.01f, hitInterval);
        }

        public void Begin(EnemyDummyCard enemy)
        {
            timer = 0f;
            attackPlan = enemy.CreateEnemyAttackPlan(targetMode, hitCount, hitInterval, damage);
            enemy.ShowEnemyAttackTelegraph(attackPlan, damage);
        }

        public bool Tick(EnemyDummyCard enemy, float deltaTime)
        {
            timer += deltaTime;
            if (timer < chargeSeconds)
            {
                enemy.ShowEnemyAttackTelegraph(attackPlan, damage);
                return false;
            }

            enemy.ExecuteEnemyAttack(attackPlan, damage);
            enemy.HideEnemyAttackTelegraph();
            return true;
        }
    }

    public sealed class EnemyWaitAction : IEnemyAction
    {
        private readonly float waitSeconds;
        private float timer;

        public EnemyActionKind Kind => EnemyActionKind.Wait;
        public float Progress01 => Mathf.Clamp01(timer / waitSeconds);

        public EnemyWaitAction(float waitSeconds)
        {
            this.waitSeconds = Mathf.Max(0.01f, waitSeconds);
        }

        public void Begin(EnemyDummyCard enemy)
        {
            timer = 0f;
        }

        public bool Tick(EnemyDummyCard enemy, float deltaTime)
        {
            timer += deltaTime;
            return timer >= waitSeconds;
        }
    }

    public sealed class EnemyHealAction : IEnemyAction
    {
        private readonly float chargeSeconds;
        private readonly float amount;
        private float timer;

        public EnemyActionKind Kind => EnemyActionKind.Heal;
        public float Progress01 => Mathf.Clamp01(timer / chargeSeconds);

        public EnemyHealAction(float chargeSeconds, float amount)
        {
            this.chargeSeconds = Mathf.Max(0.01f, chargeSeconds);
            this.amount = Mathf.Max(0f, amount);
        }

        public void Begin(EnemyDummyCard enemy)
        {
            timer = 0f;
            enemy.HideEnemyAttackTelegraph();
        }

        public bool Tick(EnemyDummyCard enemy, float deltaTime)
        {
            timer += deltaTime;
            if (timer < chargeSeconds)
            {
                return false;
            }

            enemy.ReceiveHeal(amount);
            return true;
        }
    }

    public sealed class EnemySleepAction : IEnemyAction
    {
        private readonly float sleepSeconds;
        private float timer;

        public EnemyActionKind Kind => EnemyActionKind.Sleep;
        public float Progress01 => Mathf.Clamp01(timer / sleepSeconds);

        public EnemySleepAction(float sleepSeconds)
        {
            this.sleepSeconds = Mathf.Max(0.01f, sleepSeconds);
        }

        public void Begin(EnemyDummyCard enemy)
        {
            timer = 0f;
        }

        public bool Tick(EnemyDummyCard enemy, float deltaTime)
        {
            timer += deltaTime;
            return timer >= sleepSeconds;
        }
    }

    public sealed class EnemyRecoveryAction : IEnemyAction
    {
        private readonly float recoverySeconds;
        private float timer;

        public EnemyActionKind Kind => EnemyActionKind.Recovery;
        public float Progress01 => Mathf.Clamp01(timer / recoverySeconds);

        public EnemyRecoveryAction(float recoverySeconds)
        {
            this.recoverySeconds = Mathf.Max(0.01f, recoverySeconds);
        }

        public void Begin(EnemyDummyCard enemy)
        {
            timer = 0f;
            enemy.HideEnemyAttackTelegraph();
        }

        public bool Tick(EnemyDummyCard enemy, float deltaTime)
        {
            timer += deltaTime;
            return timer >= recoverySeconds;
        }
    }
}
