using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    public sealed class PlayerHandController : MonoBehaviour, IPlayerDamageReceiver
    {
        [SerializeField] private int normalCardCount = 1;
        [SerializeField] private int heavyCardCount = 1;
        [SerializeField] private int splashCardCount = 1;
        [SerializeField] private int shockCardCount = 1;
        [SerializeField] private int poisonCardCount = 1;
        [SerializeField] private int pierceCardCount = 1;
        [SerializeField] private int gravitonCardCount = 1;
        [SerializeField] private int shieldBlockerCardCount = 1;
        [SerializeField] private int powerCardCount = 2;
        [SerializeField] private int manaGeneratorCardCount = 1;
        [SerializeField] private int manaCapacityCardCount = 1;
        [SerializeField] private int turretCardCount = 1;
        [SerializeField] private int startingHandCount = 5;
        [SerializeField] private int maxHandSize = 5;
        [SerializeField] private float drawInterval = 1f;
        [SerializeField] private int maxMana = 3;
        [SerializeField] private int startingMana = 3;
        [SerializeField] private float maxPlayerHealth = 17f;
        [SerializeField] private float manaRegenInterval = 1.18f;
        [SerializeField] private float drawButtonAdvanceSeconds = 0.3f;
        [SerializeField] private float manaButtonAdvanceSeconds = 0.3f;
        [SerializeField] private float damageMeterWindowSeconds = 5f;
        [SerializeField] private float cardSpacing = 1.18f;
        [SerializeField] private Vector3 handCenter = new Vector3(0f, 0.18f, -4.15f);
        [SerializeField] private Vector2 cardSize = new Vector2(1f, 1.45f);
        [SerializeField] private Color attackCardColor = Color.white;
        [SerializeField] private Color heavyAttackCardColor = new Color(0.72f, 0.24f, 0.82f);
        [SerializeField] private Color splashCardColor = new Color(0.92f, 0.06f, 0.04f);
        [SerializeField] private Color shockCardColor = new Color(0.1f, 0.46f, 1f);
        [SerializeField] private Color poisonCardColor = new Color(0.16f, 0.68f, 0.26f);
        [SerializeField] private Color pierceCardColor = new Color(0.12f, 0.82f, 0.86f);
        [SerializeField] private Color gravitonCardColor = Color.black;
        [SerializeField] private Color shieldBlockerCardColor = new Color(0.78f, 0.82f, 0.84f);
        [SerializeField] private Color powerCardColor = new Color(0.92f, 0.68f, 0.12f);
        [SerializeField] private Color manaGeneratorCardColor = new Color(0.18f, 0.76f, 0.95f);
        [SerializeField] private Color manaCapacityCardColor = new Color(0.38f, 0.52f, 1f);

        private readonly List<IHandCard> cards = new List<IHandCard>();
        private readonly Dictionary<IHandCard, CardDefinition> cardDefinitions = new Dictionary<IHandCard, CardDefinition>();
        private readonly Queue<CardDefinition> returningCards = new Queue<CardDefinition>();
        private CardDeckRuntime deck;
        private float drawTimer;
        private PlayerManaState manaState;
        private PlayerHealthState healthState;
        private PlayerHandLayout handLayout;
        private PlayerHudView hudView;
        private PlayerCardFactory cardFactory;

        public Vector3 TargetPoint => handLayout.TargetPoint;

        public void ConfigureDeckCounts(
            int normal,
            int heavy,
            int splash,
            int shock,
            int poison,
            int pierce,
            int graviton,
            int shieldBlocker,
            int power,
            int manaGenerator,
            int manaCapacity,
            int startingHand,
            int maxHand,
            int turret = 0)
        {
            normalCardCount = Mathf.Max(0, normal);
            heavyCardCount = Mathf.Max(0, heavy);
            splashCardCount = Mathf.Max(0, splash);
            shockCardCount = Mathf.Max(0, shock);
            poisonCardCount = Mathf.Max(0, poison);
            pierceCardCount = Mathf.Max(0, pierce);
            gravitonCardCount = Mathf.Max(0, graviton);
            shieldBlockerCardCount = Mathf.Max(0, shieldBlocker);
            powerCardCount = Mathf.Max(0, power);
            manaGeneratorCardCount = Mathf.Max(0, manaGenerator);
            manaCapacityCardCount = Mathf.Max(0, manaCapacity);
            turretCardCount = Mathf.Max(0, turret);
            maxHandSize = Mathf.Max(1, maxHand);
            startingHandCount = Mathf.Clamp(startingHand, 0, maxHandSize);
        }

        public void ConfigureMana(int maximum, int starting)
        {
            maxMana = Mathf.Max(0, maximum);
            startingMana = Mathf.Clamp(starting, 0, maxMana);
            manaState = new PlayerManaState(maxMana, startingMana, manaRegenInterval);
        }

        private void Awake()
        {
            manaState = new PlayerManaState(maxMana, startingMana, manaRegenInterval);
            healthState = new PlayerHealthState(maxPlayerHealth);
            handLayout = new PlayerHandLayout(handCenter, cardSpacing);
            cardFactory = new PlayerCardFactory(
                handCenter,
                cardSize,
                TrySpendMana,
                AddManaRegenIntervalReduction,
                RemoveManaRegenIntervalReduction,
                AddMaxManaBonus,
                RemoveMaxManaBonus);
        }

        private void Start()
        {
            CombatTargetRegistry.SetPlayer(this);
            deck = new CardDeckRuntime(CreateStartingDeck());
            hudView = new PlayerHudView(transform, HandleAdvanceButtonClicked);
            hudView.Create();
            SpawnInitialHand();
        }

        private void Update()
        {
            manaState.Tick(Time.deltaTime);
            AddDrawProgress(Time.deltaTime);
            hudView.Tick(Time.deltaTime);
            RefreshCardAvailability();
            ReflowHand();
            RefreshHud();
        }

        private void OnDestroy()
        {
            CombatTargetRegistry.ClearPlayer(this);
        }

        public void SpawnInitialHand()
        {
            for (int i = 0; i < startingHandCount; i++)
            {
                TryDrawCard(true);
            }

            ReflowHand(true);
        }

        public void AddCardToDeck(CardDefinition definition)
        {
            if (deck == null)
            {
                throw new System.InvalidOperationException("The deck must be initialized before adding a card.");
            }

            deck.AddCard(definition);
            RefreshHud();
        }

        private void HandleCardPlayed(IHandCard card)
        {
            card.LeftHand -= HandleCardLeftHand;
            card.Played -= HandleCardPlayed;

            if (cardDefinitions.TryGetValue(card, out CardDefinition definition))
            {
                if (definition.Kind == CardKind.Turret)
                {
                    returningCards.Enqueue(definition);
                }
                else
                {
                    deck.Discard(definition);
                }

                cardDefinitions.Remove(card);
            }

            cards.Remove(card);
            ReflowHand(true);
        }

        private void HandleCardLeftHand(IHandCard card)
        {
            cards.Remove(card);
            ReflowHand(true);
        }

        private void AddManaProgress(float seconds)
        {
            manaState.Tick(seconds);
        }

        private void AddDrawProgress(float seconds)
        {
            while (returningCards.Count > 0 && cards.Count < maxHandSize)
            {
                AddCardToHand(returningCards.Dequeue(), true);
            }

            drawTimer += seconds;

            if (drawTimer < drawInterval)
            {
                return;
            }

            if (TryDrawCard(false))
            {
                drawTimer = 0f;
            }
        }

        private void HandleAdvanceButtonClicked()
        {
            AddManaProgress(manaButtonAdvanceSeconds);
            AddDrawProgress(drawButtonAdvanceSeconds);
            RefreshHud();
        }

        private void AddManaRegenIntervalReduction(float seconds)
        {
            manaState.AddRegenIntervalReduction(seconds);
        }

        private void RemoveManaRegenIntervalReduction(float seconds)
        {
            manaState.RemoveRegenIntervalReduction(seconds);
        }

        private void AddMaxManaBonus(int amount)
        {
            manaState.AddMaxManaBonus(amount);
            RefreshCardAvailability();
            RefreshHud();
        }

        private void RemoveMaxManaBonus(int amount)
        {
            manaState.RemoveMaxManaBonus(amount);
            RefreshCardAvailability();
            RefreshHud();
        }

        private List<CardDefinition> CreateStartingDeck()
        {
            PrototypeDeckCounts counts = new PrototypeDeckCounts(
                normalCardCount,
                heavyCardCount,
                splashCardCount,
                shockCardCount,
                poisonCardCount,
                pierceCardCount,
                gravitonCardCount,
                shieldBlockerCardCount,
                powerCardCount,
                manaGeneratorCardCount,
                manaCapacityCardCount,
                turretCardCount);
            PrototypeCardPalette palette = new PrototypeCardPalette(
                attackCardColor,
                heavyAttackCardColor,
                splashCardColor,
                shockCardColor,
                poisonCardColor,
                pierceCardColor,
                gravitonCardColor,
                shieldBlockerCardColor,
                powerCardColor,
                manaGeneratorCardColor,
                manaCapacityCardColor);
            return PrototypeDeckBuilder.Create(counts, palette);
        }

        private bool TryDrawCard(bool snap)
        {
            if (cards.Count >= maxHandSize)
            {
                return false;
            }

            if (!deck.TryDraw(out CardDefinition definition))
            {
                return false;
            }

            AddCardToHand(definition, snap);
            return true;
        }

        private void AddCardToHand(CardDefinition definition, bool snap)
        {
            IHandCard card = CreateHandCard(definition);
            cards.Add(card);
            cardDefinitions.Add(card, definition);
            ReflowHand(snap);
        }

        private IHandCard CreateHandCard(CardDefinition definition)
        {
            IHandCard card = cardFactory.Create(definition);
            if (card is FieldStructureCard fieldCard)
            {
                fieldCard.RefundMana = RefundMana;
            }

            card.LeftHand += HandleCardLeftHand;
            card.Played += HandleCardPlayed;
            return card;
        }

        private bool TrySpendMana(int amount)
        {
            bool spent = manaState.TrySpend(amount);
            if (spent)
            {
                RefreshCardAvailability();
            }

            return spent;
        }

        private void RefundMana(int amount)
        {
            manaState.Restore(amount);
            RefreshCardAvailability();
            RefreshHud();
        }

        public void ReceiveEnemyAttack(float damage)
        {
            float clampedDamage = Mathf.Max(0f, damage);
            if (clampedDamage <= 0f)
            {
                return;
            }

            bool defeated = healthState.ApplyDamage(clampedDamage);
            hudView.ShowDamageFlash(defeated ? 0.82f : 0.28f);

            if (defeated)
            {
                Debug.Log("Player defeated. Prototype health reset.");
            }

            RefreshHud();
        }

        private void RefreshCardAvailability()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                IHandCard card = cards[i];
                if (card == null)
                {
                    continue;
                }

                card.SetPlayable(manaState.CurrentMana >= card.ManaCost);
            }
        }

        private void ReflowHand(bool snap = false)
        {
            handLayout.Apply(cards, snap);
        }

        private void RefreshHud()
        {
            float nextDraw = Mathf.Max(0f, drawInterval - drawTimer);
            hudView.RefreshDeck(deck, manaState, cards.Count, maxHandSize, nextDraw);
            hudView.RefreshHealth(healthState);
            hudView.RefreshDamageWindow(damageMeterWindowSeconds);
        }
    }

}
