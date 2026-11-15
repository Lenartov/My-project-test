using System;
using UnityEngine;

namespace RealtimeCardPrototype
{
    public sealed class PlayerCardFactory
    {
        private readonly PlayerCardCatalog catalog;
        private readonly Vector3 handPosition;
        private readonly Vector2 cardSize;
        private readonly Func<int, bool> trySpendMana;
        private readonly Action<float> addManaRegenIntervalReduction;
        private readonly Action<float> removeManaRegenIntervalReduction;
        private readonly Action<int> addMaxManaBonus;
        private readonly Action<int> removeMaxManaBonus;

        public PlayerCardFactory(
            Vector3 handPosition,
            Vector2 cardSize,
            Func<int, bool> trySpendMana,
            Action<float> addManaRegenIntervalReduction,
            Action<float> removeManaRegenIntervalReduction,
            Action<int> addMaxManaBonus,
            Action<int> removeMaxManaBonus)
        {
            catalog = PlayerCardCatalog.Load();
            this.handPosition = handPosition;
            this.cardSize = cardSize;
            this.trySpendMana = trySpendMana;
            this.addManaRegenIntervalReduction = addManaRegenIntervalReduction;
            this.removeManaRegenIntervalReduction = removeManaRegenIntervalReduction;
            this.addMaxManaBonus = addMaxManaBonus;
            this.removeMaxManaBonus = removeMaxManaBonus;
        }

        public IHandCard Create(CardDefinition definition)
        {
            CardView prefab = catalog.GetPrefab(definition.Kind);
            CardView view = UnityEngine.Object.Instantiate(prefab, handPosition, Quaternion.identity);
            view.name = definition.Title;
            view.ConfigurePlayer(definition, cardSize);

            IHandCard card = view.GetComponent<IHandCard>();
            card.ConfigureManaCost(definition.ManaCost);
            card.TryPayMana = trySpendMana;

            switch (card)
            {
                case AttackCard attack:
                    attack.ConfigureProfile(definition.HitProfile);
                    break;
                case SplashAttackCard blast:
                    blast.ConfigureProfile(definition.HitProfile);
                    break;
                case ShockMarkCard shock:
                    shock.ConfigureProfile(definition.HitProfile);
                    break;
                case PierceAttackCard pierce:
                    pierce.ConfigureProfile(definition.HitProfile);
                    break;
                case ManaGeneratorCard focus:
                    focus.ManaRegenReductionStarted = addManaRegenIntervalReduction;
                    focus.ManaRegenReductionEnded = removeManaRegenIntervalReduction;
                    break;
                case ManaCapacityCard well:
                    well.MaxManaBonusStarted = addMaxManaBonus;
                    well.MaxManaBonusEnded = removeMaxManaBonus;
                    break;
            }

            return card;
        }
    }
}
