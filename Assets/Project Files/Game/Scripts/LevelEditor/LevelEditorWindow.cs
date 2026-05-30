#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter.Editor
{
    /// <summary>
    /// Single unified Level Editor window.
    /// Menu: BlockShooter / Level Editor
    ///
    /// Left panel  — level metadata, conveyor settings, prefab slots.
    /// Right panel — interactive shooter grid + conveyor group list + Generate button.
    ///
    /// "Generate Level Prefab" creates a fully self-contained prefab at
    /// Assets/Levels/Level_XXX.prefab with every sub-system wired inside it.
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        // ── Constants ─────────────────────────────────────────────────────────
        private const string SavePath  = "Assets/Levels/";
        private const float  CellPx    = 52f;
        private const float  CellPad   = 3f;
        private const float  LeftWidth = 220f;

        // ── Level metadata ────────────────────────────────────────────────────
        private string          _levelName  = "Level 1";
        private int             _levelIndex = 1;
        private LevelDifficulty _difficulty = LevelDifficulty.Normal;
        private LevelGoalType   _goalType   = LevelGoalType.ClearAllBlocks;
        private int             _goalAmount = 0;

        // ── Conveyor settings ─────────────────────────────────────────────────
        private float _conveyorSpeed = 1.5f;
        private int   _laneCount     = 5;
        private float _laneSpacing   = 0.22f;
        private float _rowSpacing    = 0.22f;

        // ── Shooter grid ──────────────────────────────────────────────────────
        private int   _gridCols     = 4;
        private int   _gridRows     = 2;
        private float _gridCellSize = 1.2f;
        private int   _defaultShots = 3;

        private enum CellType { Empty, ShooterBlock, Door }

        private CellType[,]       _cellTypes;
        private BlockColorType[,] _cellColors;
        private int[,]            _cellShots;  // -1 = use default
        private int[,]            _doorCount;

        // ── Conveyor groups ───────────────────────────────────────────────────
        private class ConveyorGroupDef
        {
            public BlockColorType color    = BlockColorType.Red;
            public int            rowCount = 20;
            public int            lanes    = 5;
        }

        private readonly List<ConveyorGroupDef> _groups = new();

        // ── Prefab references ─────────────────────────────────────────────────
        private GameObject _shooterBlockPrefab;
        private GameObject _wallElementPrefab;
        private GameObject _conveyorBlockPrefab;
        private GameObject _slotIndicatorPrefab;
        private GameObject _trackSegmentPrefab;
        private GameObject _arrowPrefab;

        // ── UI state ──────────────────────────────────────────────────────────
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;

        // ── Block color palette ───────────────────────────────────────────────
        private static readonly Color[] BlockColors =
        {
            new Color(0.90f, 0.20f, 0.20f), // Red
            new Color(0.20f, 0.50f, 0.90f), // Blue
            new Color(0.20f, 0.80f, 0.30f), // Green
            new Color(0.95f, 0.80f, 0.10f), // Yellow
            new Color(0.90f, 0.50f, 0.10f), // Orange
            new Color(0.70f, 0.20f, 0.90f), // Purple
        };

        private static Color GetBlockColor(BlockColorType t)
        {
            int i = (int)t;
            return i >= 0 && i < BlockColors.Length ? BlockColors[i] : Color.gray;
        }

        // ── Menu entry ────────────────────────────────────────────────────────

        [MenuItem("BlockShooter/Level Editor", false, 10)]
        public static void Open()
        {
            var w = GetWindow<LevelEditorWindow>("Level Editor");
            w.minSize = new Vector2(700, 500);
            w.Show();
        }

        private void OnEnable()
        {
            RebuildGrid();
            if (_groups.Count == 0)
            {
                _groups.Add(new ConveyorGroupDef { color = BlockColorType.Red,  rowCount = 20, lanes = 5 });
                _groups.Add(new ConveyorGroupDef { color = BlockColorType.Blue, rowCount = 20, lanes = 5 });
            }
        }

        // ── GUI ───────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawToolbar();
            GUILayout.BeginHorizontal();
            DrawLeftPanel();
            DrawDivider();
            DrawRightPanel();
            GUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("BLOCK SHOOTER  —  LEVEL EDITOR", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ── Left panel ─────────────────────────────────────────────────────────

        private void DrawLeftPanel()
        {
            _leftScroll = GUILayout.BeginScrollView(_leftScroll,
                GUILayout.Width(LeftWidth), GUILayout.ExpandHeight(true));

            DrawSection("LEVEL");
            _levelName  = EditorGUILayout.TextField("Name",       _levelName);
            _levelIndex = EditorGUILayout.IntField ("Index",      _levelIndex);
            _difficulty = (LevelDifficulty)EditorGUILayout.EnumPopup("Difficulty", _difficulty);
            _goalType   = (LevelGoalType)  EditorGUILayout.EnumPopup("Goal",       _goalType);
            if (_goalType != LevelGoalType.ClearAllBlocks)
                _goalAmount = EditorGUILayout.IntField("Amount", _goalAmount);

            DrawSection("CONVEYOR");
            _conveyorSpeed = EditorGUILayout.FloatField("Speed",        _conveyorSpeed);
            _laneCount     = EditorGUILayout.IntField  ("Lanes",        _laneCount);
            _laneSpacing   = EditorGUILayout.FloatField("Lane Spacing", _laneSpacing);
            _rowSpacing    = EditorGUILayout.FloatField("Row Spacing",  _rowSpacing);

            DrawSection("GRID");
            EditorGUI.BeginChangeCheck();
            _gridCols     = EditorGUILayout.IntField  ("Columns",      _gridCols);
            _gridRows     = EditorGUILayout.IntField  ("Rows",         _gridRows);
            _gridCellSize = EditorGUILayout.FloatField("Cell Size",    _gridCellSize);
            _defaultShots = EditorGUILayout.IntField  ("Default Shots",_defaultShots);
            if (EditorGUI.EndChangeCheck()) RebuildGrid();

            DrawSection("PREFABS");
            _shooterBlockPrefab  = ObjectField("Shooter Block",  _shooterBlockPrefab);
            _wallElementPrefab   = ObjectField("Wall Element",   _wallElementPrefab);
            _conveyorBlockPrefab = ObjectField("Conveyor Block", _conveyorBlockPrefab);
            _slotIndicatorPrefab = ObjectField("Slot Indicator", _slotIndicatorPrefab);
            _trackSegmentPrefab  = ObjectField("Track Segment",  _trackSegmentPrefab);
            _arrowPrefab         = ObjectField("Track Arrow",    _arrowPrefab);

            GUILayout.EndScrollView();
        }

        private static GameObject ObjectField(string label, GameObject current)
            => (GameObject)EditorGUILayout.ObjectField(label, current, typeof(GameObject), false);

        // ── Right panel ────────────────────────────────────────────────────────

        private void DrawRightPanel()
        {
            _rightScroll = GUILayout.BeginScrollView(_rightScroll,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            DrawSection("SHOOTER GRID  (LMB = cycle type  |  RMB = color / shots)");
            DrawShooterGrid();

            DrawSection("CONVEYOR GROUPS");
            DrawConveyorGroups();

            GUILayout.Space(12);
            DrawGenerateButton();

            GUILayout.EndScrollView();
        }

        // ── Shooter grid ───────────────────────────────────────────────────────

        private void DrawShooterGrid()
        {
            if (_cellTypes == null) RebuildGrid();

            GUILayout.BeginVertical();
            for (int row = _gridRows - 1; row >= 0; row--)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < _gridCols; col++)
                    DrawGridCell(col, row);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private void DrawGridCell(int col, int row)
        {
            CellType       type = _cellTypes[col, row];
            BlockColorType clr  = _cellColors[col, row];

            Color bg = type == CellType.Empty
                ? new Color(0.25f, 0.25f, 0.28f)
                : GetBlockColor(clr);
            if (type == CellType.Door)
                bg = Color.Lerp(bg, Color.black, 0.45f);

            string label = type == CellType.Empty ? ""
                         : type == CellType.Door  ? $"Door\n{_doorCount[col, row]}"
                         : $"{clr}\n{GetShotsLabel(col, row)}";

            Rect r = GUILayoutUtility.GetRect(
                CellPx + CellPad, CellPx + CellPad,
                GUILayout.Width(CellPx + CellPad));
            r = new Rect(r.x + CellPad * 0.5f, r.y + CellPad * 0.5f, CellPx, CellPx);

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = bg;
            GUI.Box(r, label, EditorStyles.helpBox);
            GUI.backgroundColor = prev;

            Event e = Event.current;
            if (!r.Contains(e.mousePosition)) return;

            if (e.type == EventType.MouseDown && e.button == 0) { CycleCell(col, row); e.Use(); Repaint(); }
            else if (e.type == EventType.MouseDown && e.button == 1) { ShowCellMenu(col, row); e.Use(); }
        }

        private string GetShotsLabel(int col, int row)
            => _cellShots[col, row] < 0 ? $"×{_defaultShots}" : $"×{_cellShots[col, row]}";

        private void CycleCell(int col, int row)
        {
            _cellTypes[col, row] = _cellTypes[col, row] switch
            {
                CellType.Empty        => CellType.ShooterBlock,
                CellType.ShooterBlock => CellType.Door,
                _                     => CellType.Empty,
            };
        }

        private void ShowCellMenu(int col, int row)
        {
            var menu = new GenericMenu();
            menu.AddDisabledItem(new GUIContent($"Cell ({col}, {row})"));
            menu.AddSeparator("");

            foreach (BlockColorType ct in System.Enum.GetValues(typeof(BlockColorType)))
            {
                var captured = ct;
                menu.AddItem(new GUIContent("Color/" + ct), _cellColors[col, row] == ct,
                    () => { _cellColors[col, row] = captured; Repaint(); });
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Shots/Default"), _cellShots[col, row] < 0,
                () => { _cellShots[col, row] = -1; Repaint(); });
            for (int s = 1; s <= 10; s++)
            {
                int shots = s;
                menu.AddItem(new GUIContent($"Shots/{s}"), _cellShots[col, row] == shots,
                    () => { _cellShots[col, row] = shots; Repaint(); });
            }

            if (_cellTypes[col, row] == CellType.Door)
            {
                menu.AddSeparator("");
                for (int d = 1; d <= 10; d++)
                {
                    int n = d;
                    menu.AddItem(new GUIContent($"Door Blocks/{d}"), _doorCount[col, row] == n,
                        () => { _doorCount[col, row] = n; Repaint(); });
                }
            }

            menu.ShowAsContext();
        }

        // ── Conveyor groups ────────────────────────────────────────────────────

        private void DrawConveyorGroups()
        {
            for (int i = _groups.Count - 1; i >= 0; i--)
            {
                var g = _groups[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = GetBlockColor(g.color);
                GUILayout.Box("", GUILayout.Width(16), GUILayout.Height(16));
                GUI.backgroundColor = prev;

                g.color    = (BlockColorType)EditorGUILayout.EnumPopup(g.color, GUILayout.Width(80));
                GUILayout.Label("Rows",  GUILayout.Width(36));
                g.rowCount = EditorGUILayout.IntField(g.rowCount, GUILayout.Width(38));
                GUILayout.Label("Lanes", GUILayout.Width(38));
                g.lanes    = EditorGUILayout.IntField(g.lanes,    GUILayout.Width(30));

                if (GUILayout.Button("×", GUILayout.Width(22)))
                    _groups.RemoveAt(i);

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Group"))
                _groups.Add(new ConveyorGroupDef());
        }

        // ── Generate button ────────────────────────────────────────────────────

        private void DrawGenerateButton()
        {
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.45f);
            if (GUILayout.Button("GENERATE LEVEL PREFAB", GUILayout.Height(38)))
                GeneratePrefab();
            GUI.backgroundColor = Color.white;
        }

        // ── Helper drawing ─────────────────────────────────────────────────────

        private void RebuildGrid()
        {
            _gridCols = Mathf.Max(1, _gridCols);
            _gridRows = Mathf.Max(1, _gridRows);

            var oldTypes  = _cellTypes;
            var oldColors = _cellColors;
            var oldShots  = _cellShots;
            var oldDoor   = _doorCount;

            _cellTypes  = new CellType[_gridCols, _gridRows];
            _cellColors = new BlockColorType[_gridCols, _gridRows];
            _cellShots  = new int[_gridCols, _gridRows];
            _doorCount  = new int[_gridCols, _gridRows];

            for (int c = 0; c < _gridCols; c++)
            for (int r = 0; r < _gridRows; r++)
            {
                _cellShots[c, r] = -1;
                _doorCount[c, r] = 3;

                if (oldTypes == null || c >= oldTypes.GetLength(0) || r >= oldTypes.GetLength(1)) continue;
                _cellTypes[c, r]  = oldTypes[c, r];
                _cellColors[c, r] = oldColors[c, r];
                _cellShots[c, r]  = oldShots[c, r];
                _doorCount[c, r]  = oldDoor[c, r];
            }
        }

        private static void DrawSection(string title)
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            var r = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.4f));
            GUILayout.Space(4);
        }

        private static void DrawDivider()
        {
            var r = GUILayoutUtility.GetRect(1, float.MaxValue,
                GUILayout.Width(1), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(r, new Color(0.3f, 0.3f, 0.3f));
        }

        // ── Prefab generation ──────────────────────────────────────────────────

        private void GeneratePrefab()
        {
            EnsureDirectory(SavePath.TrimEnd('/'));

            string prefabName = $"Level_{_levelIndex:000}";
            string prefabPath = SavePath + prefabName + ".prefab";

            var root = new GameObject(prefabName);

            // LevelRoot
            var lr = root.AddComponent<LevelRoot>();
            lr.levelIndex              = _levelIndex;
            lr.levelName               = _levelName;
            lr.difficulty              = _difficulty;
            lr.goalType                = _goalType;
            lr.goalAmount              = _goalAmount;
            lr.conveyorSpeedMultiplier = _conveyorSpeed;

            // Track (SplineContainer + ConveyorController)
            var trackGo = CreateChild(root.transform, "Track");
            var splineContainer = trackGo.AddComponent<SplineContainer>();
            BuildDefaultSpline(splineContainer);
            var convCtrl = trackGo.AddComponent<ConveyorController>();
            convCtrl.speed = 1.5f;
            lr.conveyorController = convCtrl;

            if (_trackSegmentPrefab != null)
            {
                var rend = trackGo.AddComponent<ConveyorTrackRenderer>();
                rend.segmentPrefab = _trackSegmentPrefab;
                if (_arrowPrefab != null) rend.arrowPrefab = _arrowPrefab;
            }

            // Block Groups under Track
            var groupsParent = CreateChild(trackGo.transform, "Groups");
            foreach (var gd in _groups)
            {
                var groupGo = CreateChild(groupsParent.transform, $"Group_{gd.color}");
                var bg = groupGo.AddComponent<BlockGroup>();
                bg.colorType   = gd.color;
                bg.rowCount    = gd.rowCount;
                bg.laneCount   = gd.lanes;
                bg.laneSpacing = _laneSpacing;
                bg.rowSpacing  = _rowSpacing;

                if (_conveyorBlockPrefab != null)
                    BuildBlockChildren(groupGo.transform, gd);
            }

            // FireRange
            var frGo = CreateChild(root.transform, "FireRange");
            frGo.transform.localPosition = new Vector3(0f, 0.5f, 2f);
            var frCol = frGo.AddComponent<BoxCollider>();
            frCol.isTrigger = true;
            frCol.size = new Vector3(6f, 2f, 3f);
            lr.fireRange = frGo.AddComponent<FireRange>();

            // SlotDeck
            var slotDeckGo = CreateChild(root.transform, "SlotDeck");
            slotDeckGo.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            var slotSys = slotDeckGo.AddComponent<SlotSystem>();
            slotSys.slotIndicatorPrefab = _slotIndicatorPrefab;
            lr.slotSystem = slotSys;

            float totalSlotW = (_gridCols - 1) * _gridCellSize;
            for (int i = 0; i < _gridCols; i++)
            {
                var slotGo = CreateChild(slotDeckGo.transform, $"Slot_{i}");
                slotGo.transform.localPosition =
                    new Vector3(-totalSlotW * 0.5f + i * _gridCellSize, 0f, 0f);
            }

            // ShooterGrid
            var sgGo = CreateChild(root.transform, "ShooterGrid");
            sgGo.transform.localPosition = new Vector3(0f, 0f, -2f);
            var sg = sgGo.AddComponent<ShooterGrid>();
            sg.shooterBlockPrefab = _shooterBlockPrefab != null
                ? _shooterBlockPrefab.GetComponent<ShooterBlock>() : null;
            lr.shooterGrid = sg;

            float halfW = (_gridCols - 1) * _gridCellSize * 0.5f;
            float halfD = (_gridRows - 1) * _gridCellSize * 0.5f;

            for (int r = 0; r < _gridRows; r++)
            for (int c = 0; c < _gridCols; c++)
            {
                var cellPos = new Vector3(
                    -halfW + c * _gridCellSize,
                    0f,
                    -halfD + r * _gridCellSize);

                string cellName = $"Cell_r{r}_c{c}";

                switch (_cellTypes[c, r])
                {
                    case CellType.ShooterBlock when _shooterBlockPrefab != null:
                    {
                        var cellGo = (GameObject)PrefabUtility.InstantiatePrefab(
                            _shooterBlockPrefab, sgGo.transform);
                        cellGo.name = cellName;
                        cellGo.transform.localPosition = cellPos;
                        var sb = cellGo.GetComponent<ShooterBlock>();
                        if (sb != null)
                        {
                            int shots = _cellShots[c, r] >= 0 ? _cellShots[c, r] : _defaultShots;
                            sb.EditorSetup(_cellColors[c, r], shots, c, r);
                        }
                        break;
                    }
                    case CellType.Door:
                    {
                        var cellGo = CreateChild(sgGo.transform, cellName);
                        cellGo.transform.localPosition = cellPos;
                        var door = cellGo.AddComponent<BlockDoor>();
                        door.blockCount  = _doorCount[c, r];
                        door.spawnColors = new List<BlockColorType> { _cellColors[c, r] };
                        break;
                    }
                    case CellType.Empty when _wallElementPrefab != null:
                    {
                        var cellGo = (GameObject)PrefabUtility.InstantiatePrefab(
                            _wallElementPrefab, sgGo.transform);
                        cellGo.name = cellName;
                        cellGo.transform.localPosition = cellPos;
                        cellGo.GetComponent<WallElement>()?.SetGridPosition(c, r);
                        break;
                    }
                }
            }

            // Ground
            var groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundGo.name = "Ground";
            groundGo.transform.SetParent(root.transform, false);
            groundGo.transform.localPosition = new Vector3(0f, -0.01f, 0f);
            groundGo.transform.localScale    = new Vector3(1.5f, 1f, 1.5f);
            Object.DestroyImmediate(groundGo.GetComponent<MeshCollider>());

            // Save prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool ok);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (ok)
            {
                Debug.Log($"[LevelEditor] Saved: {prefabPath}");
                EditorUtility.DisplayDialog("Done", $"Prefab saved:\n{prefabPath}", "OK");
                Selection.activeObject = prefab;
            }
            else
            {
                Debug.LogError($"[LevelEditor] Failed to save: {prefabPath}");
                EditorUtility.DisplayDialog("Error", $"Failed to save:\n{prefabPath}", "OK");
            }
        }

        private void BuildBlockChildren(Transform parent, ConveyorGroupDef gd)
        {
            for (int row = 0; row < gd.rowCount; row++)
            {
                var rowGo = CreateChild(parent, $"Row_{row}");
                for (int lane = 0; lane < gd.lanes; lane++)
                {
                    var blockGo = (GameObject)PrefabUtility.InstantiatePrefab(
                        _conveyorBlockPrefab, rowGo.transform);
                    blockGo.name = $"Block_{lane}";
                    blockGo.transform.localPosition = Vector3.zero;
                    blockGo.GetComponent<ConveyorBlock3D>()?.SetGroupIndex(row, lane);
                }
            }
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void BuildDefaultSpline(SplineContainer container)
        {
            var spline = container.Spline;
            spline.Clear();
            spline.Add(new BezierKnot(new Unity.Mathematics.float3(-3f, 0f,  0f)));
            spline.Add(new BezierKnot(new Unity.Mathematics.float3( 0f, 0f,  5f)));
            spline.Add(new BezierKnot(new Unity.Mathematics.float3( 3f, 0f,  0f)));
            spline.Add(new BezierKnot(new Unity.Mathematics.float3( 0f, 0f, -5f)));
            spline.Closed = true;
        }

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
