using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    public sealed class PlayerCardFactory
    {
        private delegate IHandCard CardCreator(CardView view, CardDefinition definition);

        private readonly Vector3 handPosition;
        private readonly Vector2 cardSize;
        private readonly Func<int, bool> trySpendMana;
        private readonly Action<float> addManaRegenIntervalReduction;
        private readonly Action<float> removeManaRegenIntervalReduction;
        private readonly Action<int> addMaxManaBonus;
        private readonly Action<int> removeMaxManaBonus;
        private readonly Dictionary<CardKind, CardCreator> creators;

        public PlayerCardFactory(
            Vector3 handPosition,
            Vector2 cardSize,
            Func<int, bool> trySpendMana,
            Action<float> addManaRegenIntervalReduction,
            Action<float> removeManaRegenIntervalReduction,
            Action<int> addMaxManaBonus,
            Action<int> removeMaxManaBonus)
        {
            this.handPosition = handPosition;
            this.cardSize = cardSize;
            this.trySpendMana = trySpendMana;
            this.addManaRegenIntervalReduction = addManaRegenIntervalReduction;
            this.removeManaRegenIntervalReduction = removeManaRegenIntervalReduction;
            this.addMaxManaBonus = addMaxManaBonus;
            this.removeMaxManaBonus = removeMaxManaBonus;
            creators = new Dictionary<CardKind, CardCreator>
            {
                { CardKind.Attack, CreateAttackCard },
                { CardKind.SplashAttack, CreateSplashCard },
                { CardKind.ShockMark, CreateShockCard },
                { CardKind.PoisonHover, CreatePoisonCard },
                { CardKind.PierceAttack, CreatePierceCard },
                { CardKind.Graviton, CreateGravitonCard },
                { CardKind.ShieldBlocker, CreateShieldCard },
                { CardKind.PowerBoost, CreatePowerCard },
                { CardKind.ManaGenerator, CreateManaGeneratorCard },
                { CardKind.ManaCapacity, CreateManaCapacityCard }
            };
        }

        public IHandCard Create(CardDefinition definition)
        {
            CardView view = CardView.Create(definition.Title, handPosition, cardSize, definition.Color, CardTeam.Player);
            view.Configure(CardTeam.Player, definition.Title, definition.Body, definition.Color, GetReadableTextColor(definition.Color));

            if (!creators.TryGetValue(definition.Kind, out CardCreator creator))
            {
                throw new ArgumentOutOfRangeException(nameof(definition), definition.Kind, "No player card creator is registered for this card kind.");
            }

            IHandCard card = creator(view, definition);
            card.ConfigureManaCost(definition.ManaCost);
            return card;
        }

        private IHandCard CreateAttackCard(CardView view, CardDefinition definition)
        {
            AttackCard card = view.gameObject.AddComponent<AttackCard>();
            card.ConfigureProfile(definition.HitProfile);
            card.TryPayMana = trySpendMana;
            return card;
        }

        private IHandCard CreateSplashCard(CardView view, CardDefinition definition)
        {
            SplashAttackCard card = view.gameObject.AddComponent<SplashAttackCard>();
            card.ConfigureProfile(definition.HitProfile);
            card.TryPayMana = trySpendMana;
            return card;
        }

        private IHandCard CreateShockCard(CardView view, CardDefinition definition)
        {
            ShockMarkCard card = view.gameObject.AddComponent<ShockMarkCard>();
            card.ConfigureProfile(definition.HitProfile);
            card.TryPayMana = trySpendMana;
            return card;
        }

        private IHandCard CreatePoisonCard(CardView view, CardDefinition definition)
        {
            PoisonHoverCard card = view.gameObject.AddComponent<PoisonHoverCard>();
            card.TryPayMana = trySpendMana;
            return card;
        }

        private IHandCard CreatePierceCard(CardView view, CardDefinition definition)
        {
            PierceAttackCard card = view.gameObject.AddComponent<PierceAttackCard>();
            card.ConfigureProfile(definition.HitProfile);
            card.TryPayMana = trySpendMana;
            return card;
        }

        private IHandCard CreateGravitonCard(CardView view, CardDefinition definition)
        {
            GravitonCard card = view.gameObject.AddComponent<GravitonCard>();
            card.TryPayMana = trySpendMana;
            return card;
        }

        private IHandCard CreateShieldCard(CardView view, CardDefinition definition)
        {
            ShieldBlockerCard card = view.gameObject.AddComponent<ShieldBlockerCard>();
            card.TryPayMana = trySpendMana;
            return card;
        }

        private IHandCard CreatePowerCard(CardView view, CardDefinition definition)
        {
            PowerBoostCard card = view.gameObject.AddComponent<PowerBoostCard>();
            card.TryPayMana = trySpendMana;
            return card;
        }

        private IHandCard CreateManaGeneratorCard(CardView view, CardDefinition definition)
        {
            ManaGeneratorCard card = view.gameObject.AddComponent<ManaGeneratorCard>();
            card.TryPayMana = trySpendMana;
            card.ManaRegenReductionStarted = addManaRegenIntervalReduction;
            card.ManaRegenReductionEnded = removeManaRegenIntervalReduction;
            return card;
        }

        private IHandCard CreateManaCapacityCard(CardView view, CardDefinition definition)
        {
            ManaCapacityCard card = view.gameObject.AddComponent<ManaCapacityCard>();
            card.TryPayMana = trySpendMana;
            card.MaxManaBonusStarted = addMaxManaBonus;
            card.MaxManaBonusEnded = removeMaxManaBonus;
            return card;
        }

        private Color GetReadableTextColor(Color backgroundColor)
        {
            float luminance = backgroundColor.r * 0.2126f + backgroundColor.g * 0.7152f + backgroundColor.b * 0.0722f;
            return luminance > 0.62f ? Color.black : Color.white;
        }
    }
}
