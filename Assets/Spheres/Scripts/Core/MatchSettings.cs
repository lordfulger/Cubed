using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spheres
{
    public enum ControlType { None, KeyboardMouse, Gamepad1, Gamepad2, Gamepad3, Gamepad4, Bot, Online, Gamepad5, Gamepad6, Gamepad7, Gamepad8 }

    public static class ControlTypes
    {
        public const int MaxGamepads = 8;

        /// <summary>1-based joystick number for a gamepad control type, or 0.</summary>
        public static int Joystick(ControlType c)
        {
            if (c >= ControlType.Gamepad1 && c <= ControlType.Gamepad4) return (int)c - (int)ControlType.Gamepad1 + 1;
            if (c >= ControlType.Gamepad5 && c <= ControlType.Gamepad8) return (int)c - (int)ControlType.Gamepad5 + 5;
            return 0;
        }

        public static ControlType Gamepad(int joystick) =>
            joystick <= 4 ? (ControlType)((int)ControlType.Gamepad1 + joystick - 1) : (ControlType)((int)ControlType.Gamepad5 + joystick - 5);

        public static bool IsGamepad(ControlType c) => Joystick(c) > 0;
    }

    /// <summary>Who picks cards after a round.</summary>
    public enum PickRule { Losers, Everyone, Winners, Nobody }

    public enum MapRotation { Fixed, Cycle, Random, Vote }

    public enum BotDifficulty { Easy, Normal, Hard, Brutal, Dummy }

    public enum GameMode { Elimination, Deathmatch, KingOfTheHill, CardRush, Practice, SphereBall, HoldTheOrb, Infection }

    [Serializable]
    public class PlayerSlot
    {
        public string name = "Player";
        public ControlType control = ControlType.Bot;
        public int team;
        public BotDifficulty difficulty = BotDifficulty.Normal;
        /// <summary>Network client id that controls this slot (ControlType.Online), otherwise -1.</summary>
        public int netOwner = -1;
        /// <summary>Cosmetic hat id (see Hats).</summary>
        public string hat = "";
        /// <summary>Encoded CharacterLook (face + hat) used in the match.</summary>
        public string look = "";
        /// <summary>Which saved character a local human uses (-1 = the profile's active one).</summary>
        public int lookIndex = -1;

        public bool IsHuman => control != ControlType.None && control != ControlType.Bot;
        public bool IsLocalHuman => IsHuman && control != ControlType.Online;
        public bool IsOnline => control == ControlType.Online;
        public bool IsActive => control != ControlType.None;
        public PlayerSlot Clone() => (PlayerSlot)MemberwiseClone();
    }

    /// <summary>Everything that configures a match. Persisted in PlayerPrefs as JSON.</summary>
    [Serializable]
    public class MatchSettings
    {
        const string PrefsKey = "spheres.settings.v1";

        public List<PlayerSlot> slots = new List<PlayerSlot>();

        // Match flow
        public GameMode mode = GameMode.Elimination;
        /// <summary>Deathmatch: team kills needed to win a round.</summary>
        public int killsToWinRound = 5;
        /// <summary>King of the Hill: seconds of control needed to win a round.</summary>
        public float hillSeconds = 20f;
        /// <summary>Sphere Ball: goals needed to win a round.</summary>
        public int goalsToWinRound = 3;
        /// <summary>Hold the Orb: seconds of carrying needed to win a round.</summary>
        public float orbSeconds = 25f;
        /// <summary>Infection: survivors win the round if anyone is uninfected after this long (when no round time limit is set).</summary>
        public float infectionSeconds = 90f;
        /// <summary>The team leading by <see cref="curseLead"/> rounds gets a small handicap card after each round.</summary>
        public bool catchUpCurses;
        public int curseLead = 2;
        public int roundsToWin = 5;
        /// <summary>Elimination / Card Rush: fights a team must win to take a round (2 = best of 3). Cards are picked after each round.</summary>
        public int pointsToWinRound = 2;
        /// <summary>The match never ends: keep picking cards and fighting forever.</summary>
        public bool endless;
        public float roundTimeLimit = 0f;

        // Cards
        public int cardsPerHand = 5;
        public int picksPerRound = 1;
        public int startingPicks = 0;
        /// <summary>Seconds to choose a card. 0 = no timer.</summary>
        public float pickTimer = 20f;
        public PickRule pickRule = PickRule.Losers;
        /// <summary>When picking several cards in a row, draw a fresh hand for each pick.</summary>
        public bool rerollBetweenPicks = true;
        public List<string> disabledCards = new List<string>();

        // Map
        public string mapId = "builtin:garden";
        public MapRotation mapRotation = MapRotation.Fixed;

        // Atmosphere (cosmetic)
        public WeatherKind weather = WeatherKind.Clear;
        public TimeOfDay timeOfDay = TimeOfDay.MapDefault;
        /// <summary>Picked by the host each match so "Random" weather matches on every machine.</summary>
        public int weatherSeed;

        // Borders and falling
        public bool damagingBorders = true;
        public float borderDamage = 25f;
        public bool fallDamage = true;
        /// <summary>Damage from hitting the bottom of the arena.</summary>
        public float bottomDamage = 35f;
        /// <summary>Damage from landing hard on blocks after a long fall.</summary>
        public bool landingDamage = true;
        public float landingDamageScale = 3f;
        /// <summary>Seconds into a round before the borders start closing in. 0 = never.</summary>
        public float borderShrinkAfter = 60f;
        public float borderShrinkDuration = 40f;

        // Combat
        public bool friendlyFire;
        /// <summary>Blocking bounces bullets back off the bubble (instead of just stopping them).</summary>
        public bool reflectOnBlock = true;

        // Preferences
        public float mouseSensitivity = 2f;
        public float gamepadSensitivity = 160f;
        public bool invertY;
        public float volume = 0.6f;
        public float musicVolume = 0.45f;

        // Online
        public string onlineName = "Player";
        public int onlinePort = 27015;
        public string lastJoinAddress = "127.0.0.1";
        /// <summary>Device used when playing as an online client.</summary>
        public ControlType onlineControl = ControlType.KeyboardMouse;

        /// <summary>Copy personal preferences (not match rules) from another settings object.</summary>
        public void CopyPreferencesFrom(MatchSettings o)
        {
            mouseSensitivity = o.mouseSensitivity;
            gamepadSensitivity = o.gamepadSensitivity;
            invertY = o.invertY;
            volume = o.volume;
            musicVolume = o.musicVolume;
            onlineName = o.onlineName;
            onlinePort = o.onlinePort;
            lastJoinAddress = o.lastJoinAddress;
            onlineControl = o.onlineControl;
        }

        public static MatchSettings CreateDefault()
        {
            var s = new MatchSettings();
            s.slots.Add(new PlayerSlot { name = "You", control = ControlType.KeyboardMouse, team = 0 });
            s.slots.Add(new PlayerSlot { name = "Bot Blue", control = ControlType.Bot, team = 1 });
            return s;
        }

        public MatchSettings Clone()
        {
            var c = JsonUtility.FromJson<MatchSettings>(JsonUtility.ToJson(this));
            c.Validate();
            return c;
        }

        public void Validate()
        {
            if (slots == null) slots = new List<PlayerSlot>();
            if (disabledCards == null) disabledCards = new List<string>();
            slots.RemoveAll(s => s == null);
            foreach (var s in slots) s.team = Mathf.Clamp(s.team, 0, Teams.Max - 1);
            roundsToWin = Mathf.Clamp(roundsToWin, 1, 50);
            cardsPerHand = Mathf.Clamp(cardsPerHand, 1, 10);
            picksPerRound = Mathf.Clamp(picksPerRound, 0, 10);
            startingPicks = Mathf.Clamp(startingPicks, 0, 10);
            pickTimer = Mathf.Clamp(pickTimer, 0f, 600f);
            if (string.IsNullOrWhiteSpace(onlineName)) onlineName = "Player";
            if (onlinePort <= 0 || onlinePort > 65535) onlinePort = 27015;
            if (lastJoinAddress == null) lastJoinAddress = "127.0.0.1";
            if (string.IsNullOrEmpty(mapId)) mapId = "builtin:garden";
        }

        public IEnumerable<PlayerSlot> ActiveSlots => slots.Where(s => s.IsActive);

        public int TeamCount => ActiveSlots.Select(s => s.team).Distinct().Count();

        /// <summary>Returns an error message if the match cannot start, otherwise null.</summary>
        public string CheckPlayable()
        {
            if (!ActiveSlots.Any()) return "Add some players.";
            if (TeamCount < 2) return "Players must be on at least two different teams.";
            if (ActiveSlots.Count(s => s.IsLocalHuman) > 8) return "Split-screen supports up to 8 human players.";
            var pads = ActiveSlots.Where(s => s.IsLocalHuman).GroupBy(s => s.control).FirstOrDefault(g => g.Count() > 1);
            if (pads != null) return "Two players can't share " + pads.Key + ".";
            return null;
        }

        public void Save()
        {
            try
            {
                // Online slots belong to a session, never persist them.
                var copy = JsonUtility.FromJson<MatchSettings>(JsonUtility.ToJson(this));
                copy.slots.RemoveAll(s => s == null || s.IsOnline);
                PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(copy));
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        public static MatchSettings Load()
        {
            try
            {
                if (PlayerPrefs.HasKey(PrefsKey))
                {
                    var s = JsonUtility.FromJson<MatchSettings>(PlayerPrefs.GetString(PrefsKey));
                    if (s != null)
                    {
                        s.Validate();
                        s.slots.RemoveAll(x => x.IsOnline);
                        if (s.slots.Count > 0) return s;
                    }
                }
            }
            catch (Exception e) { Debug.LogException(e); }
            return CreateDefault();
        }
    }
}
