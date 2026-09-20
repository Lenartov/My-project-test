using System.Collections.Generic;
using UnityEngine;

public sealed class EnemyPatrol : MonoBehaviour
{
	[SerializeField] private List<Transform> _points = new();

	private int _currentPointIndex;

	public Vector3 CurrentPoint => _points[_currentPointIndex].position;

	public void MoveToNextPoint()
	{
		_currentPointIndex++;

		if (_currentPointIndex >= _points.Count)
		{
			_currentPointIndex = 0;
		}
	}
}