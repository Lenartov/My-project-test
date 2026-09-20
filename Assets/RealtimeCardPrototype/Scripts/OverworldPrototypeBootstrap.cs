using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealtimeCardPrototype
{
    public sealed class OverworldPrototypeBootstrap : MonoBehaviour
    {
        private const float MapSize = 50f;

        private Camera overworldCamera;
        private OverworldCameraController cameraController;
        private GameObject popup;
        private Text popupTitle;
        private Text popupDescription;
        private OverworldDeckPanel deckPanel;

        private void Start()
        {
            CreateMap();
            CreateCamera();
            CreateLight();
            if (!OverworldProgress.IsTrainingMissionComplete)
            {
                CreateLocation("Training Grounds", "A lone raider has blocked the old road. Prepare your deck, then drive them away.", new Vector3(-6f, 0.35f, -1.5f), new Color(0.88f, 0.55f, 0.18f));
            }
            CreateInterface();
        }

        private void CreateMap()
        {
            GameObject map = GameObject.CreatePrimitive(PrimitiveType.Plane);
            map.name = "Valemere Overworld Map";
            map.transform.localScale = new Vector3(MapSize / 10f, 1f, MapSize / 10f);

            Texture2D mapTexture = Resources.Load<Texture2D>("Overworld/ValemereMap");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);

            material.mainTexture = mapTexture;
            material.mainTextureScale = new Vector2(1f, -1f);
            material.mainTextureOffset = new Vector2(0f, 1f);
            material.SetFloat("_Smoothness", 0f);
            map.GetComponent<Renderer>().material = material;
        }

        private void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Overworld Camera");
            cameraObject.tag = "MainCamera";
            overworldCamera = cameraObject.AddComponent<Camera>();
            overworldCamera.orthographic = true;
            overworldCamera.orthographicSize = 19f;
            overworldCamera.clearFlags = CameraClearFlags.SolidColor;
            overworldCamera.backgroundColor = new Color(0.06f, 0.12f, 0.16f);
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 31f, -19.4f), Quaternion.Euler(58f, 0f, 0f));

            cameraController = cameraObject.AddComponent<OverworldCameraController>();
            cameraController.Configure(MapSize);
            cameraController.LocationClicked += ShowPopup;
        }

        private void CreateLight()
        {
            GameObject lightObject = new GameObject("Overworld Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.74f, 0.8f, 0.72f);
        }

        private void CreateLocation(string title, string description, Vector3 position, Color color)
        {
            GameObject location = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            location.name = title + " Location";
            location.transform.position = position;
            location.transform.localScale = new Vector3(0.7f, 0.2f, 0.7f);
            location.GetComponent<Renderer>().material = CreateMarkerMaterial(color);
            location.AddComponent<OverworldLocation>().Configure(title, description);

            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "Marker";
            beacon.transform.SetParent(location.transform, false);
            beacon.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            beacon.transform.localScale = Vector3.one * 0.52f;
            beacon.GetComponent<Renderer>().material = CreateMarkerMaterial(color);
            Destroy(beacon.GetComponent<Collider>());

            TextMesh label = PrototypeSceneFactory.CreateFloatingText("Label", location.transform, new Vector3(0f, 0.08f, -0.7f), 0.12f);
            label.text = title.ToUpperInvariant();
            label.color = Color.white;
        }

        private Material CreateMarkerMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.color = color;
            return material;
        }

        private void CreateInterface()
        {
            CampaignUiFactory.EnsureEventSystem();
            GameObject canvasObject = new GameObject("Overworld UI");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            Text title = CampaignUiFactory.CreateText("Title", canvasObject.transform, 32, TextAnchor.UpperLeft, Color.white);
            title.fontStyle = FontStyle.Bold;
            title.text = "VALEMERE";
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(32f, -26f);
            title.rectTransform.sizeDelta = new Vector2(420f, 52f);

            Button deckButton = CreateButton("Open Deck", canvasObject.transform, new Vector2(-30f, -28f), "CARDS");
            RectTransform deckButtonRect = deckButton.GetComponent<RectTransform>();
            deckButtonRect.anchorMin = new Vector2(1f, 1f);
            deckButtonRect.anchorMax = new Vector2(1f, 1f);
            deckButtonRect.pivot = new Vector2(1f, 1f);
            deckButtonRect.sizeDelta = new Vector2(150f, 48f);

            deckPanel = canvasObject.AddComponent<OverworldDeckPanel>();
            deckPanel.Initialize(cameraController);
            deckButton.onClick.AddListener(deckPanel.Show);

            popup = new GameObject("Location Popup");
            popup.transform.SetParent(canvasObject.transform, false);
            Image panel = popup.AddComponent<Image>();
            panel.color = new Color(0.035f, 0.055f, 0.075f, 0.94f);
            RectTransform popupRect = popup.GetComponent<RectTransform>();
            popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupRect.pivot = new Vector2(0.5f, 0.5f);
            popupRect.sizeDelta = new Vector2(640f, 370f);

            popupTitle = CampaignUiFactory.CreateText("Popup Title", popup.transform, 30, TextAnchor.UpperCenter, Color.white);
            popupTitle.fontStyle = FontStyle.Bold;
            popupTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            popupTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            popupTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            popupTitle.rectTransform.anchoredPosition = new Vector2(0f, -35f);
            popupTitle.rectTransform.sizeDelta = new Vector2(-56f, 54f);

            popupDescription = CampaignUiFactory.CreateText("Popup Description", popup.transform, 20, TextAnchor.MiddleCenter, new Color(0.86f, 0.91f, 0.9f));
            popupDescription.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            popupDescription.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            popupDescription.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            popupDescription.rectTransform.sizeDelta = new Vector2(470f, 130f);

            Button closeButton = CreateButton("Cancel", popup.transform, new Vector2(0f, -142f), "CANCEL");
            closeButton.onClick.AddListener(HidePopup);
            Button editDeckButton = CreateButton("Edit Deck", popup.transform, new Vector2(-108f, -82f), "EDIT DECK");
            editDeckButton.onClick.AddListener(OpenDeck);
            Button startBattleButton = CreateButton("Start Battle", popup.transform, new Vector2(108f, -82f), "START BATTLE");
            startBattleButton.onClick.AddListener(StartBattle);

            Button dismissButton = CreateButton("Dismiss", popup.transform, new Vector2(280f, 140f), "X");
            RectTransform dismissRect = dismissButton.GetComponent<RectTransform>();
            dismissRect.anchorMin = new Vector2(1f, 1f);
            dismissRect.anchorMax = new Vector2(1f, 1f);
            dismissRect.pivot = new Vector2(1f, 1f);
            dismissRect.anchoredPosition = new Vector2(-18f, -18f);
            dismissRect.sizeDelta = new Vector2(44f, 44f);
            dismissButton.onClick.AddListener(HidePopup);
            popup.SetActive(false);
        }

        private Button CreateButton(string name, Transform parent, Vector2 position, string label)
        {
            GameObject buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.48f, 0.56f);
            Button button = buttonObject.AddComponent<Button>();
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(180f, 48f);
            Text text = CampaignUiFactory.CreateText("Label", buttonObject.transform, 18, TextAnchor.MiddleCenter, Color.white);
            text.fontStyle = FontStyle.Bold;
            text.text = label;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private void ShowPopup(OverworldLocation location)
        {
            popupTitle.text = location.Title;
            popupDescription.text = location.Description;
            popup.SetActive(true);
            cameraController.enabled = false;
        }

        private void HidePopup()
        {
            popup.SetActive(false);
            cameraController.enabled = true;
        }

        private void OpenDeck()
        {
            HidePopup();
            deckPanel.Show();
        }

        private void StartBattle()
        {
            SceneManager.LoadScene("OverworldTrainingBattle");
        }
    }

    public sealed class OverworldLocation : MonoBehaviour
    {
        public string Title { get; private set; }
        public string Description { get; private set; }

        public void Configure(string title, string description)
        {
            Title = title;
            Description = description;
        }
    }

    public sealed class OverworldCameraController : MonoBehaviour
    {
        private const float ZoomSpeed = 3f;
        private const float MinZoom = 4.5f;
        private const float MaxZoom = 12f;
        private const float DragThreshold = 0.08f;
        private const float MapEdgePadding = 0.35f;

        private Camera controlledCamera;
        private float mapHalfSize;
        private Vector3 dragStartGroundPoint;
        private bool isDragging;

        public event System.Action<OverworldLocation> LocationClicked;

        public void Configure(float mapSize)
        {
            controlledCamera = GetComponent<Camera>();
            mapHalfSize = mapSize * 0.5f;
        }

        private void Update()
        {
            float zoom = Input.mouseScrollDelta.y;
            controlledCamera.orthographicSize = Mathf.Clamp(controlledCamera.orthographicSize - zoom * ZoomSpeed, MinZoom, MaxZoom);

            if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
            {
                isDragging = false;
                TryGetGroundPoint(Input.mousePosition, out dragStartGroundPoint);
            }

            if (Input.GetMouseButton(0) && TryGetGroundPoint(Input.mousePosition, out Vector3 currentGroundPoint))
            {
                Vector3 displacement = dragStartGroundPoint - currentGroundPoint;
                if (displacement.sqrMagnitude > DragThreshold * DragThreshold)
                {
                    isDragging = true;
                }

                if (isDragging)
                {
                    transform.position += displacement;
                    ClampToMapBounds();
                }
            }

            if (Input.GetMouseButtonUp(0) && !isDragging && !EventSystem.current.IsPointerOverGameObject())
            {
                Ray ray = controlledCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.TryGetComponent(out OverworldLocation location))
                {
                    LocationClicked?.Invoke(location);
                }
            }

            ClampToMapBounds();
        }

        private bool TryGetGroundPoint(Vector3 screenPosition, out Vector3 groundPoint)
        {
            Ray ray = controlledCamera.ScreenPointToRay(screenPosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float distance))
            {
                groundPoint = ray.GetPoint(distance);
                return true;
            }

            groundPoint = Vector3.zero;
            return false;
        }

        private void ClampToMapBounds()
        {
            TryGetGroundPoint(new Vector3(0f, 0f), out Vector3 bottomLeft);
            TryGetGroundPoint(new Vector3(Screen.width, 0f), out Vector3 bottomRight);
            TryGetGroundPoint(new Vector3(0f, Screen.height), out Vector3 topLeft);
            TryGetGroundPoint(new Vector3(Screen.width, Screen.height), out Vector3 topRight);

            float minX = Mathf.Min(bottomLeft.x, bottomRight.x, topLeft.x, topRight.x);
            float maxX = Mathf.Max(bottomLeft.x, bottomRight.x, topLeft.x, topRight.x);
            float minZ = Mathf.Min(bottomLeft.z, bottomRight.z, topLeft.z, topRight.z);
            float maxZ = Mathf.Max(bottomLeft.z, bottomRight.z, topLeft.z, topRight.z);
            float edge = mapHalfSize - MapEdgePadding;
            Vector3 position = transform.position;

            if (minX < -edge) position.x += -edge - minX;
            else if (maxX > edge) position.x += edge - maxX;
            if (minZ < -edge) position.z += -edge - minZ;
            else if (maxZ > edge) position.z += edge - maxZ;

            transform.position = position;
        }
    }
}
