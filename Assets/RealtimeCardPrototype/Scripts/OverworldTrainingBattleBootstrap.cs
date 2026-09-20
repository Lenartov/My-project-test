using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealtimeCardPrototype
{
    public sealed class OverworldTrainingBattleBootstrap : MonoBehaviour
    {
        private EnemyDummyCard enemy;
        private GameObject resultPopup;

        private void Start()
        {
            CombatTargetRegistry.ClearAll();
            PrototypeSceneFactory.ConfigureCombatPhysics();
            PrototypeSceneFactory.EnsureCamera("Training Battle Camera", new PrototypeCameraSettings(new Vector3(0f, 8.8f, -6.6f)));
            PrototypeSceneFactory.CreateDirectionalLight("Training Battle Key Light");
            PrototypeSceneFactory.EnsureActivityTimeController();
            PrototypeSceneFactory.CreateTable("Training Battle Table", new Color(0.09f, 0.12f, 0.14f));
            CreatePlayerHand();
            CreateEnemy();
            CreateResultPopup();
        }

        private void CreatePlayerHand()
        {
            PlayerHandController controller = PrototypeSceneFactory.CreatePlayerHand("Training Battle Player Hand");
            controller.ConfigureMana(5, 5);
            controller.ConfigureDeckCounts(
                normal: OverworldDeckState.GetActiveCardCount("strike"),
                heavy: OverworldDeckState.GetActiveCardCount("heavy"),
                splash: OverworldDeckState.GetActiveCardCount("blast"),
                shock: OverworldDeckState.GetActiveCardCount("shock"),
                poison: 0,
                pierce: OverworldDeckState.GetActiveCardCount("pierce"),
                graviton: 0,
                shieldBlocker: OverworldDeckState.GetActiveCardCount("block"),
                power: 0,
                manaGenerator: 0,
                manaCapacity: 0,
                startingHand: 5,
                maxHand: 5);
        }

        private void CreateEnemy()
        {
            enemy = EnemyCardFactory.Create(new EnemyCardSpawnSettings(
                "Training Raider",
                "RAIDER",
                new Vector3(0f, 0.18f, 2.4f),
                new Vector2(1.05f, 1.5f),
                new Color(0.72f, 0.2f, 0.16f),
                0f,
                1f,
                3f,
                30f,
                30f,
                0f,
                0.132f,
                0.2f,
                0.044f));
            enemy.Died += HandleEnemyDefeated;
        }

        private void HandleEnemyDefeated(EnemyDummyCard defeatedEnemy)
        {
            defeatedEnemy.Died -= HandleEnemyDefeated;
            OverworldProgress.CompleteTrainingMission();
            resultPopup.SetActive(true);
        }

        private void CreateResultPopup()
        {
            CampaignUiFactory.EnsureEventSystem();
            GameObject canvasObject = new GameObject("Training Battle Result UI");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            resultPopup = new GameObject("Victory Popup");
            resultPopup.transform.SetParent(canvasObject.transform, false);
            Image panel = resultPopup.AddComponent<Image>();
            panel.color = new Color(0.03f, 0.06f, 0.08f, 0.96f);
            RectTransform popupRect = resultPopup.GetComponent<RectTransform>();
            popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupRect.pivot = new Vector2(0.5f, 0.5f);
            popupRect.sizeDelta = new Vector2(560f, 310f);

            Text title = CampaignUiFactory.CreateText("Victory Title", resultPopup.transform, 34, TextAnchor.UpperCenter, Color.white);
            title.fontStyle = FontStyle.Bold;
            title.text = "VICTORY";
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(480f, 60f), new Vector2(0.5f, 1f));

            Text description = CampaignUiFactory.CreateText("Victory Description", resultPopup.transform, 20, TextAnchor.MiddleCenter, new Color(0.84f, 0.92f, 0.91f));
            description.text = "The road is clear.\nREWARD UNLOCKED: STORM MAGE\nNew cards are now available in your collection.";
            SetRect(description.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(450f, 100f), new Vector2(0.5f, 0.5f));

            Button returnButton = CreateButton("Return To Map", resultPopup.transform, "RETURN TO MAP");
            SetRect(returnButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(210f, 48f), new Vector2(0.5f, 0f));
            returnButton.onClick.AddListener(ReturnToMap);
            resultPopup.SetActive(false);
        }

        private void ReturnToMap()
        {
            SceneManager.LoadScene("OverworldPrototype");
        }

        private static Button CreateButton(string name, Transform parent, string label)
        {
            GameObject buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.2f, 0.48f, 0.56f);
            Button button = buttonObject.AddComponent<Button>();
            Text text = CampaignUiFactory.CreateText("Label", buttonObject.transform, 18, TextAnchor.MiddleCenter, Color.white);
            text.fontStyle = FontStyle.Bold;
            text.text = label;
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
