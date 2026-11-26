using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    public sealed class DamagePopup : MonoBehaviour
    {
        [SerializeField] private float lifetime = 0.75f;
        [SerializeField] private float riseSpeed = 0.85f;
        [SerializeField] private float fadeDelay = 0.12f;

        private static readonly System.Random SpawnRandom = new System.Random();
        private TextMesh textMesh;
        private Camera mainCamera;
        private Color startColor;
        private float age;

        private void Awake()
        {
            textMesh = GetComponent<TextMesh>();
            mainCamera = Camera.main;
            startColor = textMesh != null ? textMesh.color : Color.red;
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            age += deltaTime;
            transform.position += Vector3.up * (riseSpeed * deltaTime);

            if (mainCamera != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
            }

            if (textMesh != null)
            {
                float fadeTime = Mathf.Max(0.01f, lifetime - fadeDelay);
                float alpha = 1f - Mathf.Clamp01((age - fadeDelay) / fadeTime);
                textMesh.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            }

            if (age >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        public static void Spawn(int damage, Vector3 position, Color color)
        {
            if (damage > 0)
            {
                Spawn($"-{damage}", position, color);
            }
        }

        public static void SpawnHealing(float amount, Vector3 position, Color color)
        {
            Spawn($"+{CombatDamage.ToHitPoints(amount)}", position, color);
        }

        private static void Spawn(string textValue, Vector3 position, Color color)
        {
            GameObject popup = new GameObject("Damage Popup");
            Vector3 offset = new Vector3(
                (float)SpawnRandom.NextDouble() * 0.44f - 0.22f,
                0.60f + (float)SpawnRandom.NextDouble() * 0.10f,
                (float)SpawnRandom.NextDouble() * 0.28f - 0.14f);
            popup.transform.position = position + offset;
            popup.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            TextMesh text = popup.AddComponent<TextMesh>();
            text.text = textValue;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.08f;
            text.fontSize = 64;
            text.fontStyle = FontStyle.Bold;
            text.color = color;

            MeshRenderer renderer = popup.GetComponent<MeshRenderer>();
            renderer.sortingOrder = 50;
            popup.AddComponent<DamagePopup>();
        }
    }

    public readonly struct DamageResult
    {
        public readonly int Damage;
        public readonly float PostureDamage;

        public DamageResult(int damage, float postureDamage)
        {
            Damage = damage;
            PostureDamage = postureDamage;
        }
    }

    public static class DamageWindowTracker
    {
        private struct DamageSample
        {
            public float Time;
            public int Amount;
        }

        private static readonly Queue<DamageSample> samples = new Queue<DamageSample>();

        public static void Record(int amount)
        {
            if (amount > 0f)
            {
                samples.Enqueue(new DamageSample { Time = Time.time, Amount = amount });
            }
        }

        public static int GetRecentDamage(float windowSeconds)
        {
            float cutoff = Time.time - Mathf.Max(0f, windowSeconds);
            while (samples.Count > 0 && samples.Peek().Time < cutoff)
            {
                samples.Dequeue();
            }

            int total = 0;
            foreach (DamageSample sample in samples)
            {
                total += sample.Amount;
            }

            return total;
        }
    }
}
