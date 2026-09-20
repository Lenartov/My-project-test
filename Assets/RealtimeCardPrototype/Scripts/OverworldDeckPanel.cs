using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealtimeCardPrototype
{
    public sealed class OverworldDeckPanel : MonoBehaviour
    {
        private const float CollectionCardWidth = 270f;
        private const float CollectionCardHeight = 360f;

        private OverworldCameraController cameraController;
        private GameObject root;
        private Transform collectionContent;
        private Transform activeDeckContent;
        private Text deckCountText;

        public void Initialize(OverworldCameraController controller)
        {
            cameraController = controller;
            CreateRoot();
        }

        public void Show()
        {
            EnsureRootExists();
            root.SetActive(true);
            cameraController.enabled = false;
            Refresh();
        }

        private void OnDisable()
        {
            OverworldDeckState.Save();
        }

        private void CreateRoot()
        {
            root = new GameObject("Deck Collection Panel");
            root.transform.SetParent(transform, false);
            Image overlay = root.AddComponent<Image>();
            overlay.color = new Color(0.015f, 0.025f, 0.04f, 0.96f);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            CreateBackdrop();
            CreateCollectionArea();
            CreateActiveDeckArea();
            CreateHeader();
            root.SetActive(false);
        }

        private void CreateBackdrop()
        {
            CreateBackdropPanel("Upper Teal Glow", new Color(0.05f, 0.26f, 0.3f, 0.22f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-360f, -110f), new Vector2(1220f, 260f));
            CreateBackdropPanel("Lower Violet Glow", new Color(0.2f, 0.1f, 0.32f, 0.18f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(430f, 90f), new Vector2(920f, 230f));
            CreateBackdropPanel("Collection Divider", new Color(0.36f, 0.76f, 0.78f, 0.28f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 239f), new Vector2(1790f, 2f));
        }

        private void CreateBackdropPanel(string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(root.transform, false);
            Image image = panel.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            SetRect(panel.GetComponent<RectTransform>(), anchorMin, anchorMax, position, size, new Vector2(0.5f, 0.5f));
        }

        private void EnsureRootExists()
        {
            if (root != null)
            {
                collectionContent = root.transform.Find("Collection Scroll View/Collection Content");
                activeDeckContent = root.transform.Find("Active Deck Section/Active Deck Cards");
                Transform deckCountTransform = root.transform.Find("Active Deck Section/Deck Count");
                deckCountText = deckCountTransform != null ? deckCountTransform.GetComponent<Text>() : null;
                if (collectionContent != null && activeDeckContent != null && deckCountText != null)
                {
                    return;
                }

                root.SetActive(false);
            }

            CreateRoot();
        }

        private void CreateHeader()
        {
            GameObject topBar = new GameObject("Deck Top Bar");
            topBar.transform.SetParent(root.transform, false);
            Image topBarImage = topBar.AddComponent<Image>();
            topBarImage.color = new Color(0.025f, 0.05f, 0.075f, 0.98f);
            RectTransform topBarRect = topBar.GetComponent<RectTransform>();
            topBarRect.anchorMin = new Vector2(0f, 1f);
            topBarRect.anchorMax = new Vector2(1f, 1f);
            topBarRect.pivot = new Vector2(0.5f, 1f);
            topBarRect.anchoredPosition = Vector2.zero;
            topBarRect.sizeDelta = new Vector2(0f, 76f);

            Button closeButton = CreateButton("Close Deck", root.transform, "CLOSE", new Color(0.2f, 0.38f, 0.46f));
            SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -17f), new Vector2(138f, 42f), new Vector2(1f, 1f));
            closeButton.onClick.AddListener(Hide);
        }

        private void CreateCollectionArea()
        {
            GameObject viewport = new GameObject("Collection Scroll View");
            viewport.transform.SetParent(root.transform, false);
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0.045f, 0.07f, 0.1f, 0.62f);
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            ScrollRect scrollRect = viewport.AddComponent<ScrollRect>();
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(0f, 230f);
            viewportRect.offsetMax = new Vector2(0f, -76f);

            GameObject content = new GameObject("Collection Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            collectionContent = content.transform;
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 900f);
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void CreateActiveDeckArea()
        {
            GameObject section = new GameObject("Active Deck Section");
            section.transform.SetParent(root.transform, false);
            Image background = section.AddComponent<Image>();
            background.color = new Color(0.07f, 0.11f, 0.16f, 0.98f);
            Outline sectionOutline = section.AddComponent<Outline>();
            sectionOutline.effectColor = new Color(0.3f, 0.68f, 0.72f, 0.45f);
            sectionOutline.effectDistance = new Vector2(0f, 2f);
            RectTransform sectionRect = section.GetComponent<RectTransform>();
            sectionRect.anchorMin = Vector2.zero;
            sectionRect.anchorMax = new Vector2(1f, 0f);
            sectionRect.pivot = new Vector2(0.5f, 0f);
            sectionRect.anchoredPosition = Vector2.zero;
            sectionRect.sizeDelta = new Vector2(0f, 230f);

            Text title = CreateText("Active Deck Title", section.transform, 23, TextAnchor.UpperLeft, Color.white);
            title.fontStyle = FontStyle.Bold;
            title.text = "ACTIVE DECK";
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -16f), new Vector2(260f, 34f), new Vector2(0f, 1f));

            deckCountText = CreateText("Deck Count", section.transform, 19, TextAnchor.UpperRight, new Color(0.78f, 0.9f, 0.88f));
            SetRect(deckCountText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -19f), new Vector2(220f, 30f), new Vector2(1f, 1f));

            GameObject cards = new GameObject("Active Deck Cards", typeof(RectTransform));
            cards.transform.SetParent(section.transform, false);
            activeDeckContent = cards.transform;
            RectTransform cardsRect = cards.GetComponent<RectTransform>();
            cardsRect.anchorMin = Vector2.zero;
            cardsRect.anchorMax = new Vector2(1f, 0f);
            cardsRect.pivot = new Vector2(0.5f, 0f);
            cardsRect.anchoredPosition = Vector2.zero;
            cardsRect.sizeDelta = new Vector2(0f, 165f);
        }

        private void Hide()
        {
            OverworldDeckState.Save();
            root.SetActive(false);
            cameraController.enabled = true;
        }

        private void Refresh()
        {
            EnsureRootExists();
            ClearChildren(collectionContent);
            ClearChildren(activeDeckContent);
            CreateCollectionCards();
            CreateActiveDeckCards();
        }

        private void CreateCollectionCards()
        {
            List<string> groups = new List<string> { "Starter Warrior" };
            if (OverworldProgress.IsStormMageUnlocked)
            {
                groups.Add("Storm Mage");
            }
            float y = -30f;
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                string group = groups[groupIndex];
                Text groupTitle = CreateText(group + " Heading", collectionContent, 24, TextAnchor.UpperLeft, new Color(0.9f, 0.78f, 0.43f));
                groupTitle.fontStyle = FontStyle.Bold;
                List<OverworldCardEntry> entries = GetEntries(group);
                groupTitle.text = group.ToUpperInvariant();
                SetRect(groupTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, y), new Vector2(400f, 34f), new Vector2(0f, 1f));
                y -= 54f;

                for (int i = 0; i < entries.Count; i++)
                {
                    int column = i % 6;
                    int row = i / 6;
                    float x = 26f + column * 312f;
                    float cardY = y - row * (CollectionCardHeight + 16f);
                    CreateCardButton(collectionContent, entries[i], new Vector2(x, cardY), new Vector2(CollectionCardWidth, CollectionCardHeight), true, -1);
                }

                y -= Mathf.Ceil(entries.Count / 6f) * (CollectionCardHeight + 16f) + 30f;
            }

            collectionContent.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, -y + 22f);
        }

        private void CreateActiveDeckCards()
        {
            IReadOnlyList<string> activeCards = OverworldDeckState.ActiveCardIds;
            deckCountText.text = $"{activeCards.Count} / {OverworldDeckState.ActiveDeckLimit}";
            if (activeCards.Count == 0)
            {
                Text empty = CreateText("Empty Deck", activeDeckContent, 18, TextAnchor.MiddleCenter, new Color(0.62f, 0.7f, 0.74f));
                empty.text = "Choose cards from the collection";
                SetRect(empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 44f), new Vector2(0.5f, 0.5f));
                return;
            }

            float cardWidth = Mathf.Min(120f, 1800f / activeCards.Count);
            for (int i = 0; i < activeCards.Count; i++)
            {
                OverworldCardEntry entry = OverworldDeckState.FindEntry(activeCards[i]);
                float x = 60f + cardWidth * i;
                CreateCardButton(activeDeckContent, entry, new Vector2(x, -12f), new Vector2(cardWidth - 7f, 132f), false, i);
            }
        }

        private void CreateCardButton(Transform parent, OverworldCardEntry entry, Vector2 position, Vector2 size, bool isCollectionCard, int deckIndex)
        {
            int availableCopies = OverworldDeckState.GetAvailableCopies(entry.Id);
            bool isAvailable = !isCollectionCard || availableCopies > 0 && OverworldDeckState.ActiveCardIds.Count < OverworldDeckState.ActiveDeckLimit;
            GameObject cardObject = new GameObject(entry.Definition.Title + " Card");
            cardObject.transform.SetParent(parent, false);
            Image cardImage = cardObject.AddComponent<Image>();
            Color baseColor = isAvailable ? entry.Definition.Color : Color.Lerp(entry.Definition.Color, Color.gray, 0.8f);
            cardImage.color = Color.Lerp(baseColor, new Color(0.04f, 0.065f, 0.095f), 0.64f);
            Outline outline = cardObject.AddComponent<Outline>();
            outline.effectColor = isAvailable ? Color.Lerp(baseColor, Color.white, 0.2f) : new Color(0.2f, 0.23f, 0.26f);
            outline.effectDistance = new Vector2(2f, -2f);
            Shadow shadow = cardObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.68f);
            shadow.effectDistance = new Vector2(5f, -6f);
            Button button = cardObject.AddComponent<Button>();
            button.interactable = isAvailable;
            SetRect(cardObject.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
            cardObject.AddComponent<DeckCardHover>().Configure(cardImage.color, isAvailable ? 1.06f : 1f, isAvailable);

            GameObject ribbon = new GameObject("Title Ribbon");
            ribbon.transform.SetParent(cardObject.transform, false);
            Image ribbonImage = ribbon.AddComponent<Image>();
            ribbonImage.color = Color.Lerp(baseColor, Color.black, 0.26f);
            SetRect(ribbon.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(size.x - 10f, size.y > 160f ? 50f : 32f), new Vector2(0.5f, 1f));

            Text title = CreateText("Title", cardObject.transform, size.y > 160f ? 22 : 15, TextAnchor.UpperCenter, Color.white);
            title.fontStyle = FontStyle.Bold;
            title.text = entry.Definition.Title;
            Shadow titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            titleShadow.effectDistance = new Vector2(1f, -2f);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, size.y > 160f ? -15f : -11f), new Vector2(size.x - 14f, 30f), new Vector2(0.5f, 1f));

            Text body = CreateText("Body", cardObject.transform, size.y > 160f ? 16 : 11, TextAnchor.MiddleCenter, GetTextColor(baseColor));
            body.text = entry.Definition.Body;
            body.color = new Color(0.9f, 0.95f, 0.96f);
            SetRect(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(size.x - 28f, 58f), new Vector2(0.5f, 0.5f));

            Text cost = CreateText("Cost", cardObject.transform, size.y > 160f ? 14 : 10, TextAnchor.LowerLeft, new Color(0.95f, 0.98f, 1f));
            cost.fontStyle = FontStyle.Bold;
            cost.text = $"COST {entry.Definition.ManaCost}";
            SetRect(cost.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14f, 13f), new Vector2(78f, 28f), Vector2.zero);

            if (isCollectionCard)
            {
                GameObject countBadge = new GameObject("Available Copies Badge");
                countBadge.transform.SetParent(cardObject.transform, false);
                Image badgeImage = countBadge.AddComponent<Image>();
                badgeImage.color = availableCopies > 0 ? new Color(0.12f, 0.2f, 0.28f) : new Color(0.16f, 0.16f, 0.17f);
                Outline badgeOutline = countBadge.AddComponent<Outline>();
                badgeOutline.effectColor = availableCopies > 0 ? Color.Lerp(baseColor, Color.white, 0.18f) : Color.gray;
                badgeOutline.effectDistance = new Vector2(1f, -1f);
                SetRect(countBadge.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(12f, 12f), new Vector2(40f, 34f), new Vector2(1f, 1f));
                Text copies = CreateText("Available Copies", countBadge.transform, 16, TextAnchor.MiddleCenter, Color.white);
                copies.fontStyle = FontStyle.Bold;
                copies.text = availableCopies.ToString();
                SetRect(copies.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
                button.onClick.AddListener(() => AddToDeck(entry.Id));
            }
            else
            {
                button.onClick.AddListener(() => RemoveFromDeck(deckIndex));
            }
        }

        private void AddToDeck(string id)
        {
            if (OverworldDeckState.TryAdd(id))
            {
                Refresh();
            }
        }

        private void RemoveFromDeck(int index)
        {
            OverworldDeckState.RemoveAt(index);
            Refresh();
        }

        private static List<OverworldCardEntry> GetEntries(string ownerTag)
        {
            List<OverworldCardEntry> entries = new List<OverworldCardEntry>();
            foreach (OverworldCardEntry entry in OverworldDeckState.Collection)
            {
                if (entry.OwnerTag == ownerTag)
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color color)
        {
            GameObject buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            Button button = buttonObject.AddComponent<Button>();
            Text text = CreateText("Label", buttonObject.transform, 16, TextAnchor.MiddleCenter, Color.white);
            text.fontStyle = FontStyle.Bold;
            text.text = label;
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            return button;
        }

        private static Text CreateText(string name, Transform parent, int size, TextAnchor alignment, Color color)
        {
            return CampaignUiFactory.CreateText(name, parent, size, alignment, color);
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private static Color GetTextColor(Color background)
        {
            return background.r * 0.2126f + background.g * 0.7152f + background.b * 0.0722f > 0.63f ? Color.black : Color.white;
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

    public sealed class DeckCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image image;
        private Color baseColor;
        private float hoverScale;
        private bool isInteractive;
        private float targetScale = 1f;
        private Color targetColor;

        public void Configure(Color color, float scale, bool interactive)
        {
            image = GetComponent<Image>();
            baseColor = color;
            hoverScale = scale;
            isInteractive = interactive;
            targetColor = color;
        }

        private void Update()
        {
            float interpolation = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, interpolation);
            image.color = Color.Lerp(image.color, targetColor, interpolation);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isInteractive)
            {
                return;
            }

            transform.SetAsLastSibling();
            targetScale = hoverScale;
            targetColor = Color.Lerp(baseColor, Color.white, 0.18f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isInteractive)
            {
                return;
            }

            targetScale = 1f;
            targetColor = baseColor;
        }
    }
}
