using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    internal readonly struct PrototypeDeckCounts
    {
        public readonly int Normal;
        public readonly int Heavy;
        public readonly int Splash;
        public readonly int Shock;
        public readonly int Poison;
        public readonly int Pierce;
        public readonly int Graviton;
        public readonly int ShieldBlocker;
        public readonly int Power;
        public readonly int ManaGenerator;
        public readonly int ManaCapacity;
        public readonly int Turret;

        public int Total => Normal + Heavy + Splash + Shock + Poison + Pierce + Graviton + ShieldBlocker + Power + ManaGenerator + ManaCapacity + Turret;

        public PrototypeDeckCounts(
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
            int turret)
        {
            Normal = normal;
            Heavy = heavy;
            Splash = splash;
            Shock = shock;
            Poison = poison;
            Pierce = pierce;
            Graviton = graviton;
            ShieldBlocker = shieldBlocker;
            Power = power;
            ManaGenerator = manaGenerator;
            ManaCapacity = manaCapacity;
            Turret = turret;
        }
    }

    internal readonly struct PrototypeCardPalette
    {
        public readonly Color Attack;
        public readonly Color HeavyAttack;
        public readonly Color Splash;
        public readonly Color Shock;
        public readonly Color Poison;
        public readonly Color Pierce;
        public readonly Color Graviton;
        public readonly Color ShieldBlocker;
        public readonly Color Power;
        public readonly Color ManaGenerator;
        public readonly Color ManaCapacity;

        public PrototypeCardPalette(
            Color attack,
            Color heavyAttack,
            Color splash,
            Color shock,
            Color poison,
            Color pierce,
            Color graviton,
            Color shieldBlocker,
            Color power,
            Color manaGenerator,
            Color manaCapacity)
        {
            Attack = attack;
            HeavyAttack = heavyAttack;
            Splash = splash;
            Shock = shock;
            Poison = poison;
            Pierce = pierce;
            Graviton = graviton;
            ShieldBlocker = shieldBlocker;
            Power = power;
            ManaGenerator = manaGenerator;
            ManaCapacity = manaCapacity;
        }
    }

    internal static class PrototypeDeckBuilder
    {
        internal static readonly CardDefinition Turret = new CardDefinition(
            CardKind.Turret, "TURRET", $"cost {TurretCard.Cost}\nauto-fire", TurretCard.Cost, new Color(0.18f, 0.85f, 0.64f), default);

        public static List<CardDefinition> Create(PrototypeDeckCounts counts, PrototypeCardPalette palette)
        {
            List<CardDefinition> definitions = new List<CardDefinition>(counts.Total);

            CardHitProfile normalProfile = CardHitProfile.BasicAttack();
            CardHitProfile heavyProfile = CreateHeavyProfile();
            CardHitProfile splashProfile = CreateSplashProfile();
            CardHitProfile shockProfile = CreateShockProfile();
            CardHitProfile pierceProfile = CreatePierceProfile();

            AddCopies(definitions, new CardDefinition(CardKind.Strike, "STRIKE", "cost 1", 1, palette.Attack, normalProfile), counts.Normal);
            AddCopies(definitions, new CardDefinition(CardKind.Heavy, "HEAVY", "cost 2", 2, palette.HeavyAttack, heavyProfile), counts.Heavy);
            AddCopies(definitions, new CardDefinition(CardKind.Blast, "BLAST", "cost 3\nsplash", 3, palette.Splash, splashProfile), counts.Splash);
            AddCopies(definitions, new CardDefinition(CardKind.Shock, "SHOCK", "cost 2\nzap", 2, palette.Shock, shockProfile), counts.Shock);
            AddCopies(definitions, new CardDefinition(CardKind.Poison, "POISON", "cost 2\nhover", 2, palette.Poison, default), counts.Poison);
            AddCopies(definitions, new CardDefinition(CardKind.Pierce, "PIERCE", "cost 2\nphase", 2, palette.Pierce, pierceProfile), counts.Pierce);
            AddCopies(definitions, new CardDefinition(CardKind.Grav, "GRAV", "cost 4\npull", 4, palette.Graviton, default), counts.Graviton);
            AddCopies(definitions, new CardDefinition(CardKind.Block, "BLOCK", "cost 1\nblock 3", 1, palette.ShieldBlocker, default), counts.ShieldBlocker);
            AddCopies(definitions, new CardDefinition(CardKind.Power, "POWER", "cost 3\nboost", 3, palette.Power, default), counts.Power);
            AddCopies(definitions, new CardDefinition(CardKind.Focus, "FOCUS", "cost 2\nmana", 2, palette.ManaGenerator, default), counts.ManaGenerator);
            AddCopies(definitions, new CardDefinition(CardKind.Well, "WELL", "cost 3\nmax +1", 3, palette.ManaCapacity, default), counts.ManaCapacity);
            AddCopies(definitions, Turret, counts.Turret);

            return definitions;
        }

        private static CardHitProfile CreateHeavyProfile()
        {
            CardHitProfile profile = CardHitProfile.BasicAttack();
            profile.damage *= 2f;
            profile.postureDamage *= 1.6f;
            profile.impulse *= 1.25f;
            return profile;
        }

        private static CardHitProfile CreateSplashProfile()
        {
            CardHitProfile profile = CardHitProfile.BasicAttack();
            profile.damage *= 1.2f;
            profile.postureDamage *= 1.1f;
            profile.impulse *= 1.05f;
            return profile;
        }

        private static CardHitProfile CreateShockProfile()
        {
            CardHitProfile profile = CardHitProfile.BasicAttack();
            profile.damage *= 0.65f;
            profile.postureDamage *= 0.7f;
            profile.impulse *= 0.8f;
            return profile;
        }

        private static CardHitProfile CreatePierceProfile()
        {
            CardHitProfile profile = CardHitProfile.BasicAttack();
            profile.damage *= 0.35f;
            profile.postureDamage *= 0.5f;
            profile.impulse *= 0.45f;
            profile.maxSpeedForDamage = 30f;
            profile.speedDamageScale = 0.05f;
            return profile;
        }

        private static void AddCopies(List<CardDefinition> definitions, CardDefinition definition, int count)
        {
            for (int i = 0; i < count; i++)
            {
                definitions.Add(definition);
            }
        }
    }
}
