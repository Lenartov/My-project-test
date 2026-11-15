using DG.Tweening;
using UnityEngine;

public class RotateTest : MonoBehaviour
{
    void Start()
    {
        transform.DORotate(new Vector3(0, 360, 0), 2f, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart);
	}
}
