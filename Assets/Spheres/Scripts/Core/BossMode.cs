using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// Boss Battle: everyone teams up against one giant sphere. Each wave the boss comes back bigger and picks
    /// cards of its own; the team picks cards between waves. Beat all the waves to win; if the boss wins one, it's over.
    /// The boss is a regular (huge) player driven by <see cref="BossBrain"/>, so it syncs online like anyone else.
    /// </summary>
    public partial class GameManager
    {
        public const int BossTeam = 7;
        public Player Boss { get; private set; }
        public bool IsBossMode => Mode == GameMode.Boss;

        static readonly string[] BossFirst = { "Grumbleball", "Orbzilla", "Big Bertha", "Sir Bounceington", "Mega Marble", "The Great Gumball", "Lord Roundbottom", "Bowlzebub", "Spheremageddon", "Chonk" };
        static readonly string[] BossTitle = { "the Unpoppable", "of the Final Round", "the Very Round", "the Bouncy", "Destroyer of Decks", "the Absolute Unit", "the Rolling Doom", "Eater of Cards" };

        /// <summary>Create the boss for a Boss Battle match (called while the roster is built).</summary>
        Player CreateBoss(int index)
        {
            int seed = Random.Range(0, 100000);
            var slot = new PlayerSlot
            {
                name = BossFirst[seed % BossFirst.Length] + " " + BossTitle[(seed / 7) % BossTitle.Length],
                control = ControlType.Bot,
                team = BossTeam,
                difficulty = BotDifficulty.Hard,
            };
            var look = CharacterLook.ForSeed(slot.name);
            look.hat = "crown";
            look.eyes = 2; // angry
            look.mouth = 5; // fangs
            slot.look = look.Encode();
            var boss = Player.Create(slot, index, new BossBrain());
            boss.ApplyCard(CardRegistry.Get("boss_body"));
            int challengers = Settings.ActiveSlots.Count();
            for (int i = 1; i < challengers; i++) boss.ApplyCard(CardRegistry.Get("boss_power"));
            Boss = boss;
            return boss;
        }

        /// <summary>Between waves the boss grows and takes two cards of its own.</summary>
        void BossBetweenWaves()
        {
            var boss = Boss;
            if (boss == null) return;
            boss.ApplyCard(CardRegistry.Get("boss_growth"));
            var hand = CardRegistry.DrawHand(boss, 4, Settings);
            for (int i = 0; i < 2 && hand.Count > 0; i++)
            {
                int pick = BotBrain.ChooseCard(boss, hand);
                var card = hand[Mathf.Clamp(pick, 0, hand.Count - 1)];
                hand.Remove(card);
                boss.ApplyCard(card);
                SpheresHud.AddFeedText("<color=#ff6a5a>" + boss.DisplayName + "</color> took " + card.Title + "!");
            }
        }

        /// <summary>Put the boss in the middle of the arena at the start of each wave.</summary>
        void PlaceBoss()
        {
            if (Boss == null || Map == null) return;
            var center = ArenaGround(Map.FullBounds.center);
            var look = Players.Where(p => p != Boss).Select(p => p.transform.position).DefaultIfEmpty(center + Vector3.forward).First() - center;
            Boss.ResetForRound(center + Vector3.up * 0.5f, look);
        }
    }

    /// <summary>Hidden cards that make the boss a boss.</summary>
    public static class BossCards
    {
        static CardDefinition Hidden(string id, string title, string desc) =>
            new CardDefinition(id, title, desc, Rarity.Legendary, new Color(0.9f, 0.25f, 0.2f)) { Source = "Boss", CanAppear = _ => false }.Tag("boss");

        public static void RegisterAll()
        {
            CardRegistry.Register(Hidden("boss_body", "Boss Body", "Huge, tough and angry.")
                .Mul(Stat.MaxHealth, 7f).Mul(Stat.PlayerSize, 2.6f).Mul(Stat.Damage, 1.2f).Mul(Stat.MoveSpeed, 0.85f)
                .Mul(Stat.BulletSize, 1.8f).Mul(Stat.Knockback, 1.6f).Add(Stat.Ammo, 6f).Mul(Stat.ReloadTime, 0.7f));
            CardRegistry.Register(Hidden("boss_power", "Boss Power", "One more challenger, one more chunk of health.").Mul(Stat.MaxHealth, 1.45f));
            CardRegistry.Register(Hidden("boss_growth", "Boss Growth", "The boss comes back stronger.").Mul(Stat.MaxHealth, 1.2f).Mul(Stat.Damage, 1.1f).Mul(Stat.PlayerSize, 1.06f));
        }
    }

    /// <summary>The boss's brain: chases, shoots, and uses phase attacks (rings, slams, charges, homing barrages).</summary>
    public class BossBrain : IPlayerInput
    {
        public Vector3 Move { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool FireHeld { get; private set; }
        public bool BlockPressed => false;
        public bool ReloadPressed => false;

        /// <summary>1, 2 or 3 (by remaining health).</summary>
        public int Phase { get; private set; } = 1;

        Player target;
        float retarget, ringTimer = 4f, slamTimer = 6f, chargeTimer = 9f, barrageTimer = 5f, strafeTimer, strafe = 1f;
        float chargeLeft, airTime;
        bool slamming, warnedRing, warnedCharge;
        Vector3 chargeDir;
        readonly HashSet<Player> chargeHit = new HashSet<Player>();

        public void OnSpawn(Player p, Vector3 look)
        {
            Phase = 1;
            ringTimer = 4f; slamTimer = 7f; chargeTimer = 10f; barrageTimer = 6f;
            slamming = false; chargeLeft = 0f;
        }

        public void Tick(Player self, float dt)
        {
            JumpPressed = false;
            FireHeld = false;
            Move = Vector3.zero;
            var gm = GameManager.Instance;
            if (gm == null || gm.NetClientMode || !self.IsAlive || self.Frozen) return;

            float hp = self.Health / Mathf.Max(1f, self.MaxHealth);
            int phase = hp > 0.66f ? 1 : hp > 0.33f ? 2 : 3;
            if (phase != Phase)
            {
                Phase = phase;
                SpheresHud.ShowBanner(phase == 2 ? self.DisplayName + " is getting angry!" : self.DisplayName + " is ENRAGED!", new Color(1f, 0.35f, 0.25f));
                Fx.Pulse(self.Center, new Color(1f, 0.3f, 0.2f, 0.6f), self.Radius * 2f, self.Radius * 8f, 0.6f, true, 3f);
                CameraShake.Impulse(self.Center, 0.6f, 60f);
            }

            retarget -= dt;
            if (target == null || !target.IsAlive || retarget <= 0f)
            {
                retarget = 1.5f;
                target = Player.All.Where(p => p != null && p.IsAlive && p.Team != self.Team)
                    .OrderBy(p => (p.Center - self.Center).sqrMagnitude).FirstOrDefault();
            }
            if (target == null) return;

            Vector3 to = target.Center - self.Center;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            float dist = flat.magnitude;
            Vector3 dir = dist > 0.01f ? flat / dist : self.transform.forward;

            // Movement: keep a menacing distance, circle around, stay away from the edges.
            strafeTimer -= dt;
            if (strafeTimer <= 0f) { strafeTimer = Random.Range(2f, 4f); strafe = -strafe; }
            Vector3 move = Vector3.Cross(Vector3.up, dir) * strafe * 0.6f;
            if (dist > 12f) move += dir;
            else if (dist < 6f) move -= dir * 0.6f;
            var map = gm.Map;
            if (map != null)
            {
                var b = map.CurrentBounds;
                var c = b.center - self.transform.position;
                c.y = 0f;
                if (c.magnitude > Mathf.Min(b.extents.x, b.extents.z) * 0.55f) move += c.normalized * 1.2f;
            }

            // Charge.
            if (chargeLeft > 0f)
            {
                chargeLeft -= dt;
                Move = chargeDir;
                self.Velocity += chargeDir * 45f * dt;
                foreach (var p in Player.All)
                {
                    if (p == null || !p.IsAlive || p.Team == self.Team || chargeHit.Contains(p)) continue;
                    if ((p.Center - self.Center).sqrMagnitude > (self.Radius + 1.4f) * (self.Radius + 1.4f)) continue;
                    chargeHit.Add(p);
                    p.TakeDamage(new DamageInfo { Amount = 22f + 6f * Phase, Source = self, Type = DamageType.Effect, Point = p.Center, Direction = chargeDir + Vector3.up * 0.6f, Knockback = 26f, Cause = "boss_charge" });
                }
                return;
            }
            Move = move.sqrMagnitude > 1f ? move.normalized : move;

            // Aim with a little lead and fire.
            float speed = Mathf.Max(10f, self.GetStat(Stat.BulletSpeed));
            AimPoint = target.Center + target.Velocity * Mathf.Clamp(dist / speed, 0f, 0.6f);
            FireHeld = dist < 32f;

            // ---- phase attacks (with a warning flash before each)
            float sp = 1f + (Phase - 1) * 0.35f;
            ringTimer -= dt * sp;
            if (ringTimer < 0.6f && !warnedRing) { warnedRing = true; Warn(self); }
            if (ringTimer <= 0f)
            {
                warnedRing = false;
                ringTimer = 6.5f;
                Ring(self, 10 + Phase * 4, Phase * 11f);
            }
            if (Phase >= 2)
            {
                slamTimer -= dt;
                if (slamTimer <= 0f && self.Grounded && !slamming)
                {
                    slamTimer = Phase == 3 ? 5f : 7f;
                    self.Velocity = new Vector3(self.Velocity.x, 17f, self.Velocity.z) + dir * 6f;
                    slamming = true;
                    airTime = 0f;
                }
                chargeTimer -= dt;
                if (chargeTimer < 0.7f && !warnedCharge) { warnedCharge = true; Warn(self); }
                if (chargeTimer <= 0f && self.Grounded)
                {
                    warnedCharge = false;
                    chargeTimer = Phase == 3 ? 6f : 9f;
                    chargeLeft = 0.75f;
                    chargeDir = dir;
                    chargeHit.Clear();
                    Sfx.PlayAt("explode", self.Center, 0.4f);
                }
            }
            if (slamming)
            {
                airTime += dt;
                if (self.Grounded && airTime > 0.35f)
                {
                    slamming = false;
                    Explosion.Create(self.transform.position + Vector3.up * 0.3f, 5.5f + Phase, 22f + 6f * Phase, self, 24f, new Color(1f, 0.45f, 0.2f), false, "boss_slam");
                    CameraShake.Impulse(self.transform.position, 0.8f, 50f);
                }
            }
            if (Phase == 3)
            {
                barrageTimer -= dt;
                if (barrageTimer <= 0f)
                {
                    barrageTimer = 5f;
                    for (int i = 0; i < 6; i++)
                    {
                        var d = Quaternion.Euler(-35f, i * 60f, 0f) * Vector3.forward;
                        var bl = Bullet.Spawn(self, self.Center + d * (self.Radius + 0.4f), d * 14f);
                        bl.Homing = 2f;
                        bl.Gravity = 0f;
                        bl.Lifetime = 4f;
                        bl.Damage = self.GetStat(Stat.Damage) * 0.45f;
                        bl.Radius = 0.3f;
                        bl.BuildVisual();
                    }
                }
            }
        }

        static void Warn(Player self)
        {
            Fx.Pulse(self.Center, new Color(1f, 0.25f, 0.2f, 0.55f), self.Radius * 2.2f, self.Radius * 3.5f, 0.5f, true, 2.5f);
            Sfx.PlayAt("zap", self.Center, 0.6f);
        }

        static void Ring(Player self, int count, float spin)
        {
            float dmg = self.GetStat(Stat.Damage) * 0.4f;
            for (int i = 0; i < count; i++)
            {
                var d = Quaternion.Euler(0f, spin + i * 360f / count, 0f) * Vector3.forward;
                var b = Bullet.Spawn(self, self.Center + d * (self.Radius + 0.4f), d * 16f);
                b.Damage = dmg;
                b.Radius = 0.28f;
                b.Gravity = 0f;
                b.Lifetime = 3f;
                b.BouncesLeft = 0;
                b.BuildVisual();
            }
            Sfx.PlayAt("shoot", self.Center, 0.8f);
        }
    }
}
