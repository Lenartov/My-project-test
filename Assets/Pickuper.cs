using UnityEngine;
using UnityEngine.Events;

public class Pickuper : MonoBehaviour
{
    public UnityEvent onPickUpCoin;

    private void OnTriggerEnter(Collider other)
    {
        Destroy(other.gameObject);

        onPickUpCoin.Invoke();
    }
}
