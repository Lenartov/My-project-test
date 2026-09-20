using System.Collections.Generic;
using UnityEngine;

public class InventarSingle : MonoBehaviour
{
	public static InventarSingle Instance;

	public int Money { get; set; }
	public List<string> Items { get; set; } = new List<string>();

	private void Awake()
	{
		if(Instance == null)
		{
			Instance = this;
			DontDestroyOnLoad(this);
		}
		else
		{
			Destroy(gameObject);
		}
	}
}
