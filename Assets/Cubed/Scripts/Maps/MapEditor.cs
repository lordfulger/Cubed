using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cubed
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

        enum BrushKind { Block, Spawn, Erase }

        struct Brush
        {
            public BrushKind Kind;
            public string BlockType;
            public int Team;
            public string Label;
            public Color Color;
        }

        readonly List<Brush> brushes = new List<Brush>();
        int brushIndex;
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
            brushes.Add(new Brush { Kind = BrushKind.Erase, Label = "Eraser", Color = UIKit.Bad });
            brushIndex = Mathf.Clamp(brushIndex, 0, brushes.Count - 1);
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
            CubedPostFX.Attach(cam);
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
            bool overUi = panelRect.Contains(mouseGui) || (showLoad && loadRect.Contains(mouseGui));
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
                for (int i = 0; i < 9; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i < brushes.Count) brushIndex = i;
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
                if (hoverBlock != null || hoverSpawn != null)
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
                if (brush.Kind == BrushKind.Erase && hoverBlock == null && hoverSpawn == null) return;
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
                case BrushKind.Erase:
                    if (hoverBlock != null)
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
                Transform t = hoverBlock != null ? hoverBlock.transform : hoverSpawn != null ? hoverSpawn.transform : null;
                ghost.SetActive(t != null);
                if (t != null)
                {
                    ghost.transform.position = t.position;
                    ghost.transform.rotation = t.rotation;
                    ghost.transform.localScale = t.lossyScale * 1.04f;
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

            UIKit.Text(new Rect(x, y, w, 24f), "Brush  (1-9, X = eraser)", UIKit.Small, UIKit.Muted);
            y += 24f;
            float bw = (w - 6f) / 2f;
            for (int i = 0; i < brushes.Count; i++)
            {
                var b = brushes[i];
                var r = new Rect(x + (i % 2) * (bw + 6f), y + (i / 2) * 32f, bw, 28f);
                if (UIKit.Toggle(r, "     " + b.Label, i == brushIndex, true)) brushIndex = i;
                UIKit.Fill(new Rect(r.x + 6f, r.y + 7f, 14f, 14f), new Color(b.Color.r, b.Color.g, b.Color.b, 1f));
            }
            y += Mathf.CeilToInt(brushes.Count / 2f) * 32f + 8f;

            layer = UIKit.StepperInt(new Rect(x, y, w, 30f), "Layer (wheel)", layer, 0, Data.sizeY - 1);
            y += 34f;
            if (UIKit.Toggle(new Rect(x, y, (w - 12f) / 3f, 30f), "Mirror X", mirrorX, true)) mirrorX = !mirrorX;
            if (UIKit.Toggle(new Rect(x + (w - 12f) / 3f + 6f, y, (w - 12f) / 3f, 30f), "Mirror Z", mirrorZ, true)) mirrorZ = !mirrorZ;
            if (UIKit.Toggle(new Rect(x + ((w - 12f) / 3f + 6f) * 2f, y, (w - 12f) / 3f, 30f), "Grid", showGrid, true)) showGrid = !showGrid;
            y += 38f;

            UIKit.Text(new Rect(x, y, w, 20f), Data.blocks.Count + " blocks, " + Data.spawns.Count + " spawns", UIKit.Small, UIKit.Muted);
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
            UIKit.Shadowed(new Rect(340f, s.y - 34f, s.x - 360f, 24f), info, UIKit.Small, Color.white);
            if (Time.unscaledTime - statusTime < 6f)
                UIKit.Shadowed(new Rect(340f, 12f, s.x - 360f, 26f), status, UIKit.Label, UIKit.Accent);

            if (showLoad) DrawLoad();
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
