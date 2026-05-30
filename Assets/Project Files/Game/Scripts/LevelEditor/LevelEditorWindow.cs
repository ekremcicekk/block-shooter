#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter.Editor
{
    /// <summary>
    /// Unified Level Editor — BlockShooter / Level Editor
    ///
    /// LEFT   : level list + current level settings
    /// CENTER : shooter grid (max 7×6) + conveyor groups + spline presets
    /// RIGHT  : selected-cell inspector
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        // ── Layout constants ──────────────────────────────────────────────────
        private const float ListW  = 180f;
        private const float InspW  = 230f;
        private const float CellPx = 56f;
        private const float CellGap = 3f;
        private const int   MaxCols = 7;
        private const int   MaxRows = 6;

        // ── Config (loaded once from AssetDatabase) ───────────────────────────
        private LevelEditorConfig _cfg;

        // ── Level list ────────────────────────────────────────────────────────
        private string[]  _levelPaths;  // prefab asset paths
        private string[]  _levelLabels; // display names
        private int       _selectedListIndex = -1;
        private GameObject _loadedPrefab;
        private LevelRoot  _loadedRoot;  // the prefab's LevelRoot (not a scene instance)

        // ── Working copy of the design ────────────────────────────────────────
        private int    _gridCols = 4;
        private int    _gridRows = 2;
        private string _levelName = "Level 1";
        private int    _levelIndex = 1;
        private LevelGoalType _goalType   = LevelGoalType.ClearAllBlocks;
        private int           _goalAmount = 0;

        // 2D arrays [col, row]
        private GridCellType[,]   _cellType;
        private BlockColorType[,] _cellColor;
        private int[,]            _cellShots;  // -1 = default
        private int[,]            _doorCount;

        private readonly List<LevelConveyorGroup> _groups = new();

        // ── Cell selection (right panel) ──────────────────────────────────────
        private int _selCol = -1;
        private int _selRow = -1;

        // ── Scroll positions ──────────────────────────────────────────────────
        private Vector2 _listScroll;
        private Vector2 _centerScroll;

        // ── Spline preset shapes ──────────────────────────────────────────────
        private enum SplinePreset { Oval, WideLoop, Rectangle, Figure8 }

        // ── Color palette (matches GameConfig order) ──────────────────────────
        private static readonly Color[] Palette =
        {
            new Color(0.90f, 0.20f, 0.20f), // Red
            new Color(0.20f, 0.50f, 0.90f), // Blue
            new Color(0.20f, 0.80f, 0.30f), // Green
            new Color(1.00f, 0.85f, 0.10f), // Yellow
            new Color(0.60f, 0.20f, 0.90f), // Purple
            new Color(1.00f, 0.55f, 0.10f), // Orange
        };

        private static Color Col(BlockColorType t) =>
            (int)t < Palette.Length ? Palette[(int)t] : Color.gray;

        // ── Menu ──────────────────────────────────────────────────────────────
        [MenuItem("BlockShooter/Level Editor", false, 10)]
        public static void Open()
        {
            var w = GetWindow<LevelEditorWindow>("Level Editor");
            w.minSize = new Vector2(820, 560);
            w.Show();
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void OnEnable()
        {
            LoadConfig();
            RefreshLevelList();
            RebuildGrid();
            if (_groups.Count == 0) AddDefaultGroups();
        }

        private void LoadConfig()
        {
            var guids = AssetDatabase.FindAssets("t:LevelEditorConfig");
            _cfg = guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<LevelEditorConfig>(
                      AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
        }

        private void RefreshLevelList()
        {
            if (_cfg == null) { _levelPaths = new string[0]; _levelLabels = new string[0]; return; }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { _cfg.levelSavePath.TrimEnd('/') });
            var paths  = new List<string>();
            var labels = new List<string>();

            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponent<LevelRoot>() == null) continue;
                paths.Add(path);
                labels.Add(Path.GetFileNameWithoutExtension(path));
            }

            // Sort by name
            var sorted = paths.Zip(labels, (p, l) => (p, l))
                              .OrderBy(x => x.l).ToList();
            _levelPaths  = sorted.Select(x => x.p).ToArray();
            _levelLabels = sorted.Select(x => x.l).ToArray();
        }

        private void AddDefaultGroups()
        {
            _groups.Add(new LevelConveyorGroup { color = BlockColorType.Red,  rowCount = _cfg?.rowsPerGroup ?? 20, laneCount = _cfg?.laneCount ?? 5 });
            _groups.Add(new LevelConveyorGroup { color = BlockColorType.Blue, rowCount = _cfg?.rowsPerGroup ?? 20, laneCount = _cfg?.laneCount ?? 5 });
        }

        // ── GUI main ─────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (_cfg == null) { DrawNoConfig(); return; }

            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawListPanel();
            DrawDivider();
            DrawCenterPanel();
            DrawDivider();
            DrawInspectorPanel();
            EditorGUILayout.EndHorizontal();
        }

        // ── No Config warning ─────────────────────────────────────────────────
        private void DrawNoConfig()
        {
            GUILayout.Space(40);
            EditorGUILayout.HelpBox(
                "LevelEditorConfig asset not found.\nCreate one via:\nAssets → Create → BlockShooter → Level Editor Config",
                MessageType.Warning);
            if (GUILayout.Button("Refresh", GUILayout.Height(28)))
                LoadConfig();
        }

        // ── Toolbar ───────────────────────────────────────────────────────────
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("BLOCK SHOOTER  —  LEVEL EDITOR", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Edit Config", EditorStyles.toolbarButton, GUILayout.Width(80)))
                Selection.activeObject = _cfg;
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            { LoadConfig(); RefreshLevelList(); }
            EditorGUILayout.EndHorizontal();
        }

        // ── LEFT: Level list ──────────────────────────────────────────────────
        private void DrawListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListW), GUILayout.ExpandHeight(true));

            SectionLabel("LEVELS");

            // New level button
            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.5f);
            if (GUILayout.Button("+ New Level", GUILayout.Height(26)))
                CreateNewLevel();
            GUI.backgroundColor = Color.white;

            GUILayout.Space(4);

            // List
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _levelLabels.Length; i++)
            {
                bool selected = _selectedListIndex == i;
                GUI.backgroundColor = selected ? new Color(0.4f, 0.6f, 1f) : Color.white;
                if (GUILayout.Button(_levelLabels[i], selected ? EditorStyles.boldLabel : EditorStyles.label,
                    GUILayout.Height(22)))
                {
                    _selectedListIndex = i;
                    LoadLevel(_levelPaths[i]);
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndScrollView();

            GUILayout.Space(4);

            // Current level info
            SectionLabel("LEVEL SETTINGS");
            _levelIndex = EditorGUILayout.IntField ("Index", _levelIndex);
            _levelName  = EditorGUILayout.TextField("Name",  _levelName);
            _goalType   = (LevelGoalType)EditorGUILayout.EnumPopup("Goal", _goalType);
            if (_goalType != LevelGoalType.ClearAllBlocks)
                _goalAmount = EditorGUILayout.IntField("Amount", _goalAmount);

            GUILayout.Space(8);

            // Save button
            GUI.backgroundColor = new Color(0.4f, 0.75f, 1f);
            if (GUILayout.Button(_loadedPrefab != null ? "Update Prefab" : "Generate Prefab",
                GUILayout.Height(30)))
                SaveLevel();
            GUI.backgroundColor = Color.white;

            if (_loadedPrefab != null)
            {
                GUILayout.Space(4);
                if (GUILayout.Button("Open Prefab", GUILayout.Height(22)))
                    AssetDatabase.OpenAsset(_loadedPrefab);
            }

            EditorGUILayout.EndVertical();
        }

        // ── CENTER ────────────────────────────────────────────────────────────
        private void DrawCenterPanel()
        {
            _centerScroll = EditorGUILayout.BeginScrollView(_centerScroll,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // Grid dimensions
            SectionLabel("SHOOTER GRID");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Cols", GUILayout.Width(34));
            int newCols = EditorGUILayout.IntSlider(_gridCols, 1, MaxCols, GUILayout.Width(140));
            GUILayout.Label("Rows", GUILayout.Width(34));
            int newRows = EditorGUILayout.IntSlider(_gridRows, 1, MaxRows, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();
            if (newCols != _gridCols || newRows != _gridRows)
            {
                _gridCols = newCols; _gridRows = newRows;
                RebuildGrid();
            }

            GUILayout.Space(6);
            DrawGrid();

            GUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "LMB = cycle type (Empty → Block → Door)   RMB = open inspector",
                MessageType.None);

            // Conveyor groups
            SectionLabel("CONVEYOR GROUPS");
            DrawConveyorGroups();

            // Spline presets
            SectionLabel("SPLINE PRESET");
            DrawSplinePresets();

            EditorGUILayout.EndScrollView();
        }

        // ── Grid drawing ──────────────────────────────────────────────────────
        private void DrawGrid()
        {
            GUILayout.BeginVertical();
            for (int row = _gridRows - 1; row >= 0; row--)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < _gridCols; col++)
                    DrawCell(col, row);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private void DrawCell(int col, int row)
        {
            GridCellType   type  = _cellType[col, row];
            BlockColorType color = _cellColor[col, row];
            bool           sel   = _selCol == col && _selRow == row;

            Color bg = type == GridCellType.Empty
                ? new Color(0.22f, 0.22f, 0.25f)
                : Col(color);
            if (type == GridCellType.Door)
                bg = Color.Lerp(bg, Color.black, 0.5f);
            if (sel)
                bg = Color.Lerp(bg, Color.white, 0.35f);

            string shots = _cellShots[col, row] < 0
                ? $"×{_cfg?.defaultShots ?? 3}"
                : $"×{_cellShots[col, row]}";

            string label = type == GridCellType.Empty       ? ""
                         : type == GridCellType.Door        ? $"Door\n{_doorCount[col, row]}"
                         : $"{ShortName(color)}\n{shots}";

            Rect r = GUILayoutUtility.GetRect(CellPx + CellGap, CellPx + CellGap,
                GUILayout.Width(CellPx + CellGap));
            r = new Rect(r.x + CellGap * 0.5f, r.y + CellGap * 0.5f, CellPx, CellPx);

            // Selection border
            if (sel)
            {
                EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4),
                    Color.white);
            }

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = bg;
            GUI.Box(r, label, EditorStyles.helpBox);
            GUI.backgroundColor = prev;

            Event e = Event.current;
            if (!r.Contains(e.mousePosition)) return;

            if (e.type == EventType.MouseDown)
            {
                if (e.button == 0) { CycleType(col, row); _selCol = col; _selRow = row; }
                else if (e.button == 1) { _selCol = col; _selRow = row; }
                e.Use(); Repaint();
            }
        }

        private static string ShortName(BlockColorType t) => t.ToString().Substring(0, 3);

        private void CycleType(int col, int row)
        {
            _cellType[col, row] = _cellType[col, row] switch
            {
                GridCellType.Empty        => GridCellType.ShooterBlock,
                GridCellType.ShooterBlock => GridCellType.Door,
                _                         => GridCellType.Empty,
            };
        }

        // ── Conveyor groups ───────────────────────────────────────────────────
        private void DrawConveyorGroups()
        {
            for (int i = _groups.Count - 1; i >= 0; i--)
            {
                var g = _groups[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = Col(g.color);
                GUILayout.Box("", GUILayout.Width(18), GUILayout.Height(18));
                GUI.backgroundColor = prev;

                g.color    = (BlockColorType)EditorGUILayout.EnumPopup(g.color, GUILayout.Width(74));
                GUILayout.Label("Rows",  GUILayout.Width(34));
                g.rowCount = EditorGUILayout.IntField(g.rowCount,  GUILayout.Width(36));
                GUILayout.Label("Lanes", GUILayout.Width(36));
                g.laneCount= EditorGUILayout.IntField(g.laneCount, GUILayout.Width(28));

                if (GUILayout.Button("×", GUILayout.Width(20)))
                    _groups.RemoveAt(i);

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Group"))
                _groups.Add(new LevelConveyorGroup
                {
                    color     = BlockColorType.Red,
                    rowCount  = _cfg?.rowsPerGroup ?? 20,
                    laneCount = _cfg?.laneCount    ?? 5
                });
        }

        // ── Spline presets ────────────────────────────────────────────────────
        private void DrawSplinePresets()
        {
            EditorGUILayout.BeginHorizontal();
            foreach (SplinePreset p in System.Enum.GetValues(typeof(SplinePreset)))
            {
                if (GUILayout.Button(p.ToString(), GUILayout.Height(24)))
                    ApplySplinePreset(p);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "Presets write the spline shape into the prefab.\n" +
                "For fine editing: click 'Open Prefab' then use Unity's Spline tool.",
                MessageType.None);
        }

        private void ApplySplinePreset(SplinePreset preset)
        {
            if (_loadedPrefab == null) { Debug.LogWarning("[LevelEditor] No prefab loaded."); return; }

            var root = _loadedPrefab.GetComponent<LevelRoot>();
            if (root == null) return;
            var cc = root.conveyorController;
            if (cc == null) return;
            var sc = cc.GetComponent<SplineContainer>();
            if (sc == null) return;

            using (var scope = new PrefabUtility.EditPrefabContentsScope(
                AssetDatabase.GetAssetPath(_loadedPrefab)))
            {
                var conveyorGo = scope.prefabContentsRoot.transform.Find("Track");
                if (conveyorGo == null) return;
                var splineContainer = conveyorGo.GetComponent<SplineContainer>();
                if (splineContainer == null) return;

                WritePreset(splineContainer, preset);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelEditor] Spline preset '{preset}' applied.");
        }

        private static void WritePreset(SplineContainer sc, SplinePreset preset)
        {
            var s = sc.Spline;
            s.Clear();

            switch (preset)
            {
                case SplinePreset.Oval:
                    s.Add(Knot(-3f, 0f,  0f)); s.Add(Knot( 0f, 0f, 5f));
                    s.Add(Knot( 3f, 0f,  0f)); s.Add(Knot( 0f, 0f,-5f));
                    break;
                case SplinePreset.WideLoop:
                    s.Add(Knot(-5f, 0f,  0f)); s.Add(Knot( 0f, 0f, 4f));
                    s.Add(Knot( 5f, 0f,  0f)); s.Add(Knot( 0f, 0f,-4f));
                    break;
                case SplinePreset.Rectangle:
                    s.Add(Knot(-4f, 0f,-3f)); s.Add(Knot( 4f, 0f,-3f));
                    s.Add(Knot( 4f, 0f, 5f)); s.Add(Knot(-4f, 0f, 5f));
                    break;
                case SplinePreset.Figure8:
                    s.Add(Knot( 0f, 0f, 0f)); s.Add(Knot( 3f, 0f, 3f));
                    s.Add(Knot( 0f, 0f, 6f)); s.Add(Knot(-3f, 0f, 3f));
                    s.Add(Knot( 0f, 0f, 0f)); s.Add(Knot( 3f, 0f,-3f));
                    s.Add(Knot( 0f, 0f,-6f)); s.Add(Knot(-3f, 0f,-3f));
                    break;
            }

            s.Closed = true;
        }

        private static BezierKnot Knot(float x, float y, float z) =>
            new BezierKnot(new Unity.Mathematics.float3(x, y, z));

        // ── RIGHT: Cell inspector ─────────────────────────────────────────────
        private void DrawInspectorPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(InspW), GUILayout.ExpandHeight(true));

            if (_selCol < 0 || _selRow < 0 ||
                _selCol >= _gridCols || _selRow >= _gridRows)
            {
                GUILayout.Space(20);
                EditorGUILayout.HelpBox("Click a grid cell to inspect it.", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            SectionLabel($"CELL  ({_selCol}, {_selRow})");

            // Type
            _cellType[_selCol, _selRow] =
                (GridCellType)EditorGUILayout.EnumPopup("Type", _cellType[_selCol, _selRow]);

            GridCellType type = _cellType[_selCol, _selRow];

            if (type != GridCellType.Empty)
            {
                // Color palette
                GUILayout.Space(6);
                GUILayout.Label("Color", EditorStyles.boldLabel);
                DrawColorPicker(_selCol, _selRow);

                GUILayout.Space(6);

                if (type == GridCellType.ShooterBlock)
                {
                    GUILayout.Label("Shots", EditorStyles.boldLabel);
                    bool useDefault = _cellShots[_selCol, _selRow] < 0;
                    bool newDefault = EditorGUILayout.Toggle("Use Default", useDefault);
                    if (newDefault != useDefault)
                        _cellShots[_selCol, _selRow] = newDefault ? -1 : (_cfg?.defaultShots ?? 3);
                    if (!newDefault)
                        _cellShots[_selCol, _selRow] =
                            EditorGUILayout.IntSlider(_cellShots[_selCol, _selRow], 1, 20);

                    GUILayout.Space(10);
                    // Future properties placeholder
                    GUILayout.Label("Properties", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("More options coming soon\n(Locked, Armored, etc.)", MessageType.None);
                }
                else if (type == GridCellType.Door)
                {
                    GUILayout.Label("Door Block Count", EditorStyles.boldLabel);
                    _doorCount[_selCol, _selRow] =
                        EditorGUILayout.IntSlider(_doorCount[_selCol, _selRow], 1, 15);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawColorPicker(int col, int row)
        {
            var types = (BlockColorType[])System.Enum.GetValues(typeof(BlockColorType));
            int perRow = 3;
            for (int i = 0; i < types.Length; i++)
            {
                if (i % perRow == 0) EditorGUILayout.BeginHorizontal();

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = Col(types[i]);
                bool isSel = _cellColor[col, row] == types[i];
                GUIStyle style = isSel ? EditorStyles.helpBox : GUI.skin.button;

                if (GUILayout.Button(types[i].ToString().Substring(0, 3),
                    style, GUILayout.Width(64), GUILayout.Height(28)))
                {
                    _cellColor[col, row] = types[i];
                    Repaint();
                }
                GUI.backgroundColor = prev;

                if (i % perRow == perRow - 1 || i == types.Length - 1)
                    EditorGUILayout.EndHorizontal();
            }
        }

        // ── Load / Save ───────────────────────────────────────────────────────
        private void LoadLevel(string path)
        {
            _loadedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (_loadedPrefab == null) return;

            _loadedRoot = _loadedPrefab.GetComponent<LevelRoot>();
            if (_loadedRoot == null) return;

            _levelIndex = _loadedRoot.levelIndex;
            _levelName  = _loadedRoot.levelName;
            _goalType   = _loadedRoot.goalType;
            _goalAmount = _loadedRoot.goalAmount;
            _gridCols   = Mathf.Clamp(_loadedRoot.gridCols, 1, MaxCols);
            _gridRows   = Mathf.Clamp(_loadedRoot.gridRows, 1, MaxRows);

            RebuildGrid();

            // Restore cells
            foreach (var c in _loadedRoot.cells)
            {
                if (c.col >= _gridCols || c.row >= _gridRows) continue;
                _cellType [c.col, c.row] = c.type;
                _cellColor[c.col, c.row] = c.color;
                _cellShots[c.col, c.row] = c.shotCount;
                _doorCount[c.col, c.row] = c.doorCount;
            }

            _groups.Clear();
            foreach (var g in _loadedRoot.groups)
                _groups.Add(new LevelConveyorGroup
                    { color = g.color, rowCount = g.rowCount, laneCount = g.laneCount });

            _selCol = -1; _selRow = -1;
            Repaint();
        }

        private void CreateNewLevel()
        {
            // Auto-increment index
            int maxIndex = 0;
            foreach (var lbl in _levelLabels)
            {
                var parts = lbl.Split('_');
                if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out int n))
                    maxIndex = Mathf.Max(maxIndex, n);
            }
            _levelIndex    = maxIndex + 1;
            _levelName     = $"Level {_levelIndex}";
            _goalType      = LevelGoalType.ClearAllBlocks;
            _goalAmount    = 0;
            _gridCols      = 4;
            _gridRows      = 2;
            _loadedPrefab  = null;
            _loadedRoot    = null;
            _selectedListIndex = -1;
            _selCol = -1; _selRow = -1;

            RebuildGrid();
            _groups.Clear();
            AddDefaultGroups();
            Repaint();
        }

        private void SaveLevel()
        {
            if (_cfg == null) { Debug.LogError("[LevelEditor] LevelEditorConfig not found!"); return; }

            EnsureDirectory(_cfg.levelSavePath.TrimEnd('/'));
            string prefabName = $"Level_{_levelIndex:000}";
            string prefabPath = _cfg.levelSavePath + prefabName + ".prefab";

            // If updating existing, use EditPrefabContentsScope; else create fresh
            if (_loadedPrefab != null &&
                AssetDatabase.GetAssetPath(_loadedPrefab) == prefabPath)
            {
                UpdateExistingPrefab(prefabPath);
            }
            else
            {
                BuildAndSaveNewPrefab(prefabPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshLevelList();

            // Re-select saved level
            _selectedListIndex = System.Array.IndexOf(_levelPaths, prefabPath);
            if (_selectedListIndex >= 0)
                LoadLevel(_levelPaths[_selectedListIndex]);

            EditorUtility.DisplayDialog("Saved", $"Level prefab saved:\n{prefabPath}", "OK");
        }

        // ── Full rebuild (new prefab) ──────────────────────────────────────────
        private void BuildAndSaveNewPrefab(string prefabPath)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            PopulateRoot(root);

            bool ok;
            _loadedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out ok);
            Object.DestroyImmediate(root);

            if (!ok) Debug.LogError($"[LevelEditor] Failed to save: {prefabPath}");
        }

        // ── In-place update (existing prefab) ─────────────────────────────────
        private void UpdateExistingPrefab(string prefabPath)
        {
            using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
            var root = scope.prefabContentsRoot;

            // Destroy all children — we'll rebuild them
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            // Re-apply the LevelRoot component data
            var lr = root.GetComponent<LevelRoot>();
            if (lr == null) lr = root.AddComponent<LevelRoot>();
            WriteDesignData(lr);
            PopulateChildren(root.transform, lr);
        }

        private void PopulateRoot(GameObject root)
        {
            var lr = root.AddComponent<LevelRoot>();
            WriteDesignData(lr);
            PopulateChildren(root.transform, lr);
        }

        private void WriteDesignData(LevelRoot lr)
        {
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
                    col       = c, row       = r,
                    type      = _cellType [c, r],
                    color     = _cellColor[c, r],
                    shotCount = _cellShots[c, r],
                    doorCount = _doorCount[c, r],
                });

            lr.groups.Clear();
            foreach (var g in _groups)
                lr.groups.Add(new LevelConveyorGroup
                    { color = g.color, rowCount = g.rowCount, laneCount = g.laneCount });
        }

        private void PopulateChildren(Transform root, LevelRoot lr)
        {
            float cellSize  = _cfg.gridCellSize;
            int   slotCount = _cfg.slotCount;

            // ── Track ──
            var trackGo = Child(root, "Track");
            var spline  = trackGo.AddComponent<SplineContainer>();
            WritePreset(spline, SplinePreset.Oval);
            var convCtrl = trackGo.AddComponent<ConveyorController>();
            convCtrl.speed = _cfg.conveyorSpeed;
            lr.conveyorController = convCtrl;

            if (_cfg.trackSegmentPrefab != null)
            {
                var rend = trackGo.AddComponent<ConveyorTrackRenderer>();
                rend.segmentPrefab = _cfg.trackSegmentPrefab;
                if (_cfg.arrowPrefab != null) rend.arrowPrefab = _cfg.arrowPrefab;
            }

            // Block groups
            var groupsGo = Child(trackGo.transform, "Groups");
            foreach (var gd in _groups)
            {
                var groupGo = Child(groupsGo.transform, $"Group_{gd.color}");
                var bg = groupGo.AddComponent<BlockGroup>();
                bg.colorType   = gd.color;
                bg.rowCount    = gd.rowCount;
                bg.laneCount   = gd.laneCount;
                bg.laneSpacing = _cfg.laneSpacing;
                bg.rowSpacing  = _cfg.rowSpacing;
                if (_cfg.conveyorBlockPrefab != null)
                    BuildBlockChildren(groupGo.transform, gd);
            }

            // ── FireRange ──
            var frGo  = Child(root, "FireRange");
            frGo.transform.localPosition = new Vector3(0f, 0.5f, 2f);
            var frCol = frGo.AddComponent<BoxCollider>();
            frCol.isTrigger = true;
            frCol.size = new Vector3(cellSize * _gridCols + 1f, 2f, 3f);
            lr.fireRange = frGo.AddComponent<FireRange>();

            // ── SlotDeck ──
            var deckGo = Child(root, "SlotDeck");
            deckGo.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            var ss = deckGo.AddComponent<SlotSystem>();
            ss.slotIndicatorPrefab = _cfg.slotIndicatorPrefab;
            lr.slotSystem = ss;

            float totalSlotW = (slotCount - 1) * cellSize;
            for (int i = 0; i < slotCount; i++)
                Child(deckGo.transform, $"Slot_{i}").transform.localPosition =
                    new Vector3(-totalSlotW * 0.5f + i * cellSize, 0f, 0f);

            // ── ShooterGrid ──
            var sgGo = Child(root, "ShooterGrid");
            sgGo.transform.localPosition = new Vector3(0f, 0f, -2f);
            var sg = sgGo.AddComponent<ShooterGrid>();
            sg.shooterBlockPrefab = _cfg.shooterBlockPrefab != null
                ? _cfg.shooterBlockPrefab.GetComponent<ShooterBlock>() : null;
            lr.shooterGrid = sg;

            float halfW = (_gridCols - 1) * cellSize * 0.5f;
            float halfD = (_gridRows - 1) * cellSize * 0.5f;

            for (int r = 0; r < _gridRows; r++)
            for (int c = 0; c < _gridCols; c++)
            {
                var pos = new Vector3(-halfW + c * cellSize, 0f, -halfD + r * cellSize);
                string name = $"Cell_r{r}_c{c}";

                switch (_cellType[c, r])
                {
                    case GridCellType.ShooterBlock when _cfg.shooterBlockPrefab != null:
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(
                            _cfg.shooterBlockPrefab, sgGo.transform);
                        go.name = name;
                        go.transform.localPosition = pos;
                        var sb = go.GetComponent<ShooterBlock>();
                        int shots = _cellShots[c, r] >= 0 ? _cellShots[c, r] : _cfg.defaultShots;
                        sb?.EditorSetup(_cellColor[c, r], shots, c, r);
                        break;
                    }
                    case GridCellType.Door:
                    {
                        var go = Child(sgGo.transform, name);
                        go.transform.localPosition = pos;
                        var door = go.AddComponent<BlockDoor>();
                        door.blockCount  = _doorCount[c, r];
                        door.spawnColors = new List<BlockColorType> { _cellColor[c, r] };
                        break;
                    }
                    case GridCellType.Empty when _cfg.wallElementPrefab != null:
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(
                            _cfg.wallElementPrefab, sgGo.transform);
                        go.name = name;
                        go.transform.localPosition = pos;
                        go.GetComponent<WallElement>()?.SetGridPosition(c, r);
                        break;
                    }
                }
            }

            // ── Ground ──
            var groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundGo.name = "Ground";
            groundGo.transform.SetParent(root, false);
            groundGo.transform.localPosition = new Vector3(0f, -0.01f, 0f);
            groundGo.transform.localScale    = new Vector3(1.5f, 1f, 1.5f);
            Object.DestroyImmediate(groundGo.GetComponent<MeshCollider>());
        }

        private void BuildBlockChildren(Transform parent, LevelConveyorGroup gd)
        {
            for (int row = 0; row < gd.rowCount; row++)
            {
                var rowGo = Child(parent, $"Row_{row}");
                for (int lane = 0; lane < gd.laneCount; lane++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(
                        _cfg.conveyorBlockPrefab, rowGo.transform);
                    go.name = $"Block_{lane}";
                    go.transform.localPosition = Vector3.zero;
                    go.GetComponent<ConveyorBlock3D>()?.SetGroupIndex(row, lane);
                }
            }
        }

        // ── Grid state ────────────────────────────────────────────────────────
        private void RebuildGrid()
        {
            _gridCols = Mathf.Clamp(_gridCols, 1, MaxCols);
            _gridRows = Mathf.Clamp(_gridRows, 1, MaxRows);

            var prevType  = _cellType;
            var prevColor = _cellColor;
            var prevShots = _cellShots;
            var prevDoor  = _doorCount;

            _cellType  = new GridCellType[_gridCols, _gridRows];
            _cellColor = new BlockColorType[_gridCols, _gridRows];
            _cellShots = new int[_gridCols, _gridRows];
            _doorCount = new int[_gridCols, _gridRows];

            for (int c = 0; c < _gridCols; c++)
            for (int r = 0; r < _gridRows; r++)
            {
                _cellShots[c, r] = -1;
                _doorCount[c, r] = 3;
                if (prevType == null || c >= prevType.GetLength(0) || r >= prevType.GetLength(1)) continue;
                _cellType [c, r] = prevType [c, r];
                _cellColor[c, r] = prevColor[c, r];
                _cellShots[c, r] = prevShots[c, r];
                _doorCount[c, r] = prevDoor [c, r];
            }
        }

        // ── Utility ───────────────────────────────────────────────────────────
        private static void SectionLabel(string title)
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            var r = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.35f));
            GUILayout.Space(4);
        }

        private static void DrawDivider()
        {
            var r = GUILayoutUtility.GetRect(1, float.MaxValue,
                GUILayout.Width(1), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(r, new Color(0.3f, 0.3f, 0.3f));
        }

        private static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject Child(GameObject parent, string name)
            => Child(parent.transform, name);

        private static void EnsureDirectory(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
