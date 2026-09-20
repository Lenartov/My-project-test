using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    public readonly struct CardDefinition
    {
        public readonly CardKind Kind;
        public readonly string Title;
        public readonly string Body;
        public readonly int ManaCost;
        public readonly Color Color;
        public readonly CardHitProfile HitProfile;

        public CardDefinition(CardKind kind, string title, string body, int manaCost, Color color, CardHitProfile hitProfile)
        {
            Kind = kind;
            Title = title;
            Body = body;
            ManaCost = manaCost;
            Color = color;
            HitProfile = hitProfile;
        }
    }

    public enum CardKind
    {
        Attack,
        SplashAttack,
        ShockMark,
        PoisonHover,
        PierceAttack,
        Graviton,
        ShieldBlocker,
        PowerBoost,
        ManaGenerator,
        ManaCapacity
    }

    public sealed class CardDeckRuntime
    {
        private readonly Queue<CardDefinition> drawPile = new Queue<CardDefinition>();
        private readonly List<CardDefinition> discardPile = new List<CardDefinition>();
        private readonly System.Random random = new System.Random();

        public int DrawCount => drawPile.Count;
        public int DiscardCount => discardPile.Count;

        public CardDeckRuntime(List<CardDefinition> startingCards)
        {
            List<CardDefinition> cards = new List<CardDefinition>(startingCards);
            ShuffleIntoDrawPile(cards);
        }

        public bool TryDraw(out CardDefinition definition)
        {
            if (drawPile.Count == 0)
            {
                RefillDrawPileFromDiscard();
            }

            if (drawPile.Count == 0)
            {
                definition = default;
                return false;
            }

            definition = drawPile.Dequeue();
            return true;
        }

        public void Discard(CardDefinition definition)
        {
            discardPile.Add(definition);
        }

        public void AddCard(CardDefinition definition)
        {
            drawPile.Enqueue(definition);
        }

        private void RefillDrawPileFromDiscard()
        {
            if (discardPile.Count == 0)
            {
                return;
            }

            ShuffleIntoDrawPile(discardPile);
            discardPile.Clear();
            Debug.Log("Draw pile empty. Discard reshuffled into deck.");
        }

        private void ShuffleIntoDrawPile(List<CardDefinition> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                CardDefinition temporary = cards[i];
                cards[i] = cards[swapIndex];
                cards[swapIndex] = temporary;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                drawPile.Enqueue(cards[i]);
            }
        }
    }
}
