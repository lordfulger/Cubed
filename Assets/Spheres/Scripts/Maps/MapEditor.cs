using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// In-game map builder. Fly around, paint blocks on a grid, place team spawns, mirror for symmetry,
    /// resize the arena (whose faces are the damaging borders), save/load and test-play against bots.
    /// </summary>
    public class MapEditor : MonoBehaviour
    {
        public MapRuntime Map { get; private set; }
        public MapData Data => Map != null ? Map.Data : null;
        public bool IsOpen { get; private set; }

        enum BrushKind { Block, Spawn, Erase, Mech, Prop }

        struct Brush
        {
            public BrushKind Kind;
            public string BlockType;
            /// <summary>Mechanism or prop kind id.</summary>
            public string Id;
            public int Team;
            public string Label;
            public Color Color;
        }

        readonly List<Brush> brushes = new List<Brush>();
        int brushIndex;
        /// <summary>Palette tab: 0 blocks, 1 moving parts, 2 props, 3 spawns.</summary>
        int category;
        static readonly string[] Categories = { "Blocks", "Moving", "Props", "Spawns" };
        Mechanism selectedMech, hoverMech;
        Prop hoverProp;
        Rect mechRect;
        int layer = 8;
        bool mirrorX, mirrorZ, showGrid = true, showLoad, hidden;
        Camera cam;
        float yaw = 45f, pitch = 35f;
        GameObject ghost;
        Material ghostMat;
        readonly Stack<string> undo = new Stack<string>();
        readonly Stack<string> redo = new Stack<string>();
        string status = "";
        float statusTime;
        Vector2 loadScroll;
        Rect panelRect, loadRect;

        // Hover state
        bool hasCell;
        Vector3Int cell;
        BlockInfo hoverBlock;
        SpawnMarker hoverSpawn;
        bool stroking;
        int strokeY;
        readonly HashSet<Vector3Int> strokeCells = new HashSet<Vector3Int>();

        GameManager Gm => GameManager.Instance;
        Brush Current => brushes[Mathf.Clamp(brushIndex, 0, brushes.Count - 1)];

        // ------------------------------------------------------------------ lifecycle

        void BuildPalette()
        {
            brushes.Clear();
            foreach (var t in BlockTypes.All.Where(t => t.ShowInEditor))
                brushes.Add(new Brush { Kind = BrushKind.Block, BlockType = t.Id, Label = t.Name, Color = t.Color });
            for (int team = 0; team < Teams.Max; team++)
                brushes.Add(new Brush { Kind = BrushKind.Spawn, Team = team, Label = Teams.GetName(team) + " spawn", Color = Teams.GetColor(team) });
            brushes.Add(new Brush { Kind = BrushKind.Spawn, Team = -1, Label = "Any-team spawn", Color = Color.white });
            foreach (var k in MechKinds.All)
                brushes.Add(new Brush { Kind = BrushKind.Mech, Id = k, Label = MechKinds.Name(k), Color = k == MechKinds.Pendulum || k == MechKinds.Saw || k == MechKinds.Spikes ? new Color(1f, 0.35f, 0.3f) : new Color(0.45f, 0.85f, 1f) });
            foreach (var k in PropKinds.All.Where(k => !k.HideInEditor))
                brushes.Add(new Brush { Kind = BrushKind.Prop, Id = k.Id, Label = k.Name, Color = k.Color });
            brushes.Add(new Brush { Kind = BrushKind.Erase, Label = "Eraser", Color = UIKit.Bad });
            brushIndex = Mathf.Clamp(brushIndex, 0, brushes.Count - 1);
        }

        static int CategoryOf(Brush b) =>
            b.Kind == BrushKind.Block ? 0 : b.Kind == BrushKind.Mech ? 1 : b.Kind == BrushKind.Prop ? 2 : b.Kind == BrushKind.Spawn ? 3 : -1;

        /// <summary>Brush indices shown in the current palette tab (eraser always last).</summary>
        List<int> VisibleBrushes()
        {
            var list = new List<int>();
            for (int i = 0; i < brushes.Count; i++)
                if (CategoryOf(brushes[i]) == category) list.Add(i);
            list.Add(brushes.Count - 1);
            return list;
        }

        public static MapData NewMap()
        {
            var m = new MapData { name = "My Map", author = "", sizeX = 40, sizeY = 30, sizeZ = 40 };
            m.Box(12, 7, 12, 16, 1, 16, "solid");
            m.Spawn(14, 8, 20, 0);
            m.Spawn(25, 8, 20, 1);
            return m;
        }

        public void Open(MapData data)
        {
            Close();
            BuildPalette();
            IsOpen = true;
            hidden = false;
            undo.Clear();
            redo.Clear();
            Rebuild(data != null ? data.Clone() : NewMap());

            cam = new GameObject("Editor Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Map.Background;
            cam.fieldOfView = 65f;
            cam.farClipPlane = 800f;
            cam.depth = 5f;
            SpheresPostFX.Attach(cam);
            FrameMap();

            ghostMat = Mats.NewTransparent(new Color(1f, 1f, 1f, 0.35f));
            ghost = Prim.Cube("Ghost", null, Vector3.zero, Vector3.one * 1.02f, ghostMat);
            ghost.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ghost.SetActive(false);
            layer = Mathf.Clamp(layer, 0, Data.sizeY - 1);
            Status("Editing \"" + Data.name + "\"");
        }

        public void Close()
        {
            if (Map != null) Destroy(Map.gameObject);
            if (cam != null) Destroy(cam.gameObject);
            if (ghost != null) Destroy(ghost);
            if (ghostMat != null) Destroy(ghostMat);
            Map = null;
            cam = null;
            ghost = null;
            IsOpen = false;
            hidden = false;
            showLoad = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Hide while test-playing.</summary>
        public void Hide()
        {
            if (!IsOpen) return;
            hidden = true;
            if (Map != null) Map.gameObject.SetActive(false);
            if (cam != null) cam.enabled = false;
            if (ghost != null) ghost.SetActive(false);
        }

        public void Show()
        {
            if (!IsOpen) return;
            hidden = false;
            if (Map != null) Map.gameObject.SetActive(true);
            if (cam != null) cam.enabled = true;
        }

        void Rebuild(MapData data)
        {
            if (Map != null) DestroyImmediate(Map.gameObject);
            Map = MapRuntime.Build(data, true);
            Map.SetBorderStyle(true, true);
            selectedMech = null;
            if (cam != null) cam.backgroundColor = Map.Background;
        }

        void FrameMap()
        {
            var b = Data.Bounds;
            yaw = 45f;
            pitch = 35f;
            float dist = Mathf.Max(b.size.x, b.size.z) * 0.9f;
            cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.position = b.center - cam.transform.forward * dist;
        }

        void Status(string s)
        {
            status = s;
            statusTime = Time.unscaledTime;
        }

        void PushUndo()
        {
            undo.Push(Data.ToJson(false));
            redo.Clear();
            if (undo.Count > 100)
            {
                var keep = undo.Take(100).Reverse().ToList();
                undo.Clear();
                foreach (var k in keep) undo.Push(k);
            }
        }

        void Undo()
        {
            if (undo.Count == 0) return;
            redo.Push(Data.ToJson(false));
            Rebuild(MapData.FromJson(undo.Pop()));
            Status("Undo");
        }

        void Redo()
        {
            if (redo.Count == 0) return;
            undo.Push(Data.ToJson(false));
            Rebuild(MapData.FromJson(redo.Pop()));
            Status("Redo");
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            if (!IsOpen || hidden || cam == null || Gm == null || Gm.State != GameState.Editor) return;
            float dt = Time.unscaledDeltaTime;
            bool typing = GUIUtility.keyboardControl != 0;
            Vector2 mouseGui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / UIKit.Scale;
            bool overUi = panelRect.Contains(mouseGui) || (showLoad && loadRect.Contains(mouseGui)) || (selectedMech != null && mechRect.Contains(mouseGui));
            if (!overUi && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
            {
                GUIUtility.keyboardControl = 0; // leave text fields so WASD flies again
                typing = false;
            }

            // Camera fly controls (hold right mouse to look).
            bool looking = Input.GetMouseButton(1);
            Cursor.lockState = looking ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !looking;
            if (looking)
            {
                float sens = Gm.Settings.mouseSensitivity;
                yaw += SafeInput.Axis("Mouse X") * sens;
                pitch = Mathf.Clamp(pitch - SafeInput.Axis("Mouse Y") * sens, -89f, 89f);
            }
            cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            if (!typing)
            {
                Vector3 move = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) move += cam.transform.forward;
                if (Input.GetKey(KeyCode.S)) move -= cam.transform.forward;
                if (Input.GetKey(KeyCode.D)) move += cam.transform.right;
                if (Input.GetKey(KeyCode.A)) move -= cam.transform.right;
                if (Input.GetKey(KeyCode.E)) move += Vector3.up;
                if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;
                float speed = Input.GetKey(KeyCode.LeftShift) ? 40f : 15f;
                cam.transform.position += move * speed * dt;

                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand);
                if (ctrl && Input.GetKeyDown(KeyCode.Z)) Undo();
                if (ctrl && Input.GetKeyDown(KeyCode.Y)) Redo();
                if (ctrl && Input.GetKeyDown(KeyCode.S)) Save();
                if (Input.GetKeyDown(KeyCode.F)) FrameMap();
                if (Input.GetKeyDown(KeyCode.G)) showGrid = !showGrid;
                if (Input.GetKeyDown(KeyCode.X) && !ctrl) brushIndex = brushes.Count - 1;
                if (Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.RightBracket)) layer++;
                if (Input.GetKeyDown(KeyCode.PageDown) || Input.GetKeyDown(KeyCode.LeftBracket)) layer--;
                var vis = VisibleBrushes();
                for (int i = 0; i < 9; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i < vis.Count) brushIndex = vis[i];
                if (Input.GetKeyDown(KeyCode.Tab)) { category = (category + 1) % Categories.Length; brushIndex = VisibleBrushes()[0]; }
                if (Input.GetKeyDown(KeyCode.Delete) && selectedMech != null) { PushUndo(); Map.RemoveMech(selectedMech); selectedMech = null; }
            }
            if (!overUi)
            {
                float scroll = Input.mouseScrollDelta.y;
                if (scroll > 0.1f) layer++;
                else if (scroll < -0.1f) layer--;
            }
            layer = Mathf.Clamp(layer, 0, Data.sizeY - 1);

            UpdateHover(overUi || looking);
            UpdatePainting(overUi || looking);
            UpdateGhost();
        }

        void UpdateHover(bool blocked)
        {
            hasCell = false;
            hoverBlock = null;
            hoverSpawn = null;
            hoverMech = null;
            hoverProp = null;
            if (blocked) return;
            var ray = cam.ScreenPointToRay(Input.mousePosition);

            if (stroking && Current.Kind == BrushKind.Block)
            {
                // Paint along a flat plane during a stroke so painting doesn't climb toward the camera.
                var plane = new Plane(Vector3.up, new Vector3(0f, strokeY, 0f));
                if (plane.Raycast(ray, out float enter))
                {
                    var p = ray.GetPoint(enter);
                    cell = new Vector3Int(Mathf.FloorToInt(p.x), strokeY, Mathf.FloorToInt(p.z));
                    hasCell = InBounds(cell);
                }
                return;
            }

            if (Physics.Raycast(ray, out var hit, 1000f, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                hoverBlock = hit.collider.GetComponent<BlockInfo>();
                hoverSpawn = hit.collider.GetComponent<SpawnMarker>();
                hoverMech = hit.collider.GetComponentInParent<Mechanism>();
                hoverProp = hit.collider.GetComponentInParent<Prop>();
                if (hoverBlock != null || hoverSpawn != null || hoverMech != null || hoverProp != null)
                {
                    var p = hit.point + hit.normal * 0.5f;
                    cell = new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
                    hasCell = InBounds(cell);
                    return;
                }
            }
            var layerPlane = new Plane(Vector3.up, new Vector3(0f, layer, 0f));
            if (layerPlane.Raycast(ray, out float e))
            {
                var p = ray.GetPoint(e);
                cell = new Vector3Int(Mathf.FloorToInt(p.x), layer, Mathf.FloorToInt(p.z));
                hasCell = InBounds(cell);
            }
        }

        bool InBounds(Vector3Int c) =>
            c.x >= 0 && c.y >= 0 && c.z >= 0 && c.x < Data.sizeX && c.y < Data.sizeY && c.z < Data.sizeZ;

        void UpdatePainting(bool blocked)
        {
            bool down = Input.GetMouseButtonDown(0) && !blocked;
            bool held = Input.GetMouseButton(0);
            if (!held && stroking)
            {
                stroking = false;
                strokeCells.Clear();
            }
            if (blocked && !stroking) return;

            var brush = Current;
            if (down)
            {
                if (brush.Kind == BrushKind.Erase && hoverBlock == null && hoverSpawn == null && hoverMech == null && hoverProp == null) return;
                if (brush.Kind == BrushKind.Mech && hoverMech != null)
                {
                    selectedMech = hoverMech; // click an existing part to edit it
                    Status("Selected " + MechKinds.Name(selectedMech.Data.kind));
                    return;
                }
                if (brush.Kind != BrushKind.Erase && !hasCell) return;
                PushUndo();
                stroking = true;
                strokeCells.Clear();
                strokeY = cell.y;
            }
            if (!stroking) return;

            switch (brush.Kind)
            {
                case BrushKind.Block:
                    if (hasCell && strokeCells.Add(cell))
                        foreach (var c in Mirrored(cell)) PlaceBlock(c, brush.BlockType);
                    break;
                case BrushKind.Spawn:
                    if (down && hasCell)
                    {
                        PlaceSpawn(cell, brush.Team);
                        if (mirrorX || mirrorZ)
                        {
                            var mirrored = Mirrored(cell).Skip(1).ToList();
                            int i = 0;
                            foreach (var c in mirrored) PlaceSpawn(c, MirrorTeam(brush.Team, i++));
                        }
                        stroking = false;
                    }
                    break;
                case BrushKind.Mech:
                    if (down && hasCell)
                    {
                        var data = MechKinds.Preset(brush.Id, cell.x, cell.y, cell.z);
                        if (brush.Id == MechKinds.Pendulum) data.y = Mathf.Min(Data.sizeY - 1, cell.y + Mathf.CeilToInt(data.length) + 1);
                        Data.mechs.Add(data);
                        selectedMech = Map.CreateMech(data, Data.mechs.Count - 1);
                        foreach (var m in MirroredMechs(data))
                        {
                            Data.mechs.Add(m);
                            Map.CreateMech(m, Data.mechs.Count - 1);
                        }
                        Status("Placed " + MechKinds.Name(brush.Id) + " (adjust it in the panel on the right)");
                        stroking = false;
                    }
                    break;
                case BrushKind.Prop:
                    if (hasCell && strokeCells.Add(cell))
                        foreach (var c in Mirrored(cell))
                        {
                            if (Data.props.Count >= 400) break;
                            var pd = new PropData(brush.Id, c.x + 0.5f, c.y, c.z + 0.5f);
                            Data.props.Add(pd);
                            Map.CreateProp(pd, Data.props.Count - 1, true);
                        }
                    break;
                case BrushKind.Erase:
                    if (hoverMech != null)
                    {
                        if (hoverMech == selectedMech) selectedMech = null;
                        Map.RemoveMech(hoverMech);
                        hoverMech = null;
                    }
                    else if (hoverProp != null)
                    {
                        Map.RemoveProp(hoverProp);
                        hoverProp = null;
                    }
                    else if (hoverBlock != null)
                    {
                        if (hoverBlock.Data != null && (mirrorX || mirrorZ))
                        {
                            var b = hoverBlock.Data;
                            foreach (var c in Mirrored(new Vector3Int(b.x, b.y, b.z)).Skip(1))
                            {
                                var other = Map.Blocks.FirstOrDefault(x => x.Data != null && x.Data != b && x.Data.Contains(c.x, c.y, c.z));
                                if (other != null) Map.RemoveBlock(other);
                            }
                        }
                        Map.RemoveBlock(hoverBlock);
                        hoverBlock = null;
                    }
                    else if (hoverSpawn != null)
                    {
                        Map.RemoveSpawn(hoverSpawn);
                        hoverSpawn = null;
                    }
                    break;
            }
        }

        /// <summary>Copies of a moving part for the active mirror axes (travel is mirrored too).</summary>
        IEnumerable<MechData> MirroredMechs(MechData m)
        {
            var list = new List<MechData>();
            bool point = m.kind == MechKinds.Pendulum;
            int mx = point ? Data.sizeX - 1 - m.x : Data.sizeX - m.x - m.w;
            int mz = point ? Data.sizeZ - 1 - m.z : Data.sizeZ - m.z - m.d;
            if (mirrorX && mx != m.x) { var c = m.Clone(); c.x = mx; c.dx = -c.dx; if (c.axis != 0) c.period = -c.period; list.Add(c); }
            if (mirrorZ && mz != m.z) { var c = m.Clone(); c.z = mz; c.dz = -c.dz; if (c.axis != 2) c.period = -c.period; list.Add(c); }
            if (mirrorX && mirrorZ && mx != m.x && mz != m.z) { var c = m.Clone(); c.x = mx; c.z = mz; c.dx = -c.dx; c.dz = -c.dz; list.Add(c); }
            return list;
        }

        static int MirrorTeam(int team, int index)
        {
            if (team < 0) return team;
            // First mirror gets the "opposing" team, the rest cycle through the others.
            return index == 0 ? team ^ 1 : (team + index + 1) % Teams.Max;
        }

        IEnumerable<Vector3Int> Mirrored(Vector3Int c)
        {
            var list = new List<Vector3Int> { c };
            if (mirrorX) list.Add(new Vector3Int(Data.sizeX - 1 - c.x, c.y, c.z));
            if (mirrorZ) list.Add(new Vector3Int(c.x, c.y, Data.sizeZ - 1 - c.z));
            if (mirrorX && mirrorZ) list.Add(new Vector3Int(Data.sizeX - 1 - c.x, c.y, Data.sizeZ - 1 - c.z));
            return list.Distinct();
        }

        void PlaceBlock(Vector3Int c, string type)
        {
            if (!InBounds(c) || Data.BlockAt(c.x, c.y, c.z) != null) return;
            var spawn = Data.spawns.FirstOrDefault(s => s.x == c.x && s.y == c.y && s.z == c.z);
            if (spawn != null) return;
            var b = new BlockData(c.x, c.y, c.z, 1, 1, 1, type);
            Data.blocks.Add(b);
            Map.CreateBlock(b);
        }

        void PlaceSpawn(Vector3Int c, int team)
        {
            if (!InBounds(c) || Data.BlockAt(c.x, c.y, c.z) != null) return;
            foreach (var m in Map.GetComponentsInChildren<SpawnMarker>())
                if (m.Data.x == c.x && m.Data.y == c.y && m.Data.z == c.z) Map.RemoveSpawn(m);
            var s = new SpawnData(c.x, c.y, c.z, team);
            Data.spawns.Add(s);
            Map.CreateSpawnMarker(s);
        }

        void UpdateGhost()
        {
            if (ghost == null) return;
            var brush = Current;
            if (brush.Kind == BrushKind.Erase)
            {
                Transform t = hoverMech != null ? hoverMech.transform : hoverProp != null ? hoverProp.transform : hoverBlock != null ? hoverBlock.transform : hoverSpawn != null ? hoverSpawn.transform : null;
                ghost.SetActive(t != null);
                if (t != null)
                {
                    ghost.transform.position = t.position;
                    ghost.transform.rotation = t.rotation;
                    ghost.transform.localScale = hoverMech != null ? hoverMech.Data.Size * 1.04f : hoverProp != null ? hoverProp.Kind.Size * 1.06f : t.lossyScale * 1.04f;
                    ghostMat.color = new Color(1f, 0.25f, 0.2f, 0.45f);
                }
                return;
            }
            ghost.SetActive(hasCell);
            if (!hasCell) return;
            ghost.transform.rotation = Quaternion.identity;
            bool spawn = brush.Kind == BrushKind.Spawn;
            ghost.transform.localScale = spawn ? new Vector3(0.8f, 1f, 0.8f) : Vector3.one * 1.02f;
            ghost.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f);
            if (brush.Kind == BrushKind.Mech)
            {
                var preset = MechKinds.Preset(brush.Id, cell.x, cell.y, cell.z);
                if (brush.Id != MechKinds.Pendulum && brush.Id != MechKinds.Spikes)
                {
                    ghost.transform.localScale = preset.Size * 1.02f;
                    ghost.transform.position = preset.Center;
                }
            }
            else if (brush.Kind == BrushKind.Prop)
            {
                var k = PropKinds.Get(brush.Id);
                ghost.transform.localScale = k.Size;
                ghost.transform.position = new Vector3(cell.x + 0.5f, cell.y + k.Size.y * 0.5f, cell.z + 0.5f);
            }
            var c = brush.Color;
            ghostMat.color = new Color(c.r, c.g, c.b, 0.45f);
        }

        // ------------------------------------------------------------------ grid rendering

        void OnRenderObject()
        {
            if (!IsOpen || hidden || cam == null || Camera.current != cam || Data == null) return;
            var mat = Mats.Lines;
            mat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            int sx = Data.sizeX, sy = Data.sizeY, sz = Data.sizeZ;
            if (showGrid)
            {
                GL.Color(new Color(1f, 1f, 1f, 0.09f));
                float y = layer + 0.001f;
                for (int x = 0; x <= sx; x++)
                {
                    GL.Vertex3(x, y, 0f);
                    GL.Vertex3(x, y, sz);
                }
                for (int z = 0; z <= sz; z++)
                {
                    GL.Vertex3(0f, y, z);
                    GL.Vertex3(sx, y, z);
                }
                GL.Color(new Color(0.4f, 0.8f, 1f, 0.35f));
                GL.Vertex3(sx * 0.5f, y, 0f); GL.Vertex3(sx * 0.5f, y, sz);
                GL.Vertex3(0f, y, sz * 0.5f); GL.Vertex3(sx, y, sz * 0.5f);
            }
            // Arena (border) box.
            GL.Color(new Color(1f, 0.35f, 0.3f, 0.6f));
            Vector3[] p =
            {
                new Vector3(0, 0, 0), new Vector3(sx, 0, 0), new Vector3(sx, 0, sz), new Vector3(0, 0, sz),
                new Vector3(0, sy, 0), new Vector3(sx, sy, 0), new Vector3(sx, sy, sz), new Vector3(0, sy, sz),
            };
            int[] edges = { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 };
            for (int i = 0; i < edges.Length; i++) GL.Vertex(p[edges[i]]);
            GL.End();
            GL.PopMatrix();
        }

        // ------------------------------------------------------------------ actions

        void Save()
        {
            if (string.IsNullOrWhiteSpace(Data.name)) Data.name = "My Map";
            try
            {
                string id = MapRegistry.SaveUserMap(Data);
                Profile.Current.Unlock("architect");
                Status("Saved " + MapRegistry.Get(id)?.FilePath);
            }
            catch (System.Exception e)
            {
                Status("Save failed: " + e.Message);
                Debug.LogException(e);
            }
        }

        void TestPlay()
        {
            string err = Gm.Settings.CheckPlayable();
            if (err != null)
            {
                Status("Can't test: " + err + " (set up players under Play)");
                return;
            }
            Gm.StartMatch(Data.Clone());
        }

        // ------------------------------------------------------------------ GUI

        public void DrawGUI()
        {
            if (!IsOpen || Data == null) return;
            var s = UIKit.Size;
            panelRect = new Rect(0f, 0f, 320f, s.y);
            UIKit.Fill(panelRect, new Color(0.06f, 0.07f, 0.1f, 0.9f));
            float x = 14f, w = 292f, y = 12f;
            UIKit.Text(new Rect(x, y, w, 32f), "Map Editor", UIKit.Header, Color.white);
            y += 38f;

            UIKit.Text(new Rect(x, y, 70f, 30f), "Name", UIKit.Small, UIKit.Muted);
            Data.name = GUI.TextField(new Rect(x + 70f, y, w - 70f, 30f), Data.name ?? "", 40, UIKit.Field);
            y += 34f;
            UIKit.Text(new Rect(x, y, 70f, 30f), "Author", UIKit.Small, UIKit.Muted);
            Data.author = GUI.TextField(new Rect(x + 70f, y, w - 70f, 30f), Data.author ?? "", 40, UIKit.Field);
            y += 40f;

            int nx = UIKit.StepperInt(new Rect(x, y, w, 30f), "Width", Data.sizeX, 8, 128);
            y += 32f;
            int ny = UIKit.StepperInt(new Rect(x, y, w, 30f), "Height", Data.sizeY, 8, 96);
            y += 32f;
            int nz = UIKit.StepperInt(new Rect(x, y, w, 30f), "Depth", Data.sizeZ, 8, 128);
            y += 36f;
            if (nx != Data.sizeX || ny != Data.sizeY || nz != Data.sizeZ)
            {
                PushUndo();
                Map.ResizeArena(nx, ny, nz);
                int removed = Data.blocks.RemoveAll(b => b.x + b.W > Data.sizeX || b.y + b.H > Data.sizeY || b.z + b.D > Data.sizeZ)
                            + Data.spawns.RemoveAll(sp => sp.x >= Data.sizeX || sp.y >= Data.sizeY || sp.z >= Data.sizeZ);
                if (removed > 0)
                {
                    Rebuild(Data);
                    Status("Removed " + removed + " blocks/spawns outside the new arena (Ctrl+Z to undo)");
                }
            }

            UIKit.Text(new Rect(x, y, w, 24f), "Brush  (Tab = next tab, 1-9, X = eraser)", UIKit.Small, UIKit.Muted);
            y += 24f;
            float tw = (w - 3f * 4f) / 4f;
            for (int i = 0; i < Categories.Length; i++)
                if (UIKit.Toggle(new Rect(x + i * (tw + 4f), y, tw, 28f), Categories[i], i == category, true))
                {
                    category = i;
                    brushIndex = VisibleBrushes()[0];
                }
            y += 34f;
            float bw = (w - 6f) / 2f;
            var visible = VisibleBrushes();
            for (int n = 0; n < visible.Count; n++)
            {
                int i = visible[n];
                var b = brushes[i];
                var r = new Rect(x + (n % 2) * (bw + 6f), y + (n / 2) * 32f, bw, 28f);
                if (UIKit.Toggle(r, "     " + b.Label, i == brushIndex, true)) brushIndex = i;
                UIKit.Fill(new Rect(r.x + 6f, r.y + 7f, 14f, 14f), new Color(b.Color.r, b.Color.g, b.Color.b, 1f));
            }
            y += Mathf.CeilToInt(visible.Count / 2f) * 32f + 8f;

            layer = UIKit.StepperInt(new Rect(x, y, w, 30f), "Layer (wheel)", layer, 0, Data.sizeY - 1);
            y += 34f;
            if (UIKit.Toggle(new Rect(x, y, (w - 12f) / 3f, 30f), "Mirror X", mirrorX, true)) mirrorX = !mirrorX;
            if (UIKit.Toggle(new Rect(x + (w - 12f) / 3f + 6f, y, (w - 12f) / 3f, 30f), "Mirror Z", mirrorZ, true)) mirrorZ = !mirrorZ;
            if (UIKit.Toggle(new Rect(x + ((w - 12f) / 3f + 6f) * 2f, y, (w - 12f) / 3f, 30f), "Grid", showGrid, true)) showGrid = !showGrid;
            y += 38f;

            UIKit.Text(new Rect(x, y, w, 20f), Data.blocks.Count + " blocks, " + Data.mechs.Count + " moving, " + Data.props.Count + " props, " + Data.spawns.Count + " spawns", UIKit.Small, UIKit.Muted);
            y += 26f;

            float hw = (w - 6f) / 2f;
            if (UIKit.Btn(new Rect(x, y, hw, 34f), "New", true)) { PushUndo(); Rebuild(NewMap()); Status("New map"); }
            if (UIKit.Btn(new Rect(x + hw + 6f, y, hw, 34f), "Load...", true)) { showLoad = !showLoad; MapRegistry.RefreshUserMaps(); }
            y += 40f;
            if (UIKit.Btn(new Rect(x, y, hw, 34f), "Save", true)) Save();
            if (UIKit.Btn(new Rect(x + hw + 6f, y, hw, 34f), "Undo", true)) Undo();
            y += 40f;
            if (UIKit.Btn(new Rect(x, y, w, 40f), "<b>Test Play</b>")) TestPlay();
            y += 46f;
            if (UIKit.Btn(new Rect(x, y, w, 34f), "Exit to Menu", true)) { Gm.ReturnToMenu(); return; }
            y += 44f;

            const string help =
                "Hold RMB + WASD/QE to fly (Shift = fast)\n" +
                "LMB place / drag to paint\n" +
                "Wheel or [ ] = layer, F = frame map\n" +
                "Ctrl+Z / Ctrl+Y undo/redo, Ctrl+S save\n" +
                "The red box is the arena: its faces are the damaging borders, the bottom is the fall line.";
            UIKit.Text(new Rect(x, y, w, s.y - y - 40f), help, UIKit.Wrap, UIKit.Muted);

            // Hover / status
            string info = hasCell ? "Cell " + cell.x + ", " + cell.y + ", " + cell.z : "";
            if (hoverBlock != null && hoverBlock.Type != null) info += "   " + hoverBlock.Type.Name;
            if (hoverSpawn != null) info += "   Spawn (" + (hoverSpawn.Data.team >= 0 ? Teams.GetName(hoverSpawn.Data.team) : "any") + ")";
            if (hoverMech != null) info += "   " + MechKinds.Name(hoverMech.Data.kind) + " (click with a Moving brush to edit)";
            if (hoverProp != null) info += "   " + hoverProp.Kind.Name;
            UIKit.Shadowed(new Rect(340f, s.y - 34f, s.x - 360f, 24f), info, UIKit.Small, Color.white);
            if (Time.unscaledTime - statusTime < 6f)
                UIKit.Shadowed(new Rect(340f, 12f, s.x - 360f, 26f), status, UIKit.Label, UIKit.Accent);

            if (selectedMech != null) DrawMechPanel();
            if (showLoad) DrawLoad();
        }

        static readonly string[] AxisNames = { "X", "Y (up)", "Z" };

        void DrawMechPanel()
        {
            var s = UIKit.Size;
            var m = selectedMech.Data;
            float w = 300f;
            mechRect = new Rect(s.x - w - 12f, 12f, w, Mathf.Min(s.y - 24f, 640f));
            UIKit.Fill(mechRect, new Color(0.06f, 0.07f, 0.1f, 0.92f));
            float x = mechRect.x + 10f, y = mechRect.y + 8f, iw = w - 20f;
            UIKit.Text(new Rect(x, y, iw - 40f, 28f), MechKinds.Name(m.kind), UIKit.Label, UIKit.Accent);
            if (UIKit.Btn(new Rect(mechRect.xMax - 40f, y, 30f, 26f), "X", true)) { selectedMech = null; return; }
            y += 32f;
            var before = JsonUtility.ToJson(m);
            string snapshot = Data.ToJson(false);
            const float rh = 28f, gap = 30f;
            bool point = m.kind == MechKinds.Pendulum;
            if (!point)
            {
                m.w = UIKit.StepperInt(new Rect(x, y, iw, rh), "Width", m.w, 1, 40); y += gap;
                if (m.kind != MechKinds.Spikes && m.kind != MechKinds.Saw) { m.h = UIKit.StepperInt(new Rect(x, y, iw, rh), "Height", m.h, 1, 40); y += gap; }
                m.d = UIKit.StepperInt(new Rect(x, y, iw, rh), "Depth", m.d, 1, 40); y += gap;
            }
            else
            {
                m.w = UIKit.StepperInt(new Rect(x, y, iw, rh), "Ball size", m.w, 1, 6); y += gap;
                m.length = UIKit.Stepper(new Rect(x, y, iw, rh), "Arm length", m.length, 1f, 1f, 30f); y += gap;
                m.angle = UIKit.Stepper(new Rect(x, y, iw, rh), "Swing", m.angle, 5f, 5f, 175f, "0", "°"); y += gap;
            }
            bool moves = m.kind == MechKinds.Door || m.kind == MechKinds.Platform || m.kind == MechKinds.Elevator || m.kind == MechKinds.Piston || m.kind == MechKinds.Saw;
            if (moves)
            {
                m.dx = UIKit.Stepper(new Rect(x, y, iw, rh), "Travel X", m.dx, 1f, -40f, 40f); y += gap;
                m.dy = UIKit.Stepper(new Rect(x, y, iw, rh), "Travel Y", m.dy, 1f, -40f, 40f); y += gap;
                m.dz = UIKit.Stepper(new Rect(x, y, iw, rh), "Travel Z", m.dz, 1f, -40f, 40f); y += gap;
            }
            if (m.kind == MechKinds.Spinner || m.kind == MechKinds.Pendulum || m.kind == MechKinds.Saw)
            {
                m.axis = UIKit.Cycle(new Rect(x, y, iw, rh), "Axis", m.axis, AxisNames); y += gap;
            }
            m.period = UIKit.Stepper(new Rect(x, y, iw, rh), "Cycle time", m.period, 0.5f, -30f, 30f, "0.0", "s"); y += gap;
            if (Mathf.Abs(m.period) < 0.5f) m.period = 0.5f;
            m.phase = UIKit.Stepper(new Rect(x, y, iw, rh), "Offset", m.phase, 0.05f, 0f, 0.95f, "0.00"); y += gap;
            m.deadly = UIKit.CheckRow(new Rect(x, y, iw, rh), "Deadly (hurts on touch)", m.deadly); y += gap;
            if (m.deadly) { m.damage = UIKit.Stepper(new Rect(x, y, iw, rh), "Damage", m.damage, 5f, 5f, 200f); y += gap; }
            if (m.kind != MechKinds.Pendulum && m.kind != MechKinds.Saw && m.kind != MechKinds.Spikes)
            {
                var types = BlockTypes.All.Where(t => !t.Transparent).ToList();
                int ti = Mathf.Max(0, types.FindIndex(t => t.Id == m.type));
                ti = UIKit.Cycle(new Rect(x, y, iw, rh), "Look", ti, types.Select(t => t.Name).ToArray());
                m.type = types[Mathf.Clamp(ti, 0, types.Count - 1)].Id;
                y += gap;
            }
            y += 4f;
            if (UIKit.Toggle(new Rect(x, y, iw, rh), "Preview motion", Map.PreviewMotion, true)) Map.PreviewMotion = !Map.PreviewMotion;
            y += gap + 4f;
            if (UIKit.Btn(new Rect(x, y, iw, 30f), "Delete part (Del)", true))
            {
                PushUndo();
                Map.RemoveMech(selectedMech);
                selectedMech = null;
                return;
            }
            mechRect.height = y + 40f - mechRect.y;
            if (JsonUtility.ToJson(m) != before)
            {
                undo.Push(snapshot);
                redo.Clear();
                selectedMech = Map.RebuildMech(selectedMech);
            }
        }

        void DrawLoad()
        {
            var s = UIKit.Size;
            loadRect = new Rect(340f, 60f, Mathf.Min(560f, s.x - 360f), Mathf.Min(560f, s.y - 120f));
            UIKit.Fill(loadRect, UIKit.Panel);
            UIKit.Text(new Rect(loadRect.x + 14f, loadRect.y + 8f, 300f, 30f), "Load map", UIKit.Header, Color.white);
            if (UIKit.Btn(new Rect(loadRect.xMax - 44f, loadRect.y + 8f, 34f, 30f), "X", true)) showLoad = false;
            var view = new Rect(loadRect.x + 8f, loadRect.y + 46f, loadRect.width - 16f, loadRect.height - 54f);
            var maps = MapRegistry.All.ToList();
            var content = new Rect(0f, 0f, view.width - 20f, maps.Count * 40f);
            loadScroll = GUI.BeginScrollView(view, loadScroll, content);
            for (int i = 0; i < maps.Count; i++)
            {
                var m = maps[i];
                var r = new Rect(0f, i * 40f, content.width, 36f);
                UIKit.Fill(r, new Color(1f, 1f, 1f, 0.04f));
                UIKit.Text(new Rect(r.x + 8f, r.y, r.width - 200f, r.height), m.Data.name + "  <color=#8890a8>" + m.Source + "</color>", UIKit.Label, Color.white);
                if (UIKit.Btn(new Rect(r.xMax - (m.IsUserMap ? 180f : 90f), r.y + 4f, 84f, 28f), "Edit", true))
                {
                    PushUndo();
                    Rebuild(m.Data.Clone());
                    if (!m.IsUserMap) Data.name = m.Data.name + " (copy)";
                    showLoad = false;
                    Status("Loaded " + m.Data.name);
                }
                if (m.IsUserMap && UIKit.Btn(new Rect(r.xMax - 90f, r.y + 4f, 84f, 28f), "Delete", true))
                {
                    MapRegistry.DeleteUserMap(m.Id);
                    Status("Deleted " + m.Data.name);
                }
            }
            GUI.EndScrollView();
        }
    }
}
