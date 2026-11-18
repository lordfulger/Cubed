using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spheres
{
    /// <summary>All cards known to the game. Mods add cards here (directly or via <see cref="SpheresAPI"/>).</summary>
    public static class CardRegistry
    {
        static readonly Dictionary<string, CardDefinition> byId = new Dictionary<string, CardDefinition>();
        static readonly List<CardDefinition> ordered = new List<CardDefinition>();

        static CardRegistry()
        {
            BuiltInCards.RegisterAll();
        }

        public static IReadOnlyList<CardDefinition> All => ordered;

        public static void Register(CardDefinition card)
        {
            if (card == null || string.IsNullOrEmpty(card.Id))
            {
                Debug.LogWarning("[Spheres] Tried to register a card without an id");
                return;
            }
            if (string.IsNullOrEmpty(card.Title)) card.Title = card.Id;
            if (card.Description == null) card.Description = "";
            foreach (var e in card.Effects)
                if (!CardEffectRegistry.Exists(e))
                    Debug.LogWarning("[Spheres] Card '" + card.Id + "' references unknown effect '" + e + "' (register it before the card is picked)");

            if (byId.TryGetValue(card.Id, out var existing))
            {
                ordered[ordered.IndexOf(existing)] = card;
                Debug.Log("[Spheres] Card '" + card.Id + "' replaced by " + card.Source);
            }
            else ordered.Add(card);
            byId[card.Id] = card;
        }

        public static bool Unregister(string id)
        {
            if (id == null || !byId.TryGetValue(id, out var c)) return false;
            byId.Remove(id);
            ordered.Remove(c);
            return true;
        }

        public static CardDefinition Get(string id) => id != null && byId.TryGetValue(id, out var c) ? c : null;

        /// <summary>
        /// Chance of each rarity per card slot. The rarity is rolled first and then a card of that rarity is picked,
        /// so the odds don't depend on how many cards each rarity has (there are far more commons than legendaries).
        /// Mods can change these.
        /// </summary>
        public static float RarityWeight(Rarity r)
        {
            switch (r)
            {
                case Rarity.Uncommon: return UncommonChance;
                case Rarity.Rare: return RareChance;
                case Rarity.Legendary: return LegendaryChance;
                default: return CommonChance;
            }
        }

        public static float CommonChance = 0.52f, UncommonChance = 0.30f, RareChance = 0.13f, LegendaryChance = 0.05f;

        public static bool IsEnabled(CardDefinition c, MatchSettings settings) =>
            settings == null || settings.disabledCards == null || !settings.disabledCards.Contains(c.Id);

        /// <summary>Can this player be offered this card right now?</summary>
        public static bool IsEligible(CardDefinition c, Player p, MatchSettings settings)
        {
            if (!IsEnabled(c, settings)) return false;
            if (p != null)
            {
                if (c.MaxStacks > 0 && p.Cards.Count(x => x == c) >= c.MaxStacks) return false;
                if (c.CanAppear != null)
                {
                    try { if (!c.CanAppear(p)) return false; }
                    catch (System.Exception e) { Debug.LogException(e); return false; }
                }
            }
            return true;
        }

        /// <summary>Draw a hand of distinct cards using rarity weights.</summary>
        public static List<CardDefinition> DrawHand(Player p, int count, MatchSettings settings)
        {
            var pool = ordered.Where(c => IsEligible(c, p, settings)).ToList();
            var hand = new List<CardDefinition>();
            while (hand.Count < count && pool.Count > 0)
            {
                // 1) roll a rarity (only among rarities that still have cards), 2) pick a card of that rarity.
                var byRarity = pool.Where(c => c.WeightMultiplier > 0f).GroupBy(c => c.Rarity).ToList();
                if (byRarity.Count == 0) break;
                float total = byRarity.Sum(g => RarityWeight(g.Key));
                float roll = Random.value * total;
                var group = byRarity[byRarity.Count - 1];
                foreach (var g in byRarity)
                {
                    roll -= RarityWeight(g.Key);
                    if (roll <= 0f) { group = g; break; }
                }
                float inner = group.Sum(c => c.WeightMultiplier);
                float pick = Random.value * inner;
                CardDefinition chosen = group.Last();
                foreach (var c in group)
                {
                    pick -= c.WeightMultiplier;
                    if (pick <= 0f) { chosen = c; break; }
                }
                hand.Add(chosen);
                pool.Remove(chosen);
            }
            return hand;
        }
    }
}
