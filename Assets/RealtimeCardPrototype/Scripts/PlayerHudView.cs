using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealtimeCardPrototype
{
    internal sealed class PlayerHudView
    {
        private const float DamageFlashFadeSpeed = 0.75f;

        private readonly Transform parent;
        private readonly Action advanceTimers;
        private Text deckStatusText;
        private Text playerHealthText;
        private Text damageWindowText;
        private Image damageFlashImage;
        private float damageFlashAlpha;

        public PlayerHudView(Transform parent, Action advanceTimers)
        {
            this.parent = parent;
            this.advanceTimers = advanceTimers;
        }

        public void Create()
        {
            EnsureEventSystem();

            GameObject canvasObject = new GameObject("Prototype UI");
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            deckStatusText = CreateStatusText(canvasObject.transform);
            playerHealthText = CreatePlayerHealthText(canvasObject.transform);
            damageWindowText = CreateDamageWindowText(canvasObject.transform);
            CreateAdvanceButton(canvasObject.transform);
            damageFlashImage = CreateDamageFlash(canvasObject.transform);
        }

        public void RefreshDeck(CardDeckRuntime deck, PlayerManaState mana, int handCount, int maxHandSize, float nextDrawSeconds)
        {
            if (deckStatusText == null || deck == null)
            {
                return;
            }

            string drawLabel = handCount >= maxHandSize ? "hand full" : $"{nextDrawSeconds:0.0}s";
            deckStatusText.text = $"Mana {mana.CurrentMana}/{mana.EffectiveMaxMana} ({mana.EffectiveRegenInterval:0.00}s)  Deck {deck.DrawCount}  Hand {handCount}/{maxHandSize}  Discard {deck.DiscardCount}  Draw {drawLabel}";
        }

        public void RefreshHealth(PlayerHealthState health)
        {
            if (playerHealthText != null)
            {
                playerHealthText.text = $"HP {health.CurrentHealth:0}/{health.MaxHealth:0}";
            }
        }

        public void RefreshDamageWindow(float windowSeconds)
        {
            if (damageWindowText != null)
            {
                float damage = DamageWindowTracker.GetRecentDamage(windowSeconds);
                damageWindowText.text = $"DMG {windowSeconds:0}s\n{damage:0.0}";
            }
        }

        public void ShowDamageFlash(float alpha)
        {
            damageFlashAlpha = Mathf.Max(damageFlashAlpha, alpha);
            UpdateDamageFlash();
        }

        public void Tick(float deltaTime)
        {
            if (damageFlashAlpha <= 0f)
            {
                return;
            }

            damageFlashAlpha = Mathf.Max(0f, damageFlashAlpha - deltaTime * DamageFlashFadeSpeed);
            UpdateDamageFlash();
        }

        private Text CreateStatusText(Transform canvas)
        {
            Text text = CreateText("Deck Status", canvas, 14, TextAnchor.UpperCenter, Color.white);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -18f);
            rect.sizeDelta = new Vector2(880f, 34f);
            return text;
        }

        private Text CreatePlayerHealthText(Transform canvas)
        {
            Text text = CreateText("Player Health", canvas, 18, TextAnchor.UpperLeft, new Color(1f, 0.88f, 0.84f, 1f));
            text.fontStyle = FontStyle.Bold;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -18f);
            rect.sizeDelta = new Vector2(220f, 36f);
            return text;
        }

        private Text CreateDamageWindowText(Transform canvas)
        {
            Text text = CreateText("Recent Damage Meter", canvas, 18, TextAnchor.MiddleLeft, new Color(0.92f, 0.96f, 1f, 1f));
            text.fontStyle = FontStyle.Bold;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(24f, 0f);
            rect.sizeDelta = new Vector2(170f, 58f);
            return text;
        }

        private Image CreateDamageFlash(Transform canvas)
        {
            GameObject imageObject = new GameObject("Damage Flash");
            imageObject.transform.SetParent(canvas, false);
            RectTransform rect = imageObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = imageObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = false;
            return image;
        }

        private void CreateAdvanceButton(Transform canvas)
        {
            GameObject buttonObject = new GameObject("Advance Timers Button");
            buttonObject.transform.SetParent(canvas, false);
            RectTransform rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-24f, 0f);
            rect.sizeDelta = new Vector2(138f, 42f);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.16f, 0.22f, 0.28f, 0.88f);
            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.22f, 0.32f, 0.4f, 0.95f);
            colors.pressedColor = new Color(0.1f, 0.16f, 0.22f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => advanceTimers());

            Text label = CreateText("Label", buttonObject.transform, 18, TextAnchor.MiddleCenter, Color.white);
            label.fontStyle = FontStyle.Bold;
            label.text = "+0.3s";
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment, Color color)
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

        private void UpdateDamageFlash()
        {
            if (damageFlashImage != null)
            {
                damageFlashImage.color = new Color(1f, 0.03f, 0.02f, damageFlashAlpha);
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                eventSystemObject.AddComponent<EventSystem>();
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }
        }

        private static Font GetBuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
