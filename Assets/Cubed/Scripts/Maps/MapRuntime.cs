using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cubed
{
    /// <summary>Attached to every block GameObject.</summary>
    public class BlockInfo : MonoBehaviour
    {
        public BlockData Data;
        public BlockType Type;
        public float Hp;
        /// <summary>True for blocks spawned during play (e.g. Cube Wall) that are not part of the map.</summary>
        public bool Temporary;

        public void Damage(float amount)
        {
            if (Type == null || !Type.Destructible || amount <= 0f) return;
            Hp -= amount;
            if (Hp <= 0f) Break();
        }

        bool broken;

        public void Break()
        {
            if (broken) return;
            broken = true;
            var size = transform.lossyScale;
            int count = Mathf.Clamp(Mathf.RoundToInt(size.x * size.y * size.z * 3f), 4, 30);
            Fx.Burst(transform.position, Type != null ? Type.Color : Color.gray, count, 7f, 0.25f, 0.9f);
            Sfx.Play("hit", 0.6f);
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

        Transform blockRoot, spawnRoot, borderRoot;
        readonly Transform[] walls = new Transform[6];

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

            foreach (var b in data.blocks) map.CreateBlock(b);
            if (editorMode)
                foreach (var s in data.spawns) map.CreateSpawnMarker(s);

            map.BuildBorders();
            map.RefreshBounds();
            GameEvents.RaiseMapLoaded(data);
            return map;
        }

        // ------------------------------------------------------------------ blocks

        public BlockInfo CreateBlock(BlockData b)
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
            var mat = type.Transparent ? Mats.Transparent(c) : Mats.Solid(c);
            var go = Prim.Cube("Block_" + type.Id, blockRoot, b.Center, b.Size, mat, true);
            if (type.Transparent) go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var info = go.AddComponent<BlockInfo>();
            info.Data = b;
            info.Type = type;
            info.Hp = type.Health * Mathf.Max(1f, b.W * b.H * b.D * 0.5f);
            return info;
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
                var go = Prim.Cube(bottom ? "Border_Bottom" : "Border", borderRoot, Vector3.zero, Vector3.one, Mats.Transparent(c));
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
                if (walls[i] != null) walls[i].GetComponent<MeshRenderer>().sharedMaterial = Mats.Transparent(c);
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
        }
    }
}
