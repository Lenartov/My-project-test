using System.Collections;
using TMPro;
using UnityEngine;

public class ColorChanger : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI text;
	private MeshRenderer meshRenderer;

	private void Start()
	{
		meshRenderer = GetComponent<MeshRenderer>();

		StartCoroutine(Timer());
	}

	private IEnumerator Timer()
	{
		float timer = 10f;

		while (true)
		{
			timer -= Time.deltaTime;


			text.text = timer.ToString("F2");
			yield return null;
		}
	}

	private IEnumerator ChangeColor()
	{
		WaitForSeconds wait = new WaitForSeconds(0.5f);

		while (true)
		{
			yield return wait;
			meshRenderer.material.color = Color.green;
			yield return wait;
			meshRenderer.material.color = Color.blue;
			StartCoroutine(ChangePosition());
			meshRenderer.material.color = Color.red;
		}
	}

	private void OnControllerColliderHit(ControllerColliderHit hit)
	{
		
	}

	private IEnumerator ChangePosition()
	{
		yield return new WaitForSeconds(0.5f);
		transform.position = Random.insideUnitSphere * 5f;
	} 

	private bool IsPositionValid()
	{
		return meshRenderer.material.color == Color.red;
	}
}
