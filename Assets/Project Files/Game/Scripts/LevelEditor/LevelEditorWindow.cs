#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter.Editor
{
    public class LevelEditorWindow : EditorWindow
    {
        // ── Layout ────────────────────────────────────────────────────────────
        private const float ListW = 175f;
        private const float RightW = 200f;
        private const float CellSize = 66f;
        private const float CellGap  = 4f;
        private const int   MaxCols  = 7;
        private const int   MaxRows  = 6;

        // ── Config ────────────────────────────────────────────────────────────
        private LevelEditorConfig _cfg;

        // ── Level list ────────────────────────────────────────────────────────
        private List<string> _levelPaths  = new();
        private List<string> _levelLabels = new();
        private int          _activeIndex = -1;   // index in _levelPaths

        // ── Working state ─────────────────────────────────────────────────────
        private int           _gridCols   = 4;
        private int           _gridRows   = 2;
        private int           _levelIndex = 1;
        private string        _levelName  = "Level 1";
        private LevelGoalType _goalType   = LevelGoalType.ClearAllBlocks;
        private int           _goalAmount = 0;

        // [col, row]
        private GridCellType[,]   _type;
        private BlockColorType[,] _color;
        private int[,]            _shots;   // -1 = default
        private int[,]            _doors;   // door block count

        private List<LevelConveyorGroup> _groups = new();

        // ── Selection ─────────────────────────────────────────────────────────
        private int _selC = -1, _selR = -1;

        // ── Scroll ────────────────────────────────────────────────────────────
        private Vector2 _listScroll;
        private Vector2 _midScroll;

        // ── Color palette ─────────────────────────────────────────────────────
        private static readonly (BlockColorType type, Color color, string label)[] Colors =
        {
            (BlockColorType.Red,    new Color(.90f,.20f,.20f), "Red"   ),
            (BlockColorType.Blue,   new Color(.20f,.50f,.90f), "Blue"  ),
            (BlockColorType.Green,  new Color(.20f,.80f,.30f), "Green" ),
            (BlockColorType.Yellow, new Color(1.00f,.85f,.10f),"Yellow"),
            (BlockColorType.Purple, new Color(.60f,.20f,.90f), "Purple"),
            (BlockColorType.Orange, new Color(1.00f,.55f,.10f),"Orange"),
        };

        private static Color GetColor(BlockColorType t) =>
            Colors.FirstOrDefault(x => x.type == t).color;

        // ── Menu ──────────────────────────────────────────────────────────────
        [MenuItem("BlockShooter/Level Editor", false, 10)]
        public static void Open()
        {
            var w = GetWindow<LevelEditorWindow>("Level Editor");
            w.minSize = new Vector2(780, 520);
            w.Show();
        }

        private void OnEnable() { LoadConfig(); Refresh(); }

        private void LoadConfig()
        {
            var g = AssetDatabase.FindAssets("t:LevelEditorConfig");
            _cfg = g.Length > 0
                ? AssetDatabase.LoadAssetAtPath<LevelEditorConfig>(AssetDatabase.GUIDToAssetPath(g[0]))
                : null;
        }

        private void Refresh()
        {
            _levelPaths.Clear(); _levelLabels.Clear();
            if (_cfg == null) return;

            string folder = _cfg.levelSavePath.TrimEnd('/');
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            foreach (var gid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(gid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponent<LevelRoot>() == null) continue;
                _levelPaths.Add(path);
                _levelLabels.Add(Path.GetFileNameWithoutExtension(path));
            }

            var sorted = _levelPaths.Zip(_levelLabels, (p, l) => (p, l))
                                    .OrderBy(x => x.l).ToList();
            _levelPaths  = sorted.Select(x => x.p).ToList();
            _levelLabels = sorted.Select(x => x.l).ToList();
        }

        // ── Main GUI ──────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (_cfg == null) { DrawMissingConfig(); return; }
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawLeft();
            Divider();
            DrawCenter();
            Divider();
            DrawRight();
            EditorGUILayout.EndHorizontal();
        }

        // ── Missing config ────────────────────────────────────────────────────
        private void DrawMissingConfig()
        {
            GUILayout.Space(30);
            EditorGUILayout.HelpBox(
                "LevelEditorConfig bulunamadı.\n\nOluşturmak için:\nAssets → Create → BlockShooter → Level Editor Config",
                MessageType.Warning);
            if (GUILayout.Button("Yenile", GUILayout.Height(28)))
            { LoadConfig(); Refresh(); }
        }

        // ── Toolbar ───────────────────────────────────────────────────────────
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("  BLOCK SHOOTER — LEVEL EDITOR", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Config", EditorStyles.toolbarButton, GUILayout.Width(50)))
                Selection.activeObject = _cfg;
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(55)))
            { LoadConfig(); Refresh(); }

            GUILayout.Space(8);
            GUI.backgroundColor = new Color(.3f, .85f, .45f);
            if (GUILayout.Button("  ▶  SAVE PREFAB  ", EditorStyles.toolbarButton, GUILayout.Height(18)))
                SavePrefab();
            GUI.backgroundColor = Color.white;
            GUILayout.Space(4);
            EditorGUILayout.EndHorizontal();
        }

        // ── LEFT panel ────────────────────────────────────────────────────────
        private void DrawLeft()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListW), GUILayout.ExpandHeight(true));
            Header("LEVELS");

            GUI.backgroundColor = new Color(.45f, .85f, .5f);
            if (GUILayout.Button("+ New Level", GUILayout.Height(24)))
                NewLevel();
            GUI.backgroundColor = Color.white;
            GUILayout.Space(3);

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _levelLabels.Count; i++)
            {
                bool active = _activeIndex == i;
                GUI.backgroundColor = active ? new Color(.4f, .65f, 1f) : new Color(.25f,.25f,.25f);
                if (GUILayout.Button(_levelLabels[i], GUILayout.Height(22)))
                { _activeIndex = i; LoadLevel(i); }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndScrollView();

            GUILayout.Space(6);
            Header("SETTINGS");
            _levelIndex = EditorGUILayout.IntField("Index", _levelIndex);
            _levelName  = EditorGUILayout.TextField("Name",  _levelName);
            _goalType   = (LevelGoalType)EditorGUILayout.EnumPopup("Goal", _goalType);
            if (_goalType != LevelGoalType.ClearAllBlocks)
                _goalAmount = EditorGUILayout.IntField("Amount", _goalAmount);

            EditorGUILayout.EndVertical();
        }

        // ── CENTER panel ──────────────────────────────────────────────────────
        private void DrawCenter()
        {
            _midScroll = EditorGUILayout.BeginScrollView(_midScroll,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            Header("SHOOTER GRID");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Columns", GUILayout.Width(55));
            int nc = EditorGUILayout.IntSlider(_gridCols, 1, MaxCols);
            GUILayout.Label("Rows", GUILayout.Width(36));
            int nr = EditorGUILayout.IntSlider(_gridRows, 1, MaxRows);
            EditorGUILayout.EndHorizontal();
            if (nc != _gridCols || nr != _gridRows) { _gridCols = nc; _gridRows = nr; ResizeGrid(); }

            GUILayout.Space(6);
            DrawGrid();
            GUILayout.Space(4);
            EditorGUILayout.LabelField("  LMB = Empty → Block → Door   |   Click = select cell",
                EditorStyles.miniLabel);

            Header("CONVEYOR GROUPS");
            DrawGroups();

            EditorGUILayout.EndScrollView();
        }

        // ── Grid ──────────────────────────────────────────────────────────────
        private void DrawGrid()
        {
            if (_type == null) InitGrid();

            // Row 0 is "back" — display high rows at top
            for (int r = _gridRows - 1; r >= 0; r--)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(2);
                for (int c = 0; c < _gridCols; c++)
                    DrawCell(c, r);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawCell(int c, int r)
        {
            GridCellType   t    = _type[c, r];
            BlockColorType col  = _color[c, r];
            bool           isSel = _selC == c && _selR == r;

            // Background
            Color bg = t == GridCellType.Empty
                ? new Color(.18f,.18f,.20f)
                : GetColor(col);
            if (t == GridCellType.Door) bg = Color.Lerp(bg, Color.black, .55f);

            // Reserve rect
            Rect outer = GUILayoutUtility.GetRect(
                CellSize + CellGap, CellSize + CellGap,
                GUILayout.Width(CellSize + CellGap),
                GUILayout.Height(CellSize + CellGap));
            Rect cell = new Rect(outer.x + CellGap * .5f, outer.y + CellGap * .5f, CellSize, CellSize);

            // Selection ring
            if (isSel)
                EditorGUI.DrawRect(new Rect(cell.x-2, cell.y-2, cell.width+4, cell.height+4), Color.white);

            EditorGUI.DrawRect(cell, bg);

            // Content
            if (t != GridCellType.Empty)
            {
                string line1 = t == GridCellType.Door ? "DOOR" : col.ToString().ToUpper().Substring(0,3);
                string line2 = t == GridCellType.Door
                    ? $"×{_doors[c,r]}"
                    : (_shots[c,r] < 0 ? $"×{_cfg?.defaultShots ?? 3}" : $"×{_shots[c,r]}");

                var style = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize  = 11,
                    normal    = { textColor = Color.white }
                };
                EditorGUI.LabelField(
                    new Rect(cell.x, cell.y + 4f, cell.width, cell.height * .5f - 2f),
                    line1, style);
                style.fontSize = 10;
                EditorGUI.LabelField(
                    new Rect(cell.x, cell.y + cell.height * .5f, cell.width, cell.height * .5f - 4f),
                    line2, style);
            }

            // Click
            Event e = Event.current;
            if (e.type == EventType.MouseDown && cell.Contains(e.mousePosition))
            {
                if (e.button == 0)
                {
                    _type[c, r] = _type[c, r] switch
                    {
                        GridCellType.Empty        => GridCellType.ShooterBlock,
                        GridCellType.ShooterBlock => GridCellType.Door,
                        _                         => GridCellType.Empty,
                    };
                }
                _selC = c; _selR = r;
                e.Use(); Repaint();
            }
        }

        // ── Groups ────────────────────────────────────────────────────────────
        private void DrawGroups()
        {
            for (int i = _groups.Count - 1; i >= 0; i--)
            {
                var g = _groups[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // Color swatch
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = GetColor(g.color);
                GUILayout.Box("", GUILayout.Width(20), GUILayout.Height(20));
                GUI.backgroundColor = prev;

                g.color     = (BlockColorType)EditorGUILayout.EnumPopup(g.color, GUILayout.Width(70));
                GUILayout.Label("Rows",  GUILayout.Width(32));
                g.rowCount  = EditorGUILayout.IntField(g.rowCount,  GUILayout.Width(34));
                GUILayout.Label("Lanes", GUILayout.Width(36));
                g.laneCount = EditorGUILayout.IntField(g.laneCount, GUILayout.Width(26));

                if (GUILayout.Button("✕", GUILayout.Width(20), GUILayout.Height(20)))
                    _groups.RemoveAt(i);

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Group", GUILayout.Height(22)))
                _groups.Add(new LevelConveyorGroup
                    { color = BlockColorType.Red,
                      rowCount = _cfg?.rowsPerGroup ?? 20,
                      laneCount = _cfg?.laneCount ?? 5 });
        }

        // ── RIGHT panel ───────────────────────────────────────────────────────
        private void DrawRight()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(RightW), GUILayout.ExpandHeight(true));
            Header("CELL INSPECTOR");

            if (_selC < 0 || _selR < 0 || _type == null ||
                _selC >= _gridCols || _selR >= _gridRows)
            {
                EditorGUILayout.HelpBox("Düzenlemek için\nbir hücreye tıkla.", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            int c = _selC, r = _selR;
            GUILayout.Label($"Hücre  ({c}, {r})", EditorStyles.boldLabel);
            GUILayout.Space(4);

            // Type buttons
            GUILayout.Label("Tür:", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (GridCellType t in System.Enum.GetValues(typeof(GridCellType)))
            {
                bool active = _type[c, r] == t;
                GUI.backgroundColor = active ? new Color(.4f,.7f,1f) : new Color(.3f,.3f,.3f);
                if (GUILayout.Button(t.ToString(), GUILayout.Height(22)))
                    _type[c, r] = t;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (_type[c, r] == GridCellType.Empty) { EditorGUILayout.EndVertical(); return; }

            GUILayout.Space(8);

            // Color palette
            GUILayout.Label("Renk:", EditorStyles.miniLabel);
            int perRow = 2;
            for (int i = 0; i < Colors.Length; i++)
            {
                if (i % perRow == 0) EditorGUILayout.BeginHorizontal();

                var entry = Colors[i];
                bool isSel = _color[c, r] == entry.type;

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = entry.color;
                GUIStyle style = new GUIStyle(GUI.skin.button);
                if (isSel) { style.fontStyle = FontStyle.Bold; style.normal.textColor = Color.white; }

                if (GUILayout.Button(entry.label,
                    style, GUILayout.Height(28), GUILayout.Width(90)))
                {
                    _color[c, r] = entry.type;
                    Repaint();
                }
                GUI.backgroundColor = prev;

                if (i % perRow == perRow - 1 || i == Colors.Length - 1)
                    EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(8);

            if (_type[c, r] == GridCellType.ShooterBlock)
            {
                GUILayout.Label("Shot Sayısı:", EditorStyles.miniLabel);
                bool useDefault = _shots[c, r] < 0;
                bool nd = EditorGUILayout.Toggle("Default kullan", useDefault);
                if (nd != useDefault)
                    _shots[c, r] = nd ? -1 : (_cfg?.defaultShots ?? 3);
                if (!nd)
                    _shots[c, r] = EditorGUILayout.IntSlider(_shots[c, r], 1, 20);
            }
            else if (_type[c, r] == GridCellType.Door)
            {
                GUILayout.Label("Kapıdan çıkacak blok:", EditorStyles.miniLabel);
                _doors[c, r] = EditorGUILayout.IntSlider(_doors[c, r], 1, 15);
            }

            EditorGUILayout.EndVertical();
        }

        // ── Load level ────────────────────────────────────────────────────────
        private void LoadLevel(int listIndex)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(_levelPaths[listIndex]);
            if (go == null) return;
            var lr = go.GetComponent<LevelRoot>();
            if (lr == null) return;

            _levelIndex = lr.levelIndex;
            _levelName  = lr.levelName;
            _goalType   = lr.goalType;
            _goalAmount = lr.goalAmount;
            _gridCols   = Mathf.Clamp(lr.gridCols, 1, MaxCols);
            _gridRows   = Mathf.Clamp(lr.gridRows, 1, MaxRows);
            InitGrid();

            foreach (var cell in lr.cells)
            {
                if (cell.col >= _gridCols || cell.row >= _gridRows) continue;
                _type [cell.col, cell.row] = cell.type;
                _color[cell.col, cell.row] = cell.color;
                _shots[cell.col, cell.row] = cell.shotCount;
                _doors[cell.col, cell.row] = cell.doorCount;
            }

            _groups.Clear();
            foreach (var g in lr.groups)
                _groups.Add(new LevelConveyorGroup
                    { color = g.color, rowCount = g.rowCount, laneCount = g.laneCount });

            _selC = -1; _selR = -1;
            Repaint();
        }

        // ── New level ─────────────────────────────────────────────────────────
        private void NewLevel()
        {
            int maxIdx = 0;
            foreach (var lbl in _levelLabels)
            {
                var p = lbl.Split('_');
                if (p.Length > 1 && int.TryParse(p[p.Length - 1], out int n))
                    maxIdx = Mathf.Max(maxIdx, n);
            }
            _levelIndex   = maxIdx + 1;
            _levelName    = $"Level {_levelIndex}";
            _goalType     = LevelGoalType.ClearAllBlocks;
            _goalAmount   = 0;
            _gridCols     = 4;
            _gridRows     = 2;
            _activeIndex  = -1;
            _selC = -1; _selR = -1;
            InitGrid();
            _groups.Clear();
            _groups.Add(new LevelConveyorGroup { color = BlockColorType.Red,
                rowCount = _cfg?.rowsPerGroup ?? 20, laneCount = _cfg?.laneCount ?? 5 });
            _groups.Add(new LevelConveyorGroup { color = BlockColorType.Blue,
                rowCount = _cfg?.rowsPerGroup ?? 20, laneCount = _cfg?.laneCount ?? 5 });
            Repaint();
        }

        // ── Save prefab ───────────────────────────────────────────────────────
        private void SavePrefab()
        {
            if (_cfg == null) { EditorUtility.DisplayDialog("Hata", "LevelEditorConfig bulunamadı!", "OK"); return; }

            string dir  = _cfg.levelSavePath.TrimEnd('/');
            string name = $"Level_{_levelIndex:000}";
            string path = dir + "/" + name + ".prefab";

            EnsureDir(dir);

            var root = new GameObject(name);
            var lr   = root.AddComponent<LevelRoot>();

            // Write design data
            lr.levelIndex = _levelIndex;
            lr.levelName  = _levelName;
            lr.goalType   = _goalType;
            lr.goalAmount = _goalAmount;
            lr.gridCols   = _gridCols;
            lr.gridRows   = _gridRows;
            lr.cells.Clear();
            for (int c = 0; c < _gridCols; c++)
            for (int r = 0; r < _gridRows; r++)
                lr.cells.Add(new LevelGridCell
                {
                    col = c, row = r,
                    type = _type[c,r], color = _color[c,r],
                    shotCount = _shots[c,r], doorCount = _doors[c,r]
                });
            lr.groups.Clear();
            foreach (var g in _groups)
                lr.groups.Add(new LevelConveyorGroup
                    { color = g.color, rowCount = g.rowCount, laneCount = g.laneCount });

            // Build hierarchy
            BuildHierarchy(root.transform, lr);

            // Save
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (ok)
            {
                Refresh();
                _activeIndex = _levelPaths.IndexOf(path);
                Debug.Log($"[LevelEditor] Saved: {path}");
                EditorUtility.DisplayDialog("Kaydedildi", $"{path}", "OK");
            }
            else
            {
                Debug.LogError($"[LevelEditor] Save failed: {path}");
            }
        }

        // ── Hierarchy builder ─────────────────────────────────────────────────
        private void BuildHierarchy(Transform root, LevelRoot lr)
        {
            float cs = _cfg.gridCellSize;

            // ── Track + Groups ──
            var trackGo = Go(root, "Track");
            var sc      = trackGo.AddComponent<SplineContainer>();
            BuildOvalSpline(sc);
            var cc = trackGo.AddComponent<ConveyorController>();
            cc.speed = _cfg.conveyorSpeed;
            lr.conveyorController = cc;

            if (_cfg.trackSegmentPrefab != null)
            {
                var tr = trackGo.AddComponent<ConveyorTrackRenderer>();
                tr.segmentPrefab = _cfg.trackSegmentPrefab;
                if (_cfg.arrowPrefab != null) tr.arrowPrefab = _cfg.arrowPrefab;
            }

            var groupsRoot = Go(trackGo.transform, "Groups");
            foreach (var gd in _groups)
            {
                var gGo = Go(groupsRoot.transform, $"Group_{gd.color}");
                var bg  = gGo.AddComponent<BlockGroup>();
                bg.colorType   = gd.color;
                bg.rowCount    = gd.rowCount;
                bg.laneCount   = gd.laneCount;
                bg.laneSpacing = _cfg.laneSpacing;
                bg.rowSpacing  = _cfg.rowSpacing;

                if (_cfg.conveyorBlockPrefab != null)
                {
                    for (int row = 0; row < gd.rowCount; row++)
                    {
                        var rowGo = Go(gGo.transform, $"Row_{row}");
                        for (int lane = 0; lane < gd.laneCount; lane++)
                        {
                            var bGo = (GameObject)PrefabUtility.InstantiatePrefab(
                                _cfg.conveyorBlockPrefab, rowGo.transform);
                            bGo.name = $"Block_{lane}";
                            bGo.transform.localPosition = Vector3.zero;
                            bGo.GetComponent<ConveyorBlock3D>()?.SetGroupIndex(row, lane);
                        }
                    }
                }
            }

            // ── FireRange ──
            var frGo = Go(root, "FireRange");
            frGo.transform.localPosition = new Vector3(0f, 0.5f, 2f);
            var fc = frGo.AddComponent<BoxCollider>();
            fc.isTrigger = true;
            fc.size = new Vector3(cs * _gridCols + 1f, 2f, 3f);
            lr.fireRange = frGo.AddComponent<FireRange>();

            // ── SlotDeck ──
            var deckGo = Go(root, "SlotDeck");
            deckGo.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            var ss = deckGo.AddComponent<SlotSystem>();
            if (_cfg.slotIndicatorPrefab != null) ss.slotIndicatorPrefab = _cfg.slotIndicatorPrefab;
            lr.slotSystem = ss;

            int slots = _cfg.slotCount;
            float totalW = (slots - 1) * cs;
            for (int i = 0; i < slots; i++)
                Go(deckGo.transform, $"Slot_{i}").transform.localPosition =
                    new Vector3(-totalW * .5f + i * cs, 0f, 0f);

            // ── ShooterGrid ──
            var sgGo = Go(root, "ShooterGrid");
            sgGo.transform.localPosition = new Vector3(0f, 0f, -2f);
            var sg = sgGo.AddComponent<ShooterGrid>();
            if (_cfg.shooterBlockPrefab != null)
                sg.shooterBlockPrefab = _cfg.shooterBlockPrefab.GetComponent<ShooterBlock>();
            lr.shooterGrid = sg;

            float hw = (_gridCols - 1) * cs * .5f;
            float hd = (_gridRows - 1) * cs * .5f;

            for (int r = 0; r < _gridRows; r++)
            for (int c = 0; c < _gridCols; c++)
            {
                Vector3 pos = new Vector3(-hw + c * cs, 0f, -hd + r * cs);
                string  nm  = $"Cell_r{r}_c{c}";

                switch (_type[c, r])
                {
                    case GridCellType.ShooterBlock when _cfg.shooterBlockPrefab != null:
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(
                            _cfg.shooterBlockPrefab, sgGo.transform);
                        go.name = nm;
                        go.transform.localPosition = pos;
                        int sh = _shots[c, r] >= 0 ? _shots[c, r] : _cfg.defaultShots;
                        go.GetComponent<ShooterBlock>()?.EditorSetup(_color[c, r], sh, c, r);
                        break;
                    }
                    case GridCellType.Door:
                    {
                        var go = Go(sgGo.transform, nm);
                        go.transform.localPosition = pos;
                        var d = go.AddComponent<BlockDoor>();
                        d.blockCount  = _doors[c, r];
                        d.spawnColors = new List<BlockColorType> { _color[c, r] };
                        break;
                    }
                    case GridCellType.Empty when _cfg.wallElementPrefab != null:
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(
                            _cfg.wallElementPrefab, sgGo.transform);
                        go.name = nm;
                        go.transform.localPosition = pos;
                        go.GetComponent<WallElement>()?.SetGridPosition(c, r);
                        break;
                    }
                }
            }

            // ── Ground ──
            var gnd = GameObject.CreatePrimitive(PrimitiveType.Plane);
            gnd.name = "Ground";
            gnd.transform.SetParent(root, false);
            gnd.transform.localPosition = new Vector3(0f, -.01f, 0f);
            gnd.transform.localScale    = new Vector3(1.5f, 1f, 1.5f);
            Object.DestroyImmediate(gnd.GetComponent<MeshCollider>());
        }

        // ── Spline ────────────────────────────────────────────────────────────
        private static void BuildOvalSpline(SplineContainer sc)
        {
            var s = sc.Spline; s.Clear();
            s.Add(new BezierKnot(new Unity.Mathematics.float3(-3f, 0f, 0f)));
            s.Add(new BezierKnot(new Unity.Mathematics.float3( 0f, 0f, 5f)));
            s.Add(new BezierKnot(new Unity.Mathematics.float3( 3f, 0f, 0f)));
            s.Add(new BezierKnot(new Unity.Mathematics.float3( 0f, 0f,-5f)));
            s.Closed = true;
        }

        // ── Grid helpers ──────────────────────────────────────────────────────
        private void InitGrid()
        {
            var pt = _type; var pc = _color; var ps = _shots; var pd = _doors;
            _type  = new GridCellType  [_gridCols, _gridRows];
            _color = new BlockColorType[_gridCols, _gridRows];
            _shots = new int           [_gridCols, _gridRows];
            _doors = new int           [_gridCols, _gridRows];
            for (int c = 0; c < _gridCols; c++)
            for (int r = 0; r < _gridRows; r++)
            {
                _shots[c, r] = -1; _doors[c, r] = 3;
                if (pt == null || c >= pt.GetLength(0) || r >= pt.GetLength(1)) continue;
                _type[c,r] = pt[c,r]; _color[c,r] = pc[c,r];
                _shots[c,r] = ps[c,r]; _doors[c,r] = pd[c,r];
            }
        }

        private void ResizeGrid() { InitGrid(); _selC = -1; _selR = -1; }

        // ── UI helpers ────────────────────────────────────────────────────────
        private static void Header(string t)
        {
            GUILayout.Space(6);
            EditorGUILayout.LabelField(t, EditorStyles.boldLabel);
            EditorGUI.DrawRect(
                GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true)),
                new Color(.5f,.5f,.5f,.4f));
            GUILayout.Space(3);
        }

        private static void Divider()
        {
            EditorGUI.DrawRect(
                GUILayoutUtility.GetRect(1, float.MaxValue,
                    GUILayout.Width(1), GUILayout.ExpandHeight(true)),
                new Color(.25f,.25f,.25f));
        }

        private static GameObject Go(Transform p, string n)
        {
            var g = new GameObject(n);
            g.transform.SetParent(p, false);
            return g;
        }

        private static void EnsureDir(string path)
        {
            string[] parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string nxt = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(nxt))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = nxt;
            }
        }
    }
}
#endif
