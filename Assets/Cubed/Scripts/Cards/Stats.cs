using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cubed
{
    /// <summary>Every tunable number on a player. Cards modify these.</summary>
    public enum Stat
    {
        MaxHealth,
        MoveSpeed,
        JumpHeight,
        ExtraJumps,
        AirControl,
        Damage,
        AttackCooldown,
        Ammo,
        ReloadTime,
        BulletSpeed,
        BulletGravity,
        BulletsPerShot,
        Spread,
        Bounces,
        BulletSize,
        BulletLifetime,
        Knockback,
        Lifesteal,
        BlockCooldown,
        BlockDuration,
        Regen,
        Homing,
        ExplosionRadius,
        Pierce,
        ExtraLives,
        PlayerSize,
        DamageTaken,
        HazardDamageTaken,
        HandSize,
    }

    public struct StatDef
    {
        public string Label;
        public float Base, Min, Max;
        public bool Integer;
        /// <summary>+1 when bigger is better for the player, -1 when smaller is better, 0 when neutral.</summary>
        public int Polarity;
        public string Unit;
    }

    public static class StatInfo
    {
        static readonly StatDef[] defs = BuildDefs();

        public static int Count => defs.Length;
        public static StatDef Get(Stat s) => defs[(int)s];

        /// <summary>Change a base value (e.g. a mod that makes everyone faster).</summary>
        public static void SetBase(Stat s, float value) => defs[(int)s].Base = value;

        /// <summary>Change clamping limits for a stat.</summary>
        public static void SetLimits(Stat s, float min, float max)
        {
            defs[(int)s].Min = min;
            defs[(int)s].Max = max;
        }

        static StatDef[] BuildDefs()
        {
            var d = new StatDef[Enum.GetValues(typeof(Stat)).Length];

            void D(Stat s, string label, float b, float min, float max, int polarity = 1, bool integer = false, string unit = "")
            {
                d[(int)s] = new StatDef { Label = label, Base = b, Min = min, Max = max, Polarity = polarity, Integer = integer, Unit = unit };
            }

            D(Stat.MaxHealth, "Max HP", 100f, 1f, 10000f);
            D(Stat.MoveSpeed, "Move Speed", 8f, 1f, 40f);
            D(Stat.JumpHeight, "Jump Height", 2.3f, 0.3f, 20f);
            D(Stat.ExtraJumps, "Air Jumps", 0f, 0f, 10f, 1, true);
            D(Stat.AirControl, "Air Control", 0.6f, 0.05f, 3f);
            D(Stat.Damage, "Damage", 25f, 0.5f, 2000f);
            D(Stat.AttackCooldown, "Attack Delay", 0.32f, 0.03f, 5f, -1, false, "s");
            D(Stat.Ammo, "Ammo", 3f, 1f, 200f, 1, true);
            D(Stat.ReloadTime, "Reload Time", 1.6f, 0.1f, 15f, -1, false, "s");
            D(Stat.BulletSpeed, "Bullet Speed", 45f, 4f, 400f);
            D(Stat.BulletGravity, "Bullet Drop", 6f, 0f, 120f, -1);
            D(Stat.BulletsPerShot, "Bullets", 1f, 1f, 40f, 1, true);
            D(Stat.Spread, "Spread", 2f, 0f, 120f, -1, false, "°");
            D(Stat.Bounces, "Bounces", 0f, 0f, 40f, 1, true);
            D(Stat.BulletSize, "Bullet Size", 0.12f, 0.03f, 3f);
            D(Stat.BulletLifetime, "Bullet Range", 4f, 0.3f, 30f, 1, false, "s");
            D(Stat.Knockback, "Knockback", 6f, 0f, 150f);
            D(Stat.Lifesteal, "Lifesteal", 0f, 0f, 5f);
            D(Stat.BlockCooldown, "Block Cooldown", 4f, 0.25f, 30f, -1, false, "s");
            D(Stat.BlockDuration, "Block Duration", 0.35f, 0.05f, 6f, 1, false, "s");
            D(Stat.Regen, "Regen", 0f, 0f, 200f, 1, false, "/s");
            D(Stat.Homing, "Homing", 0f, 0f, 20f);
            D(Stat.ExplosionRadius, "Blast Radius", 0f, 0f, 20f, 1, false, "m");
            D(Stat.Pierce, "Pierce", 0f, 0f, 30f, 1, true);
            D(Stat.ExtraLives, "Extra Lives", 0f, 0f, 10f, 1, true);
            D(Stat.PlayerSize, "Size", 1f, 0.4f, 3f, 0);
            D(Stat.DamageTaken, "Damage Taken", 1f, 0.05f, 10f, -1);
            D(Stat.HazardDamageTaken, "Border/Fall Damage Taken", 1f, 0f, 10f, -1);
            D(Stat.HandSize, "Card Choices", 0f, -4f, 6f, 1, true);
            return d;
        }

        static string Num(float v)
        {
            if (Mathf.Abs(v - Mathf.Round(v)) < 0.001f) return Mathf.RoundToInt(v).ToString();
            return v.ToString("0.##");
        }

        /// <summary>Human readable text for a modifier plus whether it helps (+1), hurts (-1) or is neutral (0).</summary>
        public static string Format(StatModifier m, out int polarity)
        {
            var d = Get(m.Stat);
            int dir;
            string text;
            switch (m.Op)
            {
                case StatOp.Add:
                    text = (m.Value >= 0 ? "+" : "") + Num(m.Value) + d.Unit + " " + d.Label;
                    dir = m.Value >= 0 ? 1 : -1;
                    break;
                case StatOp.Multiply:
                    float pct = (m.Value - 1f) * 100f;
                    text = (pct >= 0 ? "+" : "") + Mathf.RoundToInt(pct) + "% " + d.Label;
                    dir = m.Value >= 1f ? 1 : -1;
                    break;
                default:
                    text = d.Label + " = " + Num(m.Value) + d.Unit;
                    dir = m.Value >= d.Base ? 1 : -1;
                    break;
            }
            polarity = d.Polarity == 0 ? 0 : dir * d.Polarity;
            return text;
        }
    }

    public enum StatOp { Add, Multiply, Set }

    [Serializable]
    public struct StatModifier
    {
        public Stat Stat;
        public StatOp Op;
        public float Value;

        public StatModifier(Stat stat, StatOp op, float value)
        {
            Stat = stat;
            Op = op;
            Value = value;
        }

        public override string ToString() => StatInfo.Format(this, out _);
    }

    /// <summary>
    /// Computed stat block. Order independent: value = Set ?? (base + ΣAdd) × ΠMultiply, then clamped.
    /// </summary>
    public class PlayerStats
    {
        readonly float[] values = new float[StatInfo.Count];
        readonly float[] adds = new float[StatInfo.Count];
        readonly float[] muls = new float[StatInfo.Count];
        readonly float[] sets = new float[StatInfo.Count];
        readonly bool[] hasSet = new bool[StatInfo.Count];

        public PlayerStats() { Recalculate(null, null); }

        public float this[Stat s] => values[(int)s];
        public int Int(Stat s) => Mathf.RoundToInt(values[(int)s]);

        public void Recalculate(IList<CardDefinition> cards, IList<StatModifier> extra)
        {
            for (int i = 0; i < values.Length; i++)
            {
                adds[i] = 0f;
                muls[i] = 1f;
                hasSet[i] = false;
            }
            if (cards != null)
                foreach (var c in cards)
                    if (c != null && c.Modifiers != null)
                        foreach (var m in c.Modifiers) Accumulate(m);
            if (extra != null)
                foreach (var m in extra) Accumulate(m);

            for (int i = 0; i < values.Length; i++)
            {
                var d = StatInfo.Get((Stat)i);
                float v = hasSet[i] ? sets[i] : (d.Base + adds[i]) * muls[i];
                v = Mathf.Clamp(v, d.Min, d.Max);
                if (d.Integer) v = Mathf.Round(v);
                values[i] = v;
            }
        }

        void Accumulate(StatModifier m)
        {
            int i = (int)m.Stat;
            if (i < 0 || i >= values.Length) return;
            switch (m.Op)
            {
                case StatOp.Add: adds[i] += m.Value; break;
                case StatOp.Multiply: muls[i] *= m.Value; break;
                case StatOp.Set: sets[i] = m.Value; hasSet[i] = true; break;
            }
        }
    }
}
