using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spheres
{
    /// <summary>Attached to every block GameObject.</summary>
    public class BlockInfo : MonoBehaviour
    {
        public BlockData Data;
        public BlockType Type;
        public float Hp;
        /// <summary>True for blocks spawned during play (e.g. Cube Wall) that are not part of the map.</summary>
        public bool Temporary;
        /// <summary>Position in the map's block list (-1 for temporary blocks). Used to sync breaks online.</summary>
        public int Index = -1;
        /// <summary>Who last damaged it (credited for blast-block kills).</summary>
        public Player LastAttacker;

        public void Damage(float amount) => Damage(amount, null);

        public void Damage(float amount, Player source)
        {
            if (Type == null || !Type.Destructible || amount <= 0f || broken) return;
            if (GameManager.Instance != null && GameManager.Instance.NetClientMode) return; // host decides
            if (source != null) LastAttacker = source;
            Hp -= amount;
            if (Hp <= 0f) Break();
            else if (Type.Destructible && Hp < 0.5f * Type.Health) Crack();
        }

        bool broken, cracked;

        void Crack()
        {
            if (cracked) return;
            cracked = true;
            Fx.Burst(transform.position, Type.Color, 4, 2.5f, 0.12f, 0.5f);
        }

        public bool IsBroken => broken;

        public void Break()
        {
            if (broken) return;
            broken = true;
            var size = transform.lossyScale;
            var pos = transform.position;
            int count = Mathf.Clamp(Mathf.RoundToInt(size.x * size.y * size.z * 3f), 4, 30);
            Fx.Burst(pos, Type != null ? Type.Color : Color.gray, count, 7f, 0.25f, 0.9f);
            Sfx.PlayAt("hit", pos, 0.6f);
            var map = GetComponentInParent<MapRuntime>();
            if (map != null && Index >= 0) map.NoteBroken(Index);
            if (Index >= 0 && Net.NetSession.Mirroring) Net.NetSession.Instance.MirrorBlockGone(Index);
            if (Type != null && Type.ExplodeRadius > 0f)
            {
                var t = Type;
                var owner = LastAttacker;
                System.Action boom = () => Explosion.Create(pos, t.ExplodeRadius * Mathf.Max(1f, Mathf.Pow(size.x * size.y * size.z, 1f / 3f)), t.ExplodeDamage, owner, 20f, new Color(1f, 0.5f, 0.15f), true, "blast_block");
                if (map != null) map.Delay(0.15f, boom); else boom();
            }
            Destroy(gameObject);
        }

        /// <summary>Client: the host broke this block (its effects arrive separately).</summary>
        public void NetBreak()
        {
            if (broken) return;
            broken = true;
            Destroy(gameObject);
        }
    }

    /// <summary>Editor-only marker for spawn points.</summary>
    public class SpawnMarker : MonoBehaviour
    {
        public SpawnData Data;
    }

    /// <summary>
    /// The live arena: blocks, spawn points and the box of damaging borders (which can shrink).
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class MapRuntime : MonoBehaviour
    {
        public MapData Data { get; private set; }
        public bool EditorMode { get; private set; }
        public Bounds FullBounds { get; private set; }
        public Bounds CurrentBounds { get; private set; }
        /// <summary>0 = full size, 1 = fully shrunk.</summary>
        public float Shrink { get; private set; }
        /// <summary>Fraction of the XZ size left when fully shrunk.</summary>
        public float MinShrinkFraction = 0.3f;

        public Color Background = new Color(0.07f, 0.08f, 0.12f);
        /// <summary>Horizon color used for distance fog (set when the atmosphere is built).</summary>
        public Color FogColor = new Color(0.2f, 0.23f, 0.36f);

        Transform blockRoot, spawnRoot, borderRoot, mechRoot, propRoot;
        readonly List<BlockInfo> blocksByIndex = new List<BlockInfo>();
        readonly List<Mechanism> mechList = new List<Mechanism>();
        readonly List<Prop> propList = new List<Prop>();
        readonly List<int> brokenBlocks = new List<int>();
        readonly List<(float at, System.Action action)> delayed = new List<(float, System.Action)>();

        /// <summary>Round clock that drives moving parts (identical on host and clients).</summary>
        public float Clock { get; private set; }
        /// <summary>Editor: animate moving parts so you can preview them.</summary>
        public bool PreviewMotion;
        public IReadOnlyList<Mechanism> Mechanisms => mechList;
        public IReadOnlyList<Prop> Props => propList;
        public IReadOnlyList<int> BrokenBlocks => brokenBlocks;
        readonly Transform[] walls = new Transform[6];

        void OnDestroy()
        {
            foreach (var m in animated.Values) if (m != null) Destroy(m);
            foreach (var m in glowOverlays.Values) if (m != null) Destroy(m);
            foreach (var m in wallMats) if (m != null) Destroy(m);
        }

        public static MapRuntime Build(MapData data, bool editorMode)
        {
            data.Validate();
            var go = new GameObject("Map: " + data.name);
            var map = go.AddComponent<MapRuntime>();
            map.Data = data;
            map.EditorMode = editorMode;
            map.blockRoot = new GameObject("Blocks").transform;
            map.blockRoot.SetParent(go.transform, false);
            map.spawnRoot = new GameObject("Spawns").transform;
            map.spawnRoot.SetParent(go.transform, false);
            map.borderRoot = new GameObject("Borders").transform;
            map.borderRoot.SetParent(go.transform, false);

            if (!string.IsNullOrEmpty(data.background) && ColorUtility.TryParseHtmlString(data.background, out var bg))
                map.Background = bg;

            map.mechRoot = new GameObject("Mechanisms").transform;
            map.mechRoot.SetParent(go.transform, false);
            map.propRoot = new GameObject("Props").transform;
            map.propRoot.SetParent(go.transform, false);

            for (int i = 0; i < data.blocks.Count; i++) map.blocksByIndex.Add(map.CreateBlock(data.blocks[i], i));
            for (int i = 0; i < data.mechs.Count; i++) map.CreateMech(data.mechs[i], i);
            bool puppetProps = editorMode || (GameManager.Instance != null && GameManager.Instance.NetClientMode);
            for (int i = 0; i < data.props.Count; i++) map.CreateProp(data.props[i], i, puppetProps);
            if (editorMode)
                foreach (var s in data.spawns) map.CreateSpawnMarker(s);

            map.BuildBorders();
            map.RefreshBounds();
            map.BuildAtmosphere();
            GameEvents.RaiseMapLoaded(data);
            return map;
        }

        // ------------------------------------------------------------------ blocks

        public Mechanism CreateMech(MechData m, int index)
        {
            try
            {
                var mech = Mechanism.Create(m, index, mechRoot);
                mechList.Add(mech);
                return mech;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        public Prop CreateProp(PropData p, int index, bool puppet)
        {
            var prop = Prop.Create(p, index, propRoot, puppet);
            propList.Add(prop);
            return prop;
        }

        /// <summary>Editor: remove a moving part from the map data and scene.</summary>
        public void RemoveMech(Mechanism m)
        {
            if (m == null) return;
            Data.mechs.Remove(m.Data);
            mechList.Remove(m);
            DestroyImmediate(m.gameObject);
        }

        /// <summary>Editor: rebuild a moving part after its settings changed.</summary>
        public Mechanism RebuildMech(Mechanism m)
        {
            if (m == null) return null;
            var data = m.Data;
            int idx = mechList.IndexOf(m);
            mechList.Remove(m);
            DestroyImmediate(m.gameObject);
            var fresh = Mechanism.Create(data, Data.mechs.IndexOf(data), mechRoot);
            if (idx >= 0 && idx <= mechList.Count) mechList.Insert(idx, fresh); else mechList.Add(fresh);
            return fresh;
        }

        public void RemoveProp(Prop p)
        {
            if (p == null) return;
            Data.props.Remove(p.Data);
            propList.Remove(p);
            DestroyImmediate(p.gameObject);
        }

        public Prop PropAt(int index) => index >= 0 && index < propList.Count ? propList[index] : null;

        public BlockInfo BlockByIndex(int index) => index >= 0 && index < blocksByIndex.Count ? blocksByIndex[index] : null;

        internal void NoteBroken(int index)
        {
            if (!brokenBlocks.Contains(index)) brokenBlocks.Add(index);
        }

        /// <summary>Client: apply a block break sent by the host.</summary>
        public void NetBreakBlock(int index)
        {
            var b = BlockByIndex(index);
            NoteBroken(index);
            if (b != null) b.NetBreak();
        }

        /// <summary>Run an action after a delay (cleared with the map).</summary>
        public void Delay(float seconds, System.Action action)
        {
            if (action != null) delayed.Add((Time.time + seconds, action));
        }

        void UpdateClock(float dt)
        {
            var gm = GameManager.Instance;
            if (EditorMode)
            {
                Clock = PreviewMotion ? Clock + Time.unscaledDeltaTime : 0f;
                return;
            }
            if (gm == null) { Clock += dt; return; }
            float target = gm.RoundTime;
            if (!gm.NetClientMode) { Clock = target; return; }
            // Clients: advance locally and ease toward the host's clock from snapshots.
            bool running = (gm.State == GameState.Fighting || gm.State == GameState.RoundEnd) && !gm.HostPaused;
            if (running) Clock += dt;
            float err = target - Clock;
            if (Mathf.Abs(err) > 0.5f) Clock = target;
            else Clock += err * Mathf.Min(1f, dt * 2f);
        }

        void DriveMechanisms()
        {
            float dt = Time.deltaTime;
            UpdateClock(dt);
            if (mechList.Count > 0)
            {
                foreach (var m in mechList) if (m != null) m.Drive(Clock, dt);
                Physics.SyncTransforms();
                var gm = GameManager.Instance;
                bool live = !EditorMode && gm != null && gm.State == GameState.Fighting && !gm.Paused;
                if (!EditorMode)
                    foreach (var m in mechList) if (m != null) m.Depenetrate();
                if (live && !gm.NetClientMode)
                    foreach (var m in mechList) if (m != null) m.HurtPlayers(dt);
            }
            for (int i = 0; i < delayed.Count; i++)
            {
                if (Time.time < delayed[i].at) continue;
                var a = delayed[i].action;
                delayed.RemoveAt(i--);
                try { a(); } catch (System.Exception e) { Debug.LogException(e); }
            }
        }

        public BlockInfo CreateBlock(BlockData b) => CreateBlock(b, -1);

        public BlockInfo CreateBlock(BlockData b, int index)
        {
            var type = BlockTypes.Get(b.type);
            Color c = type.Color;
            if (!type.Transparent)
            {
                // Subtle per-block shading so large flat areas read better.
                float shade = 0.94f + 0.1f * Mathf.Abs(Mathf.Sin(b.x * 12.9898f + b.y * 78.233f + b.z * 37.719f));
                c = new Color(c.r * shade, c.g * shade, c.b * shade, c.a);
                c = Quantize(c);
            }
            Material mat;
            if (type.Transparent && type.Glowing && Mats.GlowAvailable) mat = Mats.Glow(new Color(c.r, c.g, c.b, 0.55f), 1.8f, 0f);
            else if (type.Transparent) mat = Mats.Transparent(c);
            else if (type.DamagePerSecond > 0f) mat = AnimatedMaterial(type, 0);
            else if (type.BounceSpeed > 0f) mat = AnimatedMaterial(type, 1);
            else mat = Looks.Block(c);
            // Tiled mesh: the bevel texture repeats once per cell regardless of the block's size.
            var go = Prim.Create(type.Transparent ? Prim.CubeMesh : Looks.TiledBox(b.W, b.H, b.D), "Block_" + type.Id, blockRoot, b.Center, b.Size, mat);
            go.AddComponent<BoxCollider>();
            if (type.Transparent) go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!type.Transparent && (type.DamagePerSecond > 0f || type.BounceSpeed > 0f) && Mats.GlowAvailable)
            {
                // Emissive skin so lava and bouncers bloom.
                bool lava = type.DamagePerSecond > 0f;
                var glow = GlowOverlay(type, lava);
                var skin = Prim.Create(Prim.CubeMesh, "Glow", go.transform, lava ? Vector3.zero : new Vector3(0f, 0.5f, 0f),
                    lava ? Vector3.one * 1.01f : new Vector3(1.01f, 0.06f / b.H, 1.01f), glow);
                skin.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (type.ExplodeRadius > 0f && Mats.GlowAvailable)
            {
                // Pulsing hazard stripe so blast blocks read as "don't shoot me while standing here".
                var stripe = Prim.Cube("Stripe", go.transform, Vector3.zero, new Vector3(1.01f, 0.18f / b.H, 1.01f), GlowOverlay(type, false));
                stripe.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var info = go.AddComponent<BlockInfo>();
            info.Index = index;
            info.Data = b;
            info.Type = type;
            info.Hp = type.Health * Mathf.Max(1f, b.W * b.H * b.D * 0.5f);
            return info;
        }

        // ---- animated hazard / bouncer materials (one instance per type per map)
        readonly Dictionary<string, Material> animated = new Dictionary<string, Material>();
        readonly List<(Material mat, Color baseColor, int kind)> animatedList = new List<(Material, Color, int)>();

        Material AnimatedMaterial(BlockType type, int kind)
        {
            if (animated.TryGetValue(type.Id, out var m)) return m;
            m = new Material(Looks.Block(type.Color)) { name = "Animated_" + type.Id };
            animated[type.Id] = m;
            animatedList.Add((m, type.Color, kind));
            return m;
        }

        readonly Dictionary<string, Material> glowOverlays = new Dictionary<string, Material>();

        Material GlowOverlay(BlockType type, bool lava)
        {
            if (glowOverlays.TryGetValue(type.Id, out var m)) return m;
            var c = lava ? Color.Lerp(type.Color, new Color(1f, 0.6f, 0.1f), 0.5f) : type.Color;
            m = Mats.NewGlow(new Color(c.r, c.g, c.b, lava ? 0.55f : 0.8f), lava ? 2.2f : 2.6f, 0f);
            glowOverlays[type.Id] = m;
            animatedList.Add((m, new Color(c.r, c.g, c.b, lava ? 0.55f : 0.8f), lava ? 2 : 3));
            return m;
        }

        Material[] wallMats = new Material[6];
        Color[] wallBase = new Color[6];
        Transform sky;

        void Update()
        {
            DriveMechanisms();
            float t = Time.time;
            foreach (var (mat, c, kind) in animatedList)
            {
                if (mat == null) continue;
                if (kind >= 2)
                {
                    float pulse = kind == 2 ? 0.75f + 0.25f * Mathf.Sin(t * 2.6f) * Mathf.Sin(t * 0.9f + 2f) : 0.6f + 0.4f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 4f), 3f);
                    mat.color = new Color(c.r, c.g, c.b, c.a * pulse);
                    continue;
                }
                if (kind == 0) // lava: slow churning glow
                {
                    float k = 0.5f + 0.5f * Mathf.Sin(t * 2.2f) * Mathf.Sin(t * 0.7f + 1f);
                    mat.color = Color.Lerp(c * 0.85f, Color.Lerp(c, new Color(1f, 0.85f, 0.3f), 0.6f), k);
                }
                else // bouncer: rhythmic pulse
                {
                    float k = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 4f), 3f);
                    mat.color = Color.Lerp(c, Color.white, k * 0.35f);
                }
                if (mat.HasProperty("_MainTex")) mat.mainTextureOffset = kind == 0 ? new Vector2(t * 0.05f, t * 0.03f) : Vector2.zero;
            }

            // Borders light up as players approach them.
            var b = CurrentBounds;
            for (int i = 0; i < 6; i++)
            {
                if (wallMats[i] == null) continue;
                float nearest = 999f;
                foreach (var p in Player.All)
                {
                    if (!p.IsAlive) continue;
                    Vector3 c = p.Center;
                    float d;
                    switch (i)
                    {
                        case 0: d = c.y - b.min.y; break;
                        case 1: d = b.max.y - c.y; break;
                        case 2: d = c.x - b.min.x; break;
                        case 3: d = b.max.x - c.x; break;
                        case 4: d = c.z - b.min.z; break;
                        default: d = b.max.z - c.z; break;
                    }
                    nearest = Mathf.Min(nearest, d);
                }
                var col = wallBase[i];
                float glow = Mathf.Clamp01(1f - nearest / 7f);
                float shrinkPulse = Shrink > 0f && Shrink < 1f ? 0.15f * (0.5f + 0.5f * Mathf.Sin(t * 6f)) : 0f;
                col.a = Mathf.Clamp01(col.a + glow * 0.55f + shrinkPulse);
                wallMats[i].color = col;
            }
            if (sky != null) sky.Rotate(0f, Time.deltaTime * 0.6f, 0f, Space.World);
        }

        void BuildAtmosphere()
        {
            var bg = Background;
            Color.RGBToHSV(bg, out float h, out float s, out float v);
            Color top = Color.HSVToRGB(h, s * 0.9f, Mathf.Clamp01(v * 0.6f));
            Color horizon = Color.HSVToRGB(Mathf.Repeat(h + 0.04f, 1f), Mathf.Clamp01(s * 0.7f + 0.1f), Mathf.Clamp01(v * 2.6f + 0.08f));
            Color bottom = Color.HSVToRGB(h, s, Mathf.Clamp01(v * 0.35f));
            var center = FullBounds.center;
            FogColor = Color.Lerp(horizon, bottom, 0.45f);
            sky = new GameObject("Atmosphere").transform;
            sky.SetParent(transform, false);
            sky.position = center;
            Looks.SkyDome(sky, center, top, horizon, bottom);

            // Distant floating cubes give the void some depth and parallax.
            var rng = new System.Random(Data.name.GetHashCode());
            var far = Mats.Solid(Color.Lerp(bottom, horizon, 0.35f));
            var farLight = Mats.Solid(Color.Lerp(horizon, top, 0.25f));
            for (int i = 0; i < 70; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = 120f + (float)rng.NextDouble() * 200f;
                float y = ((float)rng.NextDouble() - 0.6f) * 160f;
                float size = 4f + (float)rng.NextDouble() * 22f;
                var go = Prim.Cube("FarCube", sky, center + new Vector3(Mathf.Cos(ang) * dist, y, Mathf.Sin(ang) * dist), Vector3.one * size, rng.NextDouble() < 0.3 ? farLight : far);
                go.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);
                var r = go.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            Looks.StarsAndSun(sky, center, Color.Lerp(horizon, Color.white, 0.6f));
            if (Options.Current.ambientParticles) Looks.Motes(transform, FullBounds, Color.Lerp(horizon, Color.white, 0.5f) * new Color(1f, 1f, 1f, 0.5f));
        }

        static Color Quantize(Color c)
        {
            const float q = 32f;
            return new Color(Mathf.Round(c.r * q) / q, Mathf.Round(c.g * q) / q, Mathf.Round(c.b * q) / q, c.a);
        }

        public IEnumerable<BlockInfo> Blocks => blockRoot.GetComponentsInChildren<BlockInfo>();

        /// <summary>Spawns a temporary block that is not saved into the map (used by cards).</summary>
        public BlockInfo SpawnTemporaryBlock(Vector3 center, Vector3 size, Quaternion rotation, string typeId, float lifetime)
        {
            var type = BlockTypes.Get(typeId);
            var mat = type.Transparent ? Mats.Transparent(type.Color) : Mats.Solid(type.Color);
            if (Net.NetSession.Mirroring) Net.NetSession.Instance.MirrorBox(center, rotation, size, type.Color, lifetime);
            var go = Prim.Cube("TempBlock", blockRoot, center, size, mat, false);
            go.transform.rotation = rotation;
            go.AddComponent<BoxCollider>();
            var info = go.AddComponent<BlockInfo>();
            info.Type = type;
            info.Hp = type.Health;
            info.Temporary = true;
            if (lifetime > 0f) go.AddComponent<TimedDestroy>().Seconds = lifetime;
            return info;
        }

        public void RemoveBlock(BlockInfo info)
        {
            if (info == null) return;
            if (info.Data != null) Data.blocks.Remove(info.Data);
            DestroyImmediate(info.gameObject);
        }

        // ------------------------------------------------------------------ spawns

        public SpawnMarker CreateSpawnMarker(SpawnData s)
        {
            Color c = s.team >= 0 ? Teams.GetColor(s.team) : Color.white;
            c.a = 0.55f;
            var go = Prim.Cube("Spawn", spawnRoot, s.Position + Vector3.up * 0.5f, new Vector3(0.8f, 1f, 0.8f), Mats.Transparent(c), true);
            var marker = go.AddComponent<SpawnMarker>();
            marker.Data = s;
            return marker;
        }

        public void RemoveSpawn(SpawnMarker m)
        {
            if (m == null) return;
            Data.spawns.Remove(m.Data);
            DestroyImmediate(m.gameObject);
        }

        /// <summary>
        /// Picks spawn positions for a team: team-specific spawns first, then neutral ones, then
        /// automatically generated positions on top of solid blocks.
        /// </summary>
        public List<Vector3> GetSpawnPositions(int team, int count, List<Vector3> alreadyUsed)
        {
            Physics.SyncTransforms();
            var result = new List<Vector3>();
            var own =Data.spawns.Where(s => s.team == team).Select(s => s.Position).ToList();
            var neutral = Data.spawns.Where(s => s.team < 0 || s.team >= Teams.Max).Select(s => s.Position).ToList();

            foreach (var p in own.Concat(neutral))
            {
                if (result.Count >= count) break;
                if (alreadyUsed.Any(u => (u - p).sqrMagnitude < 0.5f)) continue;
                result.Add(p);
            }

            // Reuse this team's own spawns with an offset if there are more players than spawns.
            int reuse = 0;
            while (result.Count < count && own.Count > 0 && reuse < 16)
            {
                var basePos = own[reuse % own.Count];
                var offset = Quaternion.Euler(0f, reuse * 77f, 0f) * Vector3.forward * 1.2f;
                result.Add(basePos + offset + Vector3.up * 0.5f);
                reuse++;
            }

            if (result.Count < count)
            {
                var generated = GenerateSpawns(32);
                var used = new List<Vector3>(alreadyUsed);
                used.AddRange(result);
                while (result.Count < count && generated.Count > 0)
                {
                    // Greedy: farthest from every used spawn.
                    Vector3 best = generated[0];
                    float bestScore = -1f;
                    foreach (var g in generated)
                    {
                        float score = used.Count == 0 ? Random.value : used.Min(u => (u - g).sqrMagnitude);
                        if (score > bestScore) { bestScore = score; best = g; }
                    }
                    result.Add(best);
                    used.Add(best);
                    generated.Remove(best);
                }
            }

            while (result.Count < count) result.Add(FullBounds.center);
            return result;
        }

        List<Vector3> GenerateSpawns(int samples)
        {
            var list = new List<Vector3>();
            var b = FullBounds;
            for (int i = 0; i < samples * 4 && list.Count < samples; i++)
            {
                var origin = new Vector3(Random.Range(b.min.x + 2f, b.max.x - 2f), b.max.y - 0.5f, Random.Range(b.min.z + 2f, b.max.z - 2f));
                if (!Physics.Raycast(origin, Vector3.down, out var hit, b.size.y, ~0, QueryTriggerInteraction.Ignore)) continue;
                var info = hit.collider.GetComponent<BlockInfo>();
                if (info == null || info.Temporary || info.Type.DamagePerSecond > 0f || info.Type.Destructible) continue;
                if (hit.normal.y < 0.7f) continue;
                if (Physics.CheckSphere(hit.point + Vector3.up * 1.2f, 0.6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                list.Add(hit.point + Vector3.up * 0.05f);
            }
            return list;
        }

        // ------------------------------------------------------------------ borders

        void BuildBorders()
        {
            for (int i = 0; i < 6; i++)
            {
                if (walls[i] != null) Destroy(walls[i].gameObject);
                bool bottom = i == 0;
                Color c = bottom ? new Color(1f, 0.25f, 0.2f, 0.28f) : new Color(1f, 0.3f, 0.25f, 0.1f);
                if (wallMats[i] == null)
                {
                    wallMats[i] = Mats.NewTransparent(c);
                    wallMats[i].mainTexture = Looks.GridTexture;
                }
                wallBase[i] = c;
                var go = Prim.Cube(bottom ? "Border_Bottom" : "Border", borderRoot, Vector3.zero, Vector3.one, wallMats[i]);
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                walls[i] = go.transform;
            }
        }

        /// <summary>Recolor borders (e.g. blue when damaging borders are disabled).</summary>
        public void SetBorderStyle(bool sidesDamaging, bool bottomDamaging)
        {
            for (int i = 0; i < 6; i++)
            {
                bool bottom = i == 0;
                bool dmg = bottom ? bottomDamaging : sidesDamaging;
                Color c = dmg ? new Color(1f, 0.3f, 0.25f, bottom ? 0.28f : 0.1f) : new Color(0.4f, 0.7f, 1f, bottom ? 0.22f : 0.06f);
                wallBase[i] = c;
                if (wallMats[i] != null) wallMats[i].color = c;
            }
        }

        public void ResizeArena(int sx, int sy, int sz)
        {
            Data.sizeX = sx;
            Data.sizeY = sy;
            Data.sizeZ = sz;
            Data.Validate();
            RefreshBounds();
        }

        public void SetShrink(float t)
        {
            Shrink = Mathf.Clamp01(t);
            RefreshBounds();
        }

        void RefreshBounds()
        {
            FullBounds = Data.Bounds;
            var size = FullBounds.size;
            float f = Mathf.Lerp(1f, MinShrinkFraction, Shrink);
            var cur = new Bounds(FullBounds.center, new Vector3(size.x * f, size.y, size.z * f));
            CurrentBounds = cur;

            const float t = 0.08f;
            Vector3 mn = cur.min, mx = cur.max, c = cur.center, s = cur.size;
            Place(0, new Vector3(c.x, mn.y, c.z), new Vector3(s.x, t, s.z));
            Place(1, new Vector3(c.x, mx.y, c.z), new Vector3(s.x, t, s.z));
            Place(2, new Vector3(mn.x, c.y, c.z), new Vector3(t, s.y, s.z));
            Place(3, new Vector3(mx.x, c.y, c.z), new Vector3(t, s.y, s.z));
            Place(4, new Vector3(c.x, c.y, mn.z), new Vector3(s.x, s.y, t));
            Place(5, new Vector3(c.x, c.y, mx.z), new Vector3(s.x, s.y, t));
        }

        void Place(int i, Vector3 pos, Vector3 scale)
        {
            if (walls[i] == null) return;
            walls[i].position = pos;
            walls[i].localScale = scale;
            // One grid square per 2m on whichever two axes the wall spans.
            if (wallMats[i] != null)
            {
                Vector2 tiling = i < 2 ? new Vector2(scale.x, scale.z) : i < 4 ? new Vector2(scale.z, scale.y) : new Vector2(scale.x, scale.y);
                wallMats[i].mainTextureScale = tiling * 0.5f;
            }
        }
    }
}
