using UnityEngine;

public class TestFindEnemy : MonoBehaviour
{
	private GameObject[] enemies;
	private float cooldown = 0.5f;
	private float timer = 0f;

	private void Start()
	{
		Vector2 playerPos = transform.position;
		float radius = 10f;
		enemies = new GameObject[10000];
		for (int i = 0; i < 10000; i++)
		{
			GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Cube);
			enemy.transform.position = playerPos + Random.insideUnitCircle * radius;
			enemy.tag = "Enemy";
			enemies[i] = enemy;
		}
	}

	private void Update()
	{
		timer += Time.deltaTime;

		if (timer < cooldown)
		{
			return;
		}
		timer = 0f;

		int radius = 1;
		Collider[] enemyColliders = Physics.OverlapSphere(transform.position, radius);

		while (enemyColliders.Length == 1 && radius < 100)
		{
			radius += 2;
			enemyColliders = Physics.OverlapSphere(transform.position, 1f);
		}

		Physics.OverlapSphere(transform.position, 1f);
		
		if(enemyColliders.Length == 0)
		{
			return;
		}

		float minDistance = float.MaxValue;
		GameObject closestEnemy = null;

		foreach (Collider enemyCollider in enemyColliders)
		{
			if(enemyCollider.tag != "Enemy")
			{
				continue;
			}

			float distance = Vector2.Distance(transform.position, enemyCollider.transform.position);
			if(distance < minDistance)
			{
				minDistance = distance;
				closestEnemy = enemyCollider.gameObject;
			}
		}

		if (closestEnemy != null)
		{
			closestEnemy.GetComponent<Renderer>().material.color = Random.ColorHSV();
		}
	}
}
