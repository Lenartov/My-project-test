using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveObjectPosition : MonoBehaviour
{
	public List<GameObject> objectsToSave;

	public void Update()
	{
		if (Input.GetKeyDown(KeyCode.S))
		{
			SavePosition();
		}
		else if (Input.GetKeyDown(KeyCode.L))
		{
			LoadPosition();
		}
	}

	public void SavePosition()
	{
		List<ObjectTransform> objectTransforms = new List<ObjectTransform>();

		foreach (GameObject obj in objectsToSave)
		{
			ObjectTransform objectTransform = new ObjectTransform(
				obj.transform.position,
				obj.transform.eulerAngles,
				obj.transform.localScale);

			objectTransforms.Add(objectTransform);
		}

		ListObjectTransform listObjectTransform = new ListObjectTransform(objectTransforms);

		string json = JsonUtility.ToJson(listObjectTransform);
		File.WriteAllText(Application.persistentDataPath + "objectsPositions.json", json);
	}

	public void LoadPosition()
	{
		if (!File.Exists(Application.persistentDataPath + "objectsPositions.json"))
		{
			Debug.LogWarning("No saved positions found.");
			return; 
		}

		string json = File.ReadAllText(Application.persistentDataPath + "objectsPositions.json");
		ListObjectTransform listObjectTransform = JsonUtility.FromJson<ListObjectTransform>(json);

		for (int i = 0; i < objectsToSave.Count; i++)
		{
			ObjectTransform objectTransform = listObjectTransform.objectTransforms[i];

			objectsToSave[i].transform.position = objectTransform.position;
			objectsToSave[i].transform.eulerAngles = objectTransform.rotation;
			objectsToSave[i].transform.localScale = objectTransform.scale;
		}
	}
}

[Serializable]
public class ListObjectTransform
{
	public List<ObjectTransform> objectTransforms;

	public ListObjectTransform(List<ObjectTransform> objectTransforms)
	{
		this.objectTransforms = objectTransforms;
	}
}

[Serializable]
public class  ObjectTransform
{
	public Vector3 position;
	public Vector3 rotation;
	public Vector3 scale;

	public ObjectTransform(Vector3 position, Vector3 rotation, Vector3 scale)
	{
		this.position = position;
		this.rotation = rotation;
		this.scale = scale;
	}
}