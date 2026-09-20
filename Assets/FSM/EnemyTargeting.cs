using UnityEngine;

[RequireComponent(typeof(Enemy))]
public sealed class EnemyTargeting : MonoBehaviour
{
	[SerializeField] private Transform _target;
	[SerializeField] private bool _drawDebugGizmos = true;

	public Transform Target => _target;
	public float TargetDistance => _target == null ? float.PositiveInfinity : Vector3.Distance(transform.position, _target.position);

	private Enemy _enemy;
	private float _targetVisibleSince = -1f;
	private float _targetInProximitySince = -1f;

	private EnemySettings Settings
	{
		get
		{
			if (_enemy == null)
			{
				_enemy = GetComponent<Enemy>();
			}

			return _enemy.Settings;
		}
	}

	public bool HasDetectedTarget
	{
		get
		{
			bool hasConfirmedVision = HasConfirmedVision();
			bool hasConfirmedProximity = HasConfirmedProximity();

			return hasConfirmedVision || hasConfirmedProximity;
		}
	}

	public bool CanSeeTarget()
	{
		return IsTargetInVisionCone() || IsTargetInProximityRange();
	}

	public bool IsTargetInVisionCone()
	{
		if (!Settings.IsVisionEnabled || !IsTargetInRange(Settings.VisionRange))
		{
			return false;
		}

		Vector3 directionToTarget = _target.position - transform.position;
		directionToTarget.y = 0f;

		return Vector3.Angle(transform.forward, directionToTarget) <= Settings.FieldOfViewAngle * 0.5f;
	}

	public bool IsTargetInProximityRange()
	{
		return Settings.IsProximityDetectionEnabled && IsTargetInRange(Settings.ProximityDetectionRange);
	}

	public bool IsTargetInRange(float range)
	{
		return TargetDistance <= range;
	}

	private bool HasConfirmedVision()
	{
		return HasConfirmedDetection(IsTargetInVisionCone(), ref _targetVisibleSince, Settings.VisionDetectionDuration);
	}

	private bool HasConfirmedProximity()
	{
		return HasConfirmedDetection(IsTargetInProximityRange(), ref _targetInProximitySince, Settings.ProximityDetectionDuration);
	}

	private bool HasConfirmedDetection(bool condition, ref float detectedSince, float detectionDuration)
	{
		if (!condition)
		{
			detectedSince = -1f;
			return false;
		}

		if (detectedSince < 0f)
		{
			detectedSince = Time.time;
		}

		return Time.time >= detectedSince + detectionDuration;
	}

	private void OnDrawGizmos()
	{
		if (!_drawDebugGizmos)
		{
			return;
		}

		Vector3 origin = transform.position;

		if (Settings.IsVisionEnabled)
		{
			Gizmos.color = IsTargetInVisionCone() ? Color.green : Color.yellow;
			DrawVisionCone(origin);
		}

		if (Settings.IsProximityDetectionEnabled)
		{
			Gizmos.color = IsTargetInProximityRange() ? Color.green : Color.cyan;
			DrawCircle(origin, Settings.ProximityDetectionRange);
		}

		if (_target != null)
		{
			Gizmos.color = CanSeeTarget() ? Color.green : Color.red;
			Gizmos.DrawLine(origin, _target.position);
		}
	}

	private void DrawVisionCone(Vector3 origin)
	{
		const int SegmentCount = 24;
		float halfAngle = Settings.FieldOfViewAngle * 0.5f;
		Vector3 previousPoint = GetVisionPoint(origin, -halfAngle);

		Gizmos.DrawLine(origin, previousPoint);

		for (int i = 1; i <= SegmentCount; i++)
		{
			float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)SegmentCount);
			Vector3 currentPoint = GetVisionPoint(origin, angle);
			Gizmos.DrawLine(previousPoint, currentPoint);
			previousPoint = currentPoint;
		}

		Gizmos.DrawLine(origin, previousPoint);
	}

	private Vector3 GetVisionPoint(Vector3 origin, float angle)
	{
		Vector3 forward = transform.forward;
		forward.y = 0f;
		forward.Normalize();

		return origin + Quaternion.AngleAxis(angle, Vector3.up) * forward * Settings.VisionRange;
	}

	private void DrawCircle(Vector3 origin, float radius)
	{
		const int SegmentCount = 32;
		Vector3 previousPoint = origin + Vector3.forward * radius;

		for (int i = 1; i <= SegmentCount; i++)
		{
			float angle = i * 360f / SegmentCount;
			Vector3 currentPoint = origin + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * radius;
			Gizmos.DrawLine(previousPoint, currentPoint);
			previousPoint = currentPoint;
		}
	}
}
