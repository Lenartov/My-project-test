using TMPro;
using UnityEngine;

public class Counter : MonoBehaviour
{
    private TextMeshProUGUI textField;

    private int value = 0;

    private void Awake()
    {
        textField = GetComponent<TextMeshProUGUI>();
        textField.text = value.ToString();
	}

    public void Count()
    {
        value++;
        textField.text = value.ToString();
    }
}
