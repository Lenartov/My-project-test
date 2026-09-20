using UnityEngine;

[CreateAssetMenu(fileName = "EnemySettings", menuName = "FSM/Enemy Settings")]
public sealed class EnemySettings : ScriptableObject
{
	[Header("Movement")]
	[SerializeField] private float _patrolSpeed = 10f;
	[SerializeField] private float _chaseSpeed = 12f;

	[Header("Patrol")]
	[SerializeField] private float _idleDuration = 5f;
	[SerializeField, Range(0f, 1f)] private float _idleProbability = 0.5f;

	[Header("Attack")]
	[SerializeField] private float _attackRange = 1.5f;
	[SerializeField] private float _attackDamage = 10f;
	[SerializeField] private float _attackInterval = 1f;

	[Header("Detection")]
	[SerializeField] private bool _isVisionEnabled = true;
	[SerializeField] private float _visionRange = 8f;
	[SerializeField, Range(0f, 360f)] private float _fieldOfViewAngle = 70f;
	[SerializeField] private float _visionDetectionDuration = 0.5f;
	[SerializeField] private bool _isProximityDetectionEnabled = true;
	[SerializeField] private float _proximityDetectionRange = 1f;
	[SerializeField] private float _proximityDetectionDuration = 0.5f;

	public float PatrolSpeed => _patrolSpeed;
	public float ChaseSpeed => _chaseSpeed;
	public float IdleDuration => _idleDuration;
	public float IdleProbability => _idleProbability;
	public float AttackRange => _attackRange;
	public float AttackDamage => _attackDamage;
	public float AttackInterval => _attackInterval;
	public bool IsVisionEnabled => _isVisionEnabled;
	public float VisionRange => _visionRange;
	public float FieldOfViewAngle => _fieldOfViewAngle;
	public float VisionDetectionDuration => _visionDetectionDuration;
	public bool IsProximityDetectionEnabled => _isProximityDetectionEnabled;
	public float ProximityDetectionRange => _proximityDetectionRange;
	public float ProximityDetectionDuration => _proximityDetectionDuration;
}
