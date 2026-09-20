using System;

public class HealtDataModel
{
	public float Health {get; set; }

	public void TakeDamage(float damage)
	{
		Health -= damage;
		if (Health < 0)
		{
			Health = 0;
		}
	}
}
