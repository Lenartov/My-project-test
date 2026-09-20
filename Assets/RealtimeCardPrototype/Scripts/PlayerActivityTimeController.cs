using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RealtimeCardPrototype
{
    [DefaultExecutionOrder(-1000)]
    public sealed class PlayerActivityTimeController : MonoBehaviour
    {
        [SerializeField] private float idleTimeScale = 0.22f;
        [SerializeField] private float cardInteractionTimeScale = 1f;
        [SerializeField] private float speedUpSharpness = 18f;
        [SerializeField] private float slowDownSharpness = 5.5f;
        [SerializeField] private Color slowTimeOverlayColor = new Color(0.36f, 0.38f, 0.4f, 0.22f);

        private static readonly HashSet<int> ActiveCardSources = new HashSet<int>();
        private static readonly HashSet<int> PauseSources = new HashSet<int>();

        private Image overlayImage;
        private Text focusModeText;
        private float baseFixedDeltaTime;
        private float currentTimeScale = 1f;
        private bool isFocusModeActive;

        private void Awake()
        {
            ActiveCardSources.Clear();
            PauseSources.Clear();
            baseFixedDeltaTime = Time.fixedDeltaTime;
            currentTimeScale = Mathf.Clamp(Time.timeScale, idleTimeScale, cardInteractionTimeScale);
            CreateOverlay();
        }

        private void Update()
        {
            float unscaledDeltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            isFocusModeActive = Input.GetMouseButton(1);

            float targetTimeScale = GetTargetTimeScale();
            if (targetTimeScale <= 0f)
            {
                currentTimeScale = 0f;
                ApplyTimeScale(currentTimeScale);
                UpdateOverlay();
                UpdateFocusModeText();
                return;
            }

            float sharpness = targetTimeScale > currentTimeScale ? speedUpSharpness : slowDownSharpness;
            currentTimeScale = Mathf.Lerp(currentTimeScale, targetTimeScale, 1f - Mathf.Exp(-sharpness * unscaledDeltaTime));
            ApplyTimeScale(currentTimeScale);
            UpdateOverlay();
            UpdateFocusModeText();
        }

        private void OnDisable()
        {
            ResetGlobalTimeScale();
        }

        private void OnDestroy()
        {
            ResetGlobalTimeScale();
        }

        public static void ReportCardInteraction(Object source, bool isActive)
        {
            if (source == null)
            {
                return;
            }

            int id = source.GetInstanceID();
            if (isActive)
            {
                ActiveCardSources.Add(id);
            }
            else
            {
                ActiveCardSources.Remove(id);
            }
        }

        public static void ReportPause(Object source, bool isPaused)
        {
            if (source == null)
            {
                return;
            }

            int id = source.GetInstanceID();
            if (isPaused)
            {
                PauseSources.Add(id);
            }
            else
            {
                PauseSources.Remove(id);
            }
        }

        private float GetTargetTimeScale()
        {
            if (PauseSources.Count > 0)
            {
                return 0f;
            }

            if (isFocusModeActive)
            {
                return cardInteractionTimeScale;
            }

            return ActiveCardSources.Count > 0 ? cardInteractionTimeScale : idleTimeScale;
        }

        private void ApplyTimeScale(float scale)
        {
            float clampedScale = Mathf.Clamp(scale, 0f, 1f);
            Time.timeScale = clampedScale;
            Time.fixedDeltaTime = baseFixedDeltaTime * clampedScale;
        }

        private void ResetGlobalTimeScale()
        {
            Time.timeScale = 1f;
            if (baseFixedDeltaTime > 0f)
            {
                Time.fixedDeltaTime = baseFixedDeltaTime;
            }
        }

        private void CreateOverlay()
        {
            GameObject canvasObject = new GameObject("Activity Time Overlay");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject overlayObject = new GameObject("Slow Time Tint");
            overlayObject.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = overlayObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            overlayImage = overlayObject.AddComponent<Image>();
            overlayImage.color = Color.clear;
            overlayImage.raycastTarget = false;

            focusModeText = CreateFocusModeText(canvasObject.transform);
        }

        private void UpdateOverlay()
        {
            if (overlayImage == null)
            {
                return;
            }

            float slowAmount = Mathf.InverseLerp(cardInteractionTimeScale, idleTimeScale, currentTimeScale);
            Color color = slowTimeOverlayColor;
            color.a *= Mathf.Clamp01(slowAmount);
            overlayImage.color = color;
        }

        private void UpdateFocusModeText()
        {
            if (focusModeText == null)
            {
                return;
            }

            focusModeText.enabled = isFocusModeActive && PauseSources.Count == 0;
        }

        private static Text CreateFocusModeText(Transform canvas)
        {
            GameObject textObject = new GameObject("Focus Mode Label");
            textObject.transform.SetParent(canvas, false);

            Text text = textObject.AddComponent<Text>();
            text.font = GetBuiltinFont();
            text.fontSize = 20;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.LowerRight;
            text.color = new Color(1f, 0.48f, 0.2f);
            text.text = "FOCUS MODE  x1.0";
            text.raycastTarget = false;
            text.enabled = false;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 24f);
            rect.sizeDelta = new Vector2(280f, 34f);
            return text;
        }

        private static Font GetBuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

    }

    public sealed class CardActivityReporter : MonoBehaviour
    {
        private IHandCard[] handCards;
        private bool wasInteracting;

        private void Update()
        {
            bool isInteracting = IsAnyCardDragging();
            if (isInteracting == wasInteracting)
            {
                return;
            }

            wasInteracting = isInteracting;
            PlayerActivityTimeController.ReportCardInteraction(this, isInteracting);
        }

        private void OnDisable()
        {
            ClearInteraction();
        }

        private void OnDestroy()
        {
            ClearInteraction();
        }

        private bool IsAnyCardDragging()
        {
            if (handCards == null || handCards.Length == 0)
            {
                handCards = GetComponents<IHandCard>();
            }

            for (int i = 0; i < handCards.Length; i++)
            {
                IHandCard handCard = handCards[i];
                if (handCard != null && handCard.IsDragging)
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearInteraction()
        {
            if (!wasInteracting)
            {
                return;
            }

            wasInteracting = false;
            PlayerActivityTimeController.ReportCardInteraction(this, false);
        }
    }
}
