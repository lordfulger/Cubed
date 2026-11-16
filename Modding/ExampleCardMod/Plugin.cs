using BepInEx;
using BepInEx.Configuration;
using Spheres;
using HarmonyLib;
using UnityEngine;

namespace ExampleCardMod
{
    /// <summary>
    /// Example BepInEx 5 plugin for Spheres: adds cards (stat-only, effect-based and code-hook based),
    /// listens to game events, and uses a Harmony patch.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.example.spheres.examplecards";
        public const string Name = "Example Cards";
        public const string Version = "1.0.0";

        internal static ConfigEntry<bool> SparkleJumps;
        internal static ConfigEntry<float> StormDamage;

        void Awake()
        {
            SparkleJumps = Config.Bind("Fun", "SparkleJumps", true, "Spawn sparkles every time someone jumps (Harmony patch demo).");
            StormDamage = Config.Bind("Cards", "StormCallerDamage", 12f, "Damage of each Storm Caller chain zap.");

            SpheresAPI.RegisterMod(Guid, Name, Version, "Example", "Three demo cards, an event hook and a Harmony patch.");

            // 1) A custom effect, usable by C# cards and by JSON cards ("effects": ["example.chain_zap"]).
            SpheresAPI.RegisterEffect<ChainZapEffect>("example.chain_zap", "Hits arc to a second enemy.");

            // 2) A pure stat card.
            SpheresAPI.RegisterCard(new CardDefinition("example.moon_boots", "Moon Boots",
                    "Floaty jumps and an extra air jump.", Rarity.Uncommon, new Color(0.7f, 0.8f, 1f))
                { Source = Name }
                .Mul(Stat.JumpHeight, 1.6f)
                .Add(Stat.ExtraJumps, 1)
                .Mul(Stat.AirControl, 1.5f)
                .Mul(Stat.MaxHealth, 0.9f));

            // 3) A card that uses the custom effect.
            SpheresAPI.RegisterCard(new CardDefinition("example.storm_caller", "Storm Caller",
                    "Your hits chain lightning to the next nearest enemy.", Rarity.Rare, new Color(0.6f, 0.5f, 1f))
                { Source = Name }
                .Effect("example.chain_zap")
                .Stacks(2));

            // 4) A card with a code hook and an appearance condition.
            SpheresAPI.RegisterCard(new CardDefinition("example.underdog", "Underdog",
                    "Only offered when your team is behind. +40 HP and faster blocks.", Rarity.Uncommon, new Color(1f, 0.6f, 0.3f))
            {
                Source = Name,
                CanAppear = p => IsBehind(p),
                OnApply = p => Debug.Log("[ExampleCards] " + p.DisplayName + " is the underdog!"),
            }
                .Add(Stat.MaxHealth, 40f)
                .Mul(Stat.BlockCooldown, 0.8f));

            GameEvents.RoundStarted += round => Logger.LogInfo("Round " + round + " started");
            GameEvents.PlayerDied += (player, info) => Logger.LogInfo(player.DisplayName + " died (" + info.Type + ")");

            new Harmony(Guid).PatchAll();
            Logger.LogInfo(Name + " loaded");
        }

        static bool IsBehind(Player p)
        {
            var gm = SpheresAPI.Game;
            if (gm == null) return false;
            int mine = gm.TeamScores[p.Team];
            foreach (int t in gm.ActiveTeams)
                if (gm.TeamScores[t] > mine) return true;
            return false;
        }
    }

    /// <summary>When a bullet hits, zap the next nearest enemy.</summary>
    public class ChainZapEffect : CardEffect
    {
        public override void OnBulletHitPlayer(Bullet bullet, Player target, DamageInfo info)
        {
            Player next = null;
            float best = 7f;
            foreach (var p in Player.All)
            {
                if (p == target || p == Player || !p.IsAlive || p.Team == Player.Team) continue;
                float d = Vector3.Distance(p.Center, target.Center);
                if (d < best) { best = d; next = p; }
            }
            if (next == null) return;
            Fx.Zap(target.Center, next.Center, new Color(0.7f, 0.6f, 1f));
            SpheresAPI.PlaySound("zap", 0.5f);
            next.TakeDamage(new DamageInfo
            {
                Amount = Plugin.StormDamage.Value,
                Source = Player,
                Type = DamageType.Effect,
                Point = next.Center,
                Direction = next.Center - target.Center,
                Knockback = 3f,
            });
        }
    }

    /// <summary>Harmony demo: sparkles on every successful jump.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.TryJump))]
    static class SparkleJumpPatch
    {
        static void Postfix(Player __instance, bool __result)
        {
            if (__result && Plugin.SparkleJumps.Value)
                Fx.Burst(__instance.transform.position, Color.Lerp(__instance.Color, Color.white, 0.6f), 6, 4f, 0.08f, 0.4f, 2f);
        }
    }
}
