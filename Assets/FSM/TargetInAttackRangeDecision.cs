public class TargetInAttackRangeDecision : FsmDecision<Enemy>
{
	public override bool Decide(Enemy actor)
	{
		return actor.IsTargetInRange(actor.AttackRange);
	}
}
