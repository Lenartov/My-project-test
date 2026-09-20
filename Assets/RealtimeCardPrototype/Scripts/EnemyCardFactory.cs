using UnityEngine;

namespace RealtimeCardPrototype
{
    internal readonly struct EnemyCardSpawnSettings
    {
        public EnemyCardSpawnSettings(
            string objectName,
            string title,
            Vector3 position,
            Vector2 cardSize,
            Color color,
            float yaw,
            float? maxHealth,
            float? maxBalance,
            float initialSleepSeconds,
            float attackChargeSeconds,
            float attackDamage,
            float statusHeight,
            float statusDepthFactor,
            float statusCharacterSize)
        {
            ObjectName = objectName;
            Title = title;
            Position = position;
            CardSize = cardSize;
            Color = color;
            Yaw = yaw;
            MaxHealth = maxHealth;
            MaxBalance = maxBalance;
            InitialSleepSeconds = initialSleepSeconds;
            AttackChargeSeconds = attackChargeSeconds;
            AttackDamage = attackDamage;
            StatusHeight = statusHeight;
            StatusDepthFactor = statusDepthFactor;
            StatusCharacterSize = statusCharacterSize;
        }

        public string ObjectName { get; }
        public string Title { get; }
        public Vector3 Position { get; }
        public Vector2 CardSize { get; }
        public Color Color { get; }
        public float Yaw { get; }
        public float? MaxHealth { get; }
        public float? MaxBalance { get; }
        public float InitialSleepSeconds { get; }
        public float AttackChargeSeconds { get; }
        public float AttackDamage { get; }
        public float StatusHeight { get; }
        public float StatusDepthFactor { get; }
        public float StatusCharacterSize { get; }
    }

    internal static class EnemyCardFactory
    {
        private const float EnemyCardScale = 1.2f;
        private const float EnemyTextScale = 0.8f;

        public static EnemyDummyCard Create(EnemyCardSpawnSettings settings)
        {
            Vector2 cardSize = settings.CardSize * EnemyCardScale;
            CardView view = CardView.Create(settings.ObjectName, settings.Position, cardSize, settings.Color, CardTeam.Enemy);
            view.transform.rotation = Quaternion.Euler(0f, settings.Yaw, 0f);
            view.Configure(CardTeam.Enemy, settings.Title, string.Empty, settings.Color, Color.white);

            EnemyDummyCard enemy = view.gameObject.AddComponent<EnemyDummyCard>();
            if (settings.MaxHealth.HasValue && settings.MaxBalance.HasValue)
            {
                enemy.ConfigureVitals(settings.MaxHealth.Value, settings.MaxBalance.Value);
            }
            enemy.ConfigureActionTimings(settings.InitialSleepSeconds, settings.AttackChargeSeconds, 0f, settings.AttackDamage);
            enemy.ConfigureStatusText(PrototypeSceneFactory.CreateFloatingText(
                $"{settings.ObjectName} Status",
                view.transform,
                new Vector3(0f, settings.StatusHeight * EnemyCardScale, -cardSize.y * settings.StatusDepthFactor),
                settings.StatusCharacterSize * EnemyTextScale));
            return enemy;
        }
    }
}
