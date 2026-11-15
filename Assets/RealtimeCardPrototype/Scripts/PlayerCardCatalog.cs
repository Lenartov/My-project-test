using System;
using UnityEngine;

namespace RealtimeCardPrototype
{
    public sealed class PlayerCardCatalog : ScriptableObject
    {
        private const string ResourcePath = "Cards/PlayerCardCatalog";

        [Serializable]
        private struct Entry
        {
            public CardKind kind;
            public CardView prefab;
        }

        [SerializeField] private Entry[] cards;

        public static PlayerCardCatalog Load()
        {
            PlayerCardCatalog catalog = Resources.Load<PlayerCardCatalog>(ResourcePath);
            if (catalog == null)
            {
                throw new InvalidOperationException($"Player card catalog is missing from Resources/{ResourcePath}.");
            }

            return catalog;
        }

        public CardView GetPrefab(CardKind kind)
        {
            foreach (Entry entry in cards)
            {
                if (entry.kind == kind && entry.prefab != null)
                {
                    return entry.prefab;
                }
            }

            throw new InvalidOperationException($"No player card prefab is assigned for {kind}.");
        }
    }
}
