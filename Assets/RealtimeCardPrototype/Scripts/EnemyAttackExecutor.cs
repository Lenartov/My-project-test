using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    internal sealed class EnemyAttackExecutor
    {
        private readonly Transform owner;
        private readonly Func<bool> isDying;

        private readonly List<LineRenderer> telegraphLines = new List<LineRenderer>();
        private readonly List<TextMesh> telegraphDamageTexts = new List<TextMesh>();
        private readonly List<EnemyAttackTelegraphSegment> telegraphSegments = new List<EnemyAttackTelegraphSegment>();
        private int playerTargetCounter;

        public EnemyAttackExecutor(Transform owner, Func<bool> isDying)
        {
            this.owner = owner;
            this.isDying = isDying;
        }

        public EnemyAttackPlan CreatePlan(EnemyAttackTargetMode mode)
        {
            Vector3 source = GetAttackSource();
            if (mode == EnemyAttackTargetMode.Structure && CombatTargetRegistry.TryGetStructureTarget(source, out IEnemyAttackTarget structureTarget))
            {
                return new EnemyAttackPlan(mode, structureTarget, null, structureTarget.TargetPoint);
            }

            if (mode == EnemyAttackTargetMode.Structure)
            {
                return new EnemyAttackPlan(mode, null, null, source);
            }

            IPlayerDamageReceiver player = CombatTargetRegistry.Player;
            return new EnemyAttackPlan(EnemyAttackTargetMode.Player, null, player, CreatePlayerAttackPoint());
        }

        public void ShowTelegraph(EnemyAttackPlan plan, float damage)
        {
            if (isDying())
            {
                return;
            }

            if (!plan.HasStructureTarget && !plan.HasPlayerTarget)
            {
                HideTelegraph();
                return;
            }

            Vector3 source = GetAttackSource();
            telegraphSegments.Clear();
            if (plan.Sequence != null && plan.HasPlayerTarget)
            {
                plan.Sequence.CollectTelegraphSegments(source, plan.TargetPoint, telegraphSegments);
            }
            else
            {
                telegraphSegments.Add(new EnemyAttackTelegraphSegment(source, ResolveTargetPoint(plan), damage, true));
            }

            for (int i = 0; i < telegraphSegments.Count; i++)
            {
                EnsureTelegraphSegment(i);
                UpdateTelegraphSegment(i, telegraphSegments[i]);
            }

            for (int i = telegraphSegments.Count; i < telegraphLines.Count; i++)
            {
                telegraphLines[i].enabled = false;
                telegraphDamageTexts[i].gameObject.SetActive(false);
            }
        }

        public void HideTelegraph()
        {
            for (int i = 0; i < telegraphLines.Count; i++)
            {
                telegraphLines[i].enabled = false;
                telegraphDamageTexts[i].gameObject.SetActive(false);
            }
        }

        public void Execute(EnemyAttackPlan plan, float damage)
        {
            if (isDying())
            {
                return;
            }

            if (!plan.HasStructureTarget && !plan.HasPlayerTarget)
            {
                Debug.Log($"{owner.name} attack found no target.");
                return;
            }

            Vector3 source = GetAttackSource();
            Vector3 targetPoint = ResolveTargetPoint(plan);
            if (CombatTargetRegistry.TryGetAttackInterceptor(source, targetPoint, out IEnemyAttackTarget interceptor))
            {
                interceptor.ReceiveEnemyAttack(damage);
                EnemyAttackProjectile.Spawn(source, interceptor.TargetPoint, damage);
                Debug.Log($"{owner.name} attack intercepted by {interceptor.TargetTransform.name} for {damage:0.0}");
                return;
            }

            if (plan.HasStructureTarget && plan.StructureTarget.IsTargetable)
            {
                plan.StructureTarget.ReceiveEnemyAttack(damage);
                EnemyAttackProjectile.Spawn(source, plan.StructureTarget.TargetPoint, damage);
                Debug.Log($"{owner.name} attacks structure {plan.StructureTarget.TargetTransform.name} for {damage:0.0}");
                return;
            }

            if (plan.HasPlayerTarget)
            {
                plan.PlayerTarget.ReceiveEnemyAttack(damage);
                EnemyAttackProjectile.Spawn(source, targetPoint, damage);
                Debug.Log($"{owner.name} attacks player for {damage:0.0}");
            }
        }

        public void ExecuteBlockChain(EnemyAttackPlan plan, int hitIndex, List<ShieldBlockerCard> blocks, float damage)
        {
            Vector3 source = GetAttackSource();
            Vector3 previousPoint = source;
            float remainingDamage = Mathf.Max(0f, damage);
            int blockIndex = 0;

            for (; blockIndex < blocks.Count && remainingDamage > 0f; blockIndex++)
            {
                ShieldBlockerCard block = blocks[blockIndex];
                if (block == null || !block.IsTargetable)
                {
                    continue;
                }

                Vector3 blockTargetPoint = block.TargetPoint;
                EnemyAttackProjectile.Spawn(previousPoint, blockTargetPoint, remainingDamage);
                previousPoint = blockTargetPoint;
                remainingDamage = block.AbsorbEnemyAttack(remainingDamage);
            }

            for (; blockIndex < blocks.Count; blockIndex++)
            {
                ShieldBlockerCard unusedBlock = blocks[blockIndex];
                if (unusedBlock != null)
                {
                    unusedBlock.ConsumeUnusedSequenceBlock(plan.Sequence);
                }
            }

            if (remainingDamage > 0f && plan.HasPlayerTarget)
            {
                Vector3 playerTargetPoint = plan.Sequence != null
                    ? plan.Sequence.GetHitTargetPoint(plan.TargetPoint, hitIndex)
                    : plan.TargetPoint;
                plan.PlayerTarget.ReceiveEnemyAttack(remainingDamage);
                EnemyAttackProjectile.Spawn(previousPoint, playerTargetPoint, remainingDamage);
            }
        }

        private Vector3 GetAttackSource()
        {
            return owner.position + Vector3.up * 0.58f;
        }

        private Vector3 CreatePlayerAttackPoint()
        {
            playerTargetCounter++;
            float spread = Mathf.Sin((owner.GetInstanceID() * 0.173f + playerTargetCounter * 1.917f) * 12.9898f) * 0.95f;
            return new Vector3(spread, 0.42f, -3.45f);
        }

        private Vector3 ResolveTargetPoint(EnemyAttackPlan plan)
        {
            if (plan.HasStructureTarget && plan.StructureTarget.IsTargetable)
            {
                return plan.StructureTarget.TargetPoint;
            }

            return plan.TargetPoint;
        }

        private void EnsureTelegraphSegment(int index)
        {
            if (index < telegraphLines.Count)
            {
                return;
            }

            GameObject lineObject = new GameObject("Enemy Attack Telegraph Segment");
            lineObject.transform.SetParent(owner, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.numCapVertices = 4;
            line.alignment = LineAlignment.View;
            line.material = PrototypeMaterials.Create(new Color(1f, 0.02f, 0.01f, 0.72f), 0.1f);
            line.startColor = new Color(1f, 0.03f, 0.02f, 0.96f);
            line.endColor = new Color(1f, 0.03f, 0.02f, 0.4f);
            line.enabled = false;

            telegraphLines.Add(line);
            telegraphDamageTexts.Add(CreateTelegraphDamageText(lineObject.transform));
        }

        private void UpdateTelegraphSegment(int index, EnemyAttackTelegraphSegment segment)
        {
            LineRenderer line = telegraphLines[index];
            TextMesh damageText = telegraphDamageTexts[index];
            line.enabled = true;
            line.startWidth = segment.StartsAtEnemy ? 0.18f : 0.12f;
            line.endWidth = 0.005f;
            line.SetPosition(0, segment.Start);
            line.SetPosition(1, segment.End);
            damageText.gameObject.SetActive(true);
            damageText.text = segment.Damage.ToString("0");
            damageText.transform.position = Vector3.Lerp(segment.Start, segment.End, 0.5f) + Vector3.up * 0.14f;
            Camera camera = Camera.main;
            if (camera != null)
            {
                damageText.transform.rotation = Quaternion.LookRotation(damageText.transform.position - camera.transform.position);
            }
        }

        private static TextMesh CreateTelegraphDamageText(Transform parent)
        {
            GameObject textObject = new GameObject("Attack Telegraph Damage");
            textObject.transform.SetParent(parent, false);

            TextMesh text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.1f;
            text.fontSize = 64;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(1f, 0.18f, 0.1f);
            textObject.GetComponent<MeshRenderer>().sortingOrder = 51;
            textObject.SetActive(false);
            return text;
        }
    }
}
