using TMPro;
using UnityEngine;

namespace RealtimeCardPrototype
{
    internal sealed class CardFaceLabels
    {
        private static readonly Color TitleColor = new Color(1f, 0.94f, 0.75f);
        private static readonly Color ManaColor = new Color(0.16f, 0.09f, 0.05f);
        private static readonly Color EffectColor = new Color(1f, 0.97f, 0.89f);
        private readonly TextMeshPro title;
        private readonly TextMeshPro mana;
        private readonly TextMeshPro effect;

        internal CardFaceLabels(Transform card, Transform model, CardDefinition definition, float modelForward)
        {
            // The plaque and badge have the same dimensions on all exported card faces.
            title = CreateLabel(card, model, "CardTitle", definition.Title,
                new Vector3(0f, 0.113f, -0.4225f * modelForward), new Vector2(0.406f, 0.093f), TitleColor);
            mana = CreateLabel(card, model, "ManaCost", definition.ManaCost.ToString(),
                new Vector3(0f, 0.126f, 0.4785f * modelForward), new Vector2(0.14f, 0.072f), ManaColor);
            effect = CreateLabel(card, model, "CardEffect", GetEffectText(definition),
                new Vector3(0f, 0.102f, -0.595f * modelForward), new Vector2(0.48f, 0.043f), EffectColor);
        }

        internal void SetAvailable(bool available)
        {
            title.color = available ? TitleColor : new Color(0.72f, 0.70f, 0.63f);
            mana.color = available ? ManaColor : new Color(0.25f, 0.22f, 0.20f);
            effect.color = available ? EffectColor : new Color(0.66f, 0.65f, 0.61f);
        }

        internal void SetEffectVisible(bool visible)
        {
            effect.enabled = visible;
        }

        private static string GetEffectText(CardDefinition definition)
        {
            int lineBreak = definition.Body.IndexOf('\n');
            if (lineBreak >= 0 && lineBreak < definition.Body.Length - 1)
            {
                return definition.Body.Substring(lineBreak + 1).Replace('\n', ' ').Trim();
            }

            return definition.Kind == CardKind.Heavy ? "heavy hit" : "direct hit";
        }

        private static TextMeshPro CreateLabel(
            Transform card, Transform model, string name, string content, Vector3 modelPosition, Vector2 modelSize, Color color)
        {
            GameObject label = new GameObject(name);
            label.transform.SetParent(card, false);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            TextMeshPro text = label.AddComponent<TextMeshPro>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontStyle = FontStyles.Bold;
            text.fontSize = 10f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.richText = false;
            text.color = color;
            text.text = content;
            text.ForceMeshUpdate();

            Bounds glyphs = text.mesh.bounds;
            float width = model.TransformVector(Vector3.right * modelSize.x).magnitude;
            float height = model.TransformVector(Vector3.forward * modelSize.y).magnitude;
            float fit = Mathf.Min(width / glyphs.size.x, height / glyphs.size.y);
            label.transform.localScale = Vector3.one * fit;
            label.transform.position = model.TransformPoint(modelPosition) - label.transform.TransformVector(glyphs.center);
            return text;
        }
    }
}
