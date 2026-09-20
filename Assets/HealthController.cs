using UnityEngine;

public class HealthController : MonoBehaviour
{
	public BarView healthBarView;

	private HealtDataModel healthDataModel;

	private void Awake()
	{
		healthDataModel = new HealtDataModel() { Health = 100 };
	}

	private void Start()
	{
		healthBarView.SetBar(healthDataModel.Health);
	}

	public void TakeDamage()
	{
		int damage = 10;

		healthDataModel.TakeDamage(damage);
		healthBarView.SetBar(healthDataModel.Health);
	}
}
