using UnityEngine;

namespace RealtimeCardPrototype
{
    internal readonly struct PrototypeCameraSettings
    {
        public PrototypeCameraSettings(Vector3 position)
        {
            Position = position;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation => Quaternion.Euler(54f, 0f, 0f);
        public float FieldOfView => 45f;
        public Color BackgroundColor => new Color(0.035f, 0.04f, 0.048f);
    }

    internal static class PrototypeSceneFactory
    {
        public static void ConfigureCombatPhysics()
        {
            Physics.defaultContactOffset = 0.02f;
            Physics.gravity = Vector3.zero;
        }

        public static Camera EnsureCamera(string name, PrototypeCameraSettings settings)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject(name);
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            camera.transform.SetPositionAndRotation(settings.Position, settings.Rotation);
            camera.fieldOfView = settings.FieldOfView;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = settings.BackgroundColor;
            if (!camera.TryGetComponent(out AudioListener _))
            {
                camera.gameObject.AddComponent<AudioListener>();
            }

            return camera;
        }

        public static void CreateDirectionalLight(string name)
        {
            GameObject lightObject = new GameObject(name);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
        }

        public static void EnsureActivityTimeController()
        {
            if (Object.FindFirstObjectByType<PlayerActivityTimeController>() == null)
            {
                GameObject timeObject = new GameObject("Player Activity Time Controller");
                timeObject.AddComponent<PlayerActivityTimeController>();
            }
        }

        public static void CreateTable(string name, Color color)
        {
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = name;
            table.transform.position = new Vector3(0f, -0.08f, -0.6f);
            table.transform.localScale = new Vector3(10.5f, 0.08f, 9.2f);
            table.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Create(color, 0.28f);
        }

        public static TextMesh CreateFloatingText(string name, Transform parent, Vector3 localPosition, float characterSize)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            TextMesh text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = characterSize;
            text.fontSize = 64;
            text.color = Color.white;
            textObject.GetComponent<MeshRenderer>().sortingOrder = 20;
            return text;
        }

        public static PlayerHandController CreatePlayerHand(string name)
        {
            GameObject hand = new GameObject(name);
            return hand.AddComponent<PlayerHandController>();
        }
    }
}
