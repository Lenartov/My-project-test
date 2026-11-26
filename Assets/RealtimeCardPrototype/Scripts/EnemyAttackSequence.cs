using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    internal readonly struct EnemyAttackTelegraphSegment
    {
        public EnemyAttackTelegraphSegment(Vector3 start, Vector3 end, int damage, bool startsAtEnemy)
        {
            Start = start;
            End = end;
            Damage = damage;
            StartsAtEnemy = startsAtEnemy;
        }

        public Vector3 Start { get; }
        public Vector3 End { get; }
        public int Damage { get; }
        public bool StartsAtEnemy { get; }
    }

    internal sealed class EnemyAttackSequence
    {
        private readonly List<ShieldBlockerCard>[] blockChains;
        private readonly float hitInterval;
        private int nextHitIndex;
        private float hitTimer;
        private int damage;
        private bool isRunning;

        public EnemyAttackSequence(int hitCount, float hitInterval, int damage)
        {
            int clampedHitCount = Mathf.Max(1, hitCount);
            blockChains = new List<ShieldBlockerCard>[clampedHitCount];
            for (int i = 0; i < blockChains.Length; i++)
            {
                blockChains[i] = new List<ShieldBlockerCard>();
            }

            this.hitInterval = Mathf.Max(0.01f, hitInterval);
            this.damage = damage;
        }

        public int HitCount => blockChains.Length;
        public bool IsRunning => isRunning;
        public bool IsComplete => isRunning && nextHitIndex >= blockChains.Length;

        public bool TryAddRootBlock(ShieldBlockerCard block)
        {
            if (block == null || isRunning)
            {
                return false;
            }

            for (int i = 0; i < blockChains.Length; i++)
            {
                if (blockChains[i].Count != 0 || !block.TryAssignToSequence(this))
                {
                    continue;
                }

                blockChains[i].Add(block);
                return true;
            }

            return false;
        }

        public bool TryAppendBlock(ShieldBlockerCard previousBlock, ShieldBlockerCard nextBlock)
        {
            if (previousBlock == null || nextBlock == null || isRunning)
            {
                return false;
            }

            for (int i = 0; i < blockChains.Length; i++)
            {
                List<ShieldBlockerCard> chain = blockChains[i];
                if (chain.Count == 0
                    || !ReferenceEquals(chain[chain.Count - 1], previousBlock)
                    || GetRemainingDamage(chain) <= 0
                    || !nextBlock.TryAssignToSequence(this))
                {
                    continue;
                }

                chain.Add(nextBlock);
                return true;
            }

            return false;
        }

        public void CollectTelegraphSegments(Vector3 source, Vector3 playerTarget, List<EnemyAttackTelegraphSegment> segments)
        {
            for (int hitIndex = 0; hitIndex < blockChains.Length; hitIndex++)
            {
                List<ShieldBlockerCard> chain = blockChains[hitIndex];
                Vector3 previousPoint = source;
                int remainingDamage = damage;

                for (int blockIndex = 0; blockIndex < chain.Count && remainingDamage > 0; blockIndex++)
                {
                    ShieldBlockerCard block = chain[blockIndex];
                    if (block == null || !block.IsTargetable)
                    {
                        continue;
                    }

                    Vector3 blockTargetPoint = block.TargetPoint;
                    segments.Add(new EnemyAttackTelegraphSegment(previousPoint, blockTargetPoint, remainingDamage, blockIndex == 0));
                    previousPoint = blockTargetPoint;
                    remainingDamage = block.GetPreviewOverflowDamage(remainingDamage);
                }

                if (remainingDamage > 0)
                {
                    segments.Add(new EnemyAttackTelegraphSegment(
                        previousPoint,
                        GetHitTargetPoint(playerTarget, hitIndex),
                        remainingDamage,
                        chain.Count == 0));
                }
            }
        }

        public Vector3 GetHitTargetPoint(Vector3 playerTarget, int hitIndex)
        {
            if (blockChains.Length <= 1)
            {
                return playerTarget;
            }

            float offset = hitIndex - (blockChains.Length - 1) * 0.5f;
            return playerTarget + Vector3.right * offset * 0.28f;
        }

        public void Start(int newDamage, EnemyDummyCard enemy, EnemyAttackPlan plan)
        {
            if (isRunning)
            {
                return;
            }

            damage = newDamage;
            isRunning = true;
            FireNextHit(enemy, plan);
        }

        public void Tick(float deltaTime, EnemyDummyCard enemy, EnemyAttackPlan plan)
        {
            if (!isRunning || IsComplete)
            {
                return;
            }

            hitTimer += deltaTime;
            while (hitTimer >= hitInterval && !IsComplete)
            {
                hitTimer -= hitInterval;
                FireNextHit(enemy, plan);
            }
        }

        public void Cancel()
        {
            for (int i = 0; i < blockChains.Length; i++)
            {
                List<ShieldBlockerCard> chain = blockChains[i];
                for (int j = 0; j < chain.Count; j++)
                {
                    ShieldBlockerCard block = chain[j];
                    if (block != null)
                    {
                        block.ConsumeUnusedSequenceBlock(this);
                    }
                }

                chain.Clear();
            }
        }

        private void FireNextHit(EnemyDummyCard enemy, EnemyAttackPlan plan)
        {
            int hitIndex = nextHitIndex++;
            List<ShieldBlockerCard> chain = blockChains[hitIndex];
            blockChains[hitIndex] = new List<ShieldBlockerCard>();
            enemy.ExecuteEnemyAttackHit(plan, hitIndex, chain, damage);
        }

        private int GetRemainingDamage(List<ShieldBlockerCard> chain)
        {
            int remainingDamage = damage;
            for (int i = 0; i < chain.Count && remainingDamage > 0; i++)
            {
                ShieldBlockerCard block = chain[i];
                if (block != null && block.IsTargetable)
                {
                    remainingDamage = block.GetPreviewOverflowDamage(remainingDamage);
                }
            }

            return remainingDamage;
        }
    }
}
