using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cubed
{
    public enum Rarity { Common, Uncommon, Rare, Legendary }

    /// <summary>
    /// A card that can be offered during a pick phase. Cards are pure data (stat modifiers + effect ids)
    /// plus optional code hooks, so they can come from C# mods or from JSON files.
    /// </summary>
    public class CardDefinition
    {
        /// <summary>Unique id. Use "author.cardname" for mod cards.</summary>
        public string Id;
        public string Title;
        public string Description;
        public Rarity Rarity = Rarity.Common;
        public Color Color = new Color(0.6f, 0.6f, 0.7f);
        /// <summary>Maximum times one player may own this card. 0 = unlimited.</summary>
        public int MaxStacks;
        /// <summary>Multiplies the rarity weight when drawing hands.</summary>
        public float WeightMultiplier = 1f;
        /// <summary>Name of the mod (or "Cubed") that added this card.</summary>
        public string Source = "Cubed";
        public readonly List<StatModifier> Modifiers = new List<StatModifier>();
        /// <summary>Ids of <see cref="CardEffect"/>s registered in <see cref="CardEffectRegistry"/>.</summary>
        public readonly List<string> Effects = new List<string>();
        public readonly List<string> Tags = new List<string>();

        /// <summary>Optional filter: return false to keep this card out of a player's hand.</summary>
        public Func<Player, bool> CanAppear;
        /// <summary>Optional code that runs when the card is picked.</summary>
        public Action<Player> OnApply;

        public CardDefinition() { }

        public CardDefinition(string id, string title, string description, Rarity rarity, Color color)
        {
            Id = id;
            Title = title;
            Description = description;
            Rarity = rarity;
            Color = color;
        }

        public CardDefinition Add(Stat stat, float value) { Modifiers.Add(new StatModifier(stat, StatOp.Add, value)); return this; }
        public CardDefinition Mul(Stat stat, float value) { Modifiers.Add(new StatModifier(stat, StatOp.Multiply, value)); return this; }
        public CardDefinition Set(Stat stat, float value) { Modifiers.Add(new StatModifier(stat, StatOp.Set, value)); return this; }
        public CardDefinition Effect(string effectId) { Effects.Add(effectId); return this; }
        public CardDefinition Stacks(int max) { MaxStacks = max; return this; }
        public CardDefinition Tag(string tag) { Tags.Add(tag); return this; }

        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Uncommon: return new Color(0.35f, 0.85f, 0.45f);
                case Rarity.Rare: return new Color(0.35f, 0.6f, 1f);
                case Rarity.Legendary: return new Color(1f, 0.6f, 0.15f);
                default: return new Color(0.75f, 0.75f, 0.78f);
            }
        }

        public override string ToString() => Title + " (" + Id + ")";
    }

    /// <summary>
    /// Behaviour attached to a player by a card. Override any hook you need.
    /// One instance is created per picked card, so stacking a card stacks its effect.
    /// </summary>
    public abstract class CardEffect
    {
        public Player Player { get; internal set; }
        public CardDefinition Card { get; internal set; }

        public virtual void OnApplied() { }
        public virtual void OnRemoved() { }
        public virtual void OnRoundStart() { }
        public virtual void OnUpdate(float dt) { }
        /// <summary>Called for each bullet the player fires.</summary>
        public virtual void OnFire(Bullet bullet) { }
        public virtual void OnBulletBounce(Bullet bullet, RaycastHit hit) { }
        public virtual void OnBulletHitTerrain(Bullet bullet, RaycastHit hit) { }
        /// <summary>Called after one of the player's bullets damages someone.</summary>
        public virtual void OnBulletHitPlayer(Bullet bullet, Player target, DamageInfo info) { }
        /// <summary>Called when the player activates block.</summary>
        public virtual void OnBlock() { }
        /// <summary>Called when a block successfully negates damage (bullets, borders, falls...).</summary>
        public virtual void OnBlockedDamage(DamageInfo info) { }
        /// <summary>Return true to keep a blocked bullet alive (e.g. reflect it).</summary>
        public virtual bool TryReflectBullet(Bullet bullet) => false;
        /// <summary>Modify incoming damage before it is applied (info.Amount is writable).</summary>
        public virtual void OnBeforeDamaged(DamageInfo info) { }
        public virtual void OnDamaged(DamageInfo info) { }
        public virtual void OnDealtDamage(Player target, DamageInfo info) { }
        public virtual void OnKill(Player victim) { }
        public virtual void OnRevive() { }
        /// <summary>
        /// Adjust a stat on the fly. Do not call Player.GetStat / Player.MaxHealth from here
        /// (that recurses); read Player.Stats[...] for the card-computed value instead.
        /// </summary>
        public virtual void ModifyStat(Stat stat, ref float value) { }
    }

    /// <summary>Maps effect ids to factories so cards (including JSON cards) can reference effects by name.</summary>
    public static class CardEffectRegistry
    {
        static readonly Dictionary<string, Func<CardEffect>> factories = new Dictionary<string, Func<CardEffect>>();
        static readonly Dictionary<string, string> descriptions = new Dictionary<string, string>();

        static CardEffectRegistry()
        {
            BuiltInEffects.RegisterAll();
        }

        public static void Register(string id, Func<CardEffect> factory, string description = "")
        {
            if (string.IsNullOrEmpty(id) || factory == null) return;
            factories[id] = factory;
            descriptions[id] = description ?? "";
        }

        public static void Register<T>(string id, string description = "") where T : CardEffect, new() =>
            Register(id, () => new T(), description);

        public static bool Exists(string id) => id != null && factories.ContainsKey(id);

        public static IEnumerable<string> Ids => factories.Keys;

        public static string Describe(string id) => descriptions.TryGetValue(id, out var d) ? d : "";

        public static CardEffect Create(string id)
        {
            if (id != null && factories.TryGetValue(id, out var f))
            {
                try { return f(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            else Debug.LogWarning("[Cubed] Unknown card effect '" + id + "'");
            return null;
        }
    }
}
