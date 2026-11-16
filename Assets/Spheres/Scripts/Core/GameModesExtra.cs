using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spheres
{
    /// <summary>Sphere Ball, Hold the Orb, Infection and catch-up curses.</summary>
    public partial class GameManager
    {
        public const float GoalRadius = 2.6f;
        /// <summary>Temporary team ids used during an Infection round (real teams are kept in Player.HomeTeam).</summary>
        public const int SurvivorTeam = 6, InfectedTeam = 7;

        /// <summary>The ball (Sphere Ball) or orb (Hold the Orb) for this round, if any.</summary>
        public Prop ModeProp { get; private set; }
        /// <summary>Goal positions per team (Sphere Ball).</summary>
        public readonly Dictionary<int, Vector3> Goals = new Dictionary<int, Vector3>();
        /// <summary>Hold the Orb: the player carrying the orb (host only; clients use HillHolder).</summary>
        public Player OrbCarrier { get; private set; }

        readonly List<GameObject> modeVisuals = new List<GameObject>();
        readonly List<Material> modeMaterials = new List<Material>();
        float orbPickupBlock, goalCooldown;
        Player lastSurvivor;

        public bool IsInfection => Mode == GameMode.Infection;

        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // ------------------------------------------------------------------ setup (host and clients)

        /// <summary>Hill, goals and the mode ball/orb. Deterministic so clients build the same objects.</summary>
        public void BuildModeObjects()
        {
            BuildHill();
            foreach (var g in modeVisuals) if (g != null) Destroy(g);
            foreach (var m in modeMaterials) if (m != null) Destroy(m);
            modeVisuals.Clear();
            modeMaterials.Clear();
            Goals.Clear();
            ModeProp = null;
            OrbCarrier = null;
            if (Map == null) return;

            if (Mode == GameMode.SphereBall || Mode == GameMode.HoldTheOrb)
            {
                Vector3 center = ArenaGround(Map.FullBounds.center);
                var kind = Mode == GameMode.SphereBall ? PropKinds.Soccer : PropKinds.Orb;
                var data = new PropData(kind, center.x, center.y + 2f, center.z);
                ModeProp = Map.CreateProp(data, Map.Props.Count, NetClientMode);
                ModeProp.OnFellOut = ResetModeProp;
            }
            if (Mode == GameMode.SphereBall) BuildGoals();
        }

        Vector3 ArenaGround(Vector3 p)
        {
            var b = Map.FullBounds;
            Physics.SyncTransforms();
            return Physics.Raycast(new Vector3(p.x, b.max.y - 0.5f, p.z), Vector3.down, out var hit, b.size.y, Bullet.CollisionMask, QueryTriggerInteraction.Ignore)
                ? hit.point : new Vector3(p.x, b.center.y, p.z);
        }

        void BuildGoals()
        {
            var teams = ActiveTeams.ToList();
            teams.Sort();
            var b = Map.FullBounds;
            for (int i = 0; i < teams.Count; i++)
            {
                int t = teams[i];
                var own = Map.Data.spawns.Where(s => s.team == t).Select(s => s.Position).ToList();
                Vector3 pos;
                if (own.Count > 0) pos = own.Aggregate(Vector3.zero, (a, v) => a + v) / own.Count;
                else
                {
                    float ang = i * Mathf.PI * 2f / Mathf.Max(1, teams.Count);
                    pos = b.center + new Vector3(Mathf.Cos(ang) * b.extents.x * 0.6f, 0f, Mathf.Sin(ang) * b.extents.z * 0.6f);
                }
                pos = ArenaGround(pos);
                Goals[t] = pos;
                var c = Teams.GetColor(t);
                var mat = Mats.GlowAvailable ? Mats.NewGlow(new Color(c.r, c.g, c.b, 0.35f), 2.2f, 0.8f) : Mats.NewTransparent(new Color(c.r, c.g, c.b, 0.35f));
                modeMaterials.Add(mat);
                var ring = Prim.Sphere("Goal", Map.transform, pos, 1f, mat);
                ring.transform.localScale = new Vector3(GoalRadius * 2f, 0.3f, GoalRadius * 2f);
                modeVisuals.Add(ring);
                foreach (float s in new[] { -1f, 1f })
                {
                    var post = Prim.Cube("GoalPost", ring.transform, Vector3.zero, Vector3.one, mat);
                    post.transform.position = pos + new Vector3(s * GoalRadius, 2f, 0f);
                    post.transform.localScale = new Vector3(0.25f / (GoalRadius * 2f), 4f / 0.3f, 0.25f / (GoalRadius * 2f));
                }
                var beam = Prim.Cube("GoalBeam", ring.transform, new Vector3(0f, 25f, 0f), new Vector3(0.03f, 120f, 0.03f), mat);
                beam.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        void ResetModeProp(Prop p)
        {
            if (p == null || Map == null) return;
            if (OrbCarrier != null) OrbCarrier = null;
            var center = ArenaGround(Map.FullBounds.center) + Vector3.up * 2f;
            p.Teleport(center);
            p.LastAttacker = null;
            Fx.Pulse(center, new Color(1f, 1f, 1f, 0.6f), 0.5f, 5f, 0.4f, true, 2f);
        }

        // ------------------------------------------------------------------ per-frame (host)

        void UpdateExtraModes(float dt)
        {
            if (Mode == GameMode.SphereBall) UpdateSphereBall(dt);
            else if (Mode == GameMode.HoldTheOrb) UpdateOrb(dt);
            else if (Mode == GameMode.Infection) UpdateInfection();
        }

        void UpdateSphereBall(float dt)
        {
            goalCooldown -= dt;
            if (ModeProp == null || goalCooldown > 0f) return;
            var ball = ModeProp.transform.position;
            foreach (var kv in Goals)
            {
                var g = kv.Value;
                if (new Vector2(ball.x - g.x, ball.z - g.z).sqrMagnitude > GoalRadius * GoalRadius || Mathf.Abs(ball.y - g.y) > 3.5f) continue;
                int owner = kv.Key;
                var toucher = ModeProp.LastAttacker;
                int scorer = toucher != null ? toucher.Team : -1;
                if (scorer >= 0 && scorer != owner)
                {
                    RoundKills[scorer]++;
                    string who = toucher != null ? "<color=" + Hex(toucher.Color) + ">" + toucher.DisplayName + "</color>" : "";
                    SpheresHud.AddFeedText("GOAL! " + who + " scores for <color=" + Hex(Teams.GetColor(scorer)) + ">" + Teams.GetName(scorer) + "</color>");
                    SpheresHud.ShowBanner("GOAL!", Teams.GetColor(scorer));
                }
                else SpheresHud.AddFeedText("Own goal! No point.");
                for (int k = 0; k < 3; k++) Fx.Burst(g + Vector3.up, Teams.GetColor(owner), 24, 12f, 0.15f, 1.6f, 8f);
                Sfx.PlayAt("win", g, 0.6f);
                CameraShake.Impulse(g, 0.4f, 30f);
                goalCooldown = 1.2f;
                ResetModeProp(ModeProp);
                break;
            }
        }

        void UpdateOrb(float dt)
        {
            if (ModeProp == null) return;
            orbPickupBlock -= dt;
            if (OrbCarrier != null && (!OrbCarrier.IsAlive || !Players.Contains(OrbCarrier))) DropOrb();
            if (OrbCarrier == null && orbPickupBlock <= 0f)
            {
                var orb = ModeProp.transform.position;
                foreach (var p in Players)
                {
                    if (!p.IsAlive || p.Frozen) continue;
                    if ((p.Center - orb).sqrMagnitude > 1.6f * 1.6f) continue;
                    OrbCarrier = p;
                    ModeProp.SetCarried(true);
                    SpheresHud.AddFeedText("<color=" + Hex(p.Color) + ">" + p.DisplayName + "</color> grabbed the orb!");
                    Sfx.PlayAt("zap", p.Center, 0.7f);
                    break;
                }
            }
            if (OrbCarrier != null)
            {
                ModeProp.transform.position = OrbCarrier.Top + Vector3.up * 0.9f;
                HillHolder = OrbCarrier.Team;
                HillProgress[OrbCarrier.Team] += dt;
            }
            else HillHolder = -1;
        }

        void DropOrb()
        {
            if (OrbCarrier == null || ModeProp == null) return;
            var at = OrbCarrier.Center + Vector3.up * 0.5f;
            OrbCarrier = null;
            ModeProp.SetCarried(false);
            ModeProp.Teleport(at);
            orbPickupBlock = 0.6f;
            SpheresHud.AddFeedText("The orb was dropped!");
        }

        // ------------------------------------------------------------------ Infection

        void StartInfection()
        {
            lastSurvivor = null;
            var alive = Players.Where(p => p.IsAlive).ToList();
            if (alive.Count == 0) return;
            int zeros = Mathf.Max(1, alive.Count / 5);
            var shuffled = alive.OrderBy(_ => Random.value).ToList();
            for (int i = 0; i < shuffled.Count; i++)
                shuffled[i].Team = i < zeros ? InfectedTeam : SurvivorTeam;
            foreach (var p in shuffled.Take(zeros))
                SpheresHud.AddFeedText("<color=" + Hex(p.Color) + ">" + p.DisplayName + "</color> is patient zero!");
        }

        void InfectionDeath(Player victim)
        {
            if (victim.Team != SurvivorTeam) return;
            victim.Team = InfectedTeam;
            int left = Players.Count(p => p.Team == SurvivorTeam && p.IsAlive);
            if (left == 0) lastSurvivor = victim;
            SpheresHud.AddFeedText("<color=" + Hex(victim.Color) + ">" + victim.DisplayName + "</color> was infected!" + (left == 1 ? "  One survivor left!" : ""));
        }

        void UpdateInfection() { }

        bool InfectionRoundOver()
        {
            int survivors = Players.Count(p => p.Team == SurvivorTeam && p.IsAlive);
            if (survivors == 0)
            {
                int winner = lastSurvivor != null ? lastSurvivor.HomeTeam : -1;
                if (lastSurvivor != null)
                    SpheresHud.AddFeedText("<color=" + Hex(lastSurvivor.Color) + ">" + lastSurvivor.DisplayName + "</color> was the last survivor!");
                EndRound(winner);
                return true;
            }
            float limit = Settings.roundTimeLimit > 0f ? Settings.roundTimeLimit : Settings.infectionSeconds;
            if (RoundTime >= limit)
            {
                // Survivors held out: the team with the most survivors wins.
                var counts = Players.Where(p => p.Team == SurvivorTeam && p.IsAlive).GroupBy(p => p.HomeTeam)
                    .Select(g => (team: g.Key, n: g.Count())).OrderByDescending(x => x.n).ToList();
                int winner = counts.Count == 0 ? -1 : counts.Count > 1 && counts[0].n == counts[1].n ? -1 : counts[0].team;
                SpheresHud.AddFeedText("The survivors held out!");
                EndRound(winner);
            }
            return true;
        }

        /// <summary>Put everyone back on their real team (end of an Infection round, or match clear).</summary>
        void RestoreHomeTeams()
        {
            foreach (var p in Players) if (p != null) p.Team = p.HomeTeam;
        }

        // ------------------------------------------------------------------ catch-up curses

        /// <summary>After a round: the clear leader's team gets a small handicap card.</summary>
        void ApplyCatchUpCurses()
        {
            if (!Settings.catchUpCurses || ActiveTeams.Count < 2) return;
            var ordered = ActiveTeams.OrderByDescending(t => TeamScores[t]).ToList();
            int lead = TeamScores[ordered[0]] - TeamScores[ordered[1]];
            if (lead < Mathf.Max(1, Settings.curseLead)) return;
            int leader = ordered[0];
            var curses = CardRegistry.All.Where(c => c.Tags.Contains("curse")).ToList();
            if (curses.Count == 0) return;
            foreach (var p in Players.Where(p => p.Team == leader))
            {
                var card = curses[Random.Range(0, curses.Count)];
                p.ApplyCard(card);
                SpheresHud.AddFeedText("<color=" + Hex(p.Color) + ">" + p.DisplayName + "</color> is cursed: " + card.Title);
            }
            SpheresHud.ShowBanner(Teams.GetName(leader) + " is cursed for leading!", Teams.GetColor(leader));
        }
    }
}
