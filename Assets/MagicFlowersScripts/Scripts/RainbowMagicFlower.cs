using UnityEngine;

public class RainbowMagicFlower : MagicFlower
{
	[SerializeField] private Renderer[] petals;

	private readonly Color[] rainbowColors =
	{
		Color.red,
		new Color(1f, 0.5f, 0f), // orange
        Color.yellow,
		Color.green,
		Color.cyan,
		Color.blue,
		new Color(0.5f, 0f, 1f)  // violet
    };

	private int colorOffset = 0;

	public override void Water()
	{
		if (petals == null || petals.Length == 0)
		{
			Debug.LogWarning($"{flowerName}: Petals array is empty.");
			return;
		}

		for (int i = 0; i < petals.Length; i++)
		{
			if (petals[i] == null)
				continue;

			int colorIndex = (i + colorOffset) % rainbowColors.Length;
			petals[i].material.color = rainbowColors[colorIndex];
		}

		colorOffset = (colorOffset + 1) % rainbowColors.Length;
	}
}