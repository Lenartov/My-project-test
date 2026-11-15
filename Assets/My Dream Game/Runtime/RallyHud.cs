using System.Globalization;
using TMPro;
using UnityEngine;

namespace MyDreamGame
{
    internal sealed class RallyHud
    {
        private static readonly Color Ink = new Color(0.034f, 0.053f, 0.069f, 0.94f);
        private static readonly Color Glass = new Color(0.032f, 0.049f, 0.062f, 0.82f);
        private static readonly Color Paper = new Color(0.91f, 0.94f, 0.95f);
        private static readonly Color Muted = new Color(0.58f, 0.68f, 0.72f);
        private static readonly Color Amber = new Color(1f, 0.67f, 0.38f);
        private static readonly Color Blue = new Color(0.55f, 0.83f, 0.93f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopRight = Vector2.one;
        private static readonly Vector2 BottomLeft = Vector2.zero;
        private static readonly Vector2 BottomRight = new Vector2(1f, 0f);
        private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

        private readonly TMP_Text timer;
        private readonly TMP_Text sector;
        private readonly TMP_Text referenceDelta;
        private readonly TMP_Text timeLeft;
        private readonly TMP_Text speed;
        private readonly TMP_Text gear;
        private readonly TMP_Text condition;
        private readonly TMP_Text grip;
        private readonly TMP_Text brakeTemperature;
        private readonly TMP_Text note;
        private readonly TMP_Text noteRange;
        private readonly TMP_Text hazard;
        private readonly TMP_Text flash;
        private readonly RectTransform paceCard;
        private readonly RectTransform rpmBar;
        private readonly RectTransform overlay;
        private readonly TMP_Text overlayTag;
        private readonly TMP_Text overlayTitle;
        private readonly TMP_Text overlayBody;
        private readonly TMP_Text overlayAction;
        private readonly TMP_Text overlayMusic;
        private readonly TMP_Text countdown;

        internal RallyHud(Transform parent)
        {
            GameObject root = RallyScenery.NewChild("Stage timing display", parent);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.UI.CanvasScaler scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.4f;

            RectTransform timing = Panel("Timing", root.transform, TopLeft, new Vector2(32f, -32f), new Vector2(244f, 129f), Glass);
            Panel("Timing rule", timing, TopLeft, Vector2.zero, new Vector2(3f, 129f), Amber);
            Label("STAGE TIME", timing, TopLeft, new Vector2(17f, -12f), new Vector2(141f, 19f), 14f, Muted);
            timer = Label("", timing, TopLeft, new Vector2(17f, -29f), new Vector2(205f, 49f), 39f, Paper);
            sector = Label("", timing, TopLeft, new Vector2(17f, -78f), new Vector2(205f, 21f), 15f, Blue);
            referenceDelta = Label("", timing, TopLeft, new Vector2(17f, -102f), new Vector2(205f, 21f), 15f, Muted);

            RectTransform limit = Panel("Time limit", root.transform, TopRight, new Vector2(-32f, -32f), new Vector2(171f, 69f), Glass);
            Label("TIME LEFT", limit, TopRight, new Vector2(-15f, -10f), new Vector2(142f, 17f), 13f, Muted, TextAlignmentOptions.MidlineRight);
            timeLeft = Label("", limit, TopRight, new Vector2(-15f, -28f), new Vector2(142f, 32f), 30f, Paper, TextAlignmentOptions.MidlineRight);

            paceCard = Panel("Co-driver", root.transform, new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(320f, 79f), Glass);
            note = Label("", paceCard, TopLeft, new Vector2(17f, -8f), new Vector2(288f, 33f), 26f, Paper);
            noteRange = Label("", paceCard, TopLeft, new Vector2(17f, -43f), new Vector2(288f, 24f), 17f, Amber);
            hazard = Label("", paceCard, TopLeft, new Vector2(17f, -70f), new Vector2(288f, 22f), 15f, Blue);

            RectTransform cluster = Panel("Instruments", root.transform, BottomRight, new Vector2(-32f, 32f), new Vector2(254f, 123f), Glass);
            speed = Label("", cluster, TopRight, new Vector2(-18f, -11f), new Vector2(145f, 66f), 60f, Paper, TextAlignmentOptions.MidlineRight);
            Label("KM/H", cluster, TopRight, new Vector2(-20f, -78f), new Vector2(105f, 17f), 14f, Muted, TextAlignmentOptions.MidlineRight);
            gear = Label("", cluster, TopLeft, new Vector2(19f, -18f), new Vector2(79f, 53f), 45f, Amber);
            Label("GEAR", cluster, TopLeft, new Vector2(21f, -78f), new Vector2(82f, 17f), 14f, Muted);
            Panel("Tachometer track", cluster, BottomLeft, new Vector2(19f, 13f), new Vector2(216f, 4f), new Color(0.25f, 0.34f, 0.37f));
            rpmBar = Panel("Tachometer fill", cluster, BottomLeft, new Vector2(19f, 13f), new Vector2(1f, 4f), Amber);

            RectTransform health = Panel("Car condition", root.transform, BottomLeft, new Vector2(32f, 32f), new Vector2(254f, 99f), Glass);
            condition = Label("", health, TopLeft, new Vector2(16f, -11f), new Vector2(223f, 24f), 18f, Paper);
            grip = Label("", health, TopLeft, new Vector2(16f, -40f), new Vector2(222f, 20f), 15f, Blue);
            brakeTemperature = Label("", health, TopLeft, new Vector2(16f, -67f), new Vector2(222f, 20f), 15f, Muted);
            flash = Label("", root.transform, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(470f, 43f),
                21f, Amber, TextAlignmentOptions.Center);
            countdown = Label("", root.transform, Middle, new Vector2(0f, 25f), new Vector2(290f, 177f),
                116f, Paper, TextAlignmentOptions.Center);

            overlay = Panel("Stage menu", root.transform, Middle, Vector2.zero, new Vector2(600f, 410f), Ink);
            Panel("Menu rule", overlay, TopLeft, Vector2.zero, new Vector2(600f, 4f), Amber);
            overlayTag = Label("", overlay, TopLeft, new Vector2(36f, -32f), new Vector2(529f, 24f), 17f, Amber);
            overlayTitle = Label("", overlay, TopLeft, new Vector2(32f, -62f), new Vector2(536f, 91f), 65f, Paper);
            overlayBody = Label("", overlay, TopLeft, new Vector2(36f, -160f), new Vector2(528f, 126f), 20f, Muted, TextAlignmentOptions.TopLeft);
            overlayBody.lineSpacing = 8f;
            Panel("Action background", overlay, BottomLeft, new Vector2(36f, 60f), new Vector2(528f, 48f), new Color(0.14f, 0.19f, 0.21f));
            overlayAction = Label("", overlay, BottomLeft, new Vector2(50f, 60f), new Vector2(500f, 48f), 18f, Paper);
            overlayMusic = Label("", overlay, BottomLeft, new Vector2(36f, 20f), new Vector2(526f, 25f), 14f, Muted);
        }

        internal void Update(RallyCar car, float elapsed, int splitCount, RallyCourse.Turn? nextTurn,
            RallyCourse.Puddle? waterAhead, float? delta, string message, string music)
        {
            timer.text = FormatTime(elapsed);
            sector.text = "SECTOR  " + Mathf.Min(3, splitCount + 1) + " / 3";
            referenceDelta.text = delta.HasValue ? "REFERENCE   " + FormatDelta(delta.Value) + " S" : string.Empty;
            referenceDelta.color = !delta.HasValue ? Muted : delta.Value > 0.01f ? Amber : Blue;
            float remaining = Mathf.Max(0f, RallyCourse.TimeLimit - elapsed);
            timeLeft.text = FormatTime(remaining).Substring(0, 5);
            timeLeft.color = remaining < 15f ? Amber : Paper;
            speed.text = Mathf.RoundToInt(car.Speed * 3.6f).ToString("000");
            gear.text = car.Gear < 0 ? "R" : car.Gear.ToString();
            rpmBar.sizeDelta = new Vector2(216f * Mathf.Clamp01(car.Rpm / 7150f), 4f);
            condition.text = "INTEGRITY  " + Mathf.RoundToInt((1f - car.Damage) * 100f) + "%";
            condition.color = car.Damage > 0.32f ? Amber : Paper;
            if (Mathf.Abs(car.SteeringPull) > 0.009f)
            {
                condition.text += car.SteeringPull < 0f ? "   /   PULL L" : "   /   PULL R";
                condition.color = Amber;
            }
            string surface = "WET ASPHALT";
            if (car.LeftWater > 0.14f || car.RightWater > 0.14f)
            {
                surface = car.LeftWater > 0.14f && car.RightWater > 0.14f ? "STANDING WATER" :
                    car.LeftWater > 0.14f ? "WATER LEFT" : "WATER RIGHT";
            }
            else if (car.Gravel > 0.28f)
            {
                surface = "GRAVEL SHOULDER";
            }

            grip.text = car.Slip > 0.16f ? surface + "  /  SLIP" : surface;
            grip.color = car.Slip > 0.16f ? Amber : Blue;
            bool hotBrakes = car.BrakeTemperature >= 285f;
            brakeTemperature.text = "BRAKES   " + Mathf.RoundToInt(car.BrakeTemperature) + " C";
            if (hotBrakes)
            {
                brakeTemperature.text += car.BrakeFade > 0f ? "  /  FADE" : "  /  HOT";
            }

            brakeTemperature.color = hotBrakes ? Amber : Muted;
            paceCard.gameObject.SetActive(nextTurn.HasValue || waterAhead.HasValue);
            if (waterAhead.HasValue)
            {
                RallyCourse.Puddle puddle = waterAhead.Value;
                int meters = Mathf.CeilToInt(puddle.Distance - puddle.HalfLength - car.Distance);
                string side = puddle.Lateral < 0f ? "WATER LEFT" : "WATER RIGHT";
                if (nextTurn.HasValue)
                {
                    hazard.text = side + "    /    " + meters + " M";
                }
                else
                {
                    note.text = side;
                    noteRange.text = meters + " M";
                }
            }

            bool showHazardRow = nextTurn.HasValue && waterAhead.HasValue;
            hazard.gameObject.SetActive(showHazardRow);
            paceCard.sizeDelta = new Vector2(320f, showHazardRow ? 101f : 79f);
            if (nextTurn.HasValue)
            {
                RallyCourse.Turn turn = nextTurn.Value;
                float remainingDistance = turn.Start - car.Distance;
                note.text = (turn.Direction > 0 ? "RIGHT  " : "LEFT  ") + turn.Severity;
                noteRange.text = remainingDistance > 0f
                    ? Mathf.CeilToInt(remainingDistance) + " M    /    " + turn.Speed + " KM/H"
                    : "IN TURN    /    " + turn.Speed + " KM/H";
            }

            flash.text = message;
            if (overlay.gameObject.activeSelf)
            {
                overlayMusic.text = "M  TOGGLE MUSIC   /   " + music;
            }
        }

        internal void ShowMenu(string tag, string title, string body, string action)
        {
            overlayTag.text = tag;
            overlayTitle.text = title;
            overlayBody.text = body;
            overlayAction.text = action;
            overlay.gameObject.SetActive(true);
        }

        internal void HideMenu()
        {
            overlay.gameObject.SetActive(false);
        }

        internal void SetCountdown(int number)
        {
            countdown.text = number > 0 ? number.ToString() : string.Empty;
        }

        internal static string FormatTime(float seconds)
        {
            int centiseconds = Mathf.FloorToInt(seconds * 100f + 0.0001f);
            return $"{centiseconds / 6000:00}:{centiseconds / 100 % 60:00}.{centiseconds % 100:00}";
        }

        internal static string FormatDelta(float seconds)
        {
            float magnitude = Mathf.Abs(seconds);
            string sign = magnitude < 0.005f ? string.Empty : seconds < 0f ? "-" : "+";
            return sign + magnitude.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Place(name, parent, anchor, position, size);
            UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.color = color;
            return rect;
        }

        private static TMP_Text Label(string value, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
            float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            RectTransform rect = Place("Text", parent, anchor, position, size);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.richText = false;
            label.raycastTarget = false;
            label.text = value;
            return label;
        }

        private static RectTransform Place(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
