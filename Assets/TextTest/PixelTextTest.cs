using UnityEngine;

public class PixelTextTest : MonoBehaviour
{
    public PixelTextMorpherGPU morpher;

	void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            morpher.MorphTo("START");

        if (Input.GetKeyDown(KeyCode.Alpha2))
            morpher.MorphTo("LEVEL 2");

        if (Input.GetKeyDown(KeyCode.Alpha3))
            morpher.MorphTo("GAME OVER");

        if (Input.GetKeyDown(KeyCode.Alpha4))
            morpher.MorphTo("A");

        if (Input.GetKeyDown(KeyCode.Alpha5))
            morpher.MorphTo("B");
    }
}