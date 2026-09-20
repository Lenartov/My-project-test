using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public sealed class EnemyMovement : MonoBehaviour
{
	private NavMeshAgent _agent;

	public bool IsDestinationReached => !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance;

	private void Awake()
	{
		_agent = GetComponent<NavMeshAgent>();
	}

	public void MoveTo(Vector3 destination, float speed)
	{
		_agent.speed = speed;
		_agent.isStopped = false;
		_agent.SetDestination(destination);
	}

	public void Stop()
	{
		_agent.isStopped = true;
	}
}