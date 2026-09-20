public class TargetDetectedDecision : FsmDecision<Enemy>
{
	public override bool Decide(Enemy actor)
	{
		return actor.HasDetectedTarget;
	}
}
