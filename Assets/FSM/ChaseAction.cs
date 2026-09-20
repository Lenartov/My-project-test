using UnityEngine;

public class ChaseAction : FsmAction<Enemy>
{
	public override void Enter(Enemy actor)
	{
		actor.Movement.MoveTo(actor.Targeting.Target.position, actor.ChaseSpeed);
	}

	public override void Update(Enemy actor)
	{
		actor.Movement.MoveTo(actor.Targeting.Target.position, actor.ChaseSpeed);
	}
}
