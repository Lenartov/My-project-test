using UnityEngine;

namespace RealtimeCardPrototype
{
    public sealed class BossPrototypeGameBootstrap : MonoBehaviour
    {
        [SerializeField] private Vector2 bossCardSize = new Vector2(2.25f, 3.15f);
        [SerializeField] private Color tableColor = new Color(0.09f, 0.12f, 0.14f);
        [SerializeField] private Color bossColor = new Color(0.62f, 0.08f, 0.12f);
        [SerializeField] private float bossRespawnDelay = 0.9f;
        [SerializeField] private float bossMaxHealth = 420f;
        [SerializeField] private float bossMaxBalance = 8f;
        [SerializeField] private float bossAttackDamage = 10f;

        private EnemyDummyCard activeBoss;
        private float bossRespawnTimer = -1f;

        private void Start()
        {
            CombatTargetRegistry.ClearAll();
            PrototypeSceneFactory.ConfigureCombatPhysics();
            PrototypeSceneFactory.EnsureCamera("Boss Prototype Camera", new PrototypeCameraSettings(new Vector3(0f, 9.3f, -7.1f)));
            PrototypeSceneFactory.CreateDirectionalLight("Boss Prototype Key Light");
            PrototypeSceneFactory.EnsureActivityTimeController();
            PrototypeSceneFactory.CreateTable("Boss Prototype Table", tableColor);
            CreateBoss();
            CreatePlayerHand();
        }

        private void Update()
        {
            if (bossRespawnTimer < 0f)
            {
                return;
            }

            bossRespawnTimer -= Time.deltaTime;
            if (bossRespawnTimer <= 0f)
            {
                bossRespawnTimer = -1f;
                CreateBoss();
            }
        }

        private void CreateBoss()
        {
            EnemyDummyCard boss = EnemyCardFactory.Create(new EnemyCardSpawnSettings(
                "Boss Dummy",
                "BOSS",
                new Vector3(0f, 0.22f, 2.0f),
                bossCardSize,
                bossColor,
                0f,
                bossMaxHealth,
                bossMaxBalance,
                10f,
                5f,
                bossAttackDamage,
                0.15f,
                0.18f,
                0.052f));
            ConfigureBossActions(boss);
            boss.Died += HandleBossDied;
            activeBoss = boss;
        }

        private void HandleBossDied(EnemyDummyCard boss)
        {
            if (boss != null)
            {
                boss.Died -= HandleBossDied;
            }

            if (bossRespawnTimer >= 0f)
            {
                return;
            }

            activeBoss = null;
            bossRespawnTimer = bossRespawnDelay;
        }

        private void CreatePlayerHand()
        {
            PlayerHandController controller = PrototypeSceneFactory.CreatePlayerHand("Boss Scenario Player Hand");
            controller.ConfigureMana(5, 5);
            controller.ConfigureDeckCounts(
                normal: 0,
                heavy: 0,
                splash: 0,
                shock: 0,
                poison: 0,
                pierce: 0,
                graviton: 0,
                shieldBlocker: 5,
                power: 0,
                manaGenerator: 0,
                manaCapacity: 0,
                startingHand: 5,
                maxHand: 5);
        }

        private static void ConfigureBossActions(EnemyDummyCard boss)
        {
            EnemyActionStepConfiguration heavyAttack = new EnemyActionStepConfiguration(
                EnemyActionKind.Attack,
                EnemyAttackTargetMode.Player,
                1f,
                1f);
            EnemyActionStepConfiguration burstAttack = new EnemyActionStepConfiguration(
                EnemyActionKind.Attack,
                EnemyAttackTargetMode.Player,
                1f,
                0.3f,
                3,
                0.3f);

            boss.ConfigureActionCycle(heavyAttack, burstAttack);
        }

    }
}
