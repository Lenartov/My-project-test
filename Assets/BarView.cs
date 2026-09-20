using UnityEngine;
using UnityEngine.UI;

public class BarView : MonoBehaviour
{
	private Slider slider;

	private void Awake()
	{
		slider = GetComponent<Slider>();
		slider.minValue = 0;
		slider.maxValue = 100;
	}

	public void SetBar(float data)
	{
		slider.value = data;
	}
}
