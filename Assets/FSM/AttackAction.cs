using UnityEngine;

public class AttackAction : FsmAction<Enemy>
{
	private float _nextAttackTime;

	public override void Enter(Enemy actor)
	{
		actor.Movement.Stop();
		_nextAttackTime = Time.time;
	}

	public override void Update(Enemy actor)
	{
		if (!actor.IsTargetInRange(actor.AttackRange) || Time.time < _nextAttackTime)
		{
			return;
		}

		// Тут потрібно зняти actor.AttackDamage здоров'я у гравця.
		Debug.Log("Ворог атакував.", actor);
		_nextAttackTime = Time.time + actor.AttackInterval;
	}
}
