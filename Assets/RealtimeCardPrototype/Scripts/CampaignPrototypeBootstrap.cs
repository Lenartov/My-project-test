using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealtimeCardPrototype
{
    public sealed class CampaignPrototypeBootstrap : MonoBehaviour
    {
        private const int TotalWaves = 5;

        [SerializeField] private Vector2 enemyCardSize = new Vector2(1.05f, 1.5f);
        [SerializeField] private Color tableColor = new Color(0.09f, 0.12f, 0.14f);

        private readonly List<EnemyDummyCard> activeEnemies = new List<EnemyDummyCard>();
        private readonly List<CardDefinition> rewardPool = new List<CardDefinition>();

        private PlayerHandController playerHand;
        private CampaignProgressView progressView;
        private CampaignRewardChoiceView rewardChoiceView;
        private int currentWave;
        private bool isWaitingForReward;
        private bool isComplete;

        private void Start()
        {
            CombatTargetRegistry.ClearAll();
            PrototypeSceneFactory.ConfigureCombatPhysics();
            PrototypeSceneFactory.EnsureCamera("Campaign Prototype Camera", new PrototypeCameraSettings(new Vector3(0f, 9.1f, -6.9f)));
            PrototypeSceneFactory.CreateDirectionalLight("Campaign Prototype Key Light");
            PrototypeSceneFactory.EnsureActivityTimeController();
            PrototypeSceneFactory.CreateTable("Campaign Prototype Table", tableColor);

            playerHand = PrototypeSceneFactory.CreatePlayerHand("Campaign Player Hand");
            playerHand.ConfigureDeckCounts(
                normal: 3,
                heavy: 1,
                splash: 0,
                shock: 0,
                poison: 0,
                pierce: 0,
                graviton: 0,
                shieldBlocker: 1,
                power: 0,
                manaGenerator: 0,
                manaCapacity: 0,
                startingHand: 5,
                maxHand: 5);

            BuildRewardPool();
            progressView = new CampaignProgressView(transform);
            progressView.Create();
            rewardChoiceView = new CampaignRewardChoiceView(transform);
            StartNextWave();
        }

        private void Update()
        {
            if (progressView == null)
            {
                return;
            }

            progressView.Refresh(currentWave, TotalWaves, CountLivingEnemies(), isWaitingForReward, isComplete);
        }

        private void OnDestroy()
        {
            PlayerActivityTimeController.ReportPause(this, false);
        }

        private void StartNextWave()
        {
            currentWave++;
            activeEnemies.Clear();
            isWaitingForReward = false;
            SpawnWave(currentWave);
        }

        private void SpawnWave(int wave)
        {
            int enemyCount = wave == 1 ? 1 : Mathf.Min(3, 1 + (wave / 2) + UnityEngine.Random.Range(0, 2));
            float health = 16f + wave * 7f;
            float balance = 13f + wave * 3f;
            float attackChargeSeconds = Mathf.Max(3.4f, 5.6f - wave * 0.35f);
            float attackDamage = 0.8f + wave * 0.35f;

            for (int i = 0; i < enemyCount; i++)
            {
                float centeredIndex = i - (enemyCount - 1) * 0.5f;
                float x = centeredIndex * 2.35f + UnityEngine.Random.Range(-0.18f, 0.18f);
                float z = 1.85f + UnityEngine.Random.Range(-0.2f, 0.3f);
                float healthVariation = UnityEngine.Random.Range(0.88f, 1.16f);
                Color color = Color.Lerp(new Color(0.78f, 0.2f, 0.18f), new Color(0.6f, 0.08f, 0.38f), (wave - 1f) / (TotalWaves - 1f));
                EnemyDummyCard enemy = EnemyCardFactory.Create(new EnemyCardSpawnSettings(
                    $"Wave {wave} Enemy {i + 1}",
                    GetEnemyTitle(wave, i),
                    new Vector3(x, 0.18f, z),
                    enemyCardSize,
                    color,
                    UnityEngine.Random.Range(-7f, 7f),
                    health * healthVariation,
                    balance,
                    3.5f + i * 0.7f,
                    attackChargeSeconds,
                    attackDamage,
                    0.132f,
                    0.2f,
                    0.044f));
                ConfigureEnemyActions(enemy, wave, i);
                enemy.Died += HandleEnemyDied;
                activeEnemies.Add(enemy);
            }
        }

        private void HandleEnemyDied(EnemyDummyCard enemy)
        {
            if (enemy != null)
            {
                enemy.Died -= HandleEnemyDied;
            }

            if (isWaitingForReward || isComplete || CountLivingEnemies() > 0)
            {
                return;
            }

            HandleWaveCleared();
        }

        private void HandleWaveCleared()
        {
            if (currentWave >= TotalWaves)
            {
                isComplete = true;
                return;
            }

            isWaitingForReward = true;
            PlayerActivityTimeController.ReportPause(this, true);
            rewardChoiceView.Show(currentWave, CreateRewardChoices(), HandleRewardSelected);
        }

        private void HandleRewardSelected(CardDefinition definition)
        {
            playerHand.AddCardToDeck(definition);
            rewardChoiceView.Hide();
            PlayerActivityTimeController.ReportPause(this, false);
            StartNextWave();
        }

        private int CountLivingEnemies()
        {
            int livingEnemies = 0;
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                EnemyDummyCard enemy = activeEnemies[i];
                if (enemy != null && !enemy.IsDead)
                {
                    livingEnemies++;
                }
            }

            return livingEnemies;
        }

        private List<CardDefinition> CreateRewardChoices()
        {
            List<CardDefinition> choices = new List<CardDefinition>(3);
            List<int> availableIndices = new List<int>(rewardPool.Count);
            for (int i = 0; i < rewardPool.Count; i++)
            {
                availableIndices.Add(i);
            }

            for (int i = 0; i < 3; i++)
            {
                int selectedIndex = UnityEngine.Random.Range(0, availableIndices.Count);
                choices.Add(rewardPool[availableIndices[selectedIndex]]);
                availableIndices.RemoveAt(selectedIndex);
            }

            return choices;
        }

        private void ConfigureEnemyActions(EnemyDummyCard enemy, int wave, int enemyIndex)
        {
            EnemyActionStepConfiguration attack = new EnemyActionStepConfiguration(
                EnemyActionKind.Attack,
                EnemyAttackTargetMode.Player,
                1f,
                1f);
            EnemyActionStepConfiguration burstAttack = new EnemyActionStepConfiguration(
                EnemyActionKind.Attack,
                EnemyAttackTargetMode.Player,
                1f,
                0.65f,
                2,
                0.3f);
            EnemyActionStepConfiguration wait = new EnemyActionStepConfiguration(
                EnemyActionKind.Wait,
                EnemyAttackTargetMode.Player,
                0.75f,
                0f);
            EnemyActionStepConfiguration heal = new EnemyActionStepConfiguration(
                EnemyActionKind.Heal,
                EnemyAttackTargetMode.Player,
                0.8f,
                1.2f);

            if (wave <= 2)
            {
                enemy.ConfigureActionCycle(attack, wait);
                return;
            }

            if (wave == 3 || enemyIndex == 0)
            {
                enemy.ConfigureActionCycle(attack, wait, heal);
                return;
            }

            enemy.ConfigureActionCycle(burstAttack, wait, heal);
        }

        private void BuildRewardPool()
        {
            PrototypeCardPalette palette = new PrototypeCardPalette(
                Color.white,
                new Color(0.72f, 0.24f, 0.82f),
                new Color(0.92f, 0.06f, 0.04f),
                new Color(0.1f, 0.46f, 1f),
                new Color(0.16f, 0.68f, 0.26f),
                new Color(0.12f, 0.82f, 0.86f),
                Color.black,
                new Color(0.78f, 0.82f, 0.84f),
                new Color(0.92f, 0.68f, 0.12f),
                new Color(0.18f, 0.76f, 0.95f),
                new Color(0.38f, 0.52f, 1f));
            PrototypeDeckCounts counts = new PrototypeDeckCounts(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, turret: 1);
            rewardPool.AddRange(PrototypeDeckBuilder.Create(counts, palette));
        }

        private string GetEnemyTitle(int wave, int index)
        {
            string[] titles = { "SCOUT", "RAIDER", "BRUTE", "VETERAN", "ELITE" };
            return index == 0 ? titles[wave - 1] : $"{titles[wave - 1]} +";
        }
    }

    internal sealed class CampaignProgressView
    {
        private readonly Transform parent;
        private Text statusText;

        public CampaignProgressView(Transform parent)
        {
            this.parent = parent;
        }

        public void Create()
        {
            GameObject canvasObject = new GameObject("Campaign Progress UI");
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            statusText = CampaignUiFactory.CreateText("Campaign Status", canvasObject.transform, 19, TextAnchor.UpperRight, Color.white);
            statusText.fontStyle = FontStyle.Bold;
            RectTransform rect = statusText.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -18f);
            rect.sizeDelta = new Vector2(250f, 80f);
        }

        public void Refresh(int wave, int totalWaves, int livingEnemies, bool isWaitingForReward, bool isComplete)
        {
            if (isComplete)
            {
                statusText.text = "RUN COMPLETE\n5 waves cleared";
                return;
            }

            if (isWaitingForReward)
            {
                statusText.text = $"WAVE {wave}/{totalWaves}\nChoose a new card";
                return;
            }

            statusText.text = $"WAVE {wave}/{totalWaves}\nEnemies left: {livingEnemies}";
        }
    }

    internal sealed class CampaignRewardChoiceView
    {
        private readonly Transform parent;
        private GameObject root;
        private Text heading;
        private Action<CardDefinition> onCardSelected;

        public CampaignRewardChoiceView(Transform parent)
        {
            this.parent = parent;
        }

        public void Show(int completedWave, List<CardDefinition> choices, Action<CardDefinition> selectionHandler)
        {
            EnsureCreated();
            onCardSelected = selectionHandler;
            heading.text = $"WAVE {completedWave} CLEAR\nCHOOSE ONE CARD";
            CreateChoiceButtons(choices);
            root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private void EnsureCreated()
        {
            if (root != null)
            {
                return;
            }

            CampaignUiFactory.EnsureEventSystem();
            root = new GameObject("Campaign Reward Choice");
            root.transform.SetParent(parent, false);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            Image background = root.AddComponent<Image>();
            background.color = new Color(0.015f, 0.02f, 0.03f, 0.88f);

            heading = CampaignUiFactory.CreateText("Reward Heading", root.transform, 30, TextAnchor.MiddleCenter, Color.white);
            heading.fontStyle = FontStyle.Bold;
            RectTransform headingRect = heading.rectTransform;
            headingRect.anchorMin = new Vector2(0.5f, 0.5f);
            headingRect.anchorMax = new Vector2(0.5f, 0.5f);
            headingRect.pivot = new Vector2(0.5f, 0.5f);
            headingRect.anchoredPosition = new Vector2(0f, 190f);
            headingRect.sizeDelta = new Vector2(760f, 100f);
            root.SetActive(false);
        }

        private void CreateChoiceButtons(List<CardDefinition> choices)
        {
            Transform existingChoices = root.transform.Find("Choices");
            if (existingChoices != null)
            {
                UnityEngine.Object.Destroy(existingChoices.gameObject);
            }

            GameObject choicesObject = new GameObject("Choices");
            choicesObject.transform.SetParent(root.transform, false);
            for (int i = 0; i < choices.Count; i++)
            {
                CardDefinition definition = choices[i];
                GameObject buttonObject = new GameObject($"Choose {definition.Title}");
                buttonObject.transform.SetParent(choicesObject.transform, false);
                RectTransform rect = buttonObject.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2((i - 1) * 270f, -20f);
                rect.sizeDelta = new Vector2(230f, 310f);

                Image image = buttonObject.AddComponent<Image>();
                image.color = definition.Color;
                Button button = buttonObject.AddComponent<Button>();
                ColorBlock colors = button.colors;
                colors.normalColor = definition.Color;
                colors.highlightedColor = Color.Lerp(definition.Color, Color.white, 0.2f);
                colors.pressedColor = Color.Lerp(definition.Color, Color.black, 0.22f);
                button.colors = colors;
                button.onClick.AddListener(() => onCardSelected(definition));

                Color textColor = CampaignUiFactory.GetReadableTextColor(definition.Color);
                Text label = CampaignUiFactory.CreateText("Card Label", buttonObject.transform, 25, TextAnchor.MiddleCenter, textColor);
                label.fontStyle = FontStyle.Bold;
                label.text = $"{definition.Title}\n\n{definition.Body}\n\nADD TO DECK";
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(14f, 14f);
                labelRect.offsetMax = new Vector2(-14f, -14f);
            }
        }
    }

    internal static class CampaignUiFactory
    {
        public static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment, Color color)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.font = GetBuiltinFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        public static Color GetReadableTextColor(Color backgroundColor)
        {
            float luminance = backgroundColor.r * 0.2126f + backgroundColor.g * 0.7152f + backgroundColor.b * 0.0722f;
            return luminance > 0.62f ? Color.black : Color.white;
        }

        private static Font GetBuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
