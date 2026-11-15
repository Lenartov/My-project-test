using UnityEngine;

public class DoTweenTest : MonoBehaviour
{
    [SerializeField] private AnimationCurve curve;

    void Update()
    {
		float value = curve.Evaluate(Time.time);
        transform.position = new Vector3(transform.position.x, value, transform.position.z);
	}
}
