using UnityEngine;

[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyPatrol))]
[RequireComponent(typeof(EnemyTargeting))]
public class Enemy : MonoBehaviour
{
	[SerializeField] private EnemySettings _settings;

	public EnemySettings Settings => _settings;
	public float Speed => _settings.PatrolSpeed;
	public float ChaseSpeed => _settings.ChaseSpeed;
	public float IdleDuration => _settings.IdleDuration;
	public float IdleProbability => _settings.IdleProbability;
	public float AttackRange => _settings.AttackRange;
	public float AttackDamage => _settings.AttackDamage;
	public float AttackInterval => _settings.AttackInterval;

	public EnemyMovement Movement => _movement;
	public EnemyPatrol Patrol => _patrol;
	public EnemyTargeting Targeting => _targeting;
	public bool HasDetectedTarget => _targeting != null && _targeting.HasDetectedTarget;
	public bool IsIdle => Time.time < _idleEndTime;
	public bool ReachedPatrolPointThisFrame { get; set; }

	private EnemyMovement _movement;
	private EnemyPatrol _patrol;
	private EnemyTargeting _targeting;
	private FsmStateMachine<Enemy> _stateMachine;

	private float _idleEndTime;

	private void Awake()
	{
		_movement = GetComponent<EnemyMovement>();
		_patrol = GetComponent<EnemyPatrol>();
		_targeting = GetComponent<EnemyTargeting>();
	}

	private void Start()
	{
		MoveDecision moveDecision = new MoveDecision();
		IdleDecision idleDecision = new IdleDecision();
		TargetDetectedDecision targetDetectedDecision = new TargetDetectedDecision();
		TargetInAttackRangeDecision targetInAttackRangeDecision = new TargetInAttackRangeDecision();
		TargetOutsideAttackRangeDecision targetOutsideAttackRangeDecision = new TargetOutsideAttackRangeDecision();

		IdleAction idleAction = new IdleAction();
		PatrolAction patrolAction = new PatrolAction();
		ChaseAction chaseAction = new ChaseAction();
		AttackAction attackAction = new AttackAction();

		FsmState<Enemy> idleState = new FsmState<Enemy>(idleAction);
		FsmState<Enemy> patrolState = new FsmState<Enemy>(patrolAction);
		FsmState<Enemy> chaseState = new FsmState<Enemy>(chaseAction);
		FsmState<Enemy> attackState = new FsmState<Enemy>(attackAction);

		FsmTransition<Enemy> patrolToChaseTransition = new FsmTransition<Enemy>(targetDetectedDecision, chaseState);
		patrolState.AddTransition(patrolToChaseTransition);

		FsmTransition<Enemy> toIdleTransition = new FsmTransition<Enemy>(idleDecision, idleState);
		patrolState.AddTransition(toIdleTransition);

		FsmTransition<Enemy> idleToChaseTransition = new FsmTransition<Enemy>(targetDetectedDecision, chaseState);
		idleState.AddTransition(idleToChaseTransition);

		FsmTransition<Enemy> toMoveTransition = new FsmTransition<Enemy>(moveDecision, patrolState);
		idleState.AddTransition(toMoveTransition);

		FsmTransition<Enemy> chaseToAttackTransition = new FsmTransition<Enemy>(targetInAttackRangeDecision, attackState);
		chaseState.AddTransition(chaseToAttackTransition);

		FsmTransition<Enemy> attackToChaseTransition = new FsmTransition<Enemy>(targetOutsideAttackRangeDecision, chaseState);
		attackState.AddTransition(attackToChaseTransition);

		_stateMachine = new FsmStateMachine<Enemy>(this, patrolState);
	}

	private void Update()
	{
		_stateMachine.Update();
	}

	public void UpdateIdleEndTime(float idleDuration)
	{
		_idleEndTime = Time.time + idleDuration;
	}

	public bool IsTargetInRange(float range)
	{
		return _targeting != null && _targeting.IsTargetInRange(range);
	}

}
