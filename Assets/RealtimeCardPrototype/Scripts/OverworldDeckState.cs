using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RealtimeCardPrototype
{
    public sealed class OverworldCardEntry
    {
        public OverworldCardEntry(string id, string ownerTag, CardDefinition definition, int totalCopies)
        {
            Id = id;
            OwnerTag = ownerTag;
            Definition = definition;
            TotalCopies = totalCopies;
        }

        public string Id { get; }
        public string OwnerTag { get; }
        public CardDefinition Definition { get; }
        public int TotalCopies { get; }
    }

    public static class OverworldDeckState
    {
        public const int ActiveDeckLimit = 15;

        private const string SavedDeckKey = "RealtimeCardPrototype.Overworld.ActiveDeck.v3";
        private static readonly List<OverworldCardEntry> collection = CreateCollection();
        private static readonly List<string> activeCardIds = new List<string>();
        private static bool isLoaded;

        public static IReadOnlyList<OverworldCardEntry> Collection => collection;
        public static IReadOnlyList<string> ActiveCardIds
        {
            get
            {
                EnsureLoaded();
                return activeCardIds;
            }
        }

        public static int GetAvailableCopies(string id)
        {
            EnsureLoaded();
            OverworldCardEntry entry = FindEntry(id);
            return entry.TotalCopies - activeCardIds.Count(cardId => cardId == id);
        }

        public static int GetActiveCardCount(string id)
        {
            EnsureLoaded();
            return activeCardIds.Count(cardId => cardId == id);
        }

        public static bool TryAdd(string id)
        {
            EnsureLoaded();
            if (activeCardIds.Count >= ActiveDeckLimit || GetAvailableCopies(id) <= 0)
            {
                return false;
            }

            activeCardIds.Add(id);
            return true;
        }

        public static void RemoveAt(int index)
        {
            EnsureLoaded();
            if (index >= 0 && index < activeCardIds.Count)
            {
                activeCardIds.RemoveAt(index);
            }
        }

        public static void Save()
        {
            EnsureLoaded();
            PlayerPrefs.SetString(SavedDeckKey, string.Join("|", activeCardIds));
            PlayerPrefs.Save();
        }

        public static List<CardDefinition> GetActiveDeck()
        {
            EnsureLoaded();
            List<CardDefinition> definitions = new List<CardDefinition>(activeCardIds.Count);
            for (int i = 0; i < activeCardIds.Count; i++)
            {
                definitions.Add(FindEntry(activeCardIds[i]).Definition);
            }

            return definitions;
        }

        public static OverworldCardEntry FindEntry(string id)
        {
            for (int i = 0; i < collection.Count; i++)
            {
                if (collection[i].Id == id)
                {
                    return collection[i];
                }
            }

            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown overworld card id.");
        }

        private static void EnsureLoaded()
        {
            if (isLoaded)
            {
                return;
            }

            isLoaded = true;
            string savedCards = PlayerPrefs.GetString(SavedDeckKey, string.Empty);
            if (string.IsNullOrEmpty(savedCards))
            {
                AddStarterDeck();
                return;
            }

            string[] ids = savedCards.Split('|');
            for (int i = 0; i < ids.Length && activeCardIds.Count < ActiveDeckLimit; i++)
            {
                if (collection.Any(entry => entry.Id == ids[i]) && GetAvailableCopies(ids[i]) > 0)
                {
                    activeCardIds.Add(ids[i]);
                }
            }
        }

        private static List<OverworldCardEntry> CreateCollection()
        {
            CardHitProfile basic = CardHitProfile.BasicAttack();
            CardHitProfile heavy = CardHitProfile.BasicAttack();
            heavy.damage *= 2f;
            CardHitProfile splash = CardHitProfile.BasicAttack();
            splash.damage *= 1.2f;
            CardHitProfile shock = CardHitProfile.BasicAttack();
            shock.damage *= 0.65f;
            CardHitProfile pierce = CardHitProfile.BasicAttack();
            pierce.damage *= 0.35f;

            return new List<OverworldCardEntry>
            {
                Entry("strike", "Starter Warrior", CardKind.Strike, "STRIKE", "cost 1\nDirect hit", 1, Color.white, basic, 5),
                Entry("heavy", "Starter Warrior", CardKind.Heavy, "HEAVY", "cost 2\nStrong hit", 2, new Color(0.72f, 0.24f, 0.82f), heavy, 5),
                Entry("block", "Starter Warrior", CardKind.Block, "BLOCK", "cost 1\nBlock 3", 1, new Color(0.78f, 0.82f, 0.84f), default, 5),
                new OverworldCardEntry("turret", "Starter Warrior", PrototypeDeckBuilder.Turret, 1),
                Entry("blast", "Storm Mage", CardKind.Blast, "BLAST", "cost 3\nArcane burst", 3, new Color(0.92f, 0.06f, 0.04f), splash, 2),
                Entry("shock", "Storm Mage", CardKind.Shock, "SHOCK", "cost 2\nLightning mark", 2, new Color(0.1f, 0.46f, 1f), shock, 3),
                Entry("pierce", "Storm Mage", CardKind.Pierce, "PIERCE", "cost 2\nPhase strike", 2, new Color(0.12f, 0.82f, 0.86f), pierce, 2)
            };
        }

        private static void AddStarterDeck()
        {
            for (int i = 0; i < collection.Count; i++)
            {
                OverworldCardEntry entry = collection[i];
                if (entry.OwnerTag != "Starter Warrior")
                {
                    continue;
                }

                for (int copyIndex = 0; copyIndex < entry.TotalCopies && activeCardIds.Count < ActiveDeckLimit; copyIndex++)
                {
                    activeCardIds.Add(entry.Id);
                }
            }

            Save();
        }

        private static OverworldCardEntry Entry(string id, string ownerTag, CardKind kind, string title, string body, int cost, Color color, CardHitProfile profile, int copies)
        {
            return new OverworldCardEntry(id, ownerTag, new CardDefinition(kind, title, body, cost, color, profile), copies);
        }
    }
}
