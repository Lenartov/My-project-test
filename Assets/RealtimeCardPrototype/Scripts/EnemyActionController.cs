using UnityEngine;

namespace RealtimeCardPrototype
{
    internal sealed class EnemyActionController
    {
        private readonly EnemyActionIndicator indicator;

        private IEnemyAction[] actionCycle;
        private IEnemyAction activeAction;
        private int actionIndex;
        private bool isInInitialDelay;
        private bool isInRecovery;

        public EnemyActionController(Transform owner)
        {
            indicator = new EnemyActionIndicator(owner);
        }

        public bool IsInRecovery => isInRecovery;

        public void Configure(EnemyDummyCard enemy, EnemyActionConfiguration configuration)
        {
            actionCycle = EnemyActionFactory.CreateCycle(configuration);

            actionIndex = 0;
            isInRecovery = false;
            isInInitialDelay = true;
            activeAction = new EnemySleepAction(configuration.initialSleepSeconds);
            activeAction.Begin(enemy);
            RefreshIndicator();
        }

        public void Tick(EnemyDummyCard enemy, float deltaTime)
        {
            if (activeAction == null)
            {
                return;
            }

            bool isComplete = activeAction.Tick(enemy, deltaTime);
            RefreshIndicator();
            if (!isComplete)
            {
                return;
            }

            if (isInInitialDelay)
            {
                isInInitialDelay = false;
                actionIndex = 0;
                BeginCurrentAction(enemy);
                return;
            }

            if (isInRecovery)
            {
                isInRecovery = false;
                BeginCurrentAction(enemy);
                return;
            }

            actionIndex = (actionIndex + 1) % actionCycle.Length;
            BeginCurrentAction(enemy);
        }

        public void InterruptForRecovery(EnemyDummyCard enemy, float recoverySeconds)
        {
            if (actionCycle != null && actionCycle.Length > 0)
            {
                actionIndex = isInInitialDelay ? 0 : (actionIndex + 1) % actionCycle.Length;
            }

            isInInitialDelay = false;
            isInRecovery = true;
            activeAction = new EnemyRecoveryAction(recoverySeconds);
            activeAction.Begin(enemy);
            RefreshIndicator();
        }

        public void Hide()
        {
            indicator.Hide();
        }

        private void BeginCurrentAction(EnemyDummyCard enemy)
        {
            if (actionCycle == null || actionCycle.Length == 0)
            {
                activeAction = null;
                indicator.Hide();
                return;
            }

            activeAction = actionCycle[actionIndex];
            activeAction.Begin(enemy);
            RefreshIndicator();
        }

        private void RefreshIndicator()
        {
            if (activeAction == null || actionCycle == null || actionCycle.Length == 0)
            {
                return;
            }

            int nextIndex = isInInitialDelay || isInRecovery ? actionIndex : (actionIndex + 1) % actionCycle.Length;
            indicator.Set(activeAction.Kind, activeAction.Progress01, actionCycle[nextIndex].Kind);
        }
    }
}
