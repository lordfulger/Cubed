using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cubed
{
    /// <summary>
    /// A cube fighter. Driven by an <see cref="IPlayerInput"/> (human or bot).
    /// Handles movement, blocking, health, statuses, borders and fall damage.
    /// </summary>
    [DisallowMultipleComponent]
    public class Player : MonoBehaviour
    {
        public static readonly List<Player> All = new List<Player>();

        public const float Gravity = 25f;
        public const float GroundAccel = 70f;
        public const float AirAccel = 45f;
        /// <summary>Landing faster than this (m/s) causes landing damage.</summary>
        public static float SafeLandingSpeed = 19f;
        public static float BottomBounceSpeed = 27f;
        public static float SideBounceSpeed = 14f;

        public int Index;
        public PlayerSlot Slot;
        public int Team;
        public string DisplayName;
        public Color Color;
        public IPlayerInput Input;
        public PlayerCamera Camera;

        public readonly PlayerStats Stats = new PlayerStats();
        public readonly List<CardDefinition> Cards = new List<CardDefinition>();
        public readonly List<CardEffect> Effects = new List<CardEffect>();
        /// <summary>Extra modifiers that are not from cards (mods can use these).</summary>
        public readonly List<StatModifier> ExtraModifiers = new List<StatModifier>();
        /// <summary>Free storage for mods.</summary>
        public readonly Dictionary<string, object> Data = new Dictionary<string, object>();

        public float Health;
        public bool IsAlive { get; private set; }
        public int LivesRemaining;
        /// <summary>Frozen players can look around but not move or shoot (countdowns, pick phases).</summary>
        public bool Frozen;

        public CharacterController Controller { get; private set; }
        public Weapon Weapon { get; private set; }
        public Vector3 Velocity;
        public bool Grounded { get; private set; }
        public Vector3 AimDirection { get; private set; } = Vector3.forward;

        public Transform Model { get; private set; }
        public Transform Head { get; private set; }
        public Transform Muzzle { get; private set; }

        public float BlockTimer;
        public float BlockCooldownTimer;
        public bool IsBlocking => BlockTimer > 0f;
        public float InvulnerableTimer;

        public int Kills, Deaths;
        public float DamageDealt;
        public Player LastAttacker;
        public float LastDamagedTime = -100f;

        float slowTimer, slowAmount;
        float poisonTimer, poisonDps, poisonTick;
        Player poisonSource;
        int airJumpsUsed;
        float lastGroundedTime;
        float borderCooldown;
        float hazardTick;
        float flashTimer;
        BlockInfo bounceBlock, hazardBlock, groundBlock;

        GameObject shield;
        Transform hatRoot;

        /// <summary>Swap the cosmetic hat.</summary>
        public void SetHat(string id)
        {
            if (hatRoot == null) return;
            for (int i = hatRoot.childCount - 1; i >= 0; i--) Destroy(hatRoot.GetChild(i).gameObject);
            if (string.IsNullOrEmpty(id)) return;
            try { Hats.Get(id).Build(hatRoot, Color); }
            catch (Exception e) { Debug.LogException(e); }
            foreach (var r in hatRoot.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        Transform rig, footL, footR, gunRoot, eyeL, eyeR, pupilL, pupilR, shieldRing;
        static readonly Vector3 GunRest = new Vector3(0.52f, -0.25f, 0.35f);
        float walkPhase, squash, squashVel, recoil, blinkTimer = 2f, blinkTime, spawnAnim = 1f, shieldPop = 1f, lastStepSign = 1f, animLastVy;
        bool animGrounded = true, wasBlocking;
        int animLastAmmo = -1;
        Renderer[] bodyRenderers;
        Material bodyMaterial;
        static Material flashMaterial;

        public float MaxHealth => GetStat(Stat.MaxHealth);
        public float Size => Stats[Stat.PlayerSize];
        public Vector3 Center => transform.position + Vector3.up * (0.7f * Size);
        public Vector3 Top => transform.position + Vector3.up * (1.45f * Size);
        public float Radius => 0.45f * Size;
        /// <summary>Radius of the 360-degree block bubble.</summary>
        public float ShieldRadius => 1.05f * Size;
        public bool IsBot => Input is BotBrain;
        public bool IsHuman => Input is HumanInput;
        /// <summary>Controlled by a remote client (host side).</summary>
        public bool IsRemote => Input is Net.NetworkInput;
        /// <summary>
        /// On network clients every player is a puppet: the host simulates, the client only displays
        /// the received state (the local player's Input still drives the camera and is sent to the host).
        /// </summary>
        public bool NetworkPuppet;
        public bool CanAirJump => airJumpsUsed < GetStatInt(Stat.ExtraJumps);
        public float SlowFactor => slowTimer > 0f ? 1f - slowAmount : 1f;
        public bool IsPoisoned => poisonTimer > 0f;
        public bool IsSlowed => slowTimer > 0f;

        static MatchSettings Settings => GameManager.Instance != null ? GameManager.Instance.Settings : FallbackSettings;
        static readonly MatchSettings FallbackSettings = MatchSettings.CreateDefault();

        // ------------------------------------------------------------------ creation

        public static Player Create(PlayerSlot slot, int index, IPlayerInput input)
        {
            var go = new GameObject("Player " + index + " (" + slot.name + ")");
            var p = go.AddComponent<Player>();
            p.Index = index;
            p.Slot = slot;
            p.Team = slot.team;
            p.DisplayName = slot.name;
            p.Color = Teams.GetColor(slot.team);
            p.Input = input;
            p.Build();
            p.RecalculateStats();
            p.Health = p.MaxHealth;
            p.IsAlive = true;
            return p;
        }

        void Build()
        {
            Controller = gameObject.AddComponent<CharacterController>();
            Controller.radius = 0.45f;
            Controller.height = 1.4f;
            Controller.center = new Vector3(0f, 0.7f, 0f);
            Controller.slopeLimit = 50f;
            Controller.stepOffset = 0.3f;
            Controller.skinWidth = 0.04f;
            Controller.minMoveDistance = 0f;

            Model = new GameObject("Model").transform;
            Model.SetParent(transform, false);
            rig = new GameObject("Rig").transform;
            rig.SetParent(Model, false);
            bodyMaterial = Looks.Block(Color);
            var dark = Mats.Solid(Color.Lerp(Color, Color.black, 0.55f));
            var light = Looks.Block(Color.Lerp(Color, Color.white, 0.35f));
            var white = Mats.Solid(new Color(0.95f, 0.95f, 0.97f));
            var black = Mats.Solid(new Color(0.06f, 0.06f, 0.08f));
            var visor = Mats.Solid(new Color(0.08f, 0.09f, 0.13f));

            Prim.Cube("Body", rig, new Vector3(0f, 0.68f, 0f), new Vector3(0.9f, 1.02f, 0.9f), bodyMaterial);
            Prim.Cube("Cap", rig, new Vector3(0f, 1.22f, 0f), new Vector3(0.76f, 0.1f, 0.76f), light);
            Prim.Cube("Pack", rig, new Vector3(0f, 0.72f, -0.5f), new Vector3(0.58f, 0.56f, 0.16f), dark);
            footL = Prim.Cube("FootL", rig, new Vector3(-0.22f, 0.09f, 0f), new Vector3(0.3f, 0.18f, 0.42f), dark).transform;
            footR = Prim.Cube("FootR", rig, new Vector3(0.22f, 0.09f, 0f), new Vector3(0.3f, 0.18f, 0.42f), dark).transform;

            Head = new GameObject("Head").transform;
            Head.SetParent(rig, false);
            Head.localPosition = new Vector3(0f, 0.95f, 0f);
            Prim.Cube("Visor", Head, new Vector3(0f, 0.04f, 0.452f), new Vector3(0.8f, 0.34f, 0.02f), visor);
            eyeL = Prim.Cube("EyeL", Head, new Vector3(-0.18f, 0.05f, 0.47f), new Vector3(0.18f, 0.22f, 0.03f), white).transform;
            eyeR = Prim.Cube("EyeR", Head, new Vector3(0.18f, 0.05f, 0.47f), new Vector3(0.18f, 0.22f, 0.03f), white).transform;
            pupilL = Prim.Cube("PupilL", Head, new Vector3(-0.18f, 0.03f, 0.488f), new Vector3(0.09f, 0.11f, 0.02f), black).transform;
            pupilR = Prim.Cube("PupilR", Head, new Vector3(0.18f, 0.03f, 0.488f), new Vector3(0.09f, 0.11f, 0.02f), black).transform;

            gunRoot = new GameObject("Gun").transform;
            gunRoot.SetParent(Head, false);
            gunRoot.localPosition = GunRest;
            Prim.Cube("Barrel", gunRoot, new Vector3(0f, 0f, 0f), new Vector3(0.16f, 0.16f, 0.75f), dark);
            Prim.Cube("Grip", gunRoot, new Vector3(0f, -0.14f, -0.18f), new Vector3(0.12f, 0.2f, 0.14f), dark);
            Prim.Cube("GunTip", gunRoot, new Vector3(0f, 0f, 0.4f), new Vector3(0.2f, 0.2f, 0.08f), bodyMaterial);
            Prim.Cube("GunStripe", gunRoot, new Vector3(0f, 0.085f, 0.05f), new Vector3(0.1f, 0.02f, 0.4f), light);
            Muzzle = new GameObject("Muzzle").transform;
            Muzzle.SetParent(gunRoot, false);
            Muzzle.localPosition = new Vector3(0f, 0f, 0.5f);

            hatRoot = new GameObject("Hat").transform;
            hatRoot.SetParent(Head, false);
            hatRoot.localPosition = new Vector3(0f, 0.32f, 0f);
            SetHat(Slot != null ? Slot.hat : "");

            Color sc = Color;
            sc.a = 0.28f;
            shield = Prim.Sphere("Shield", transform, new Vector3(0f, 0.7f, 0f), 2.1f, Mats.Transparent(sc));
            Color rc = Color.Lerp(Color, Color.white, 0.5f);
            rc.a = 0.35f;
            shieldRing = Prim.Sphere("Ring", shield.transform, Vector3.zero, 1f, Mats.Transparent(rc)).transform;
            shieldRing.localScale = new Vector3(1.08f, 0.04f, 1.08f);
            shield.SetActive(false);

            bodyRenderers = Model.GetComponentsInChildren<Renderer>();
            Weapon = new Weapon(this);
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void OnDestroy()
        {
            All.Remove(this);
            foreach (var e in Effects.ToArray()) SafeEffect(e, x => x.OnRemoved());
        }

        // ------------------------------------------------------------------ stats & cards

        /// <summary>Current stat value including dynamic effect adjustments.</summary>
        public float GetStat(Stat s)
        {
            float v = Stats[s];
            for (int i = 0; i < Effects.Count; i++)
            {
                try { Effects[i].ModifyStat(s, ref v); }
                catch (Exception e) { Debug.LogException(e); }
            }
            var d = StatInfo.Get(s);
            return Mathf.Clamp(v, d.Min, d.Max);
        }

        public int GetStatInt(Stat s) => Mathf.RoundToInt(GetStat(s));

        public void RecalculateStats()
        {
            float oldMax = Stats[Stat.MaxHealth];
            Stats.Recalculate(Cards, ExtraModifiers);
            float newMax = Stats[Stat.MaxHealth];
            if (IsAlive && oldMax > 0f) Health = Mathf.Min(Health * newMax / oldMax, MaxHealth);
            transform.localScale = Vector3.one * Size;
        }

        /// <param name="runHooks">False on network clients: effects are attached (for stat display) but
        /// OnApplied/OnApply code only runs on the host.</param>
        public void ApplyCard(CardDefinition card, bool runHooks = true)
        {
            if (card == null) return;
            Cards.Add(card);
            foreach (var id in card.Effects)
            {
                var effect = CardEffectRegistry.Create(id);
                if (effect == null) continue;
                effect.Player = this;
                effect.Card = card;
                Effects.Add(effect);
                if (runHooks) SafeEffect(effect, x => x.OnApplied());
            }
            if (runHooks && card.OnApply != null)
            {
                try { card.OnApply(this); }
                catch (Exception e) { Debug.LogException(e); }
            }
            RecalculateStats();
            GameEvents.RaiseCardPicked(this, card);
        }

        public bool RemoveCard(CardDefinition card)
        {
            int idx = Cards.LastIndexOf(card);
            if (idx < 0) return false;
            Cards.RemoveAt(idx);
            var effect = Effects.LastOrDefault(e => e.Card == card);
            if (effect != null)
            {
                Effects.Remove(effect);
                SafeEffect(effect, x => x.OnRemoved());
            }
            RecalculateStats();
            return true;
        }

        public void ForEachEffect(Action<CardEffect> action)
        {
            for (int i = 0; i < Effects.Count; i++) SafeEffect(Effects[i], action);
        }

        static void SafeEffect(CardEffect e, Action<CardEffect> action)
        {
            try { action(e); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        // ------------------------------------------------------------------ round lifecycle

        public void ResetForRound(Vector3 position, Vector3 lookDirection)
        {
            IsAlive = true;
            Model.gameObject.SetActive(true);
            Controller.enabled = true;
            RecalculateStats();
            Teleport(position);
            Velocity = Vector3.zero;
            Health = MaxHealth;
            LivesRemaining = GetStatInt(Stat.ExtraLives);
            Weapon.Reset();
            BlockTimer = 0f;
            BlockCooldownTimer = 0f;
            InvulnerableTimer = 0f;
            slowTimer = poisonTimer = 0f;
            stunTimer = 0f;
            Barrier = 0f;
            barrierTimer = 0f;
            airJumpsUsed = 0;
            borderCooldown = 0f;
            LastAttacker = null;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude < 0.01f) lookDirection = Vector3.forward;
            AimDirection = lookDirection.normalized;
            transform.rotation = Quaternion.LookRotation(AimDirection);
            Head.localRotation = Quaternion.identity;
            shield.SetActive(false);
            PlaySpawnAnimation();
            Fx.Pulse(transform.position + Vector3.up * 0.7f, new Color(Color.r, Color.g, Color.b, 0.45f), 0.5f, 3f, 0.4f);
            Input?.OnSpawn(this, AimDirection);
            ForEachEffect(e => e.OnRoundStart());
            GameEvents.RaisePlayerSpawned(this);
        }

        public void Teleport(Vector3 position)
        {
            bool was = Controller.enabled;
            Controller.enabled = false;
            transform.position = position;
            Controller.enabled = was;
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (NetworkPuppet)
            {
                PuppetUpdate(dt);
                return;
            }
            if (!IsAlive)
            {
                // Dead humans keep steering their spectator camera.
                if (Input is HumanInput) Input.Tick(this, dt);
                return;
            }

            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f) SetBodyMaterial(null);
            }

            Input?.Tick(this, dt);
            UpdateAim();

            if (Frozen)
            {
                UpdateShield();
                return;
            }

            BlockTimer = Mathf.Max(0f, BlockTimer - dt);
            BlockCooldownTimer = Mathf.Max(0f, BlockCooldownTimer - dt);
            InvulnerableTimer = Mathf.Max(0f, InvulnerableTimer - dt);
            borderCooldown = Mathf.Max(0f, borderCooldown - dt);

            if (stunTimer > 0f) stunTimer -= dt;
            if (Input != null && Input.BlockPressed && !IsStunned) TryBlock();
            UpdateMovement(dt);
            if (!IsAlive) return;
            CheckBorders();
            if (!IsAlive) return;
            if (Input != null) Weapon.Tick(dt, Input.FireHeld && !IsStunned, Input.ReloadPressed);
            UpdateStatuses(dt);
            if (!IsAlive) return;
            for (int i = 0; i < Effects.Count; i++)
            {
                try { Effects[i].OnUpdate(dt); }
                catch (Exception e) { Debug.LogException(e); }
            }
            UpdateShield();
        }

        // ------------------------------------------------------------------ network puppet

        CapsuleCollider puppetCollider;

        /// <summary>
        /// Turn this player into a display-only puppet (network client). The CharacterController is replaced
        /// by a plain capsule so positions can be set directly while bullets/aim rays still hit the player.
        /// </summary>
        public void MakePuppet(bool predictLocally = false)
        {
            NetworkPuppet = true;
            Predicted = predictLocally;
            if (predictLocally) return; // keeps its CharacterController to simulate movement locally
            Controller.enabled = false;
            if (puppetCollider == null)
            {
                puppetCollider = gameObject.AddComponent<CapsuleCollider>();
                puppetCollider.center = new Vector3(0f, 0.7f, 0f);
                puppetCollider.radius = 0.45f;
                puppetCollider.height = 1.4f;
            }
        }

        /// <summary>Network client's own player: movement is simulated locally and corrected toward the host.</summary>
        public bool Predicted { get; private set; }
        Vector3 correction;
        readonly Vector3[] historyPos = new Vector3[64];
        readonly float[] historyTime = new float[64];
        int historyHead;

        Vector3 HistoryAt(float time)
        {
            int best = historyHead;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < historyTime.Length; i++)
            {
                float d = Mathf.Abs(historyTime[i] - time);
                if (d < bestDiff) { bestDiff = d; best = i; }
            }
            return bestDiff == float.MaxValue || historyTime[best] == 0f ? transform.position : historyPos[best];
        }

        Vector3 netPosition;
        float netYaw, netPitch, netTime, netHealth = -1f;
        bool netHasState;

        void PuppetUpdate(float dt)
        {
            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f) SetBodyMaterial(null);
            }
            Input?.Tick(this, dt);
            if (Predicted && IsAlive && !Frozen && Controller.enabled)
            {
                UpdateAim();
                UpdateMovement(dt);
                if (correction.sqrMagnitude > 0.0001f)
                {
                    Vector3 step = correction * Mathf.Min(1f, dt * 6f);
                    Controller.Move(step);
                    correction -= step;
                    for (int i = 0; i < historyPos.Length; i++) historyPos[i] += step;
                }
                historyHead = (historyHead + 1) % historyPos.Length;
                historyPos[historyHead] = transform.position;
                historyTime[historyHead] = Time.time;
                UpdateShield();
                return;
            }
            if (Input != null) UpdateAim();
            else
            {
                transform.rotation = Quaternion.Euler(0f, netYaw, 0f);
                Head.localRotation = Quaternion.Euler(netPitch, 0f, 0f);
                AimDirection = Quaternion.Euler(netPitch, netYaw, 0f) * Vector3.forward;
            }
            if (IsAlive && netHasState)
            {
                Vector3 target = netPosition + Velocity * Mathf.Min(Time.time - netTime, 0.15f);
                Vector3 pos = transform.position;
                transform.position = (target - pos).sqrMagnitude > 25f ? target : Vector3.Lerp(pos, target, 1f - Mathf.Exp(-dt * 20f));
            }
            UpdateShield();
        }

        /// <summary>Apply one player's state from a host snapshot (network clients only).</summary>
        public void NetApply(Vector3 position, Vector3 velocity, float yaw, float pitch, float health, bool alive,
            bool blocking, bool grounded, bool slowed, bool poisoned, bool invulnerable, int ammo, bool reloading,
            float reloadProgress, float blockCooldownFraction, int lives, int kills, int deaths, float damageDealt,
            bool stunned = false, float barrier = 0f)
        {
            stunTimer = stunned ? 0.2f : 0f;
            Barrier = barrier;
            if (alive != IsAlive) NetSetAlive(alive, position);
            if (Predicted && alive && !Frozen && netHasState)
            {
                // Compare the host's position with where we predicted we were one round trip ago.
                float rtt = Net.NetSession.Instance != null ? Net.NetSession.Instance.Rtt : 0.1f;
                Vector3 err = position - HistoryAt(Time.time - rtt - 0.5f / Net.NetConst.SnapshotRate);
                if (err.sqrMagnitude > 12f) { Teleport(transform.position + err); correction = Vector3.zero; }
                else correction = err;
                if ((velocity - Velocity).sqrMagnitude > 80f) Velocity = velocity; // knockback, bounces
            }
            else
            {
                if (!netHasState || Predicted) Teleport(position);
                Velocity = velocity;
            }
            netHasState = true;
            netPosition = position;
            netTime = Time.time;
            netYaw = yaw;
            netPitch = pitch;
            if (netHealth >= 0f && health < netHealth - 0.01f && alive) Flash();
            netHealth = health;
            Health = health;
            Grounded = grounded;
            BlockTimer = blocking ? 0.1f : 0f;
            BlockCooldownTimer = blockCooldownFraction * GetStat(Stat.BlockCooldown);
            slowTimer = slowed ? 0.2f : 0f;
            slowAmount = slowed ? 0.4f : 0f;
            poisonTimer = poisoned ? 0.2f : 0f;
            InvulnerableTimer = invulnerable ? 0.2f : 0f;
            Weapon.NetSet(ammo, reloading, reloadProgress);
            LivesRemaining = lives;
            Kills = kills;
            Deaths = deaths;
            DamageDealt = damageDealt;
        }

        void NetSetAlive(bool alive, Vector3 position)
        {
            IsAlive = alive;
            Model.gameObject.SetActive(alive);
            if (puppetCollider != null) puppetCollider.enabled = alive;
            else Controller.enabled = alive;
            if (!alive) shield.SetActive(false);
            else
            {
                transform.position = position;
                netHealth = -1f;
                PlaySpawnAnimation();
            }
        }

        /// <summary>Reset puppet interpolation (new round on a network client).</summary>
        public void NetResetForRound()
        {
            netHasState = false;
            netHealth = -1f;
            RecalculateStats();
        }

        void UpdateAim()
        {
            if (Input == null) return;
            Vector3 dir = Input.AimPoint - Head.position;
            if (dir.sqrMagnitude < 0.04f) return;
            AimDirection = dir.normalized;
            Vector3 flat = new Vector3(AimDirection.x, 0f, AimDirection.z);
            if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(flat);
            float pitch = -Mathf.Asin(Mathf.Clamp(AimDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
            Head.localRotation = Quaternion.Euler(Mathf.Clamp(pitch, -80f, 80f), 0f, 0f);
        }

        void UpdateMovement(float dt)
        {
            float speed = GetStat(Stat.MoveSpeed) * SlowFactor;
            Vector3 move = Input != null && !IsStunned ? Input.Move : Vector3.zero;
            move.y = 0f;
            if (move.sqrMagnitude > 1f) move.Normalize();
            Vector3 desired = move * speed;

            float accel = Grounded
                ? GroundAccel * (groundBlock != null && groundBlock.Type != null ? groundBlock.Type.GroundAccel : 1f)
                : AirAccel * GetStat(Stat.AirControl);
            Vector3 horizontal = new Vector3(Velocity.x, 0f, Velocity.z);
            // Preserve knockback: decelerate slowly when moving faster than we could run.
            if (horizontal.magnitude > speed + 0.5f && Vector3.Dot(desired, horizontal) >= 0f) accel *= Grounded ? 0.5f : 0.25f;
            horizontal = Vector3.MoveTowards(horizontal, desired, accel * dt);
            Velocity.x = horizontal.x;
            Velocity.z = horizontal.z;

            Velocity.y = Mathf.Max(Velocity.y - Gravity * dt, -70f);

            if (Input != null && Input.JumpPressed && !IsStunned) TryJump();

            float preVy = Velocity.y;
            bounceBlock = null;
            hazardBlock = null;
            groundBlock = null;
            bool wasGrounded = Grounded;
            var flags = Controller.Move(Velocity * dt);
            Grounded = (flags & CollisionFlags.Below) != 0;

            if (bounceBlock != null && preVy <= 0f)
            {
                Velocity.y = Mathf.Max(bounceBlock.Type.BounceSpeed, -preVy * 0.85f);
                Grounded = false;
                airJumpsUsed = 0;
                Sfx.PlayAt("bounce", transform.position, 0.7f);
                Fx.Burst(transform.position, bounceBlock.Type.Color, 6, 5f, 0.12f, 0.4f);
            }
            else if (Grounded)
            {
                if (!wasGrounded) OnLand(preVy);
                if (Velocity.y < 0f) Velocity.y = -2f;
                airJumpsUsed = 0;
                lastGroundedTime = Time.time;
            }

            if ((flags & CollisionFlags.Above) != 0 && Velocity.y > 0f) Velocity.y = 0f;
            if ((flags & CollisionFlags.Sides) != 0)
            {
                // Bleed knockback when slamming into walls.
                Velocity.x *= 0.9f;
                Velocity.z *= 0.9f;
            }

            if (hazardBlock != null)
            {
                hazardTick -= dt;
                if (hazardTick <= 0f)
                {
                    hazardTick = 0.25f;
                    TakeDamage(new DamageInfo
                    {
                        Amount = hazardBlock.Type.DamagePerSecond * 0.25f,
                        Type = DamageType.Hazard,
                        Point = transform.position,
                        Direction = Vector3.up,
                        Knockback = 4f,
                        Blockable = true,
                    });
                }
            }
            else hazardTick = 0f;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var info = hit.collider.GetComponent<BlockInfo>();
            if (info == null || info.Type == null) return;
            if (info.Type.DamagePerSecond > 0f) hazardBlock = info;
            if (hit.normal.y > 0.6f)
            {
                groundBlock = info;
                if (info.Type.BounceSpeed > 0f) bounceBlock = info;
            }
        }

        void OnLand(float impactVelocity)
        {
            float speed = -impactVelocity;
            var s = Settings;
            if (speed > SafeLandingSpeed && s.fallDamage && s.landingDamage)
            {
                TakeDamage(new DamageInfo
                {
                    Amount = (speed - SafeLandingSpeed) * s.landingDamageScale,
                    Type = DamageType.Fall,
                    Point = transform.position,
                    Direction = Vector3.up,
                    Blockable = true,
                });
                Fx.Burst(transform.position, Color.Lerp(Color, Color.white, 0.5f), 10, 7f, 0.15f, 0.5f);
            }
        }

        public bool TryJump()
        {
            bool coyote = !Grounded && Time.time - lastGroundedTime < 0.1f && Velocity.y <= 0f;
            if (Grounded || coyote)
            {
                DoJump();
                return true;
            }
            if (CanAirJump)
            {
                airJumpsUsed++;
                DoJump();
                Fx.Burst(transform.position, Color, 5, 4f, 0.1f, 0.35f);
                return true;
            }
            return false;
        }

        void DoJump()
        {
            Velocity.y = Mathf.Sqrt(2f * Gravity * GetStat(Stat.JumpHeight));
            Grounded = false;
            lastGroundedTime = -10f;
            Sfx.PlayAt("jump", transform.position, 0.4f);
        }

        // ------------------------------------------------------------------ borders

        enum BorderKind { Bottom, Top, Side }

        void CheckBorders()
        {
            var map = GameManager.ActiveMap;
            if (map == null) return;
            Bounds b = map.CurrentBounds;
            Vector3 p = transform.position;
            Vector3 c = Center;
            float r = Radius;

            if (p.y < b.min.y) HitBorder(Vector3.up, new Vector3(p.x, b.min.y + 0.05f, p.z), BorderKind.Bottom);
            else if (Top.y > b.max.y) HitBorder(Vector3.down, new Vector3(p.x, b.max.y - 1.5f * Size, p.z), BorderKind.Top);

            p = transform.position;
            if (c.x - r < b.min.x) HitBorder(Vector3.right, new Vector3(b.min.x + r + 0.05f, p.y, p.z), BorderKind.Side);
            else if (c.x + r > b.max.x) HitBorder(Vector3.left, new Vector3(b.max.x - r - 0.05f, p.y, p.z), BorderKind.Side);

            p = transform.position;
            if (c.z - r < b.min.z) HitBorder(Vector3.forward, new Vector3(p.x, p.y, b.min.z + r + 0.05f), BorderKind.Side);
            else if (c.z + r > b.max.z) HitBorder(Vector3.back, new Vector3(p.x, p.y, b.max.z - r - 0.05f), BorderKind.Side);
        }

        void HitBorder(Vector3 normal, Vector3 clampedPosition, BorderKind kind)
        {
            Teleport(clampedPosition);
            float bounce = kind == BorderKind.Bottom ? BottomBounceSpeed : kind == BorderKind.Top ? 4f : SideBounceSpeed;
            float vn = Vector3.Dot(Velocity, normal);
            float newVn = Mathf.Max(bounce, -vn * 0.6f);
            Velocity += normal * (newVn - vn);
            if (kind == BorderKind.Bottom) airJumpsUsed = 0;

            if (borderCooldown > 0f) return;
            borderCooldown = 0.35f;

            var s = Settings;
            bool damaging = kind == BorderKind.Bottom ? s.fallDamage : s.damagingBorders;
            Vector3 contact = Center - normal * Radius;
            Fx.Burst(contact, damaging ? new Color(1f, 0.35f, 0.25f) : new Color(0.5f, 0.75f, 1f), 12, 8f, 0.14f, 0.5f);
            Sfx.PlayAt("border", contact, damaging ? 0.8f : 0.4f);
            CameraShake.Impulse(contact, damaging ? 0.3f : 0.12f, 6f);
            if (!damaging) return;
            TakeDamage(new DamageInfo
            {
                Amount = kind == BorderKind.Bottom ? s.bottomDamage : s.borderDamage,
                Type = kind == BorderKind.Bottom ? DamageType.Fall : DamageType.Border,
                Point = contact,
                Direction = normal,
                Blockable = true,
            });
        }

        // ------------------------------------------------------------------ blocking

        public bool TryBlock()
        {
            if (!IsAlive || BlockCooldownTimer > 0f) return false;
            BlockTimer = GetStat(Stat.BlockDuration);
            BlockCooldownTimer = GetStat(Stat.BlockCooldown);
            Sfx.PlayAt("block", Center, 0.5f, 0.03f);
            ForEachEffect(e => e.OnBlock());
            GameEvents.RaisePlayerBlocked(this);
            return true;
        }

        void UpdateShield()
        {
            bool on = IsBlocking || InvulnerableTimer > 0f;
            if (shield.activeSelf != on) shield.SetActive(on);
            if (on)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 30f) * 0.04f;
                shield.transform.localScale = Vector3.one * 2.1f * pulse * EaseOutBack(shieldPop);
                shieldRing.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 3f) * 35f, Time.time * 240f, Mathf.Cos(Time.time * 2.3f) * 35f);
            }
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        /// <summary>Pop the model in (spawns, revives).</summary>
        public void PlaySpawnAnimation() => spawnAnim = 0f;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt > 0f && IsAlive) Animate(dt);
        }

        /// <summary>Procedural animation: walk cycle, lean, squash and stretch, recoil, blinking, pops.</summary>
        void Animate(float dt)
        {
            if (rig == null || !Model.gameObject.activeSelf) return;
            Vector3 hv = new Vector3(Velocity.x, 0f, Velocity.z);
            float speed = hv.magnitude;
            bool moving = Grounded && speed > 0.6f;
            float amp = moving ? Mathf.Clamp01(speed / 8f) : 0f;
            if (moving) walkPhase += dt * (4f + speed * 1.3f);
            else walkPhase = Mathf.MoveTowards(walkPhase, Mathf.Round(walkPhase / Mathf.PI) * Mathf.PI, dt * 8f);
            float s = Mathf.Sin(walkPhase), c = Mathf.Cos(walkPhase);

            if (Grounded)
            {
                footL.localPosition = new Vector3(-0.22f, 0.09f + Mathf.Max(0f, c) * 0.12f * amp, s * 0.24f * amp);
                footR.localPosition = new Vector3(0.22f, 0.09f + Mathf.Max(0f, -c) * 0.12f * amp, -s * 0.24f * amp);
            }
            else
            {
                footL.localPosition = Vector3.Lerp(footL.localPosition, new Vector3(-0.22f, 0.17f, 0.1f), dt * 12f);
                footR.localPosition = Vector3.Lerp(footR.localPosition, new Vector3(0.22f, 0.12f, -0.08f), dt * 12f);
            }

            float sign = Mathf.Sign(s);
            if (moving && speed > 2f && sign != lastStepSign) Sfx.PlayAt("step", transform.position, 0.16f, 0.15f);
            lastStepSign = sign;

            // Landing squash with dust, take-off stretch.
            if (Grounded && !animGrounded)
            {
                float impact = Mathf.Clamp01(-animLastVy / 30f);
                squashVel -= 3f + impact * 9f;
                Fx.LocalOnly = true;
                Fx.Burst(transform.position + Vector3.up * 0.05f, new Color(0.75f, 0.75f, 0.8f), 3 + Mathf.RoundToInt(impact * 8f), 2f + impact * 4f, 0.12f, 0.45f, 3f);
                Fx.LocalOnly = false;
                if (impact > 0.25f) Sfx.PlayAt("land", transform.position, 0.3f + impact * 0.5f);
            }
            if (!Grounded && animGrounded && Velocity.y > 2f) squashVel += 4f;
            animGrounded = Grounded;
            animLastVy = Velocity.y;

            squashVel += (-squash * 220f - squashVel * 16f) * dt;
            squash = Mathf.Clamp(squash + squashVel * dt, -0.4f, 0.4f);
            float airStretch = Grounded ? 0f : Mathf.Clamp(Mathf.Abs(Velocity.y) / 60f, 0f, 0.12f);
            float sy = 1f + squash + airStretch;
            float sxz = 1f / Mathf.Sqrt(Mathf.Max(0.3f, sy));
            if (spawnAnim < 1f) spawnAnim = Mathf.Min(1f, spawnAnim + dt / 0.4f);
            rig.localScale = new Vector3(sxz, sy, sxz) * Mathf.Max(0.01f, EaseOutBack(spawnAnim));

            Vector3 local = transform.InverseTransformDirection(hv);
            rig.localPosition = new Vector3(0f, moving ? Mathf.Abs(s) * 0.06f * amp : 0f, 0f);
            float wobble = IsStunned ? Mathf.Sin(Time.time * 25f) * 8f : 0f;
            var lean = Quaternion.Euler(Mathf.Clamp(local.z * 1.1f, -12f, 12f), wobble, Mathf.Clamp(-local.x * 1.4f, -14f, 14f) + (moving ? s * 3f * amp : 0f));
            rig.localRotation = Quaternion.Slerp(rig.localRotation, lean, 1f - Mathf.Exp(-dt * 10f));

            // Gun recoil on every shot, spin while reloading.
            int ammo = Weapon.Ammo;
            if (animLastAmmo >= 0 && ammo < animLastAmmo) recoil = 1f;
            animLastAmmo = ammo;
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 7f);
            gunRoot.localPosition = GunRest + new Vector3(0f, recoil * 0.03f, -recoil * 0.2f);
            gunRoot.localRotation = Weapon.Reloading
                ? Quaternion.Euler(-Weapon.ReloadProgress * 360f, 0f, 0f)
                : Quaternion.Euler(-recoil * 14f, 0f, 0f);

            // Blinking (eyes squint while stunned).
            blinkTimer -= dt;
            if (blinkTimer <= 0f)
            {
                blinkTime = 0.12f;
                blinkTimer = UnityEngine.Random.Range(2f, 5f);
            }
            float eye = 1f;
            if (blinkTime > 0f) { blinkTime -= dt; eye = 0.12f; }
            if (IsStunned) eye = 0.35f;
            eyeL.localScale = eyeR.localScale = new Vector3(0.18f, 0.22f * eye, 0.03f);
            pupilL.localScale = pupilR.localScale = new Vector3(0.09f, 0.11f * eye, 0.02f);

            if (IsBlocking && !wasBlocking) shieldPop = 0f;
            wasBlocking = IsBlocking;
            shieldPop = Mathf.Min(1f, shieldPop + dt / 0.14f);
        }

        void OnBlockedDamage(DamageInfo info)
        {
            Fx.Pulse(Center, new Color(Color.r, Color.g, Color.b, 0.5f), 1.6f, 3.2f, 0.25f);
            Sfx.PlayAt("block", Center, 0.8f, 0.05f);
            Fx.Light(Center, Color.Lerp(Color, Color.white, 0.5f), 5f, 2.5f, 0.15f);
            ForEachEffect(e => e.OnBlockedDamage(info));
            GameEvents.RaiseDamageBlocked(this, info);
        }

        /// <summary>
        /// Called by a bullet that hits this player while blocking.
        /// Returns true when an effect kept the bullet alive (e.g. reflected it).
        /// </summary>
        public bool BlockBullet(Bullet bullet)
        {
            OnBlockedDamage(new DamageInfo
            {
                Amount = bullet.Damage,
                Source = bullet.Owner,
                Type = DamageType.Bullet,
                Point = bullet.transform.position,
                Direction = bullet.Velocity.normalized,
                Bullet = bullet,
            });
            for (int i = 0; i < Effects.Count; i++)
            {
                try { if (Effects[i].TryReflectBullet(bullet)) return true; }
                catch (Exception e) { Debug.LogException(e); }
            }
            return false;
        }

        // ------------------------------------------------------------------ damage

        /// <summary>Applies damage. Returns true if any damage was dealt.</summary>
        public bool TakeDamage(DamageInfo info)
        {
            if (NetworkPuppet) return false; // only the host applies damage
            if (!IsAlive || info == null || info.Amount <= 0f) return false;
            var s = Settings;
            if (info.Source != null && info.Source != this && info.Source.Team == Team && !s.friendlyFire) return false;
            if (InvulnerableTimer > 0f) return false;
            if (IsBlocking && info.Blockable)
            {
                OnBlockedDamage(info);
                return false;
            }

            if (info.Source != null && info.Source != this)
            {
                var target = this;
                info.Source.ForEachEffect(e => e.OnBeforeDealDamage(target, info));
            }
            info.Amount *= GetStat(Stat.DamageTaken);
            if (info.IsEnvironmental) info.Amount *= GetStat(Stat.HazardDamageTaken);
            ForEachEffect(e => e.OnBeforeDamaged(info));
            var args = new DamageEventArgs { Target = this, Info = info };
            GameEvents.RaisePlayerDamaging(args);
            if (args.Cancel || info.Amount <= 0f) return false;

            if (Barrier > 0f)
            {
                float absorbed = Mathf.Min(Barrier, info.Amount);
                Barrier -= absorbed;
                info.Amount -= absorbed;
                Fx.Burst(Center, new Color(0.85f, 0.95f, 1f), 4, 4f, 0.1f, 0.3f);
                if (info.Amount <= 0f) { Sfx.PlayAt("block", Center, 0.4f); return false; }
            }
            Health -= info.Amount;
            LastDamagedTime = Time.time;
            if (info.Knockback > 0f && info.Direction.sqrMagnitude > 0.0001f)
            {
                Velocity += info.Direction.normalized * info.Knockback;
                if (Grounded && Velocity.y < 2f) Velocity.y = 2f + info.Knockback * 0.25f;
            }

            Flash();
            Sfx.PlayAt("hit", Center, 0.55f);
            CubedHud.AddDamageNumber(Top, info.Amount, info.Source != null ? info.Source.Color : new Color(1f, 0.5f, 0.3f));

            var source = info.Source;
            if (source != null && source != this)
            {
                LastAttacker = source;
                source.DamageDealt += info.Amount;
                float steal = source.GetStat(Stat.Lifesteal);
                if (steal > 0f && source.IsAlive) source.Heal(info.Amount * steal);
                source.ForEachEffect(e => e.OnDealtDamage(this, info));
            }
            ForEachEffect(e => e.OnDamaged(info));
            GameEvents.RaisePlayerDamaged(this, info);

            if (Health <= 0f) Die(info);
            return true;
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
        }

        public void Kill(DamageInfo info = null)
        {
            if (!IsAlive) return;
            LivesRemaining = 0;
            Health = 0f;
            Die(info ?? new DamageInfo { Type = DamageType.Generic });
        }

        void Die(DamageInfo info)
        {
            if (LivesRemaining > 0)
            {
                LivesRemaining--;
                Health = MaxHealth;
                InvulnerableTimer = 1.5f;
                Fx.Pulse(Center, new Color(1f, 0.95f, 0.6f, 0.6f), 1f, 6f, 0.5f);
                Sfx.PlayAt("revive", Center, 0.8f);
                PlaySpawnAnimation();
                ForEachEffect(e => e.OnRevive());
                return;
            }

            IsAlive = false;
            Health = 0f;
            Deaths++;
            var killer = info.Source != null && info.Source != this ? info.Source : LastAttacker;
            if (killer != null && killer != this)
            {
                killer.Kills++;
                var victim = this;
                killer.ForEachEffect(e => e.OnKill(victim));
            }
            Fx.Burst(Center, Color, 22, 11f, 0.28f, 1.1f);
            Fx.Pulse(Center, new Color(Color.r, Color.g, Color.b, 0.5f), 0.5f, 4f, 0.4f);
            Sfx.PlayAt("death", Center, 0.8f);
            Fx.Light(Center, Color, 8f, 4f, 0.35f);
            CameraShake.Impulse(Center, 0.35f, 15f);
            Model.gameObject.SetActive(false);
            shield.SetActive(false);
            Controller.enabled = false;
            Velocity = Vector3.zero;
            CubedHud.AddKill(killer, this, info);
            GameEvents.RaisePlayerDied(this, info);
        }

        void Flash()
        {
            if (flashMaterial == null) flashMaterial = Mats.Solid(Color.white);
            flashTimer = 0.08f;
            SetBodyMaterial(flashMaterial);
        }

        void SetBodyMaterial(Material m)
        {
            if (bodyRenderers == null) return;
            foreach (var r in bodyRenderers)
                if (r != null && r.name == "Body") r.sharedMaterial = m != null ? m : bodyMaterial;
        }

        // ------------------------------------------------------------------ statuses

        public void ApplySlow(float amount, float duration)
        {
            slowAmount = slowTimer > 0f ? Mathf.Max(slowAmount, amount) : amount;
            slowAmount = Mathf.Clamp(slowAmount, 0f, 0.9f);
            slowTimer = Mathf.Max(slowTimer, duration);
        }

        public void ApplyPoison(float damagePerSecond, float duration, Player source)
        {
            poisonDps = poisonTimer > 0f ? Mathf.Max(poisonDps, damagePerSecond) : damagePerSecond;
            poisonTimer = Mathf.Max(poisonTimer, duration);
            poisonSource = source;
        }

        float stunTimer, barrierTimer;
        /// <summary>Temporary shield HP that absorbs damage before health.</summary>
        public float Barrier;
        public bool IsStunned => stunTimer > 0f;

        public void ApplyStun(float duration)
        {
            if (duration <= 0f) return;
            stunTimer = Mathf.Max(stunTimer, duration);
            Fx.Burst(Top, new Color(1f, 0.95f, 0.4f), 6, 3f, 0.1f, 0.5f, -2f);
        }

        public void AddBarrier(float amount, float duration)
        {
            Barrier = Mathf.Max(Barrier, 0f) + amount;
            barrierTimer = Mathf.Max(barrierTimer, duration);
        }

        void UpdateStatuses(float dt)
        {
            if (barrierTimer > 0f)
            {
                barrierTimer -= dt;
                if (barrierTimer <= 0f) Barrier = 0f;
            }
            if (slowTimer > 0f) slowTimer -= dt;
            float regen = GetStat(Stat.Regen);
            if (regen > 0f) Heal(regen * dt);

            if (poisonTimer > 0f)
            {
                poisonTimer -= dt;
                poisonTick -= dt;
                if (poisonTick <= 0f)
                {
                    poisonTick = 0.5f;
                    if (UnityEngine.Random.value < 0.6f) Fx.Burst(Center, new Color(0.5f, 1f, 0.3f), 3, 2f, 0.1f, 0.5f, -2f);
                    TakeDamage(new DamageInfo
                    {
                        Amount = poisonDps * 0.5f,
                        Source = poisonSource,
                        Type = DamageType.Effect,
                        Point = Center,
                        Blockable = false,
                    });
                }
            }
        }
    }
}
