using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    public sealed class PrototypeGameBootstrap : MonoBehaviour
    {
        [SerializeField] private Vector2 cardSize = new Vector2(1.05f, 1.5f);
        [SerializeField] private Color tableColor = new Color(0.09f, 0.12f, 0.14f);
        [SerializeField] private Color enemyColor = new Color(0.78f, 0.2f, 0.18f);
        [SerializeField] private float enemyRespawnDelay = 0.75f;

        private readonly List<EnemyDummyCard> activeEnemies = new List<EnemyDummyCard>();
        private float enemyRespawnTimer = -1f;

        private void Start()
        {
            CombatTargetRegistry.ClearAll();
            PrototypeSceneFactory.ConfigureCombatPhysics();
            PrototypeSceneFactory.EnsureCamera("Prototype Camera", new PrototypeCameraSettings(new Vector3(0f, 8.8f, -6.6f)));
            PrototypeSceneFactory.CreateDirectionalLight("Prototype Key Light");
            PrototypeSceneFactory.EnsureActivityTimeController();
            PrototypeSceneFactory.CreateTable("Prototype Table", tableColor);
            CreateEnemyDummies();
            CreatePlayerHand();
        }

        private void Update()
        {
            if (enemyRespawnTimer < 0f)
            {
                return;
            }

            enemyRespawnTimer -= Time.deltaTime;
            if (enemyRespawnTimer <= 0f)
            {
                enemyRespawnTimer = -1f;
                CreateEnemyDummies();
            }
        }

        private void CreateEnemyDummies()
        {
            activeEnemies.Clear();
            const float enemyScreenUpOffset = 0.9f;
            CreateEnemy("Dummy A", new Vector3(-2.4f, 0.18f, 0.85f + enemyScreenUpOffset), 0f, 10f);
            CreateEnemy("Dummy B", new Vector3(0f, 0.18f, 1.05f + enemyScreenUpOffset), 0f, 13f);
            CreateEnemy("Dummy C", new Vector3(2.4f, 0.18f, 0.85f + enemyScreenUpOffset), 0f, 16f);
        }

        private void CreateEnemy(string name, Vector3 position, float yaw, float initialWaitSeconds)
        {
            EnemyDummyCard dummy = EnemyCardFactory.Create(new EnemyCardSpawnSettings(
                name,
                "DUMMY",
                position,
                cardSize,
                enemyColor,
                yaw,
                null,
                null,
                initialWaitSeconds,
                5f,
                2f,
                0.132f,
                0.2f,
                0.044f));
            dummy.Died += HandleEnemyDied;
            activeEnemies.Add(dummy);
        }

        private void HandleEnemyDied(EnemyDummyCard enemy)
        {
            if (enemy != null)
            {
                enemy.Died -= HandleEnemyDied;
            }

            if (enemyRespawnTimer >= 0f || !AreAllEnemiesDead())
            {
                return;
            }

            enemyRespawnTimer = enemyRespawnDelay;
        }

        private bool AreAllEnemiesDead()
        {
            if (activeEnemies.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < activeEnemies.Count; i++)
            {
                EnemyDummyCard enemy = activeEnemies[i];
                if (enemy != null && !enemy.IsDead)
                {
                    return false;
                }
            }

            return true;
        }

        private void CreatePlayerHand()
        {
            PrototypeSceneFactory.CreatePlayerHand("Player Hand");
        }
    }
}
