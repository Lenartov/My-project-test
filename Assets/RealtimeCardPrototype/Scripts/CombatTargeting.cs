using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    public enum EnemyAttackTargetMode
    {
        Player,
        Structure
    }

    public interface IEnemyAttackTarget
    {
        Transform TargetTransform { get; }
        Vector3 TargetPoint { get; }
        int TargetPriority { get; }
        bool IsTargetable { get; }
        bool CanBeDirectTarget { get; }
        bool CanInterceptAttacks { get; }
        float InterceptRadius { get; }
        void ReceiveEnemyAttack(int damage);
    }

    public interface IPlayerDamageReceiver
    {
        Vector3 TargetPoint { get; }
        void ReceiveEnemyAttack(int damage);
    }

    public static class CombatTargetRegistry
    {
        private static readonly List<IEnemyAttackTarget> StructureTargets = new List<IEnemyAttackTarget>();
        private static readonly List<FieldPlacementBody> FieldPlacements = new List<FieldPlacementBody>();

        public static IPlayerDamageReceiver Player { get; private set; }

        public static void ClearAll()
        {
            StructureTargets.Clear();
            FieldPlacements.Clear();
            Player = null;
        }

        public static void SetPlayer(IPlayerDamageReceiver player)
        {
            Player = player;
        }

        public static void ClearPlayer(IPlayerDamageReceiver player)
        {
            if (ReferenceEquals(Player, player))
            {
                Player = null;
            }
        }

        public static void RegisterStructure(IEnemyAttackTarget target)
        {
            if (target != null && !StructureTargets.Contains(target))
            {
                StructureTargets.Add(target);
            }
        }

        public static void UnregisterStructure(IEnemyAttackTarget target)
        {
            StructureTargets.Remove(target);
        }

        public static void RegisterFieldPlacement(FieldPlacementBody placement)
        {
            if (placement != null && !FieldPlacements.Contains(placement))
            {
                FieldPlacements.Add(placement);
            }
        }

        public static void UnregisterFieldPlacement(FieldPlacementBody placement)
        {
            FieldPlacements.Remove(placement);
        }

        public static void ResolveFieldPlacement(FieldPlacementBody self)
        {
            if (self == null || !self.IsActive)
            {
                return;
            }

            for (int i = FieldPlacements.Count - 1; i >= 0; i--)
            {
                FieldPlacementBody other = FieldPlacements[i];
                if (other == null || !other.IsActive)
                {
                    FieldPlacements.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(other, self))
                {
                    continue;
                }

                Vector3 delta = self.Position - other.Position;
                delta.y = 0f;
                float minDistance = self.Radius + other.Radius;
                float distance = delta.magnitude;
                if (distance >= minDistance)
                {
                    continue;
                }

                Vector3 direction = distance > 0.001f
                    ? delta / distance
                    : GetFallbackSeparationDirection(self, other);

                float depth = minDistance - distance;
                float selfWeight = self.IsDragging && !other.IsDragging ? 0.65f : 0.5f;
                float otherWeight = other.IsDragging && !self.IsDragging ? 0.65f : 0.5f;
                Vector3 separation = direction * depth * 0.62f;

                self.ApplySeparation(separation * selfWeight);
                other.ApplySeparation(-separation * otherWeight);
            }
        }

        public static bool TryGetStructureTarget(Vector3 origin, out IEnemyAttackTarget target, bool turretOnly = false)
        {
            target = null;
            int bestPriority = int.MinValue;
            float bestDistanceSq = float.PositiveInfinity;

            for (int i = StructureTargets.Count - 1; i >= 0; i--)
            {
                IEnemyAttackTarget candidate = StructureTargets[i];
                if (candidate == null || IsDestroyed(candidate) || !candidate.IsTargetable)
                {
                    StructureTargets.RemoveAt(i);
                    continue;
                }

                if (!candidate.CanBeDirectTarget || (turretOnly && candidate is not TurretCard))
                {
                    continue;
                }

                float distanceSq = (candidate.TargetPoint - origin).sqrMagnitude;
                if (candidate.TargetPriority > bestPriority || candidate.TargetPriority == bestPriority && distanceSq < bestDistanceSq)
                {
                    bestPriority = candidate.TargetPriority;
                    bestDistanceSq = distanceSq;
                    target = candidate;
                }
            }

            return target != null;
        }

        public static bool TryGetAttackInterceptor(Vector3 start, Vector3 end, out IEnemyAttackTarget target)
        {
            target = null;
            float bestDistanceAlongLine = float.PositiveInfinity;

            for (int i = StructureTargets.Count - 1; i >= 0; i--)
            {
                IEnemyAttackTarget candidate = StructureTargets[i];
                if (candidate == null || IsDestroyed(candidate) || !candidate.IsTargetable)
                {
                    StructureTargets.RemoveAt(i);
                    continue;
                }

                if (!candidate.CanInterceptAttacks)
                {
                    continue;
                }

                float radius = Mathf.Max(0.01f, candidate.InterceptRadius);
                if (!TryProjectPointOnSegment(candidate.TargetPoint, start, end, out float lineT, out float distanceSq))
                {
                    continue;
                }

                if (distanceSq > radius * radius)
                {
                    continue;
                }

                if (lineT < bestDistanceAlongLine)
                {
                    bestDistanceAlongLine = lineT;
                    target = candidate;
                }
            }

            return target != null;
        }

        private static bool IsDestroyed(IEnemyAttackTarget target)
        {
            return target is Object unityObject && unityObject == null;
        }

        private static bool TryProjectPointOnSegment(Vector3 point, Vector3 start, Vector3 end, out float lineT, out float distanceSq)
        {
            Vector3 planarPoint = point;
            Vector3 planarStart = start;
            Vector3 planarEnd = end;
            planarPoint.y = 0f;
            planarStart.y = 0f;
            planarEnd.y = 0f;

            Vector3 segment = planarEnd - planarStart;
            float segmentLengthSq = segment.sqrMagnitude;
            if (segmentLengthSq <= 0.0001f)
            {
                lineT = 0f;
                distanceSq = (planarPoint - planarStart).sqrMagnitude;
                return false;
            }

            lineT = Mathf.Clamp01(Vector3.Dot(planarPoint - planarStart, segment) / segmentLengthSq);
            Vector3 closest = planarStart + segment * lineT;
            distanceSq = (planarPoint - closest).sqrMagnitude;
            return lineT > 0.03f && lineT < 0.97f;
        }

        private static Vector3 GetFallbackSeparationDirection(FieldPlacementBody self, FieldPlacementBody other)
        {
            int selfId = self.GetInstanceID();
            int otherId = other.GetInstanceID();
            float angle = (selfId > otherId ? selfId : otherId) * 0.731f;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
        }
    }

    public readonly struct EnemyAttackPlan
    {
        public EnemyAttackPlan(EnemyAttackTargetMode mode, IEnemyAttackTarget structureTarget, IPlayerDamageReceiver playerTarget, Vector3 targetPoint)
            : this(mode, structureTarget, playerTarget, targetPoint, null)
        {
        }

        internal EnemyAttackPlan(
            EnemyAttackTargetMode mode,
            IEnemyAttackTarget structureTarget,
            IPlayerDamageReceiver playerTarget,
            Vector3 targetPoint,
            EnemyAttackSequence sequence)
        {
            Mode = mode;
            StructureTarget = structureTarget;
            PlayerTarget = playerTarget;
            TargetPoint = targetPoint;
            Sequence = sequence;
        }

        public EnemyAttackTargetMode Mode { get; }
        public IEnemyAttackTarget StructureTarget { get; }
        public IPlayerDamageReceiver PlayerTarget { get; }
        public Vector3 TargetPoint { get; }
        internal EnemyAttackSequence Sequence { get; }
        public bool HasStructureTarget => StructureTarget != null;
        public bool HasPlayerTarget => PlayerTarget != null;
    }
}
