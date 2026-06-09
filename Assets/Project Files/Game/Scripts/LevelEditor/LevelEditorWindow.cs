#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace BlockShooter.Editor
{
    public class LevelEditorWindow : EditorWindow
    {
        // ── Layout ────────────────────────────────────────────────────────────
        private const float ListW    = 180f;
        private const float RightW   = 250f;
        private const float CellSize = 48f;
        private const float CellGap  = 4f;
        private const int   MaxCols  = 7;
        private const int   MaxRows  = 6;

        // FireRange world Z — all splines pass through this point
        private const float FIRE_Z = 0.0f;

        // ── Config ────────────────────────────────────────────────────────────
        private LevelEditorConfig _cfg;
        private GameConfig        _gameCfg;
        private UnityEditorInternal.ReorderableList _levelList;
        private SerializedObject _gameCfgSerialized;

        private SerializedObject _windowSerialized;
        private UnityEditorInternal.ReorderableList _groupsList;
        private Dictionary<BranchPathData, UnityEditorInternal.ReorderableList> _branchGroupsLists = new();
        private int _groupIndexToRemoveDeferred = -1;
        private (BranchPathData branch, int index) _branchGroupIndexToRemoveDeferred = (null, -1);
        private int _branchIndexToRemoveDeferred = -1;
        private int _branchMirrorIndexDeferred   = -1;
        private List<int> _selectedKnots = new();
        [SerializeField] private bool _snapToGrid = false;
        [SerializeField] private float _snapSize = 0.5f;

        // ── Level list ────────────────────────────────────────────────────────
        private List<string> _paths  = new();
        private List<string> _labels = new();
        [SerializeField] private int _activeIdx = -1;

        // ── Design data ───────────────────────────────────────────────────────
        private int           _levelIndex  = 1;
        private string        _levelName   = "Level 1";
        private bool          _isHardLevel = false;
        private LevelGoalType _goalType    = LevelGoalType.ClearAllBlocks;
        private int           _goalAmount  = 0;

        private int   _gridCols = 4, _gridRows = 2;
        private GridCellType[,]   _type;
        private BlockColorType[,] _color;
        private int[,]            _shots, _doors, _freezeCount;

        [SerializeField] private List<LevelConveyorGroup> _groups = new();
        [SerializeField] private List<BranchPathData> _branches = new();
        [SerializeField] private float _openZoneHalfT = 0.08f;

        // ── Spline ────────────────────────────────────────────────────────────
        [SerializeField] private List<Vector3>     _knots        = new();
        [SerializeField] private List<Vector3>     _tangentsIn   = new();
        [SerializeField] private List<Vector3>     _tangentsOut  = new();
        [SerializeField] private List<TangentMode> _tangentModes = new();
        private float             _splineWidth = 3.5f;
        private float             _splineDepth = 5f;
        private int               _splinePreset = 0;  // 0=Oval 1=Wide 2=Rectangle
        private int               _editingBranchIndex = -1; // -1 = main spline, >=0 = branch index

        // Safe-area guide toggle
        private bool _showSafeArea = true;

        // Spline edit state
        private bool         _editingSpline = false;
        private bool         _isDraggingSpline = false;
        private bool         _addKnotMode   = false;
        private int          _selKnot       = -1;
        private GameObject   _previewGo     = null; // lightweight spline preview only
        private GameObject   _levelPreviewGo = null; // scene preview of selected level prefab

        // Spline edit cancel backup (restored when user presses ✕)
        private List<Vector3>     _splineEditBackupKnots  = null;
        private List<Vector3>     _splineEditBackupTanIn  = null;
        private List<Vector3>     _splineEditBackupTanOut = null;
        private List<TangentMode> _splineEditBackupModes  = null;

        // Preset undo backup (restored via "↩ Restore" button)
        private List<Vector3>     _presetBackupKnots  = null;
        private List<Vector3>     _presetBackupTanIn  = null;
        private List<Vector3>     _presetBackupTanOut = null;
        private List<TangentMode> _presetBackupModes  = null;

        // Main spline backup when editing branch spline
        private List<Vector3>     _mainSplineKnotsBackup  = null;
        private List<Vector3>     _mainSplineTanInBackup  = null;
        private List<Vector3>     _mainSplineTanOutBackup = null;
        private List<TangentMode> _mainSplineModesBackup  = null;

        // Copy
        private int _copyIdx = 0;

        // ── Cell selection ────────────────────────────────────────────────────
        private int _selC = -1, _selR = -1;

        // ── Scroll ────────────────────────────────────────────────────────────
        private Vector2 _listScroll, _midScroll;

        // ── Change tracking ───────────────────────────────────────────────────
        [SerializeField] private bool _isDirty = false;

        // ── Foldout states ────────────────────────────────────────────────────
        private bool _foldSpline  = true;
        private bool _foldGrid    = true;
        private bool _foldGroups  = true;
        private bool _foldBranch  = false;
        private bool _foldAdvancedSpline = false;

        // ── Color palette ─────────────────────────────────────────────────────
        private Color PC(BlockColorType t)
        {
            if (_gameCfg != null)
            {
                return _gameCfg.GetColor(t);
            }
            return Color.white;
        }

        private (BlockColorType t, Color c, string n)[] GetActiveColors()
        {
            if (_gameCfg != null && _gameCfg.colors != null && _gameCfg.colors.Count > 0)
            {
                var list = new List<(BlockColorType, Color, string)>();
                foreach (var def in _gameCfg.colors)
                {
                    if (def == null) continue;
                    list.Add((def.colorType, _gameCfg.GetColor(def.colorType), def.displayName));
                }
                return list.ToArray();
            }

            return new[]
            {
                (BlockColorType.Red,    new Color(.90f,.20f,.20f), "Red"   ),
                (BlockColorType.Blue,   new Color(.20f,.50f,.90f), "Blue"  ),
                (BlockColorType.Green,  new Color(.20f,.80f,.30f), "Green" ),
                (BlockColorType.Yellow, new Color(1.00f,.85f,.10f),"Yellow"),
                (BlockColorType.Purple, new Color(.60f,.20f,.90f), "Purple"),
                (BlockColorType.Orange, new Color(1.00f,.55f,.10f),"Orange"),
            };
        }

        private BlockColorType DrawColorPopup(BlockColorType selected, GUILayoutOption option)
        {
            var pal = GetActiveColors();
            string[] names = pal.Select(x => x.n).ToArray();
            int index = System.Array.FindIndex(pal, x => x.t == selected);
            if (index < 0) index = 0;

            int newIndex = EditorGUILayout.Popup(index, names, option);
            return pal[newIndex].t;
        }

        private BlockColorType DrawColorPopup(Rect rect, BlockColorType selected)
        {
            var pal = GetActiveColors();
            string[] names = pal.Select(x => x.n).ToArray();
            int index = System.Array.FindIndex(pal, x => x.t == selected);
            if (index < 0) index = 0;

            int newIndex = EditorGUI.Popup(rect, index, names);
            return pal[newIndex].t;
        }

        // ── Menu ──────────────────────────────────────────────────────────────
        [MenuItem("BlockShooter/Level Editor", false, 10)]
        public static void Open()
        {
            var w = GetWindow<LevelEditorWindow>("Level Editor");
            w.minSize = new Vector2(820, 560);
            w.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            LoadCfg();
            RefreshList();

            _branchGroupsLists.Clear();
            _selectedKnots.Clear();
            _windowSerialized = new SerializedObject(this);

            if (_activeIdx >= _paths.Count)
                _activeIdx = _paths.Count - 1;

            if (_activeIdx < 0 && _paths.Count > 0)
                _activeIdx = 0;

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (_activeIdx >= 0 && _activeIdx < _paths.Count)
                {
                    LoadLevel(_activeIdx);
                }
                else
                {
                    if (_type == null) InitGrid();
                    if (_knots.Count == 0) ApplyPreset();
                    if (_groups.Count == 0) DefaultGroups();
                    _isDirty = false;
                }
            }
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            DestroyPreview();
            DestroyLevelPreview();
            _branchGroupsLists.Clear();
            _selectedKnots.Clear();
            _windowSerialized = null;
            _groupsList = null;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                DestroyPreview();
                DestroyLevelPreview();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (_activeIdx >= 0 && _activeIdx < _paths.Count)
                {
                    LoadLevel(_activeIdx);
                }
            }
        }

        private void OnUndoRedo()
        {
            EnsureTangentLists();
            SyncPreviewSpline();
            Repaint();
        }

        // ── Load config ───────────────────────────────────────────────────────
        private void LoadCfg()
        {
            var g = AssetDatabase.FindAssets("t:LevelEditorConfig");
            _cfg = g.Length > 0
                ? AssetDatabase.LoadAssetAtPath<LevelEditorConfig>(
                      AssetDatabase.GUIDToAssetPath(g[0]))
                : null;

            var gc = AssetDatabase.FindAssets("t:GameConfig");
            _gameCfg = gc.Length > 0
                ? AssetDatabase.LoadAssetAtPath<GameConfig>(
                      AssetDatabase.GUIDToAssetPath(gc[0]))
                : null;
        }

        private void RefreshList()
        {
            _paths.Clear(); _labels.Clear();
            if (_cfg == null) return;

            SyncLevelsFromFolder();

            if (_gameCfg != null && _gameCfg.levelSequence != null)
            {
                foreach (var lr in _gameCfg.levelSequence.levelPrefabs)
                {
                    if (lr == null) continue;
                    string path = AssetDatabase.GetAssetPath(lr);
                    if (string.IsNullOrEmpty(path)) continue;
                    _paths.Add(path);
                    _labels.Add(lr.name);
                }
            }

            InitReorderableList();
            if (_levelList != null) _levelList.index = _activeIdx;
        }

        private void SyncLevelsFromFolder()
        {
            if (_cfg == null || _gameCfg == null || _gameCfg.levelSequence == null) return;

            // 1. Clean up missing/null references
            _gameCfg.levelSequence.levelPrefabs.RemoveAll(x => x == null);

            // 2. Scan the save folder for prefabs containing LevelRoot component
            string folder = _cfg.levelSavePath.TrimEnd('/').Replace('\\', '/');
            var foundPrefabs = new List<LevelRoot>();
            foreach (var gid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(gid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var lr = go != null ? go.GetComponent<LevelRoot>() : null;
                if (lr != null) foundPrefabs.Add(lr);
            }

            bool changed = false;

            // 3. Add newly created level prefabs that aren't in the list
            foreach (var lr in foundPrefabs)
            {
                if (!_gameCfg.levelSequence.levelPrefabs.Contains(lr))
                {
                    _gameCfg.levelSequence.levelPrefabs.Add(lr);
                    changed = true;
                }
            }

            // 4. Remove level prefabs that no longer exist in the directory
            for (int i = _gameCfg.levelSequence.levelPrefabs.Count - 1; i >= 0; i--)
            {
                if (!foundPrefabs.Contains(_gameCfg.levelSequence.levelPrefabs[i]))
                {
                    _gameCfg.levelSequence.levelPrefabs.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(_gameCfg.levelSequence);
                EditorUtility.SetDirty(_gameCfg);
                AssetDatabase.SaveAssets();
            }
        }

        private void InitReorderableList()
        {
            if (_gameCfg == null || _gameCfg.levelSequence == null)
            {
                _gameCfgSerialized = null;
                _levelList = null;
                return;
            }

            _gameCfgSerialized = new SerializedObject(_gameCfg.levelSequence);
            var prop = _gameCfgSerialized.FindProperty("levelPrefabs");

            _levelList = new UnityEditorInternal.ReorderableList(_gameCfgSerialized, prop, true, false, false, false);
            _levelList.headerHeight = 0f;
            _levelList.footerHeight = 0f;
            _levelList.elementHeight = 24f;

            _levelList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                if (index < 0 || index >= _paths.Count) return;

                bool active = _activeIdx == index;

                // Subtract space for custom duplicate/delete buttons on the right
                float mainW = rect.width - 48f;

                // Draw background selection highlight
                bool isHard = false;
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(_paths[index]);
                if (prefabAsset != null)
                {
                    var lrComp = prefabAsset.GetComponent<LevelRoot>();
                    if (lrComp != null)
                    {
                        isHard = lrComp.isHardLevel;
                    }
                }

                if (active)
                {
                    EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y, rect.width + 4, rect.height), new Color(.4f, .65f, 1f, 0.25f));
                }
                else if (isHard)
                {
                    EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y, rect.width + 4, rect.height), new Color(1f, 0.3f, 0.3f, 0.15f));
                }

                // Draw Level Name Label
                Rect labelRect = new Rect(rect.x, rect.y + 2, mainW, rect.height - 4);
                string label = _labels[index];
                
                GUIStyle style = new GUIStyle(EditorStyles.label);
                if (active)
                {
                    style.fontStyle = FontStyle.Bold;
                    style.normal.textColor = new Color(0.2f, 0.55f, 1f);
                }

                if (GUI.Button(labelRect, label, style))
                {
                    _activeIdx = index;
                    _levelList.index = index;
                    LoadLevel(index);
                }

                // Custom Duplicate / Delete Buttons
                var dupIcon = EditorGUIUtility.IconContent("d_TreeEditor.Duplicate");
                GUIContent dupContent = (dupIcon != null && dupIcon.image != null) ? dupIcon : new GUIContent("⊕");
                
                Rect dupRect = new Rect(rect.x + mainW + 2, rect.y + 1, 20, rect.height - 2);
                Rect delRect = new Rect(rect.x + mainW + 24, rect.y + 1, 20, rect.height - 2);

                GUI.backgroundColor = new Color(.55f, .75f, .4f);
                if (GUI.Button(dupRect, dupContent))
                {
                    DuplicateLevel(index);
                }

                GUI.backgroundColor = new Color(.9f, .3f, .3f);
                if (GUI.Button(delRect, "✕"))
                {
                    DeleteLevel(index);
                }
                GUI.backgroundColor = Color.white;
            };

            _levelList.onReorderCallbackWithDetails = (UnityEditorInternal.ReorderableList list, int oldIndex, int newIndex) =>
            {
                _gameCfgSerialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(_gameCfg);
                AssetDatabase.SaveAssets();

                // Sync activeIdx
                if (_activeIdx == oldIndex) _activeIdx = newIndex;
                else if (oldIndex < newIndex)
                {
                    if (_activeIdx > oldIndex && _activeIdx <= newIndex) _activeIdx--;
                }
                else if (oldIndex > newIndex)
                {
                    if (_activeIdx >= newIndex && _activeIdx < oldIndex) _activeIdx++;
                }

                RefreshList();
                list.index = _activeIdx;
            };
        }

        private void DefaultGroups()
        {
            int rc = _cfg?.rowsPerGroup ?? 20;
            int lc = _cfg?.laneCount ?? 5;
            _groups.Add(new LevelConveyorGroup { color = BlockColorType.Red,  rowCount = rc, laneCount = lc });
            _groups.Add(new LevelConveyorGroup { color = BlockColorType.Blue, rowCount = rc, laneCount = lc });
        }

        // ═════════════════════════════════════════════════════════════════════
        //  ANA GUI
        // ═════════════════════════════════════════════════════════════════════
        private void OnGUI()
        {
            if (_cfg == null) { DrawNoCfg(); return; }

            // ── Ctrl+S shortcut ───────────────────────────────────────────────
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.S
                && e.control && !e.alt && !e.shift)
            {
                if (_isDirty && _activeIdx >= 0)
                {
                    SavePrefab();
                    e.Use();
                }
            }

            // ── Update window title with dirty indicator ──────────────────────
            string desiredTitle = _isDirty ? "Level Editor ●" : "Level Editor";
            if (titleContent.text != desiredTitle)
                titleContent.text = desiredTitle;

            if (_gameCfgSerialized != null) _gameCfgSerialized.Update();
            
            if (_windowSerialized == null)
            {
                _windowSerialized = new SerializedObject(this);
            }
            _windowSerialized.Update();
            SetupGroupsList();

            DrawToolbar();

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                GUILayout.Space(12);
                EditorGUILayout.HelpBox("Level Editor is disabled during Play Mode.", MessageType.Info);
                return;
            }

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
            DrawLeft();
            VDiv();
            DrawCenter();
            if (_activeIdx >= 0)
            {
                VDiv();
                DrawRight();
            }
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                _isDirty = true;
            }

            if (_windowSerialized.ApplyModifiedProperties())
            {
                _isDirty = true;
                Repaint();
            }
        }

        private void DrawNoCfg()
        {
            GUILayout.Space(30);
            EditorGUILayout.HelpBox(
                "LevelEditorConfig not found.\nAssets → Create → BlockShooter → Level Editor Config",
                MessageType.Warning);
            if (GUILayout.Button("Refresh", GUILayout.Height(28))) { LoadCfg(); RefreshList(); }
        }

        // ── Toolbar ───────────────────────────────────────────────────────────
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("  BLOCK SHOOTER — LEVEL EDITOR", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Config",  EditorStyles.toolbarButton, GUILayout.Width(50)))
                Selection.activeObject = _cfg;
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(55)))
            { LoadCfg(); RefreshList(); }
            GUILayout.Space(4);
            EditorGUILayout.EndHorizontal();
        }

        // ── Left panel ────────────────────────────────────────────────────────
        private void DrawLeft()
        {
            EditorGUI.BeginDisabledGroup(_editingSpline);
            EditorGUILayout.BeginVertical(GUILayout.Width(ListW), GUILayout.ExpandHeight(true));

            // Active prefab reference — always at top, click to ping/select in Project
            if (_activeIdx >= 0 && _activeIdx < _paths.Count)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_paths[_activeIdx]);
                EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
                GUILayout.Space(3);
            }

            Hdr("LEVELS");
            GUI.backgroundColor = new Color(.45f,.85f,.5f);
            if (GUILayout.Button("+ New Level", GUILayout.Height(26))) NewLevel();
            GUI.backgroundColor = Color.white;
            GUILayout.Space(3);

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            if (_levelList != null)
            {
                _levelList.DoLayoutList();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
            EditorGUI.EndDisabledGroup();
        }

        // ── Center panel ──────────────────────────────────────────────────────
        private void DrawCenter()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (_activeIdx < 0)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.HelpBox("Please select or create a level from the left panel to begin editing.", MessageType.Info);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            // Reserve space for toolbar (~21px) and footer buttons (~52px)
            const float toolbarH = 21f;
            const float footerH  = 52f;
            float scrollH = Mathf.Max(80f, position.height - toolbarH - footerH - 10f);

            _midScroll = EditorGUILayout.BeginScrollView(_midScroll,
                GUILayout.ExpandWidth(true), GUILayout.Height(scrollH));

            // ── Foldout: Track Spline ────────────────────────────────────────
            _foldSpline = EditorGUILayout.Foldout(_foldSpline, "▸  TRACK SPLINE", true, EditorStyles.foldoutHeader);
            if (_foldSpline) DrawSplineSection();

            GUILayout.Space(2);

            // ── Foldout: Shooter Grid ────────────────────────────────────────
            EditorGUI.BeginDisabledGroup(_editingSpline);
            _foldGrid = EditorGUILayout.Foldout(_foldGrid, "▸  SHOOTER GRID", true, EditorStyles.foldoutHeader);
            if (_foldGrid) DrawGridSection();

            GUILayout.Space(2);

            // ── Foldout: Conveyor Groups ─────────────────────────────────────
            _foldGroups = EditorGUILayout.Foldout(_foldGroups, "▸  CONVEYOR GROUPS", true, EditorStyles.foldoutHeader);
            if (_foldGroups) DrawGroupsSection();
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(2);

            // ── Foldout: Branch Conveyors ────────────────────────────────────
            _foldBranch = EditorGUILayout.Foldout(_foldBranch, "▸  BRANCH CONVEYORS", true, EditorStyles.foldoutHeader);
            if (_foldBranch) DrawBranchesSection();

            EditorGUILayout.EndScrollView();

            // ── Action buttons — always visible below scroll ──────────────────
            GUILayout.Space(6);
            EditorGUI.BeginDisabledGroup(_editingSpline);
            EditorGUILayout.BeginHorizontal();
            
            // Save prefab button is disabled when there are no changes to save
            EditorGUI.BeginDisabledGroup(!_isDirty);
            GUI.backgroundColor = new Color(.3f,.85f,.45f);
            if (GUILayout.Button("  ✓  SAVE PREFAB  (Ctrl+S)  ", GUILayout.Height(34)))
                SavePrefab();
            EditorGUI.EndDisabledGroup();

            GUI.backgroundColor = new Color(.25f,.6f,1f);
            if (GUILayout.Button("  ▶  TEST IN SCENE  ", GUILayout.Height(34)))
                TestInScene();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SPLINE SECTION
        // ═════════════════════════════════════════════════════════════════════
        private void DrawSplineSection()
        {
            bool editingMain = _editingSpline && _editingBranchIndex < 0;
            if (!editingMain)
            {
                EditorGUI.BeginDisabledGroup(_editingSpline); // Disable button if currently editing a branch
                GUI.backgroundColor = new Color(.4f,.7f,1f);
                if (GUILayout.Button("✏  Edit Spline  (Scene View)", GUILayout.Height(26)))
                    StartSplineEdit();
                GUI.backgroundColor = Color.white;
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                // Active mode: info + exit button
                EditorGUILayout.HelpBox(
                    "● Drag knots in the Scene View\n" +
                    "● Shift+Click = add knot\n" +
                    "● Ctrl+Click = select multiple knots\n" +
                    "● Delete = remove selected knot (min 3)",
                    MessageType.None);

                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(.3f,.85f,.45f);
                if (GUILayout.Button("✓  Done Editing", GUILayout.Height(26)))
                    StopSplineEdit(save: true);
                GUI.backgroundColor = new Color(.9f,.35f,.35f);
                if (GUILayout.Button("✕", GUILayout.Width(30), GUILayout.Height(26)))
                    StopSplineEdit(save: false);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField($"  Knots: {_knots.Count}", EditorStyles.miniLabel);
            }

            GUILayout.Space(4);

            // Advanced options foldout (Copy Spline + Safe Area)
            _foldAdvancedSpline = EditorGUILayout.Foldout(_foldAdvancedSpline, "Advanced", true);
            if (_foldAdvancedSpline)
            {
                EditorGUI.indentLevel++;

                // Copy spline
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Copy from:", GUILayout.Width(62));
                if (_labels.Count > 0)
                {
                    string[] copyOptions = _labels.ToArray();
                    _copyIdx = Mathf.Clamp(_copyIdx, 0, copyOptions.Length - 1);
                    _copyIdx = EditorGUILayout.Popup(_copyIdx, copyOptions);
                    if (GUILayout.Button("Copy", GUILayout.Width(50), GUILayout.Height(18)))
                        CopySplineFrom(_paths[_copyIdx]);
                }
                else
                {
                    EditorGUILayout.LabelField("(no saved levels)", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();

                // Safe-area toggle
                _showSafeArea = EditorGUILayout.ToggleLeft("Show Safe Area Guide", _showSafeArea, GUILayout.Width(180));

                EditorGUI.indentLevel--;
            }

            GUILayout.Space(6);
        }

        // ── Start spline edit mode ────────────────────────────────────────────
        private void StartSplineEdit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            _selC = -1;
            _selR = -1;

            // Point references to active target
            if (_editingBranchIndex >= 0)
            {
                _mainSplineKnotsBackup = new List<Vector3>(_knots);
                _mainSplineTanInBackup = new List<Vector3>(_tangentsIn);
                _mainSplineTanOutBackup = new List<Vector3>(_tangentsOut);
                _mainSplineModesBackup = new List<TangentMode>(_tangentModes);

                var branch = _branches[_editingBranchIndex];
                _knots = branch.splineKnots;
                _tangentsIn = branch.splineTangentsIn;
                _tangentsOut = branch.splineTangentsOut;
                _tangentModes = branch.splineTangentModes.Select(m => (TangentMode)m).ToList();
            }

            // Save state so ✕ Cancel can restore exactly what we started with
            EnsureTangentLists();
            _splineEditBackupKnots  = new List<Vector3>(_knots);
            _splineEditBackupTanIn  = new List<Vector3>(_tangentsIn);
            _splineEditBackupTanOut = new List<Vector3>(_tangentsOut);
            _splineEditBackupModes  = new List<TangentMode>(_tangentModes);

            _editingSpline = true;
            _selKnot       = -1;
            _addKnotMode   = false;

            if (_editingBranchIndex < 0 && _levelPreviewGo != null)
            {
                var mainTrack = _levelPreviewGo.transform.Find("ConveyorSystem/Track");
                if (mainTrack != null)
                {
                    mainTrack.gameObject.SetActive(false);
                }
            }
            else if (_levelPreviewGo != null)
            {
                var branch = _branches[_editingBranchIndex];
                var branchTransform = _levelPreviewGo.transform.Find("ConveyorSystem/Branches/" + branch.branchName);
                if (branchTransform != null)
                {
                    branchTransform.gameObject.SetActive(false);
                }
            }
            DestroyPreview();

            // Lightweight preview: just a Track GO with SplineContainer and Mesh Builder
            _previewGo = new GameObject("[SplinePreview]");
            var track = new GameObject("Track");
            track.transform.SetParent(_previewGo.transform, false);
            track.transform.localPosition = new Vector3(0f, 0f, 0.0f);
            var sc = track.AddComponent<SplineContainer>();
            
            float trackRailHeight = _cfg.railHeight;
            if (_editingBranchIndex >= 0)
            {
                WriteKnotsToContainer(sc, _knots, _tangentsIn, _tangentsOut, _tangentModes.Select(m => (int)m).ToList(), trackRailHeight, 0f);
            }
            else
            {
                WriteKnotsToContainer(sc, trackRailHeight, 0f);
            }

            var meshBuilder = track.AddComponent<ConveyorTrackMeshBuilder>();
            meshBuilder.resolution    = _cfg.trackResolution;
            meshBuilder.beltHalfWidth = _cfg.beltHalfWidth;
            meshBuilder.wallAboveBelt = _cfg.wallAboveBelt;
            meshBuilder.railHeight    = trackRailHeight;
            meshBuilder.railWidth     = _cfg.railWidth;
            meshBuilder.bevelSize     = _cfg.trackBevelSize;

            if (_editingBranchIndex >= 0)
            {
                // Editor preview: leave mainTrackSpline null so Sweep() uses the simple
                // distance-based fallback trim instead of IsRingFullyInsideConveyor().
                // The spline-tangent-based "right" direction is unreliable in the editor
                // (e.g. at mergeT=0.5 the track tangent points sideways, making
                // worldMergeRight face forward/backward instead of left/right — this
                // causes all branch rings to be classified as "inside" and removed,
                // producing an empty mesh). The distance fallback is accurate enough
                // for interactive editing; the final saved prefab uses full trimming.
                meshBuilder.trimBranchEnd    = true;
                meshBuilder.openZoneEnabled  = false;
                // mainTrackSpline intentionally NOT set → distance-check fallback active
            }
            else
            {
                meshBuilder.openZoneEnabled = true;
                meshBuilder.openZoneHalfT   = _openZoneHalfT;
            }

            var mr = track.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterials = new Material[]
                {
                    _cfg.trackSideMaterial,
                    _cfg.trackBeltMaterial,
                };
            }

            meshBuilder.BuildMesh();

            // Select track and frame it in the scene
            Selection.activeGameObject = track;
            SceneView.lastActiveSceneView?.FrameSelected();
            SceneView.RepaintAll();
        }

        private void StopSplineEdit(bool save)
        {
            _editingSpline = false;
            _addKnotMode   = false;

            if (!save && _splineEditBackupKnots != null)
            {
                // Cancel: restore state from before edit started
                if (_editingBranchIndex >= 0)
                {
                    var branch = _branches[_editingBranchIndex];
                    branch.splineKnots = _splineEditBackupKnots;
                    branch.splineTangentsIn = _splineEditBackupTanIn;
                    branch.splineTangentsOut = _splineEditBackupTanOut;
                    branch.splineTangentModes = _splineEditBackupModes.Select(m => (int)m).ToList();
                }
                else
                {
                    _knots        = _splineEditBackupKnots;
                    _tangentsIn   = _splineEditBackupTanIn;
                    _tangentsOut  = _splineEditBackupTanOut;
                    _tangentModes = _splineEditBackupModes;
                    EnsureTangentLists();
                }
            }
            else if (save && _editingBranchIndex >= 0)
            {
                var branch = _branches[_editingBranchIndex];
                branch.splineTangentModes = _tangentModes.Select(m => (int)m).ToList();
            }

            // Restore main spline lists from backups when editing is finished
            if (_mainSplineKnotsBackup != null)
            {
                _knots = _mainSplineKnotsBackup;
                _tangentsIn = _mainSplineTanInBackup;
                _tangentsOut = _mainSplineTanOutBackup;
                _tangentModes = _mainSplineModesBackup;

                _mainSplineKnotsBackup = null;
                _mainSplineTanInBackup = null;
                _mainSplineTanOutBackup = null;
                _mainSplineModesBackup = null;
            }

            _editingBranchIndex = -1;
            _splineEditBackupKnots = null;

            DestroyPreview();
            SceneView.RepaintAll();
            Repaint();

            if (save)
            {
                SavePrefab();
            }
            else
            {
                if (_activeIdx >= 0 && _activeIdx < _paths.Count)
                {
                    ShowLevelPreview(_paths[_activeIdx]);
                }
            }
        }

        private void DeselectIfSelected(GameObject target)
        {
            if (target == null) return;

            // Check if active selection is the target or a child of target
            if (Selection.activeGameObject != null && 
                (Selection.activeGameObject == target || Selection.activeGameObject.transform.IsChildOf(target.transform)))
            {
                Selection.activeObject = null;
            }

            // Also check multiple selection in Selection.objects
            if (Selection.objects != null && Selection.objects.Length > 0)
            {
                var newSelection = new List<UnityEngine.Object>();
                bool changed = false;
                foreach (var obj in Selection.objects)
                {
                    if (obj is GameObject go && (go == target || go.transform.IsChildOf(target.transform)))
                    {
                        changed = true;
                    }
                    else if (obj != null)
                    {
                        newSelection.Add(obj);
                    }
                }
                if (changed)
                {
                    Selection.objects = newSelection.ToArray();
                }
            }
        }

        private void DestroyPreview()
        {
            if (_previewGo != null)
            {
                DeselectIfSelected(_previewGo);
                DestroyImmediate(_previewGo);
                _previewGo = null;
            }

            // Clean up any orphaned spline preview objects in the active scene (e.g. after domain reload)
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.isLoaded)
            {
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    if (root != null && (root.name == "[SplinePreview]" || root.name.Contains("[SplinePreview]")))
                    {
                        DeselectIfSelected(root);
                        DestroyImmediate(root);
                    }
                }
            }
        }

        private void ShowLevelPreview(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            DestroyLevelPreview();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;

            _levelPreviewGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (_levelPreviewGo == null) return;
            _levelPreviewGo.name = "[LevelPreview]";
            _levelPreviewGo.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            foreach (Transform t in _levelPreviewGo.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;

            // Scene view auto-focus/framing is disabled to avoid camera jumps when selecting a level
            SceneView.RepaintAll();
        }

        private void DestroyLevelPreview()
        {
            if (_levelPreviewGo != null)
            {
                DeselectIfSelected(_levelPreviewGo);
                DestroyImmediate(_levelPreviewGo);
                _levelPreviewGo = null;
            }

            // Clean up any orphaned level preview objects in the active scene (including inactive/hidden ones)
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.isLoaded)
            {
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    if (root == null) continue;
                    if (root.name == "[LevelPreview]" || root.name.Contains("[LevelPreview]"))
                    {
                        DeselectIfSelected(root);
                        DestroyImmediate(root);
                    }
                    else
                    {
                        var lrs = root.GetComponentsInChildren<LevelRoot>(true);
                        if (lrs.Length > 0 && !EditorUtility.IsPersistent(root))
                        {
                            DeselectIfSelected(root);
                            DestroyImmediate(root);
                        }
                    }
                }
            }
        }

        // ── Scene View handles ────────────────────────────────────────────────
        private void OnSceneGUI(SceneView sv)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (_activeIdx < 0) return;

            Event e = Event.current;
            if (_editingSpline)
            {
                if (e.rawType == EventType.MouseDrag && e.button == 0)
                {
                    _isDraggingSpline = true;
                }
                else if (e.rawType == EventType.MouseUp && e.button == 0)
                {
                    if (_isDraggingSpline)
                    {
                        _isDraggingSpline = false;
                        // Rebuild high quality mesh immediately on release
                        SyncPreviewSpline();
                    }
                }
            }
            else
            {
                _isDraggingSpline = false;
            }

            // Always draw guides and curve preview
            DrawSceneGuides();
            DrawSplineCurveHandles();

            if (_editingSpline)
            {
                HandleKnots(sv);
                sv.Repaint();
            }
            else if (_knots.Count >= 1)
            {
                // Show non-interactive knot dots so designer can see the shape
                Handles.color = new Color(.6f, .85f, 1f, .6f);
                foreach (var k in _knots)
                {
                    float sz = HandleUtility.GetHandleSize(k) * .07f;
                    Handles.SphereHandleCap(0, k, Quaternion.identity, sz, EventType.Repaint);
                }
            }
        }

        private void DrawSceneGuides()
        {
            if (_cfg == null) return;
            float cs = _cfg.gridCellSize;

            // Safe area guide
            if (_showSafeArea) DrawSafeAreaGuide();

            // Slot indicators (yellow)
            int slots = _cfg.slotCount;
            float tw = (slots - 1) * _cfg.slotSpacing;
            Handles.color = new Color(1f, .9f, .1f, .8f);
            for (int i = 0; i < slots; i++)
            {
                var p = new Vector3(-tw * .5f + i * _cfg.slotSpacing, 0f, -1.5f);
                float sz = HandleUtility.GetHandleSize(p) * .12f;
                Handles.SphereHandleCap(0, p, Quaternion.identity, sz, EventType.Repaint);
            }

            // Grid cell outlines
            if (_type == null) return;
            float hw = (_gridCols - 1) * cs * .5f;
            for (int r = 0; r < _gridRows; r++)
            for (int c = 0; c < _gridCols; c++)
            {
                var pos = new Vector3(-hw + c * cs, 0f, -2.5f + (r - _gridRows + 0.5f) * cs);
                Color col = _type[c, r] == GridCellType.Empty
                    ? new Color(.3f, .3f, .3f, .25f)
                    : new Color(PC(_color[c, r]).r, PC(_color[c, r]).g, PC(_color[c, r]).b, .55f);
                Handles.color = col;
                Handles.DrawWireCube(pos, new Vector3(cs * .85f, .05f, cs * .85f));
            }
        }

        private void DrawSafeAreaGuide()
        {
            // Try to project camera viewport corners onto Y=0 plane
            Vector3[] corners = new Vector3[4];
            bool usedCamera   = false;

            Camera cam = Camera.main;
            if (cam != null)
            {
                // Portrait safe-area approximation: 5% top, 8% bottom (notch / home bar)
                const float safeT = 0.92f, safeB = 0.05f, safeL = 0.02f, safeR = 0.98f;
                var vp = new Vector3[]
                {
                    new(safeL, safeB, 0), new(safeR, safeB, 0),
                    new(safeR, safeT, 0), new(safeL, safeT, 0),
                };
                bool ok = true;
                for (int i = 0; i < 4; i++)
                {
                    Ray ray = cam.ViewportPointToRay(vp[i]);
                    if (Mathf.Abs(ray.direction.y) < 0.0001f) { ok = false; break; }
                    float t = -ray.origin.y / ray.direction.y;
                    if (t < 0f) { ok = false; break; }
                    corners[i] = ray.origin + ray.direction * t;
                }
                usedCamera = ok;
            }

            if (!usedCamera)
            {
                // Fallback: fixed portrait rectangle centred on the gameplay area (9:16 ratio at 4 wide)
                float hw = 2f;
                float front = FIRE_Z + 1f;
                float back  = FIRE_Z - 6.1f;  // 9:16 * 4 ≈ 7.1
                corners[0] = new Vector3(-hw, 0, back);
                corners[1] = new Vector3(+hw, 0, back);
                corners[2] = new Vector3(+hw, 0, front);
                corners[3] = new Vector3(-hw, 0, front);
            }

            Color fill    = new Color(.15f, 1f, .55f, .04f);
            Color outline = new Color(.15f, 1f, .55f, .60f);
            Handles.DrawSolidRectangleWithOutline(corners, fill, outline);

            // Corner tick marks
            Handles.color = outline;
            float tickLen = 0.25f;
            void Tick(Vector3 a, Vector3 b, Vector3 c2)
            {
                Handles.DrawLine(a + (b - a).normalized * tickLen, a);
                Handles.DrawLine(a, a + (c2 - a).normalized * tickLen);
            }
            Tick(corners[0], corners[1], corners[3]);
            Tick(corners[1], corners[0], corners[2]);
            Tick(corners[2], corners[3], corners[1]);
            Tick(corners[3], corners[2], corners[0]);

            GUIStyle lbl = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(.2f, 1f, .6f, .9f) } };
            Handles.Label(corners[3] + Vector3.right * .1f, "SAFE AREA", lbl);
        }

        private void HandleKnots(SceneView sv)
        {
            if (_knots.Count == 0) return;

            Event e = Event.current;

            // Shift+Click → yeni knot ekle
            bool shiftHeld = e.shift;
            if (shiftHeld && e.type == EventType.MouseDown && e.button == 0)
            {
                Vector3 hitPos = GetMouseGroundHit(e.mousePosition);
                if (hitPos != Vector3.positiveInfinity)
                {
                    if (_editingBranchIndex >= 0)
                    {
                        hitPos = SnapToMainSpline(hitPos);
                    }
                    Undo.RegisterCompleteObjectUndo(this, "Add Spline Knot");
                    int insertAt = FindInsertIndex(hitPos);
                    InsertKnot(insertAt, hitPos);
                    _selKnot = insertAt;
                    _selectedKnots.Clear();
                    _selectedKnots.Add(insertAt);
                    SyncPreviewSpline();
                    e.Use(); Repaint(); return;
                }
            }

            // Delete → remove selected knot
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete)
            {
                if (_selKnot > 0 && _knots.Count > 3)
                {
                    Undo.RegisterCompleteObjectUndo(this, "Delete Spline Knot");
                    RemoveKnot(_selKnot);
                    _selectedKnots.Remove(_selKnot);
                    _selKnot = Mathf.Min(_selKnot, _knots.Count - 1);
                    SyncPreviewSpline();
                    e.Use(); Repaint(); return;
                }
            }

            EnsureTangentLists();

            // Handle per knot
            for (int i = 0; i < _knots.Count; i++)
            {
                bool isAnchor = (i == 0);
                bool isSel    = (_selectedKnots.Contains(i) || _selKnot == i);
                float sz = HandleUtility.GetHandleSize(_knots[i]) * (isSel ? .18f : .13f);

                Handles.color = isAnchor ? new Color(1f, .4f, .4f, .95f)
                              : isSel    ? new Color(1f, .95f, .2f, .95f)
                              :            new Color(.9f, .9f, .9f, .8f);

                // Screen-space click detection — fires before FreeMoveHandle so single clicks register
                if (e.type == EventType.MouseDown && e.button == 0 && !e.shift)
                {
                    Vector2 screenPt = HandleUtility.WorldToGUIPoint(_knots[i]);
                    if (Vector2.Distance(screenPt, e.mousePosition) < 20f)
                    {
                        if (e.control || e.command)
                        {
                            if (_selectedKnots.Contains(i))
                                _selectedKnots.Remove(i);
                            else
                                _selectedKnots.Add(i);
                        }
                        else
                        {
                            _selectedKnots.Clear();
                            _selectedKnots.Add(i);
                        }
                        _selKnot = i;
                        Repaint();
                        // Don't e.Use() — let FreeMoveHandle also respond for dragging
                    }
                }

                // Drag to move
                if (!shiftHeld)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 np = Handles.FreeMoveHandle(_knots[i], sz * 1.1f,
                        Vector3.zero, Handles.SphereHandleCap);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(this, "Move Spline Knot");
                        np.y = 0f;
                        if (isAnchor && _editingBranchIndex < 0) np.z = FIRE_Z;
                        if (_editingBranchIndex >= 0)
                        {
                            np = SnapToMainSpline(np, i == _knots.Count - 1);
                        }

                        // Apply grid snapping
                        if (_snapToGrid)
                        {
                            np.x = Mathf.Round(np.x / _snapSize) * _snapSize;
                            if (!isAnchor || _editingBranchIndex >= 0)
                            {
                                np.z = Mathf.Round(np.z / _snapSize) * _snapSize;
                            }
                        }

                        Vector3 delta = np - _knots[i];

                        if (_selectedKnots.Contains(i))
                        {
                            // Move all selected knots by delta
                            for (int idx = 0; idx < _knots.Count; idx++)
                            {
                                if (!_selectedKnots.Contains(idx)) continue;

                                Vector3 targetPos = _knots[idx] + delta;
                                targetPos.y = 0f;
                                bool isTargetAnchor = (idx == 0);
                                if (isTargetAnchor && _editingBranchIndex < 0) targetPos.z = FIRE_Z;

                                if (_snapToGrid)
                                {
                                    targetPos.x = Mathf.Round(targetPos.x / _snapSize) * _snapSize;
                                    if (!isTargetAnchor || _editingBranchIndex >= 0)
                                    {
                                        targetPos.z = Mathf.Round(targetPos.z / _snapSize) * _snapSize;
                                    }
                                }
                                _knots[idx] = targetPos;
                            }
                        }
                        else
                        {
                            _knots[i] = np;
                        }

                        _selKnot = i;
                        SyncPreviewSpline();
                        Repaint();
                    }
                }

                // Tangent handles — only for selected knot in non-AutoSmooth mode
                if (isSel && i < _tangentModes.Count && _tangentModes[i] != TangentMode.AutoSmooth)
                {
                    Vector3 kpos = _knots[i];

                    // TangentIn — orange
                    Vector3 inWorld = kpos + _tangentsIn[i];
                    inWorld.y = 0f;
                    Handles.color = new Color(1f, .55f, .1f, .9f);
                    Handles.DrawLine(kpos, inWorld, 1.5f);
                    float tsz = HandleUtility.GetHandleSize(inWorld) * .10f;
                    EditorGUI.BeginChangeCheck();
                    Vector3 newIn = Handles.FreeMoveHandle(inWorld, tsz, Vector3.zero, Handles.DotHandleCap);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(this, "Modify Tangent In");
                        newIn.y = 0f;
                        _tangentsIn[i] = newIn - kpos;
                        if (_tangentModes[i] == TangentMode.Mirrored || _tangentModes[i] == TangentMode.Continuous)
                            _tangentsOut[i] = -_tangentsIn[i];
                        SyncPreviewSpline();
                    }

                    // TangentOut — cyan
                    Vector3 outWorld = kpos + _tangentsOut[i];
                    outWorld.y = 0f;
                    Handles.color = new Color(.1f, .9f, .9f, .9f);
                    Handles.DrawLine(kpos, outWorld, 1.5f);
                    EditorGUI.BeginChangeCheck();
                    Vector3 newOut = Handles.FreeMoveHandle(outWorld, tsz, Vector3.zero, Handles.DotHandleCap);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(this, "Modify Tangent Out");
                        newOut.y = 0f;
                        _tangentsOut[i] = newOut - kpos;
                        if (_tangentModes[i] == TangentMode.Mirrored || _tangentModes[i] == TangentMode.Continuous)
                            _tangentsIn[i] = -_tangentsOut[i];
                        SyncPreviewSpline();
                    }
                }
            }

        }

        private void DrawSplineCurveHandles()
        {
            if (_knots.Count < 2) return;
            EnsureTangentLists();
            Handles.color = new Color(.35f, .65f, 1f, .85f);

            bool isOpen = _editingBranchIndex >= 0;
            int count = isOpen ? _knots.Count - 1 : _knots.Count;

            for (int i = 0; i < count; i++)
            {
                int nxt, prv, nxt2;
                if (isOpen)
                {
                    nxt = i + 1;
                    prv = i == 0 ? i : i - 1;
                    nxt2 = nxt == _knots.Count - 1 ? nxt : nxt + 1;
                }
                else
                {
                    nxt  = (i + 1) % _knots.Count;
                    prv  = (i - 1 + _knots.Count) % _knots.Count;
                    nxt2 = (nxt + 1) % _knots.Count;
                }

                bool iAutoSmooth   = _tangentModes[i]   == TangentMode.AutoSmooth;
                bool nxtAutoSmooth = _tangentModes[nxt] == TangentMode.AutoSmooth;

                // Control points — use explicit tangents when not AutoSmooth, else Catmull-Rom approx
                Vector3 ctrl0 = iAutoSmooth
                    ? _knots[i]   + (_knots[nxt]  - _knots[prv])  * .33f
                    : _knots[i]   + _tangentsOut[i];

                Vector3 ctrl1 = nxtAutoSmooth
                    ? _knots[nxt] - (_knots[nxt2] - _knots[i])    * .33f
                    : _knots[nxt] + _tangentsIn[nxt];

                Handles.DrawBezier(_knots[i], _knots[nxt],
                    ctrl0, ctrl1,
                    new Color(.35f, .65f, 1f, .85f), null, 2.5f);
            }
        }

        // ── Spline helpers ────────────────────────────────────────────────────
        private void ApplyPreset()
        {
            Undo.RegisterCompleteObjectUndo(this, "Apply Spline Preset");

            // Save current spline as backup so "↩ Restore" can undo accidental preset clicks
            if (_knots.Count >= 3)
            {
                EnsureTangentLists();
                _presetBackupKnots  = new List<Vector3>(_knots);
                _presetBackupTanIn  = new List<Vector3>(_tangentsIn);
                _presetBackupTanOut = new List<Vector3>(_tangentsOut);
                _presetBackupModes  = new List<TangentMode>(_tangentModes);
            }

            float hw = _splineWidth * .5f;
            float fz = FIRE_Z;
            float d  = _splineDepth;

            _selectedKnots.Clear();
            _knots.Clear();
            _tangentsIn.Clear();
            _tangentsOut.Clear();
            _tangentModes.Clear();

            switch (_splinePreset)
            {
                case 0: // Oval (Perfect Ellipse)
                    {
                        float b = d * 0.5f;
                        float k = 0.5522847f; // Bezier curve constant for circle/ellipse
                        
                        _knots.Add(new Vector3(0f, 0f, fz));
                        _knots.Add(new Vector3(+hw, 0f, fz + b));
                        _knots.Add(new Vector3(0f, 0f, fz + d));
                        _knots.Add(new Vector3(-hw, 0f, fz + b));

                        // Knot 0 (Bottom Center)
                        _tangentsIn.Add(new Vector3(-hw * k, 0f, 0f));
                        _tangentsOut.Add(new Vector3(hw * k, 0f, 0f));
                        _tangentModes.Add(TangentMode.Mirrored);

                        // Knot 1 (Right Side)
                        _tangentsIn.Add(new Vector3(0f, 0f, -b * k));
                        _tangentsOut.Add(new Vector3(0f, 0f, b * k));
                        _tangentModes.Add(TangentMode.Mirrored);

                        // Knot 2 (Top Center)
                        _tangentsIn.Add(new Vector3(hw * k, 0f, 0f));
                        _tangentsOut.Add(new Vector3(-hw * k, 0f, 0f));
                        _tangentModes.Add(TangentMode.Mirrored);

                        // Knot 3 (Left Side)
                        _tangentsIn.Add(new Vector3(0f, 0f, b * k));
                        _tangentsOut.Add(new Vector3(0f, 0f, -b * k));
                        _tangentModes.Add(TangentMode.Mirrored);
                    }
                    break;

                case 1: // Wide Capsule / Stadium (Straight parallel sides + perfect circular caps)
                    {
                        float r = Mathf.Min(hw, d * 0.5f);
                        float k = 0.5522847f;
                        float straightHeight = d - 2 * r;

                        _knots.Add(new Vector3(0f, 0f, fz)); // 0: Bottom Center
                        _knots.Add(new Vector3(+hw, 0f, fz + r)); // 1: Bottom Right Cap End / Straight Start
                        _knots.Add(new Vector3(+hw, 0f, fz + d - r)); // 2: Straight End / Top Right Cap Start
                        _knots.Add(new Vector3(0f, 0f, fz + d)); // 3: Top Center
                        _knots.Add(new Vector3(-hw, 0f, fz + d - r)); // 4: Top Left Cap End / Straight Start
                        _knots.Add(new Vector3(-hw, 0f, fz + r)); // 5: Straight End / Bottom Left Cap Start

                        // Knot 0 (Bottom Center)
                        _tangentsIn.Add(new Vector3(-hw * k, 0f, 0f));
                        _tangentsOut.Add(new Vector3(hw * k, 0f, 0f));
                        _tangentModes.Add(TangentMode.Mirrored);

                        // Knot 1 (Bottom Right)
                        _tangentsIn.Add(new Vector3(0f, 0f, -r * k));
                        _tangentsOut.Add(new Vector3(0f, 0f, straightHeight * 0.33f)); // Points straight up
                        _tangentModes.Add(TangentMode.Broken);

                        // Knot 2 (Top Right)
                        _tangentsIn.Add(new Vector3(0f, 0f, -straightHeight * 0.33f)); // Points straight down
                        _tangentsOut.Add(new Vector3(0f, 0f, r * k));
                        _tangentModes.Add(TangentMode.Broken);

                        // Knot 3 (Top Center)
                        _tangentsIn.Add(new Vector3(hw * k, 0f, 0f));
                        _tangentsOut.Add(new Vector3(-hw * k, 0f, 0f));
                        _tangentModes.Add(TangentMode.Mirrored);

                        // Knot 4 (Top Left)
                        _tangentsIn.Add(new Vector3(0f, 0f, r * k));
                        _tangentsOut.Add(new Vector3(0f, 0f, -straightHeight * 0.33f)); // Points straight down
                        _tangentModes.Add(TangentMode.Broken);

                        // Knot 5 (Bottom Left)
                        _tangentsIn.Add(new Vector3(0f, 0f, straightHeight * 0.33f)); // Points straight up
                        _tangentsOut.Add(new Vector3(0f, 0f, -r * k));
                        _tangentModes.Add(TangentMode.Broken);
                    }
                    break;

                case 2: // Wavy Loop (Capsule loop with elegant waves on parallel sides)
                    _knots.Add(new Vector3(0f, 0f, fz));
                    _knots.Add(new Vector3(+hw * 0.8f, 0f, fz + d * 0.2f));
                    _knots.Add(new Vector3(+hw * 1.2f, 0f, fz + d * 0.5f));
                    _knots.Add(new Vector3(+hw * 0.8f, 0f, fz + d * 0.8f));
                    _knots.Add(new Vector3(0f, 0f, fz + d));
                    _knots.Add(new Vector3(-hw * 0.8f, 0f, fz + d * 0.8f));
                    _knots.Add(new Vector3(-hw * 1.2f, 0f, fz + d * 0.5f));
                    _knots.Add(new Vector3(-hw * 0.8f, 0f, fz + d * 0.2f));

                    for (int i = 0; i < 8; i++)
                    {
                        _tangentsIn.Add(Vector3.zero);
                        _tangentsOut.Add(Vector3.zero);
                        _tangentModes.Add(TangentMode.AutoSmooth);
                    }
                    break;

                case 3: // Heart Loop
                    _knots.Add(new Vector3(0f, 0f, fz));
                    _knots.Add(new Vector3(+hw * 0.9f, 0f, fz + d * 0.35f));
                    _knots.Add(new Vector3(+hw * 0.7f, 0f, fz + d * 0.85f));
                    _knots.Add(new Vector3(0f, 0f, fz + d * 0.65f));
                    _knots.Add(new Vector3(-hw * 0.7f, 0f, fz + d * 0.85f));
                    _knots.Add(new Vector3(-hw * 0.9f, 0f, fz + d * 0.35f));

                    for (int i = 0; i < 6; i++)
                    {
                        _tangentsIn.Add(Vector3.zero);
                        _tangentsOut.Add(Vector3.zero);
                        _tangentModes.Add(TangentMode.AutoSmooth);
                    }
                    break;
            }

            EnsureTangentLists();
            SyncPreviewSpline();
            SceneView.RepaintAll();
            // Frame scene so the new preset shape is immediately visible
            SceneView.lastActiveSceneView?.FrameSelected();
            Repaint();
            _isDirty = true;
        }

        private void CopySplineFrom(string srcPath)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
            if (go == null)
            {
                Debug.LogWarning("[LevelEditor] CopySplineFrom: prefab not found");
                return;
            }

            var lr = go.GetComponent<LevelRoot>();
            if (lr == null || lr.splineKnots.Count < 3)
            {
                Debug.LogWarning($"[LevelEditor] CopySplineFrom: no spline data saved in {srcPath}. Save the source level first.");
                return;
            }

            Undo.RegisterCompleteObjectUndo(this, "Copy Spline");

            int n = lr.splineKnots.Count;

            var newKnots  = new List<Vector3>(lr.splineKnots);

            // Copy tangents — fall back to zeroes if lists are mismatched (legacy prefab)
            var newTanIn  = lr.splineTangentsIn.Count  == n
                ? new List<Vector3>(lr.splineTangentsIn)
                : new List<Vector3>(new Vector3[n]);
            var newTanOut = lr.splineTangentsOut.Count == n
                ? new List<Vector3>(lr.splineTangentsOut)
                : new List<Vector3>(new Vector3[n]);
            var newModes  = lr.splineTangentModes.Count == n
                ? lr.splineTangentModes.Select(m => (TangentMode)m).ToList()
                : Enumerable.Repeat(TangentMode.AutoSmooth, n).ToList();

            // Shift all knots so knot[0] aligns to FIRE_Z (knot 0 is always the FireRange anchor)
            float zOffset = FIRE_Z - newKnots[0].z;
            for (int i = 0; i < n; i++)
                newKnots[i] = new Vector3(newKnots[i].x, 0f, newKnots[i].z + zOffset);
            newKnots[0] = new Vector3(newKnots[0].x, 0f, FIRE_Z); // exact lock

            _knots        = newKnots;
            _tangentsIn   = newTanIn;
            _tangentsOut  = newTanOut;
            _tangentModes = newModes;
            EnsureTangentLists();
            SyncPreviewSpline();
            SceneView.RepaintAll();
            Repaint();
            _isDirty = true;
        }

        private void SyncPreviewSpline()
        {
            if (_previewGo == null) return;
            var sc = _previewGo.GetComponentInChildren<SplineContainer>();
            if (sc != null)
            {
                float trackRailHeight = _cfg.railHeight;
                if (_editingBranchIndex >= 0)
                {
                    WriteKnotsToContainer(sc, _knots, _tangentsIn, _tangentsOut, _tangentModes.Select(m => (int)m).ToList(), trackRailHeight, 0f);
                }
                else
                {
                    WriteKnotsToContainer(sc, trackRailHeight, 0f);
                }

                var meshBuilder = _previewGo.GetComponentInChildren<ConveyorTrackMeshBuilder>();
                if (meshBuilder != null)
                {
                    meshBuilder.isDraggingInEditor = _isDraggingSpline;
                    if (_editingBranchIndex >= 0 && meshBuilder.mainTrackSpline != null)
                    {
                        var mainTrackSpline = meshBuilder.mainTrackSpline;
                        var mainTrack = mainTrackSpline.transform;
                        float mergeT = _branches[_editingBranchIndex].mergeT;
                        
                        mainTrackSpline.Spline.Evaluate(mergeT, out var mPos, out var mTan, out var mUp);
                        Vector3 worldMergePos = mainTrack.TransformPoint(mPos);
                        Vector3 worldMergeTan = mainTrack.TransformDirection((Vector3)mTan).normalized;
                        Vector3 worldMergeUp  = mainTrack.TransformDirection((Vector3)mUp).normalized;
                        if (worldMergeUp.sqrMagnitude < 0.001f) worldMergeUp = Vector3.up;
                        Vector3 worldMergeRight = Vector3.Cross(worldMergeUp, worldMergeTan).normalized;

                        if (_knots != null && _knots.Count >= 2)
                        {
                            Vector3 branchLast = _knots[_knots.Count - 1];
                            Vector3 branchSecondLast = _knots[_knots.Count - 2];
                            Vector3 toBranch = (branchSecondLast - branchLast).normalized;
                            float dot = Vector3.Dot(toBranch, worldMergeRight);
                            meshBuilder.branchOnRightSide = (dot >= 0f);
                        }
                    }

                    meshBuilder.resolution = _cfg.trackResolution;
                    meshBuilder.BuildMesh();
                }
            }
            SceneView.RepaintAll();
        }

        private void InsertKnot(int index, Vector3 pos)
        {
            _knots.Insert(index, pos);
            if (_tangentsIn.Count >= index) _tangentsIn.Insert(index, Vector3.zero);
            else _tangentsIn.Add(Vector3.zero);

            if (_tangentsOut.Count >= index) _tangentsOut.Insert(index, Vector3.zero);
            else _tangentsOut.Add(Vector3.zero);

            if (_tangentModes.Count >= index) _tangentModes.Insert(index, TangentMode.AutoSmooth);
            else _tangentModes.Add(TangentMode.AutoSmooth);

            EnsureTangentLists();
        }

        private void RemoveKnot(int index)
        {
            if (index < 0 || index >= _knots.Count) return;
            _knots.RemoveAt(index);
            if (index < _tangentsIn.Count) _tangentsIn.RemoveAt(index);
            if (index < _tangentsOut.Count) _tangentsOut.RemoveAt(index);
            if (index < _tangentModes.Count) _tangentModes.RemoveAt(index);
            EnsureTangentLists();
        }

        private void EnsureTangentLists()
        {
            while (_tangentsIn.Count < _knots.Count)   _tangentsIn.Add(Vector3.zero);
            while (_tangentsOut.Count < _knots.Count)  _tangentsOut.Add(Vector3.zero);
            while (_tangentModes.Count < _knots.Count) _tangentModes.Add(TangentMode.AutoSmooth);
            // Trim if knots were removed
            if (_tangentsIn.Count > _knots.Count)   _tangentsIn.RemoveRange(_knots.Count, _tangentsIn.Count - _knots.Count);
            if (_tangentsOut.Count > _knots.Count)  _tangentsOut.RemoveRange(_knots.Count, _tangentsOut.Count - _knots.Count);
            if (_tangentModes.Count > _knots.Count) _tangentModes.RemoveRange(_knots.Count, _tangentModes.Count - _knots.Count);
        }

        private void MakeSplineSymmetric()
        {
            if (_knots.Count < 3) return;
            Undo.RegisterCompleteObjectUndo(this, "Make Spline Symmetric");
            EnsureTangentLists();

            int N = _knots.Count;
            for (int i = 0; i <= N / 2; i++)
            {
                int opp = N - 1 - i;
                if (opp == i) continue;

                _knots[opp] = new Vector3(-_knots[i].x, 0f, _knots[i].z);
                _tangentsIn[opp] = new Vector3(-_tangentsOut[i].x, 0f, _tangentsOut[i].z);
                _tangentsOut[opp] = new Vector3(-_tangentsIn[i].x, 0f, _tangentsIn[i].z);
                _tangentModes[opp] = _tangentModes[i];
            }

            SyncPreviewSpline();
            SceneView.RepaintAll();
            Repaint();
            _isDirty = true;
        }

        private void FlipSplineHorizontally()
        {
            if (_knots.Count == 0) return;
            Undo.RegisterCompleteObjectUndo(this, "Flip Spline Horizontally");
            EnsureTangentLists();

            for (int i = 0; i < _knots.Count; i++)
            {
                _knots[i] = new Vector3(-_knots[i].x, 0f, _knots[i].z);
                _tangentsIn[i] = new Vector3(-_tangentsIn[i].x, 0f, _tangentsIn[i].z);
                _tangentsOut[i] = new Vector3(-_tangentsOut[i].x, 0f, _tangentsOut[i].z);
            }

            SyncPreviewSpline();
            SceneView.RepaintAll();
            Repaint();
            _isDirty = true;
        }

        private void WriteKnotsToContainer(SplineContainer sc, List<Vector3> knots, List<Vector3> tangentsIn, List<Vector3> tangentsOut, List<int> tangentModes, float yOffset = 0f, float zOffset = 0f)
        {
            var spline = sc.Spline;
            spline.Clear();
            for (int i = 0; i < knots.Count; i++)
            {
                var k = knots[i];
                var tanIn  = i < tangentsIn.Count ? (Unity.Mathematics.float3)(Vector3)tangentsIn[i] : Unity.Mathematics.float3.zero;
                var tanOut = i < tangentsOut.Count ? (Unity.Mathematics.float3)(Vector3)tangentsOut[i] : Unity.Mathematics.float3.zero;
                spline.Add(new BezierKnot(new Unity.Mathematics.float3(k.x, k.y + yOffset, k.z + zOffset), tanIn, tanOut));
            }
            spline.Closed = false;
            for (int i = 0; i < spline.Count; i++)
            {
                var mode = i < tangentModes.Count ? (TangentMode)tangentModes[i] : TangentMode.AutoSmooth;
                spline.SetTangentMode(i, mode);
            }
        }

        private void WriteKnotsToContainer(SplineContainer sc, float yOffset = 0f, float zOffset = 0f)
        {
            EnsureTangentLists();
            var spline = sc.Spline;
            spline.Clear();
            for (int i = 0; i < _knots.Count; i++)
            {
                var k = _knots[i];
                var tanIn  = (float3)(Vector3)_tangentsIn[i];
                var tanOut = (float3)(Vector3)_tangentsOut[i];
                spline.Add(new BezierKnot(new float3(k.x, k.y + yOffset, k.z + zOffset), tanIn, tanOut));
            }
            spline.Closed = true;
            for (int i = 0; i < spline.Count; i++)
                spline.SetTangentMode(i, _tangentModes[i]);
        }

        private void ReadKnotsFromContainer(SplineContainer sc)
        {
            var xform = sc.transform;
            _knots.Clear();
            _tangentsIn.Clear();
            _tangentsOut.Clear();
            _tangentModes.Clear();
            foreach (var k in sc.Spline)
            {
                Vector3 w = xform.TransformPoint(k.Position);
                w.y = 0f;
                _knots.Add(w);
                // Tangents are in local spline space — convert to world then back to Vector3
                Vector3 tIn  = xform.TransformVector((Vector3)(float3)k.TangentIn);
                Vector3 tOut = xform.TransformVector((Vector3)(float3)k.TangentOut);
                tIn.y  = 0f;
                tOut.y = 0f;
                _tangentsIn.Add(tIn);
                _tangentsOut.Add(tOut);
                _tangentModes.Add(TangentMode.AutoSmooth);
            }
            // Lock anchor Z
            if (_knots.Count > 0)
                _knots[0] = new Vector3(_knots[0].x, 0f, FIRE_Z);
            EnsureTangentLists();
        }

        // ── Mouse-to-ground ray ───────────────────────────────────────────────
        private static Vector3 GetMouseGroundHit(Vector2 mousePos)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
            if (Mathf.Abs(ray.direction.y) < 0.0001f) return Vector3.positiveInfinity;
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) return Vector3.positiveInfinity;
            return ray.origin + ray.direction * t;
        }

        private Vector3 SnapToMainSpline(Vector3 position, bool isConnectionKnot = false)
        {
            if (_levelPreviewGo == null) return position;

            var conveyorSys = _levelPreviewGo.transform.Find("ConveyorSystem");
            if (conveyorSys == null) return position;
            var mainTrack = conveyorSys.Find("Track");
            if (mainTrack == null) return position;
            var mainSplineContainer = mainTrack.GetComponent<SplineContainer>();
            if (mainSplineContainer == null || mainSplineContainer.Spline == null) return position;

            var mainTrackTransform = mainTrack.transform;
            Vector3 localPos = mainTrackTransform.InverseTransformPoint(position);

            SplineUtility.GetNearestPoint(
                mainSplineContainer.Spline,
                localPos,
                out var nearestLocal,
                out float t,
                8, // resolution
                4  // iterations
            );

            Vector3 nearestWorld = mainTrackTransform.TransformPoint((Vector3)nearestLocal);
            nearestWorld.y = 0f;

            if (Vector3.Distance(position, nearestWorld) < 0.6f)
            {
                if (isConnectionKnot && _editingBranchIndex >= 0 && _editingBranchIndex < _branches.Count)
                {
                    _branches[_editingBranchIndex].mergeT = t;

                    var lr = _levelPreviewGo.GetComponent<LevelRoot>();
                    if (lr != null && lr.branches != null && _editingBranchIndex < lr.branches.Count)
                    {
                        lr.branches[_editingBranchIndex].mergeT = t;
                        var mainTrackBuilder = mainTrack.GetComponent<ConveyorTrackMeshBuilder>();
                        if (mainTrackBuilder != null)
                        {
                            mainTrackBuilder.isDraggingInEditor = _isDraggingSpline;
                            mainTrackBuilder.BuildMesh();
                        }
                    }
                }
                return nearestWorld;
            }

            return position;
        }

        private float GetMainSplineLength()
        {
            if (_knots.Count < 2) return 0f;
            var tempSpline = new Spline();
            for (int i = 0; i < _knots.Count; i++)
            {
                var k = _knots[i];
                var tanIn  = i < _tangentsIn.Count ? (float3)(Vector3)_tangentsIn[i] : float3.zero;
                var tanOut = i < _tangentsOut.Count ? (float3)(Vector3)_tangentsOut[i] : float3.zero;
                tempSpline.Add(new BezierKnot((float3)k, tanIn, tanOut));
            }
            tempSpline.Closed = true;
            for (int i = 0; i < tempSpline.Count; i++)
            {
                var mode = i < _tangentModes.Count ? _tangentModes[i] : TangentMode.AutoSmooth;
                tempSpline.SetTangentMode(i, mode);
            }
            return SplineUtility.CalculateLength(tempSpline, Matrix4x4.identity);
        }

        private float GetBranchSplineLength(BranchPathData b)
        {
            if (b.splineKnots.Count < 2) return 0f;
            var tempSpline = new Spline();
            for (int i = 0; i < b.splineKnots.Count; i++)
            {
                var k = b.splineKnots[i];
                var tanIn  = i < b.splineTangentsIn.Count ? (float3)(Vector3)b.splineTangentsIn[i] : float3.zero;
                var tanOut = i < b.splineTangentsOut.Count ? (float3)(Vector3)b.splineTangentsOut[i] : float3.zero;
                tempSpline.Add(new BezierKnot((float3)k, tanIn, tanOut));
            }
            tempSpline.Closed = false;
            for (int i = 0; i < tempSpline.Count; i++)
            {
                var mode = i < b.splineTangentModes.Count ? (TangentMode)b.splineTangentModes[i] : TangentMode.AutoSmooth;
                tempSpline.SetTangentMode(i, mode);
            }
            return SplineUtility.CalculateLength(tempSpline, Matrix4x4.identity);
        }

        private int FindInsertIndex(Vector3 pos)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _knots.Count; i++)
            {
                int nxt = (i + 1) % _knots.Count;
                Vector3 mid = (_knots[i] + _knots[nxt]) * .5f;
                float d = Vector3.Distance(pos, mid);
                if (d < bestDist) { bestDist = d; best = nxt; }
            }
            return best;
        }

        private static Vector3[] GetWireCubeVerts(Vector3 center, Vector3 size)
        {
            float hx = size.x * .5f, hz = size.z * .5f;
            return new[]
            {
                center + new Vector3(-hx, 0, -hz),
                center + new Vector3(+hx, 0, -hz),
                center + new Vector3(+hx, 0, +hz),
                center + new Vector3(-hx, 0, +hz),
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        //  GRID SECTION
        // ═════════════════════════════════════════════════════════════════════
        private void DrawGridSection()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Cols", GUILayout.Width(42));
            EditorGUI.BeginChangeCheck();
            int nc = EditorGUILayout.IntSlider(_gridCols, 1, MaxCols);
            GUILayout.Label("Rows", GUILayout.Width(34));
            int nr = EditorGUILayout.IntSlider(_gridRows, 1, MaxRows);
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck() && (nc != _gridCols || nr != _gridRows))
            { 
                _gridCols = nc; 
                _gridRows = nr; 
                ResizeGrid(); 
                _isDirty = true;
            }

            GUILayout.Space(4);

            if (_type == null) InitGrid();

            for (int r = _gridRows - 1; r >= 0; r--)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(2);
                for (int c = 0; c < _gridCols; c++) DrawCell(c, r);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(3);
            EditorGUILayout.LabelField("  Left-click = select  |  Right-click = quick menu  |  Inspector in right panel",
                EditorStyles.miniLabel);

            // ── Bulk operations ────────────────────────────────────────────────
            GUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(.5f,.18f,.18f);
            if (GUILayout.Button("Clear All Cells", GUILayout.Height(20)))
            {
                if (EditorUtility.DisplayDialog("Clear All Cells",
                    "Are you sure you want to clear every cell in the grid?", "Clear", "Cancel"))
                {
                    Undo.RecordObject(this, "Clear All Grid Cells");
                    for (int c = 0; c < _gridCols; c++)
                        for (int r = 0; r < _gridRows; r++)
                            _type[c, r] = GridCellType.Empty;
                    _selC = -1; _selR = -1;
                    _isDirty = true; Repaint();
                }
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Fill All ▼", GUILayout.Height(20)))
            {
                var menu = new GenericMenu();
                var pal = GetActiveColors();
                foreach (var entry in pal)
                {
                    var colorType = entry.t;
                    menu.AddItem(new GUIContent(entry.n), false, () =>
                    {
                        Undo.RecordObject(this, "Fill All Cells");
                        for (int c = 0; c < _gridCols; c++)
                            for (int r = 0; r < _gridRows; r++)
                            {
                                _type[c, r] = GridCellType.ShooterBlock;
                                _color[c, r] = colorType;
                                if (_shots[c, r] <= 0) _shots[c, r] = 100;
                            }
                        _isDirty = true; Repaint();
                    });
                }
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);
        }

        private void DrawCell(int c, int r)
        {
            GridCellType   t   = _type[c, r];
            BlockColorType col = _color[c, r];
            bool           sel = _selC == c && _selR == r;

            // Background color
            Color bg = t == GridCellType.Empty
                ? (sel ? new Color(.26f,.26f,.30f) : new Color(.17f,.17f,.19f))
                : t == GridCellType.Door
                ? new Color(.58f,.60f,.65f)   // light gray for door
                : PC(col);

            // Labels
            string lbl1 = t == GridCellType.Empty ? "+"
                        : t == GridCellType.Door   ? "🚪"
                        : t == GridCellType.MysteryShooter ? "❓"
                        : t == GridCellType.FreezeShooter ? "❄"
                        : col.ToString().Substring(0, 3).ToUpper();
            string lbl2 = t == GridCellType.Empty ? ""
                        : t == GridCellType.Door   ? $"×{_doors[c,r]}"
                        : t == GridCellType.FreezeShooter ? $"×{_freezeCount[c,r]}"
                        : $"×{_shots[c,r]}";

            Rect outer = GUILayoutUtility.GetRect(
                CellSize + CellGap, CellSize + CellGap,
                GUILayout.Width(CellSize + CellGap),
                GUILayout.Height(CellSize + CellGap));
            Rect cell = new Rect(outer.x + CellGap*.5f, outer.y + CellGap*.5f, CellSize, CellSize);

            // Selection border
            Color borderCol = sel ? Color.white : new Color(.38f,.38f,.42f);
            EditorGUI.DrawRect(new Rect(cell.x-1,cell.y-1,cell.width+2,cell.height+2), borderCol);
            EditorGUI.DrawRect(cell, bg);

            // Cell label
            var st = new GUIStyle(EditorStyles.boldLabel)
                { alignment = TextAnchor.MiddleCenter, fontSize = t == GridCellType.Empty ? 18 : 10,
                  normal = { textColor = t == GridCellType.Empty ? new Color(.4f,.4f,.45f) : Color.white } };
            EditorGUI.LabelField(new Rect(cell.x, cell.y+2, cell.width, cell.height*.5f+2), lbl1, st);
            if (lbl2 != "")
            {
                st.fontSize = 9;
                st.normal.textColor = Color.white;
                EditorGUI.LabelField(new Rect(cell.x, cell.y+cell.height*.5f, cell.width, cell.height*.5f-4), lbl2, st);
            }

            // Click handling
            Event e = Event.current;
            if (e.type == EventType.MouseDown && cell.Contains(e.mousePosition))
            {
                if (e.button == 0) // Left-click = select
                {
                    _selC = c; _selR = r; _selKnot = -1;
                    e.Use(); Repaint();
                }
                else if (e.button == 1) // Right-click = context menu
                {
                    _selC = c; _selR = r; _selKnot = -1;
                    ShowCellContextMenu(c, r);
                    e.Use(); Repaint();
                }
            }
        }

        // ── Right-click context menu for grid cells ──────────────────────────
        private void ShowCellContextMenu(int c, int r)
        {
            var menu = new GenericMenu();
            var pal = GetActiveColors();

            // Color sub-menu
            foreach (var entry in pal)
            {
                var colorType = entry.t;
                bool isActive = _type[c, r] != GridCellType.Empty && _color[c, r] == colorType;
                menu.AddItem(new GUIContent($"Set Color/{entry.n}"), isActive, () =>
                {
                    Undo.RecordObject(this, "Set Cell Color");
                    _color[c, r] = colorType;
                    if (_type[c, r] == GridCellType.Empty)
                    {
                        _type[c, r] = GridCellType.ShooterBlock;
                        _shots[c, r] = 100;
                    }
                    _isDirty = true; Repaint();
                });
            }

            menu.AddSeparator("");

            // Type options
            bool doorUnlocked = _gameCfg != null && _levelIndex >= _gameCfg.doorUnlockLevel;
            bool mysteryUnlocked = _gameCfg != null && _levelIndex >= _gameCfg.mysteryShooterUnlockLevel;
            bool freezeUnlocked  = _gameCfg != null && _levelIndex >= _gameCfg.freezeShooterUnlockLevel;

            menu.AddItem(new GUIContent("Set Type/Shooter Block"),
                _type[c, r] == GridCellType.ShooterBlock, () =>
            {
                Undo.RecordObject(this, "Set Cell Type");
                _type[c, r] = GridCellType.ShooterBlock;
                if (_shots[c, r] <= 0) _shots[c, r] = 100;
                _isDirty = true; Repaint();
            });

            if (doorUnlocked)
            {
                menu.AddItem(new GUIContent("Set Type/Door"),
                    _type[c, r] == GridCellType.Door, () =>
                {
                    Undo.RecordObject(this, "Set Cell Type");
                    _type[c, r] = GridCellType.Door;
                    if (_doors[c, r] <= 0) _doors[c, r] = 3;
                    _isDirty = true; Repaint();
                });
            }

            if (mysteryUnlocked)
            {
                menu.AddItem(new GUIContent("Set Type/Mystery Shooter"),
                    _type[c, r] == GridCellType.MysteryShooter, () =>
                {
                    Undo.RecordObject(this, "Set Cell Type");
                    _type[c, r] = GridCellType.MysteryShooter;
                    if (_shots[c, r] <= 0) _shots[c, r] = 100;
                    _isDirty = true; Repaint();
                });
            }

            if (freezeUnlocked)
            {
                menu.AddItem(new GUIContent("Set Type/Freeze Shooter"),
                    _type[c, r] == GridCellType.FreezeShooter, () =>
                {
                    Undo.RecordObject(this, "Set Cell Type");
                    _type[c, r] = GridCellType.FreezeShooter;
                    if (_shots[c, r] <= 0) _shots[c, r] = 100;
                    if (_freezeCount[c, r] <= 0) _freezeCount[c, r] = 50;
                    _isDirty = true; Repaint();
                });
            }

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Clear Cell"), false, () =>
            {
                Undo.RecordObject(this, "Clear Cell");
                _type[c, r] = GridCellType.Empty;
                _selC = -1; _selR = -1;
                _isDirty = true; Repaint();
            });

            menu.ShowAsContext();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  GROUPS SECTION
        // ═════════════════════════════════════════════════════════════════════
        private void SetupGroupsList()
        {
            if (_groupsList != null && _groupsList.serializedProperty.serializedObject == _windowSerialized)
                return;

            var prop = _windowSerialized.FindProperty("_groups");
            _groupsList = new UnityEditorInternal.ReorderableList(_windowSerialized, prop, true, false, false, false);
            _groupsList.headerHeight = 0f;
            _groupsList.footerHeight = 0f;
            _groupsList.elementHeight = 22f;

            _groupsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                if (index < 0 || index >= prop.arraySize) return;

                var element = prop.GetArrayElementAtIndex(index);
                var colorProp = element.FindPropertyRelative("color");
                var rowCountProp = element.FindPropertyRelative("rowCount");
                var laneProp = element.FindPropertyRelative("laneCount");

                if (laneProp.intValue != 5)
                {
                    laneProp.intValue = 5;
                }

                float currentX = rect.x;

                // Color Box Indicator
                Rect colorBoxRect = new Rect(currentX, rect.y + 2, 16, rect.height - 4);
                BlockColorType colorVal = (BlockColorType)colorProp.enumValueIndex;
                Color prevColor = GUI.backgroundColor;
                GUI.backgroundColor = PC(colorVal);
                GUI.Box(colorBoxRect, "");
                GUI.backgroundColor = prevColor;

                currentX += 20;

                // Color Dropdown
                Rect colorPopupRect = new Rect(currentX, rect.y + 2, 80, rect.height - 4);
                BlockColorType newColor = DrawColorPopup(colorPopupRect, colorVal);
                if (newColor != colorVal)
                {
                    colorProp.enumValueIndex = (int)newColor;
                }

                currentX += 85;

                // Rows Label
                Rect rowsLabelRect = new Rect(currentX, rect.y + 2, 40, rect.height - 4);
                GUI.Label(rowsLabelRect, "Rows");

                currentX += 40;

                // Rows Field
                Rect rowsFieldRect = new Rect(currentX, rect.y + 2, 50, rect.height - 4);
                int oldRows = rowCountProp.intValue;
                int newRows = EditorGUI.IntField(rowsFieldRect, oldRows);
                if (newRows != oldRows)
                {
                    rowCountProp.intValue = newRows;
                }

                currentX += 55;

                // Lanes Label
                Rect lanesLabelRect = new Rect(currentX, rect.y + 2, 60, rect.height - 4);
                GUI.Label(lanesLabelRect, "Lanes: 5", EditorStyles.miniLabel);

                // Delete Button
                float delWidth = 20;
                Rect delRect = new Rect(rect.x + rect.width - delWidth, rect.y + 2, delWidth, rect.height - 4);
                if (GUI.Button(delRect, "✕"))
                {
                    _groupIndexToRemoveDeferred = index;
                }
            };
        }

        private UnityEditorInternal.ReorderableList GetBranchGroupsList(BranchPathData branch, SerializedProperty prop)
        {
            if (!_branchGroupsLists.TryGetValue(branch, out var list) || list.serializedProperty.serializedObject != _windowSerialized)
            {
                list = new UnityEditorInternal.ReorderableList(_windowSerialized, prop, true, false, false, false);
                list.headerHeight = 0f;
                list.footerHeight = 0f;
                list.elementHeight = 22f;

                list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    if (index < 0 || index >= prop.arraySize) return;

                    var element = prop.GetArrayElementAtIndex(index);
                    var colorProp = element.FindPropertyRelative("color");
                    var rowCountProp = element.FindPropertyRelative("rowCount");
                    var laneProp = element.FindPropertyRelative("laneCount");

                    if (laneProp.intValue != 5)
                    {
                        laneProp.intValue = 5;
                    }

                    float currentX = rect.x;

                    // Color Box Indicator
                    Rect colorBoxRect = new Rect(currentX, rect.y + 2, 16, rect.height - 4);
                    BlockColorType colorVal = (BlockColorType)colorProp.enumValueIndex;
                    Color prevColor = GUI.backgroundColor;
                    GUI.backgroundColor = PC(colorVal);
                    GUI.Box(colorBoxRect, "");
                    GUI.backgroundColor = prevColor;

                    currentX += 20;

                    // Color Dropdown
                    Rect colorPopupRect = new Rect(currentX, rect.y + 2, 80, rect.height - 4);
                    BlockColorType newColor = DrawColorPopup(colorPopupRect, colorVal);
                    if (newColor != colorVal)
                    {
                        colorProp.enumValueIndex = (int)newColor;
                    }

                    currentX += 85;

                    // Rows Label
                    Rect rowsLabelRect = new Rect(currentX, rect.y + 2, 40, rect.height - 4);
                    GUI.Label(rowsLabelRect, "Rows");

                    currentX += 40;

                    // Rows Field
                    Rect rowsFieldRect = new Rect(currentX, rect.y + 2, 50, rect.height - 4);
                    int oldRows = rowCountProp.intValue;
                    int newRows = EditorGUI.IntField(rowsFieldRect, oldRows);
                    if (newRows != oldRows)
                    {
                        rowCountProp.intValue = newRows;
                    }

                    currentX += 55;

                    // Lanes Label
                    Rect lanesLabelRect = new Rect(currentX, rect.y + 2, 60, rect.height - 4);
                    GUI.Label(lanesLabelRect, "Lanes: 5", EditorStyles.miniLabel);

                    // Delete Button
                    float delWidth = 20;
                    Rect delRect = new Rect(rect.x + rect.width - delWidth, rect.y + 2, delWidth, rect.height - 4);
                    if (GUI.Button(delRect, "✕"))
                    {
                        _branchGroupIndexToRemoveDeferred = (branch, index);
                    }
                };

                _branchGroupsLists[branch] = list;
            }

            list.serializedProperty = prop;
            return list;
        }

        private void DrawGroupsSection()
        {
            // Calculate capacity
            float mainLen = GetMainSplineLength();
            float rowSpacing = _cfg != null ? _cfg.rowSpacing : 0.18f;
            int maxMainRows = Mathf.Max(0, Mathf.FloorToInt(mainLen / rowSpacing));
            int assignedMainRows = _groups.Sum(g => g.rowCount);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (assignedMainRows > maxMainRows)
            {
                GUI.color = new Color(1f, 0.3f, 0.3f);
                GUILayout.Label($"⚠️ CAPACITY WARNING: {assignedMainRows} / {maxMainRows} Rows assigned! (Exceeds capacity by {assignedMainRows - maxMainRows} rows, overlap will occur)", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Label($"Conveyor Capacity: {assignedMainRows} / {maxMainRows} Rows ({(maxMainRows > 0 ? (assignedMainRows * 100 / maxMainRows) : 0)}% used)", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(4);

            EditorGUI.BeginChangeCheck();

            if (_groupsList != null)
            {
                _groupsList.DoLayoutList();
            }

            if (GUILayout.Button("+ Group", GUILayout.Height(22)))
            {
                _windowSerialized.ApplyModifiedProperties();
                _groups.Add(new LevelConveyorGroup
                {
                    color = BlockColorType.Red,
                    rowCount  = _cfg?.rowsPerGroup ?? 20,
                    laneCount = 5
                });
                _windowSerialized.Update();
                _isDirty = true;
            }
            GUILayout.Space(8);

            // Execute deferred removal outside of list drawing loop
            if (_groupIndexToRemoveDeferred >= 0)
            {
                _windowSerialized.ApplyModifiedProperties();
                _groups.RemoveAt(_groupIndexToRemoveDeferred);
                _groupIndexToRemoveDeferred = -1;
                _windowSerialized.Update();
                _isDirty = true;
            }

            Hdr("OPEN ZONE");
            _openZoneHalfT = EditorGUILayout.Slider("Gap Half-T", _openZoneHalfT, 0.005f, 0.25f);
            if (EditorGUI.EndChangeCheck())
            {
                _isDirty = true;
            }
            GUILayout.Space(8);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  RIGHT PANEL (inspector)
        // ═════════════════════════════════════════════════════════════════════
        private void DrawRightSplineTools()
        {
            Hdr("SPLINE PRESETS");
            string[] presetNames = { "Oval", "Wide Capsule", "Wavy Loop", "Heart Loop" };
            int newPreset = EditorGUILayout.Popup("Shape Preset", _splinePreset, presetNames);
            if (newPreset != _splinePreset)
            {
                _splinePreset = newPreset;
                ApplyPreset();
            }

            if (_presetBackupKnots != null && _presetBackupKnots.Count >= 3)
            {
                GUILayout.Space(4);
                GUI.backgroundColor = new Color(1f, .75f, .2f);
                if (GUILayout.Button("↩  Restore Previous Spline", GUILayout.Height(24)))
                {
                    _knots        = new List<Vector3>(_presetBackupKnots);
                    _tangentsIn   = new List<Vector3>(_presetBackupTanIn);
                    _tangentsOut  = new List<Vector3>(_presetBackupTanOut);
                    _tangentModes = new List<TangentMode>(_presetBackupModes);
                    _presetBackupKnots = null;
                    EnsureTangentLists();
                    SyncPreviewSpline();
                    SceneView.RepaintAll();
                    Repaint();
                    _isDirty = true;
                }
                GUI.backgroundColor = Color.white;
            }

            GUILayout.Space(6);
            Hdr("SYMMETRY TOOLS");
            if (GUILayout.Button("↔ Make Symmetric (L to R)", GUILayout.Height(24)))
            {
                MakeSplineSymmetric();
            }
            GUILayout.Space(4);
            if (GUILayout.Button("⇄ Flip Horizontally", GUILayout.Height(24)))
            {
                FlipSplineHorizontally();
            }

            GUILayout.Space(6);
            Hdr("GRID SNAPPING");
            _snapToGrid = EditorGUILayout.Toggle("Snap to Grid", _snapToGrid);
            if (_snapToGrid)
            {
                _snapSize = EditorGUILayout.FloatField("Snap Size", _snapSize);
                _snapSize = Mathf.Max(0.05f, _snapSize);
            }
        }

        private void DrawRight()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(RightW), GUILayout.ExpandHeight(true));

            // Spline Editor Tools & Knot Inspector (when in spline edit mode)
            if (_editingSpline)
            {
                DrawRightSplineTools();
                if (_selKnot >= 0 && _selKnot < _knots.Count)
                {
                    GUILayout.Space(12);
                    DrawKnotInspector();
                }
                EditorGUILayout.EndVertical();
                return;
            }

            // Cell inspector
            if (_selC >= 0 && _selR >= 0 && _type != null &&
                _selC < _gridCols && _selR < _gridRows)
            {
                DrawCellInspector();
                EditorGUILayout.EndVertical();
                return;
            }

            // Nothing selected
            Hdr("LEVEL CONFIG");
            EditorGUI.BeginChangeCheck();
            _isHardLevel = EditorGUILayout.Toggle("Is Hard Level", _isHardLevel);
            if (EditorGUI.EndChangeCheck())
            {
                _isDirty = true;
                Repaint();
            }

            GUILayout.Space(20);
            EditorGUILayout.HelpBox(
                "Click a grid cell to inspect.\nOr click 'Edit Spline' to manage track spline.", MessageType.None);
            EditorGUILayout.EndVertical();
        }

        // ── Knot inspector ────────────────────────────────────────────────────
        private void DrawKnotInspector()
        {
            int  i    = _selKnot;
            bool anch = (i == 0);
            Hdr($"KNOT #{i}{(anch ? "  🔒" : "")}");

            EnsureTangentLists();

            Vector3 k = _knots[i];

            EditorGUI.BeginChangeCheck();
            float nx = EditorGUILayout.FloatField("X", k.x);
            GUILayout.BeginHorizontal();
            float nz = EditorGUILayout.FloatField("Z", anch ? FIRE_Z : k.z);
            if (anch) EditorGUILayout.LabelField("(locked)", EditorStyles.miniLabel, GUILayout.Width(48));
            GUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck() && !anch)
            {
                Undo.RecordObject(this, "Modify Knot Position");
                _knots[i] = new Vector3(nx, 0f, nz);
                SyncPreviewSpline();
                SceneView.RepaintAll();
            }

            if (anch) EditorGUILayout.HelpBox($"FireRange anchor\nZ = {FIRE_Z:F1} locked\nX is free", MessageType.None);

            GUILayout.Space(6);

            // Tangent Mode
            GUILayout.Label("Tangent Mode:", EditorStyles.miniLabel);
            TangentMode newMode = (TangentMode)EditorGUILayout.EnumPopup(_tangentModes[i]);
            if (newMode != _tangentModes[i])
            {
                Undo.RegisterCompleteObjectUndo(this, "Change Tangent Mode");
                _tangentModes[i] = newMode;
                if (newMode == TangentMode.AutoSmooth)
                {
                    _tangentsIn[i]  = Vector3.zero;
                    _tangentsOut[i] = Vector3.zero;
                }
                SyncPreviewSpline();
                SceneView.RepaintAll();
            }

            if (_tangentModes[i] != TangentMode.AutoSmooth)
            {
                GUILayout.Space(4);
                GUILayout.Label("Tangent In (orange):", EditorStyles.miniLabel);
                EditorGUI.BeginChangeCheck();
                Vector3 newTanIn = EditorGUILayout.Vector3Field("", _tangentsIn[i]);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Modify Tangent In");
                    newTanIn.y = 0f;
                    _tangentsIn[i] = newTanIn;
                    if (_tangentModes[i] == TangentMode.Mirrored || _tangentModes[i] == TangentMode.Continuous)
                        _tangentsOut[i] = -newTanIn;
                    SyncPreviewSpline(); SceneView.RepaintAll();
                }

                GUILayout.Label("Tangent Out (cyan):", EditorStyles.miniLabel);
                EditorGUI.BeginChangeCheck();
                Vector3 newTanOut = EditorGUILayout.Vector3Field("", _tangentsOut[i]);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Modify Tangent Out");
                    newTanOut.y = 0f;
                    _tangentsOut[i] = newTanOut;
                    if (_tangentModes[i] == TangentMode.Mirrored || _tangentModes[i] == TangentMode.Continuous)
                        _tangentsIn[i] = -newTanOut;
                    SyncPreviewSpline(); SceneView.RepaintAll();
                }
            }

            GUILayout.Space(8);

            if (GUILayout.Button("Subdivide (Insert Knot After)", GUILayout.Height(24)))
            {
                Undo.RegisterCompleteObjectUndo(this, "Insert Spline Knot");
                int next = (i + 1) % _knots.Count;
                Vector3 newKnotPos = (_knots[i] + _knots[next]) * 0.5f;
                InsertKnot(i + 1, newKnotPos);
                _selKnot = i + 1;
                _selectedKnots.Clear();
                _selectedKnots.Add(i + 1);
                SyncPreviewSpline();
                SceneView.RepaintAll();
                Repaint();
            }

            GUILayout.Space(4);

            if (!anch && _knots.Count > 3)
            {
                GUI.backgroundColor = new Color(1f,.4f,.4f);
                if (GUILayout.Button("Delete Knot", GUILayout.Height(24)))
                {
                    Undo.RegisterCompleteObjectUndo(this, "Delete Spline Knot");
                    RemoveKnot(i);
                    _selKnot = Mathf.Min(i, _knots.Count - 1);
                    SyncPreviewSpline();
                    SceneView.RepaintAll(); Repaint();
                }
                GUI.backgroundColor = Color.white;
            }
        }

        // ── Cell inspector ────────────────────────────────────────────────────
        private void DrawCellInspector()
        {
            int c = _selC, r = _selR;
            Hdr($"CELL  ({c}, {r})");

            bool isBlock = _type[c, r] == GridCellType.ShooterBlock || _type[c, r] == GridCellType.MysteryShooter || _type[c, r] == GridCellType.FreezeShooter;
            bool isDoor  = _type[c, r] == GridCellType.Door;

            EditorGUI.BeginChangeCheck();

            // ── Shooter Block / Mystery Shooter / Freeze Shooter ──────────────
            if (isBlock)
            {
                // Color palette — always shown at top, listed vertically
                GUILayout.Label("Color:", EditorStyles.miniLabel);
                var pal = GetActiveColors();
                foreach (var entry in pal)
                {
                    bool isSel = _color[c, r] == entry.t;
                    GUI.backgroundColor = isSel ? entry.c : Color.Lerp(entry.c, Color.black, .4f);
                    var st = new GUIStyle(GUI.skin.button) { fontStyle = isSel ? FontStyle.Bold : FontStyle.Normal };
                    if (isSel) st.normal.textColor = Color.white;
                    if (GUILayout.Button(entry.n, st, GUILayout.Height(24)))
                    {
                        _color[c, r] = entry.t;
                        if (_type[c, r] == GridCellType.Empty)
                            _type[c, r] = GridCellType.ShooterBlock;
                        _isDirty = true;
                        Repaint();
                    }
                    GUI.backgroundColor = Color.white;
                }

                GUILayout.Space(6);

                // Shot count — IntField only, no toggle/slider
                GUILayout.Label("Shot Count:", EditorStyles.miniLabel);
                int displayVal = _shots[c, r] <= 0 ? 100 : _shots[c, r];
                int newVal = EditorGUILayout.IntField(displayVal);
                _shots[c, r] = Mathf.Max(1, newVal);

                GUILayout.Space(6);

                // Freeze count input if freeze shooter
                if (_type[c, r] == GridCellType.FreezeShooter)
                {
                    GUILayout.Label("Freeze Box Count:", EditorStyles.miniLabel);
                    _freezeCount[c, r] = Mathf.Max(1, EditorGUILayout.IntField(_freezeCount[c, r]));
                    GUILayout.Space(6);
                }

                GUILayout.Space(8);

                // Converter Buttons (vertical)
                bool mysteryUnlocked = _gameCfg != null && _levelIndex >= _gameCfg.mysteryShooterUnlockLevel;
                bool freezeUnlocked  = _gameCfg != null && _levelIndex >= _gameCfg.freezeShooterUnlockLevel;
                if (_type[c, r] == GridCellType.ShooterBlock)
                {
                    if (mysteryUnlocked)
                    {
                        if (GUILayout.Button("Convert to Mystery", GUILayout.Height(24)))
                        { _type[c, r] = GridCellType.MysteryShooter; _isDirty = true; Repaint(); }
                    }
                    if (freezeUnlocked)
                    {
                        if (GUILayout.Button("Convert to Freeze", GUILayout.Height(24)))
                        { _type[c, r] = GridCellType.FreezeShooter; _isDirty = true; Repaint(); }
                    }
                }
                else if (_type[c, r] == GridCellType.MysteryShooter)
                {
                    if (GUILayout.Button("Convert to Standard", GUILayout.Height(24)))
                    { _type[c, r] = GridCellType.ShooterBlock; _isDirty = true; Repaint(); }
                    if (freezeUnlocked)
                    {
                        if (GUILayout.Button("Convert to Freeze", GUILayout.Height(24)))
                        { _type[c, r] = GridCellType.FreezeShooter; _isDirty = true; Repaint(); }
                    }
                }
                else if (_type[c, r] == GridCellType.FreezeShooter)
                {
                    if (GUILayout.Button("Convert to Standard", GUILayout.Height(24)))
                    { _type[c, r] = GridCellType.ShooterBlock; _isDirty = true; Repaint(); }
                    if (mysteryUnlocked)
                    {
                        if (GUILayout.Button("Convert to Mystery", GUILayout.Height(24)))
                        { _type[c, r] = GridCellType.MysteryShooter; _isDirty = true; Repaint(); }
                    }
                }

                GUILayout.Space(8);

                // Bottom row — compact action buttons
                GUI.backgroundColor = new Color(.5f,.18f,.18f);
                if (GUILayout.Button("Clear Cell", GUILayout.Height(24)))
                { _type[c, r] = GridCellType.Empty; _selC = -1; _selR = -1; _isDirty = true; Repaint(); }
                GUI.backgroundColor = Color.white;
            }

            // ── Door ──────────────────────────────────────────────────────────
            else if (isDoor)
            {
                GUILayout.Label("Blocks from door:", EditorStyles.miniLabel);
                _doors[c, r] = EditorGUILayout.IntSlider(_doors[c, r], 1, 15);

                GUILayout.Space(8);

                GUI.backgroundColor = new Color(.35f,.55f,1f);
                if (GUILayout.Button("Set as Shooter Block", GUILayout.Height(24)))
                { _type[c, r] = GridCellType.ShooterBlock; _isDirty = true; Repaint(); }
                
                GUILayout.Space(4);

                GUI.backgroundColor = new Color(.5f,.18f,.18f);
                if (GUILayout.Button("Clear Cell", GUILayout.Height(24)))
                { _type[c, r] = GridCellType.Empty; _selC = -1; _selR = -1; _isDirty = true; Repaint(); }
                GUI.backgroundColor = Color.white;
            }

            // ── Empty ─────────────────────────────────────────────────────────
            else
            {
                // Color palette — clicking a color auto-promotes to ShooterBlock
                GUILayout.Label("Color:", EditorStyles.miniLabel);
                var pal = GetActiveColors();
                foreach (var entry in pal)
                {
                    GUI.backgroundColor = Color.Lerp(entry.c, Color.black, .4f);
                    if (GUILayout.Button(entry.n, GUILayout.Height(24)))
                    {
                        _color[c, r] = entry.t;
                        _type[c, r]  = GridCellType.ShooterBlock;
                        _isDirty = true;
                        Repaint();
                    }
                    GUI.backgroundColor = Color.white;
                }

                GUILayout.Space(8);
                GUILayout.Label("Actions:", EditorStyles.miniLabel);

                bool doorUnlocked = _gameCfg != null && _levelIndex >= _gameCfg.doorUnlockLevel;
                if (doorUnlocked)
                {
                    GUI.backgroundColor = new Color(.5f,.3f,.9f);
                    if (GUILayout.Button("Set as Door", GUILayout.Height(24)))
                    { _type[c, r] = GridCellType.Door; _isDirty = true; Repaint(); }
                }
                
                bool mysteryUnlocked = _gameCfg != null && _levelIndex >= _gameCfg.mysteryShooterUnlockLevel;
                if (mysteryUnlocked)
                {
                    GUI.backgroundColor = new Color(0.2f, 0.7f, 0.9f);
                    if (GUILayout.Button("Set as Mystery", GUILayout.Height(24)))
                    {
                        _type[c, r] = GridCellType.MysteryShooter;
                        _color[c, r] = BlockColorType.Red;
                        _shots[c, r] = 100;
                        _isDirty = true;
                        Repaint();
                    }
                }

                bool freezeUnlocked = _gameCfg != null && _levelIndex >= _gameCfg.freezeShooterUnlockLevel;
                if (freezeUnlocked)
                {
                    GUI.backgroundColor = new Color(0.1f, 0.8f, 0.6f);
                    if (GUILayout.Button("Set as Freeze", GUILayout.Height(24)))
                    {
                        _type[c, r] = GridCellType.FreezeShooter;
                        _color[c, r] = BlockColorType.Red;
                        _shots[c, r] = 100;
                        _freezeCount[c, r] = 50;
                        _isDirty = true;
                        Repaint();
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            if (EditorGUI.EndChangeCheck())
            {
                _isDirty = true;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  LEVEL MANAGEMENT
        // ═════════════════════════════════════════════════════════════════════
        private void NewLevel()
        {
            if (_cfg == null) return;

            int maxIdx = 0;
            foreach (var lbl in _labels)
            {
                var p = lbl.Split('_');
                if (p.Length > 1 && int.TryParse(p[p.Length-1], out int n)) maxIdx = Mathf.Max(maxIdx, n);
            }
            _levelIndex = maxIdx + 1;
            _levelName  = $"Level {_levelIndex}";
            _goalType   = LevelGoalType.ClearAllBlocks;
            _goalAmount = 0;
            _isHardLevel = false;
            _gridCols   = 4; _gridRows = 2;
            _splinePreset = 0; _splineWidth = 3.5f; _splineDepth = 5f;
            _selC = -1; _selR = -1; _selKnot = -1;
            StopSplineEdit(save: false);
            DestroyLevelPreview();

            // Null arrays first so InitGrid() starts completely fresh (no copy from previous level)
            _type = null; _color = null; _shots = null; _doors = null;
            InitGrid();

            _groups.Clear(); DefaultGroups();
            _knots.Clear(); _tangentsIn.Clear(); _tangentsOut.Clear(); _tangentModes.Clear();
            ApplyPreset();

            // Create a minimal placeholder prefab so it appears in the list immediately.
            // "Save Prefab" will later rebuild it with the full hierarchy.
            string dir  = _cfg.levelSavePath.TrimEnd('/').Replace('\\', '/');
            string name = $"Level_{_levelIndex:000}";
            string path = dir + "/" + name + ".prefab";
            EnsureDir(dir);

            var stub = new GameObject(name);
            var lr   = stub.AddComponent<LevelRoot>();
            lr.gridCols   = _gridCols;   // write dims so LoadLevel can restore them correctly
            lr.gridRows   = _gridRows;
            PrefabUtility.SaveAsPrefabAsset(stub, path);
            DestroyImmediate(stub);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshList();

            _activeIdx = _paths.IndexOf(path.Replace('\\', '/'));
            if (_levelList != null) _levelList.index = _activeIdx;
            _isDirty = false;
            Repaint();
        }

        private void LoadLevel(int idx)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(_paths[idx]);
            if (go == null) return;
            var lr = go.GetComponent<LevelRoot>();
            if (lr == null) return;

            StopSplineEdit(save: false);
            DestroyLevelPreview();
            _branchGroupsLists.Clear();
            _selectedKnots.Clear();
            _windowSerialized = new SerializedObject(this);

            _levelIndex   = idx + 1;
            _levelName    = $"Level {_levelIndex}";
            _goalType     = LevelGoalType.ClearAllBlocks;
            _goalAmount   = 0;
            _isHardLevel  = lr.isHardLevel;
            // Default to 4×2 when loading a stub prefab that has gridCols/Rows = 0
            _gridCols     = lr.gridCols  > 0 ? Mathf.Clamp(lr.gridCols,  1, MaxCols) : 4;
            _gridRows     = lr.gridRows  > 0 ? Mathf.Clamp(lr.gridRows,  1, MaxRows) : 2;
            _splineWidth  = lr.splineWidth  > 0 ? lr.splineWidth  : 3.5f;
            _splineDepth  = lr.splineDepth  > 0 ? lr.splineDepth  : 5f;
            _splinePreset  = lr.splinePreset;
            _openZoneHalfT = lr.openZoneHalfT > 0f ? lr.openZoneHalfT : 0.08f;

            // Null arrays so InitGrid() creates a fully fresh grid (no cross-level bleed)
            _type = null; _color = null; _shots = null; _doors = null; _freezeCount = null;
            InitGrid();
            foreach (var cell in lr.cells)
            {
                if (cell.col >= _gridCols || cell.row >= _gridRows) continue;
                _type [cell.col, cell.row] = cell.type;
                _color[cell.col, cell.row] = cell.color;
                // -1 is the legacy "use default" sentinel → convert to explicit 100
                _shots[cell.col, cell.row] = cell.shotCount <= 0 ? 100 : cell.shotCount;
                _doors[cell.col, cell.row] = cell.doorCount;
                _freezeCount[cell.col, cell.row] = cell.freezeCount <= 0 ? 50 : cell.freezeCount;
            }

            _groups.Clear();
            foreach (var g in lr.groups)
                _groups.Add(new LevelConveyorGroup { color=g.color, rowCount=g.rowCount, laneCount=g.laneCount });

            _branches.Clear();
            foreach (var b in lr.branches)
            {
                var bCopy = new BranchPathData
                {
                    branchName = b.branchName,
                    mergeT = b.mergeT,
                    connectFromLeft = b.connectFromLeft,
                    splineKnots = new List<Vector3>(b.splineKnots),
                    splineTangentsIn = new List<Vector3>(b.splineTangentsIn),
                    splineTangentsOut = new List<Vector3>(b.splineTangentsOut),
                    splineTangentModes = new List<int>(b.splineTangentModes),
                    groups = b.groups.Select(g => new LevelConveyorGroup { color = g.color, rowCount = g.rowCount, laneCount = g.laneCount }).ToList()
                };
                _branches.Add(bCopy);
            }

            if (lr.splineKnots.Count >= 3)
            {
                _knots = new List<Vector3>(lr.splineKnots);
                _tangentsIn   = lr.splineTangentsIn.Count == lr.splineKnots.Count
                    ? new List<Vector3>(lr.splineTangentsIn)
                    : new List<Vector3>(new Vector3[lr.splineKnots.Count]);
                _tangentsOut  = lr.splineTangentsOut.Count == lr.splineKnots.Count
                    ? new List<Vector3>(lr.splineTangentsOut)
                    : new List<Vector3>(new Vector3[lr.splineKnots.Count]);
                _tangentModes = lr.splineTangentModes.Count == lr.splineKnots.Count
                    ? lr.splineTangentModes.Select(m => (TangentMode)m).ToList()
                    : Enumerable.Repeat(TangentMode.AutoSmooth, lr.splineKnots.Count).ToList();
            }
            else
            {
                ApplyPreset();
            }
            EnsureTangentLists();

            _selC = -1; _selR = -1; _selKnot = -1;
            _isDirty = false;

            // Show the saved prefab mesh in the scene view
            ShowLevelPreview(_paths[idx]);
            Repaint();
        }

        private void DeleteLevel(int idx)
        {
            if (idx < 0 || idx >= _paths.Count) return;
            string path  = _paths[idx];
            string label = _labels[idx];
            if (!EditorUtility.DisplayDialog(
                    "Delete Level",
                    $"Delete \"{label}\"?\nThis will permanently remove the prefab asset.",
                    "Delete", "Cancel")) return;

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RefreshList();

            if (_activeIdx == idx)
            {
                if (_paths.Count > 0)
                {
                    _activeIdx = Mathf.Clamp(idx, 0, _paths.Count - 1);
                    if (_levelList != null) _levelList.index = _activeIdx;
                    LoadLevel(_activeIdx);
                }
                else
                {
                    _activeIdx = -1;
                    if (_levelList != null) _levelList.index = -1;
                    DestroyLevelPreview();
                    _levelName = "";
                    _levelIndex = 1;
                }
            }
            else
            {
                int oldActiveIdx = _activeIdx;
                if (oldActiveIdx > idx)
                {
                    _activeIdx = oldActiveIdx - 1;
                }
                if (_levelList != null) _levelList.index = _activeIdx;
            }

            Repaint();
        }

        private void DuplicateLevel(int idx)
        {
            if (idx < 0 || idx >= _paths.Count) return;

            // Find highest level index in labels
            int maxIdx = 0;
            foreach (var lbl in _labels)
            {
                var p = lbl.Split('_');
                if (p.Length > 1 && int.TryParse(p[p.Length - 1], out int n)) maxIdx = Mathf.Max(maxIdx, n);
            }
            int newIndex = maxIdx + 1;
            string newName = $"Level_{newIndex:000}";

            string srcPath  = _paths[idx].Replace('\\', '/');
            string dir      = _cfg.levelSavePath.TrimEnd('/').Replace('\\', '/');
            string destPath = dir + "/" + newName + ".prefab";
            EnsureDir(dir);

            bool copied = AssetDatabase.CopyAsset(srcPath, destPath);
            if (!copied)
            {
                Debug.LogError($"[LevelEditor] DuplicateLevel: CopyAsset failed from {srcPath} to {destPath}");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshList();

            // Select the new level
            _activeIdx = _paths.IndexOf(destPath.Replace('\\', '/'));
            if (_levelList != null) _levelList.index = _activeIdx;
            if (_activeIdx >= 0) LoadLevel(_activeIdx);
            Repaint();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SAVE PREFAB
        // ═════════════════════════════════════════════════════════════════════
        private void SavePrefab()
        {
            if (_cfg == null) { EditorUtility.DisplayDialog("Error","LevelEditorConfig not found!","OK"); return; }

            if (_editingSpline)
            {
                StopSplineEdit(save: true);
                return;
            }

            string dir  = _cfg.levelSavePath.TrimEnd('/').Replace('\\', '/');
            string name = $"Level_{_levelIndex:000}";
            string path = dir + "/" + name + ".prefab";
            EnsureDir(dir);

            var root = new GameObject(name);
            
            // Add Animator and set Levels animator controller
            var animator = root.AddComponent<Animator>();
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Project Files/Game/Animations/Levels.controller");
            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
            }
            else
            {
                Debug.LogWarning("[LevelEditor] Levels.controller not found at 'Assets/Project Files/Game/Animations/Levels.controller'");
            }

            var lr   = root.AddComponent<LevelRoot>();
            WriteDesignData(lr);
            BuildHierarchy(root.transform, lr);

            // Save generated meshes as persistent assets so prefab can reference them
            SaveTrackMeshAsset(root.transform, dir, name);
            SaveDeckMeshAsset(root.transform, dir, name);
            SaveBranchMeshAssets(root.transform, dir, name);

            PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
            DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            // Do NOT call AssetDatabase.Refresh() here — it reimports all assets and can
            // invalidate the MonoBehaviour references in levelPrefabs, causing IndexOf to
            // return -1 and clearing the active selection. SaveAsPrefabAsset already
            // registers the asset; SaveAssets flushes all pending dirty marks.
            RefreshList();

            if (ok)
            {
                int found = _paths.IndexOf(path.Replace('\\', '/'));
                if (found >= 0)
                {
                    _activeIdx = found;
                    if (_levelList != null) _levelList.index = _activeIdx;
                }
                // else: keep the selection that RefreshList() already restored
                _isDirty = false;
                Debug.Log($"[LevelEditor] Saved: {path}");
                ShowLevelPreview(path);
            }
            else Debug.LogError($"[LevelEditor] Failed: {path}");
        }

        private void WriteDesignData(LevelRoot lr)
        {
            lr.gridCols     = _gridCols;
            lr.gridRows     = _gridRows;
            lr.splineWidth  = _splineWidth;
            lr.splineDepth  = _splineDepth;
            lr.splinePreset  = _splinePreset;
            lr.openZoneHalfT = _openZoneHalfT;
            lr.isHardLevel   = _isHardLevel;
            lr.splineKnots   = new List<Vector3>(_knots);
            EnsureTangentLists();
            lr.splineTangentsIn   = new List<Vector3>(_tangentsIn);
            lr.splineTangentsOut  = new List<Vector3>(_tangentsOut);
            lr.splineTangentModes = _tangentModes.Select(m => (int)m).ToList();

            lr.cells.Clear();
            for (int c = 0; c < _gridCols; c++)
            for (int r = 0; r < _gridRows; r++)
                lr.cells.Add(new LevelGridCell
                {
                    col=c, row=r, type=_type[c,r], color=_color[c,r],
                    shotCount=_shots[c,r], doorCount=_doors[c,r],
                    freezeCount=_freezeCount[c,r]
                });

            lr.groups.Clear();
            foreach (var g in _groups)
                lr.groups.Add(new LevelConveyorGroup { color=g.color, rowCount=g.rowCount, laneCount=g.laneCount });

            lr.branches.Clear();
            foreach (var b in _branches)
            {
                var bData = new BranchPathData
                {
                    branchName = b.branchName,
                    mergeT = b.mergeT,
                    connectFromLeft = b.connectFromLeft,
                    splineKnots = new List<Vector3>(b.splineKnots),
                    splineTangentsIn = new List<Vector3>(b.splineTangentsIn),
                    splineTangentsOut = new List<Vector3>(b.splineTangentsOut),
                    splineTangentModes = new List<int>(b.splineTangentModes),
                    groups = b.groups.Select(g => new LevelConveyorGroup { color = g.color, rowCount = g.rowCount, laneCount = g.laneCount }).ToList()
                };
                lr.branches.Add(bData);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  BUILD HIERARCHY
        // ═════════════════════════════════════════════════════════════════════
        private void BuildHierarchy(Transform root, LevelRoot lr)
        {
            float cs   = _cfg.gridCellSize;
            float slotZ = -1.5f;
            float gridZ = -2.5f;

            // Create ConveyorSystem parent group
            var conveyorSys = Go(root, "ConveyorSystem");

            // ── Track ──
            var trackGo = Go(conveyorSys.transform, "Track");
            trackGo.transform.localPosition = new Vector3(0f, 0f, 0f);
            var sc = trackGo.AddComponent<SplineContainer>();
            float trackRailHeight = _cfg.railHeight;
            WriteKnotsToContainer(sc, trackRailHeight, 0f);
            var cc = trackGo.AddComponent<ConveyorController>();
            cc.speed = 1.5f; // Default fallback speed
            lr.conveyorController = cc;

            // Track mesh — ConveyorTrackMeshBuilder (RequireComponent auto-adds MeshFilter + MeshRenderer)
            var meshBuilder = trackGo.AddComponent<ConveyorTrackMeshBuilder>();
            meshBuilder.resolution    = _cfg.trackResolution;
            meshBuilder.openZoneHalfT = _openZoneHalfT;
            meshBuilder.beltHalfWidth = _cfg.beltHalfWidth;
            meshBuilder.wallAboveBelt = _cfg.wallAboveBelt;
            meshBuilder.railHeight    = trackRailHeight;
            meshBuilder.railWidth     = _cfg.railWidth;
            meshBuilder.bevelSize     = _cfg.trackBevelSize;
            meshBuilder.BuildMesh();

            var mr = trackGo.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterials = new Material[]
                {
                    _cfg.trackSideMaterial,
                    _cfg.trackBeltMaterial,
                };
            }

            if (_cfg.arrowPrefab != null)
            {
                cc.arrowPrefab  = _cfg.arrowPrefab;
                cc.arrowSpacing = _cfg.arrowSpacing;
            }

            // Block groups
            var groupsGo = Go(trackGo.transform, "Groups");
            foreach (var gd in _groups)
            {
                var gGo = Go(groupsGo.transform, $"Group_{gd.color}");
                var bg  = gGo.AddComponent<BlockGroup>();
                bg.colorType   = gd.color;
                bg.rowCount    = gd.rowCount;
                bg.laneCount   = gd.laneCount;
                bg.laneSpacing = _cfg.laneSpacing;
                bg.rowSpacing  = _cfg.rowSpacing;

                if (_cfg.conveyorBlockPrefab != null)
                    for (int row = 0; row < gd.rowCount; row++)
                    {
                        var rowGo = Go(gGo.transform, $"Row_{row}");
                        for (int lane = 0; lane < gd.laneCount; lane++)
                        {
                            var bGo = (GameObject)PrefabUtility.InstantiatePrefab(
                                _cfg.conveyorBlockPrefab, rowGo.transform);
                            bGo.name = $"Block_{lane}";
                            bGo.transform.localPosition = Vector3.zero;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(bGo.transform);
                            bGo.GetComponent<ConveyorBlock3D>()?.SetGroupIndex(row, lane);
                        }
                    }
            }

            // ── Branch Paths ──
            if (_branches != null && _branches.Count > 0)
            {
                var branchesGroupGo = Go(conveyorSys.transform, "Branches");
                int branchIdx = 0;
                foreach (var b in _branches)
                {
                    var branchGo = Go(branchesGroupGo.transform, b.branchName);
                    branchGo.transform.localPosition = new Vector3(0f, 0f, 0f);
                    
                    var bSc = branchGo.AddComponent<SplineContainer>();
                    WriteKnotsToContainer(bSc, b.splineKnots, b.splineTangentsIn, b.splineTangentsOut, b.splineTangentModes, trackRailHeight, 0f);

                    var bp = branchGo.AddComponent<BranchPath>();
                    bp.mergeT = b.mergeT;
                    bp.data = b;

                    var bMeshBuilder = branchGo.AddComponent<ConveyorTrackMeshBuilder>();
                    bMeshBuilder.resolution    = _cfg.trackResolution;
                    bMeshBuilder.openZoneEnabled = false;
                    bMeshBuilder.beltHalfWidth = _cfg.beltHalfWidth;
                    bMeshBuilder.wallAboveBelt = _cfg.wallAboveBelt;
                    bMeshBuilder.railHeight    = trackRailHeight;
                    bMeshBuilder.railWidth     = _cfg.railWidth;
                    bMeshBuilder.bevelSize     = _cfg.trackBevelSize;

                    // Calculate the main conveyor wall plane at the merge point for flush trimming
                    if (sc != null && b.splineKnots != null && b.splineKnots.Count >= 2)
                    {
                        sc.Spline.Evaluate(b.mergeT, out var mPos, out var mTan, out var mUp);
                        Vector3 worldMergePos = trackGo.transform.TransformPoint(mPos);
                        Vector3 worldMergeTan = trackGo.transform.TransformDirection((Vector3)mTan).normalized;
                        Vector3 worldMergeUp  = trackGo.transform.TransformDirection((Vector3)mUp).normalized;
                        if (worldMergeUp.sqrMagnitude < 0.001f) worldMergeUp = Vector3.up;
                        Vector3 worldMergeRight = Vector3.Cross(worldMergeUp, worldMergeTan).normalized;

                        // Determine which side the branch approaches from
                        Vector3 branchLast = b.splineKnots[b.splineKnots.Count - 1];
                        Vector3 branchSecondLast = b.splineKnots[b.splineKnots.Count - 2];
                        Vector3 toBranch = (branchSecondLast - branchLast).normalized;
                        float dot = Vector3.Dot(toBranch, worldMergeRight);

                        // Wall normal points outward toward the branch
                        Vector3 wallNormal = dot < 0f ? -worldMergeRight : worldMergeRight;
                        float wallOffset = _cfg.beltHalfWidth + _cfg.railWidth;
                        Vector3 wallPoint = worldMergePos + wallNormal * wallOffset;

                        bMeshBuilder.trimBranchEnd = true;
                        bMeshBuilder.mainTrackSpline = sc;
                        bMeshBuilder.branchOnRightSide = (dot >= 0f);
                    }

                    bMeshBuilder.BuildMesh();

                    var bMr = branchGo.GetComponent<MeshRenderer>();
                    if (bMr != null)
                    {
                        bMr.sharedMaterials = new Material[]
                        {
                            _cfg.trackSideMaterial,
                            _cfg.trackBeltMaterial,
                        };
                    }

                    // Calculate branch spline length for block placement
                    float branchSplineLen = SplineUtility.CalculateLength(bSc.Spline, branchGo.transform.localToWorldMatrix);
                    float mainTrackHalfWidth = _cfg.beltHalfWidth + _cfg.railWidth;
                    float safetyOffset = mainTrackHalfWidth + _cfg.rowSpacing + 0.1f;
                    float mergeStopT = branchSplineLen > 0f
                        ? Mathf.Clamp01(1.0f - (safetyOffset / branchSplineLen))
                        : 0.95f;

                    // Place block groups along the branch spline
                    var bGroupsGo = Go(branchGo.transform, "Groups");
                    int globalRowIdx = 0;
                    foreach (var gd in b.groups)
                    {
                        var gGo = Go(bGroupsGo.transform, $"Group_{gd.color}");
                        var bg  = gGo.AddComponent<BlockGroup>();
                        bg.colorType   = gd.color;
                        bg.rowCount    = gd.rowCount;
                        bg.laneCount   = 5;
                        bg.laneSpacing = _cfg.laneSpacing;
                        bg.rowSpacing  = _cfg.rowSpacing;

                        if (_cfg.conveyorBlockPrefab != null)
                            for (int row = 0; row < gd.rowCount; row++)
                            {
                                // Calculate T position for this row on the branch spline
                                float rowT = mergeStopT - (globalRowIdx * _cfg.rowSpacing) / branchSplineLen;
                                rowT = Mathf.Clamp01(rowT);

                                // Evaluate spline at this T to get world position and orientation
                                bSc.Spline.Evaluate(rowT, out var spPos, out var spTan, out var spUp);
                                Vector3 worldPos = branchGo.transform.TransformPoint(spPos);
                                Vector3 fwd = branchGo.transform.TransformDirection((Vector3)spTan).normalized;
                                Vector3 upDir = branchGo.transform.TransformDirection((Vector3)spUp).normalized;
                                if (upDir == Vector3.zero) upDir = Vector3.up;
                                Vector3 right = Vector3.Cross(upDir, fwd).normalized;
                                Quaternion rot = fwd != Vector3.zero ? Quaternion.LookRotation(fwd, upDir) : Quaternion.identity;

                                var rowGo = Go(gGo.transform, $"Row_{row}");
                                for (int lane = 0; lane < 5; lane++)
                                {
                                    var bGo = (GameObject)PrefabUtility.InstantiatePrefab(
                                        _cfg.conveyorBlockPrefab, rowGo.transform);
                                    bGo.name = $"Block_{lane}";

                                    // Position block at the correct lane offset along the spline
                                    float xOff = (lane - 2f) * _cfg.laneSpacing;
                                    bGo.transform.position = worldPos + right * xOff;
                                    bGo.transform.rotation = rot;

                                    PrefabUtility.RecordPrefabInstancePropertyModifications(bGo.transform);
                                    bGo.GetComponent<ConveyorBlock3D>()?.SetGroupIndex(row, lane);

                                    // Apply color material
                                    var cb = bGo.GetComponent<ConveyorBlock3D>();
                                    if (cb != null && cb.blockRenderer != null && _gameCfg != null)
                                    {
                                        var mat = _gameCfg.GetMaterial(gd.color);
                                        if (mat != null) cb.blockRenderer.sharedMaterial = mat;
                                    }
                                }
                                globalRowIdx++;
                            }
                    }
                    branchIdx++;
                }
            }

            // Create logical parent groups
            var gameplayLogic = Go(root, "GameplayLogic");
            var boardPlatform = Go(root, "BoardPlatform");

            // ── FireRange ── (inside Track object)
            GameObject frGo;
            if (_cfg.fireRangePrefab != null)
            {
                frGo = (GameObject)PrefabUtility.InstantiatePrefab(_cfg.fireRangePrefab, trackGo.transform);
                frGo.name = "FireRange";
            }
            else
            {
                frGo = Go(trackGo.transform, "FireRange");
                var fc = frGo.AddComponent<BoxCollider>();
                fc.isTrigger = true;
                fc.size = new Vector3(1.8f, 2f, 0.8f);
                frGo.AddComponent<FireRange>();
            }
            frGo.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            if (PrefabUtility.IsPartOfPrefabInstance(frGo))
                PrefabUtility.RecordPrefabInstancePropertyModifications(frGo.transform);
            lr.fireRange = frGo.GetComponent<FireRange>();

            // ── SlotDeck ──
            var deckGo = Go(boardPlatform.transform, "SlotDeck");
            deckGo.transform.localPosition = new Vector3(0f, 0f, slotZ);
            var ss = deckGo.AddComponent<SlotSystem>();
            if (_cfg.slotIndicatorPrefab != null) ss.slotIndicatorPrefab = _cfg.slotIndicatorPrefab;
            lr.slotSystem = ss;

            int   slots = _cfg.slotCount;
            float tw    = (slots - 1) * _cfg.slotSpacing;
            for (int i = 0; i < slots; i++)
            {
                var slotGo = Go(deckGo.transform, $"Slot_{i}");
                slotGo.transform.localPosition = new Vector3(-tw * .5f + i * _cfg.slotSpacing, 0f, 0f);

                if (_cfg.slotIndicatorPrefab != null)
                {
                    var indGo = (GameObject)PrefabUtility.InstantiatePrefab(_cfg.slotIndicatorPrefab, slotGo.transform);
                    indGo.name = "SlotIndicator";
                    indGo.transform.localPosition = Vector3.zero;
                    indGo.transform.localRotation = Quaternion.identity;
                }
            }

            // ── ShooterGrid ──
            var sgGo = Go(boardPlatform.transform, "ShooterGrid");
            sgGo.transform.localPosition = new Vector3(0f, 0f, gridZ);
            var sg = sgGo.AddComponent<ShooterGrid>();
            if (_cfg.shooterBlockPrefab != null)
                sg.shooterBlockPrefab = _cfg.shooterBlockPrefab.GetComponent<ShooterBlock>();
            lr.shooterGrid = sg;

            float hw = (_gridCols - 1) * cs * .5f;

            for (int r = 0; r < _gridRows; r++)
            for (int c = 0; c < _gridCols; c++)
            {
                var pos = new Vector3(-hw + c*cs, 0f, (r - _gridRows + 0.5f) * cs);
                string nm = $"Cell_r{r}_c{c}";

                switch (_type[c, r])
                {
                    case GridCellType.ShooterBlock when _cfg.shooterBlockPrefab != null:
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(_cfg.shooterBlockPrefab, sgGo.transform);
                        go.name = nm; go.transform.localPosition = pos;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                        int sh = Mathf.Max(1, _shots[c,r]);
                        var sb = go.GetComponent<ShooterBlock>();
                        sb?.EditorSetup(_color[c,r], sh, c, r, isMystery: false);
                        // Apply material directly so prefab shows colors in editor
                        if (sb?.blockRenderer != null && _gameCfg != null)
                        {
                            var mat = _gameCfg.GetMaterial(_color[c,r]);
                            if (mat != null) sb.blockRenderer.sharedMaterial = mat;
                        }
                        break;
                    }
                    case GridCellType.MysteryShooter:
                    {
                        var prefab = _cfg.shooterBlockPrefab;
                        if (prefab != null)
                        {
                            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, sgGo.transform);
                            go.name = nm; go.transform.localPosition = pos;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                            int sh = Mathf.Max(1, _shots[c,r]);
                            var sb = go.GetComponent<ShooterBlock>();
                            sb?.EditorSetup(_color[c,r], sh, c, r, isMystery: true);
                        }
                        break;
                    }
                    case GridCellType.FreezeShooter:
                    {
                        var prefab = _cfg.shooterBlockPrefab;
                        if (prefab != null)
                        {
                            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, sgGo.transform);
                            go.name = nm; go.transform.localPosition = pos;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                            int sh = Mathf.Max(1, _shots[c,r]);
                            var sb = go.GetComponent<ShooterBlock>();
                            sb?.EditorSetup(_color[c,r], sh, c, r, isMystery: false);
                            if (sb?.blockRenderer != null && _gameCfg != null)
                            {
                                var mat = _gameCfg.GetMaterial(_color[c,r]);
                                if (mat != null) sb.blockRenderer.sharedMaterial = mat;
                            }
                            
                            var f = go.GetComponent<FreezeBlockFeature>();
                            if (f == null) f = go.AddComponent<FreezeBlockFeature>();
                            f.isFrozen = true;
                            f.freezeCount = _freezeCount[c, r];
                            f.SyncVisualsEditor();
                        }
                        break;
                    }
                    case GridCellType.Door:
                    {
                        var go = Go(sgGo.transform, nm); go.transform.localPosition = pos;
                        var d = go.AddComponent<BlockDoor>();
                        d.blockCount = _doors[c,r];
                        d.spawnColors = new List<BlockColorType> { _color[c,r] };
                        break;
                    }
                }
            }

            // ── Shooter Deck Mesh ──
            var isEmpty = new bool[_gridCols, _gridRows];
            for (int c = 0; c < _gridCols; c++)
            for (int r = 0; r < _gridRows; r++)
                isEmpty[c, r] = _type[c, r] == GridCellType.Empty;

            var deckMeshGo = Go(boardPlatform.transform, "ShooterDeck");
            deckMeshGo.transform.localPosition = new Vector3(0f, 0f, gridZ);
            var deckBuilder = deckMeshGo.AddComponent<ShooterDeckMeshBuilder>();
            deckBuilder.gridCols      = _gridCols;
            deckBuilder.gridRows      = _gridRows;
            deckBuilder.cellSize      = cs;
            deckBuilder.tileHeight    = _cfg.deckTileHeight;
            deckBuilder.sideWingWidth = _cfg.sideWingWidth;
            deckBuilder.backDepth     = _cfg.backDepth;
            deckBuilder.bevelSize     = _cfg.bevelSize;
            deckBuilder.bevelSegments = _cfg.bevelSegments;
            deckBuilder.BuildMesh(isEmpty);
            var deckMr = deckMeshGo.GetComponent<MeshRenderer>();
            deckMr.sharedMaterials = new Material[]
            {
                _cfg.deckTopMaterial,
                _cfg.deckWallMaterial,
            };

            // ── Ground (optional, only if prefab explicitly assigned) ──
            if (_cfg.groundPrefab != null)
            {
                var envGo = Go(root, "Environment");
                var gnd = (GameObject)PrefabUtility.InstantiatePrefab(_cfg.groundPrefab, envGo.transform);
                gnd.name = "Ground";
                gnd.transform.localPosition = Vector3.zero;
                PrefabUtility.RecordPrefabInstancePropertyModifications(gnd.transform);
            }
        }

        // ── Track mesh asset ──────────────────────────────────────────────────
        private static void SaveTrackMeshAsset(Transform root, string dir, string name)
        {
            var track = FindDeepChild(root, "Track");
            if (track == null) return;
            var mf = track.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            const string meshDir = "Assets/Project Files/Game/Models/LevelMesh";
            EnsureDir(meshDir);
            string meshPath = $"{meshDir}/{name}_TrackMesh.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
            {
                // Reuse existing asset to keep prefab references stable
                existing.Clear();
                EditorUtility.CopySerialized(mf.sharedMesh, existing);
                mf.sharedMesh = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(mf.sharedMesh, meshPath);
            }
        }

        private static void SaveDeckMeshAsset(Transform root, string dir, string name)
        {
            var deck = FindDeepChild(root, "ShooterDeck");
            if (deck == null) return;
            var mf = deck.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            const string meshDir = "Assets/Project Files/Game/Models/LevelMesh";
            EnsureDir(meshDir);
            string meshPath = $"{meshDir}/{name}_DeckMesh.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mf.sharedMesh, existing);
                mf.sharedMesh = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(mf.sharedMesh, meshPath);
            }
        }

        private static void SaveBranchMeshAssets(Transform root, string dir, string name)
        {
            var branches = FindDeepChild(root, "Branches");
            if (branches == null) return;

            const string meshDir = "Assets/Project Files/Game/Models/LevelMesh";
            EnsureDir(meshDir);

            int idx = 0;
            foreach (Transform branchChild in branches)
            {
                var mf = branchChild.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) { idx++; continue; }

                string meshPath = $"{meshDir}/{name}_BranchMesh_{idx}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing != null)
                {
                    existing.Clear();
                    EditorUtility.CopySerialized(mf.sharedMesh, existing);
                    mf.sharedMesh = existing;
                }
                else
                {
                    AssetDatabase.CreateAsset(mf.sharedMesh, meshPath);
                }
                idx++;
            }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindDeepChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        // ── Test In Scene ─────────────────────────────────────────────────────
        private void TestInScene()
        {
            SavePrefab();

            string dir  = _cfg.levelSavePath.TrimEnd('/').Replace('\\', '/');
            string path = dir + $"/Level_{_levelIndex:000}.prefab";
            var prefab  = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;

            // Kill all DOTween tweens first to prevent MissingReference on destroyed transforms
            DOTween.KillAll();

            // Clean up all preview objects (including hidden and orphaned ones)
            DestroyLevelPreview();
            DestroyPreview();

            // Remove existing LevelRoot instances from scene
            foreach (var lr in FindObjectsByType<LevelRoot>(FindObjectsSortMode.None))
            {
                DeselectIfSelected(lr.gameObject);
                DestroyImmediate(lr.gameObject);
            }

            // Assign prefab to LevelManager if present
            var lm = FindFirstObjectByType<LevelManager>();
            if (lm != null)
            {
                SyncLevelsFromFolder();
                int prefabIndex = _gameCfg.levelPrefabs.FindIndex(x => x != null && x.gameObject.name == prefab.name);
                if (prefabIndex >= 0)
                {
                    SaveManager.CurrentLevel = prefabIndex + 1;
                }
                else
                {
                    SaveManager.CurrentLevel = _levelIndex;
                }
            }
            else
            {
                PrefabUtility.InstantiatePrefab(prefab);
            }

            EditorApplication.EnterPlaymode();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════════
        private void InitGrid()
        {
            _gridCols = Mathf.Clamp(_gridCols, 1, MaxCols);
            _gridRows = Mathf.Clamp(_gridRows, 1, MaxRows);
            var pt=_type; var pc=_color; var ps=_shots; var pd=_doors; var pf=_freezeCount;
            _type  = new GridCellType  [_gridCols, _gridRows];
            _color = new BlockColorType[_gridCols, _gridRows];
            _shots = new int           [_gridCols, _gridRows];
            _doors = new int           [_gridCols, _gridRows];
            _freezeCount = new int     [_gridCols, _gridRows];
            for (int c=0;c<_gridCols;c++) for (int r=0;r<_gridRows;r++)
            {
                _shots[c,r]=100; _doors[c,r]=5; _freezeCount[c,r]=50;
                if (pt==null||c>=pt.GetLength(0)||r>=pt.GetLength(1)) continue;
                _type[c,r]=pt[c,r]; _color[c,r]=pc[c,r]; _shots[c,r]=ps[c,r]; _doors[c,r]=pd[c,r];
                if (pf!=null && c<pf.GetLength(0) && r<pf.GetLength(1)) _freezeCount[c,r]=pf[c,r];
            }
        }

        private void ResizeGrid() { InitGrid(); _selC=-1; _selR=-1; }

        private static void Hdr(string t)
        {
            GUILayout.Space(7);
            EditorGUILayout.LabelField(t, EditorStyles.boldLabel);
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(1,1,GUILayout.ExpandWidth(true)),
                new Color(.5f,.5f,.5f,.35f));
            GUILayout.Space(3);
        }

        private static void VDiv()
        {
            EditorGUI.DrawRect(
                GUILayoutUtility.GetRect(1,float.MaxValue,GUILayout.Width(1),GUILayout.ExpandHeight(true)),
                new Color(.22f,.22f,.22f));
        }

        private void DrawBranchesSection()
        {
            EditorGUI.BeginChangeCheck();

            for (int i = _branches.Count - 1; i >= 0; i--)
            {
                var b = _branches[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                bool isThisEditing = _editingSpline && _editingBranchIndex == i;
                bool otherEditing = _editingSpline && !isThisEditing;

                EditorGUI.BeginDisabledGroup(otherEditing);
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Branch Name:", GUILayout.Width(80));
                b.branchName = EditorGUILayout.TextField(b.branchName);
                
                // Mirror / Remove buttons — disabled while editing any spline
                EditorGUI.BeginDisabledGroup(_editingSpline);
                GUI.backgroundColor = new Color(.35f, .75f, 1f);
                if (GUILayout.Button("⇆ Mirror", GUILayout.Width(62)))
                    _branchMirrorIndexDeferred = i;
                GUI.backgroundColor = new Color(.9f, .3f, .3f);
                if (GUILayout.Button("✕ Remove", GUILayout.Width(68)))
                    _branchIndexToRemoveDeferred = i;
                GUI.backgroundColor = Color.white;
                EditorGUI.EndDisabledGroup(); // end inner disabled group
                EditorGUILayout.EndHorizontal();

                // Connections
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Merge T (0-1):", GUILayout.Width(90));
                b.mergeT = EditorGUILayout.Slider(b.mergeT, 0f, 1f);
                EditorGUILayout.EndHorizontal();

                float branchLen = GetBranchSplineLength(b);
                float rowSpacingBranch = _cfg != null ? _cfg.rowSpacing : 0.18f;
                int maxBranchRows = Mathf.Max(0, Mathf.FloorToInt(branchLen / rowSpacingBranch));
                int assignedBranchRows = b.groups.Sum(g => g.rowCount);

                EditorGUILayout.BeginHorizontal();
                if (assignedBranchRows > maxBranchRows)
                {
                    GUI.color = new Color(1f, 0.3f, 0.3f);
                    GUILayout.Label($"⚠️ OVERLAP WARNING: {assignedBranchRows} / {maxBranchRows} Rows assigned!", EditorStyles.boldLabel);
                    GUI.color = Color.white;
                }
                else
                {
                    GUILayout.Label($"Branch Capacity: {assignedBranchRows} / {maxBranchRows} Rows ({(maxBranchRows > 0 ? (assignedBranchRows * 100 / maxBranchRows) : 0)}% used)", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();

                // Edit Spline button
                EditorGUILayout.BeginHorizontal();
                if (_editingSpline && _editingBranchIndex == i)
                {
                    GUI.backgroundColor = new Color(.3f, .85f, .45f);
                    if (GUILayout.Button("✓ Done Editing Branch Spline", GUILayout.Height(24)))
                    {
                        StopSplineEdit(save: true);
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.backgroundColor = new Color(.4f, .7f, 1f);
                    if (GUILayout.Button("✏ Edit Branch Spline", GUILayout.Height(24)))
                    {
                        if (_editingSpline) StopSplineEdit(save: false);
                        _editingBranchIndex = i;
                        StartSplineEdit();
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);
                GUILayout.Label("Branch Groups:", EditorStyles.boldLabel);

                SerializedProperty branchesProp = _windowSerialized.FindProperty("_branches");
                SerializedProperty branchProp = branchesProp.GetArrayElementAtIndex(i);
                SerializedProperty branchGroupsProp = branchProp.FindPropertyRelative("groups");

                var branchGroupsList = GetBranchGroupsList(b, branchGroupsProp);
                branchGroupsList.DoLayoutList();

                // Disable Add Group button if editing any spline
                EditorGUI.BeginDisabledGroup(_editingSpline);
                if (GUILayout.Button("+ Add Group to Branch", EditorStyles.miniButton, GUILayout.Height(18)))
                {
                    _windowSerialized.ApplyModifiedProperties();
                    b.groups.Add(new LevelConveyorGroup
                    {
                        color = BlockColorType.Red,
                        rowCount = 10,
                        laneCount = 5
                    });
                    _windowSerialized.Update();
                    _isDirty = true;
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.EndDisabledGroup(); // end otherEditing disabled group

                EditorGUILayout.EndVertical();
                GUILayout.Space(6);
            }

            // Execute deferred branch group removal outside of the list drawing loop
            if (_branchGroupIndexToRemoveDeferred.branch != null)
            {
                _windowSerialized.ApplyModifiedProperties();
                _branchGroupIndexToRemoveDeferred.branch.groups.RemoveAt(_branchGroupIndexToRemoveDeferred.index);
                _branchGroupIndexToRemoveDeferred = (null, -1);
                _branchGroupsLists.Clear();
                _windowSerialized.Update();
                _isDirty = true;
            }

            // Execute deferred branch removal outside the draw loop to avoid layout
            // state corruption and stale SerializedProperty references in _branchGroupsLists.
            if (_branchIndexToRemoveDeferred >= 0)
            {
                _windowSerialized.ApplyModifiedProperties();
                _branches.RemoveAt(_branchIndexToRemoveDeferred);
                _branchIndexToRemoveDeferred = -1;
                _branchGroupsLists.Clear();
                _windowSerialized.Update();
                _isDirty = true;
                Repaint();
            }

            // Execute deferred branch mirror — creates a new branch with all X-coordinates negated.
            if (_branchMirrorIndexDeferred >= 0)
            {
                _windowSerialized.ApplyModifiedProperties();
                var src = _branches[_branchMirrorIndexDeferred];
                var mirrored = new BranchPathData
                {
                    branchName        = src.branchName + "_mirror",
                    mergeT            = src.mergeT,
                    connectFromLeft   = !src.connectFromLeft,
                    splineKnots       = src.splineKnots.Select(k  => new Vector3(-k.x,  k.y,  k.z)).ToList(),
                    splineTangentsIn  = src.splineTangentsIn.Select(t  => new Vector3(-t.x,  t.y,  t.z)).ToList(),
                    splineTangentsOut = src.splineTangentsOut.Select(t => new Vector3(-t.x,  t.y,  t.z)).ToList(),
                    splineTangentModes = new List<int>(src.splineTangentModes),
                    groups = src.groups.Select(g => new LevelConveyorGroup
                    {
                        color     = g.color,
                        rowCount  = g.rowCount,
                        laneCount = g.laneCount
                    }).ToList()
                };
                _branches.Add(mirrored);
                _branchMirrorIndexDeferred = -1;
                _windowSerialized.Update();
                _isDirty = true;
                Repaint();
            }

            EditorGUI.BeginDisabledGroup(_editingSpline);
            GUI.backgroundColor = new Color(.45f, .85f, .5f);
            if (GUILayout.Button("+ Add Branch Path", GUILayout.Height(26)))
            {
                _windowSerialized.ApplyModifiedProperties();
                var newBranch = new BranchPathData
                {
                    branchName = $"Branch_{_branches.Count}",
                    mergeT = 0.5f,
                    connectFromLeft = false,
                    splineKnots = new List<Vector3>
                    {
                        new Vector3(-4f, 0f, 2f),
                        new Vector3(-2f, 0f, 3f),
                        new Vector3(0f, 0f, 3.5f)
                    },
                    splineTangentsIn = new List<Vector3> { Vector3.zero, Vector3.zero, Vector3.zero },
                    splineTangentsOut = new List<Vector3> { Vector3.zero, Vector3.zero, Vector3.zero },
                    splineTangentModes = new List<int> { (int)TangentMode.AutoSmooth, (int)TangentMode.AutoSmooth, (int)TangentMode.AutoSmooth }
                };
                _branches.Add(newBranch);
                _windowSerialized.Update();
                _isDirty = true;
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck())
            {
                _isDirty = true;
            }

            GUILayout.Space(8);
        }

        private static GameObject Go(Transform p, string n)
        { var g=new GameObject(n); g.transform.SetParent(p,false); return g; }

        private static void EnsureDir(string path)
        {
            string[] pts=path.Split('/'); string cur=pts[0];
            for (int i=1;i<pts.Length;i++)
            {
                string nxt=cur+"/"+pts[i];
                if (!AssetDatabase.IsValidFolder(nxt)) AssetDatabase.CreateFolder(cur,pts[i]);
                cur=nxt;
            }
        }
    }
}
#endif
