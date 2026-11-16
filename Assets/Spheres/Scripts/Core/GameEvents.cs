using System;
using UnityEngine;

namespace Spheres
{
    public enum DamageType { Bullet, Explosion, Border, Fall, Hazard, Effect, Generic, Trap }

    /// <summary>Describes one instance of damage. Effects and event handlers may change Amount.</summary>
    public class DamageInfo
    {
        public float Amount;
        public Player Source;
        public DamageType Type = DamageType.Generic;
        public Vector3 Point;
        public Vector3 Direction;
        public float Knockback;
        /// <summary>Can an active block negate this damage?</summary>
        public bool Blockable = true;
        /// <summary>The bullet responsible, if any.</summary>
        public Bullet Bullet;
        /// <summary>
        /// Optional specific cause used for death messages, e.g. "barrel", "saw", "pendulum", "spikes",
        /// "piston", "spinner", "blast_block". Mods may use their own (see <see cref="DeathMessages"/>).
        /// </summary>
        public string Cause;

        public bool IsEnvironmental => Type == DamageType.Border || Type == DamageType.Fall || Type == DamageType.Hazard || Type == DamageType.Trap;
    }

    public class DamageEventArgs
    {
        public Player Target;
        public DamageInfo Info;
        /// <summary>Set to true to cancel the damage entirely.</summary>
        public bool Cancel;
    }

    /// <summary>
    /// Global game events. Handlers are isolated: an exception in one mod's handler is logged and
    /// does not stop the game or other handlers.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<MatchSettings> MatchStarted;
        public static event Action<int> MatchEnded;
        public static event Action<int> RoundStarted;
        public static event Action<int, int> RoundEnded;
        public static event Action PickPhaseStarted;
        public static event Action PickPhaseEnded;
        public static event Action<Player> PlayerSpawned;
        public static event Action<DamageEventArgs> PlayerDamaging;
        public static event Action<Player, DamageInfo> PlayerDamaged;
        public static event Action<Player, DamageInfo> PlayerDied;
        public static event Action<Player> PlayerBlocked;
        public static event Action<Player, DamageInfo> DamageBlocked;
        public static event Action<Player, CardDefinition> CardPicked;
        public static event Action<Player, Bullet> BulletFired;
        public static event Action<MapData> MapLoaded;
        /// <summary>Raised once after built-in and file-based content has loaded.</summary>
        public static event Action ContentLoaded;

        static void Run<T>(Action<T> a, T x)
        {
            if (a == null) return;
            foreach (var d in a.GetInvocationList())
            {
                try { ((Action<T>)d)(x); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        static void Run<T1, T2>(Action<T1, T2> a, T1 x, T2 y)
        {
            if (a == null) return;
            foreach (var d in a.GetInvocationList())
            {
                try { ((Action<T1, T2>)d)(x, y); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        static void Run(Action a)
        {
            if (a == null) return;
            foreach (var d in a.GetInvocationList())
            {
                try { ((Action)d)(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        internal static void RaiseMatchStarted(MatchSettings s) => Run(MatchStarted, s);
        internal static void RaiseMatchEnded(int winnerTeam) => Run(MatchEnded, winnerTeam);
        internal static void RaiseRoundStarted(int round) => Run(RoundStarted, round);
        internal static void RaiseRoundEnded(int round, int winnerTeam) => Run(RoundEnded, round, winnerTeam);
        internal static void RaisePickPhaseStarted() => Run(PickPhaseStarted);
        internal static void RaisePickPhaseEnded() => Run(PickPhaseEnded);
        internal static void RaisePlayerSpawned(Player p) => Run(PlayerSpawned, p);
        internal static void RaisePlayerDamaging(DamageEventArgs a) => Run(PlayerDamaging, a);
        internal static void RaisePlayerDamaged(Player p, DamageInfo i) => Run(PlayerDamaged, p, i);
        internal static void RaisePlayerDied(Player p, DamageInfo i) => Run(PlayerDied, p, i);
        internal static void RaisePlayerBlocked(Player p) => Run(PlayerBlocked, p);
        internal static void RaiseDamageBlocked(Player p, DamageInfo i) => Run(DamageBlocked, p, i);
        internal static void RaiseCardPicked(Player p, CardDefinition c) => Run(CardPicked, p, c);
        internal static void RaiseBulletFired(Player p, Bullet b) => Run(BulletFired, p, b);
        internal static void RaiseMapLoaded(MapData m) => Run(MapLoaded, m);
        internal static void RaiseContentLoaded() => Run(ContentLoaded);
    }
}
