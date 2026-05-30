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
        private const float RightW   = 210f;
        private const float CellSize = 62f;
        private const float CellGap  = 4f;
        private const int   MaxCols  = 7;
        private const int   MaxRows  = 6;

        // FireRange world Z — all splines pass through this point
        private const float FIRE_Z = 1.5f;

        // ── Config ────────────────────────────────────────────────────────────
        private LevelEditorConfig _cfg;
        private GameConfig        _gameCfg;

        // ── Level list ────────────────────────────────────────────────────────
        private List<string> _paths  = new();
        private List<string> _labels = new();
        private int          _activeIdx = -1;

        // ── Design data ───────────────────────────────────────────────────────
        private int           _levelIndex  = 1;
        private string        _levelName   = "Level 1";
        private LevelGoalType _goalType    = LevelGoalType.ClearAllBlocks;
        private int           _goalAmount  = 0;

        private int   _gridCols = 4, _gridRows = 2;
        private GridCellType[,]   _type;
        private BlockColorType[,] _color;
        private int[,]            _shots, _doors;

        private List<LevelConveyorGroup> _groups = new();

        // ── Spline ────────────────────────────────────────────────────────────
        private List<Vector3> _knots       = new();
        private float         _splineWidth = 3.5f;
        private float         _splineDepth = 5f;
        private int           _splinePreset = 0;  // 0=Oval 1=Wide 2=Rectangle

        // Safe-area guide toggle
        private bool _showSafeArea = true;

        // Spline edit state
        private bool         _editingSpline = false;
        private bool         _addKnotMode   = false;
        private int          _selKnot       = -1;
        private GameObject   _previewGo     = null; // lightweight spline preview only

        // Copy
        private int _copyIdx = 0;

        // ── Cell selection ────────────────────────────────────────────────────
        private int _selC = -1, _selR = -1;

        // ── Scroll ────────────────────────────────────────────────────────────
        private Vector2 _listScroll, _midScroll;

        // ── Color palette ─────────────────────────────────────────────────────
        private static readonly (BlockColorType t, Color c, string n)[] Pal =
        {
            (BlockColorType.Red,    new Color(.90f,.20f,.20f), "Red"   ),
            (BlockColorType.Blue,   new Color(.20f,.50f,.90f), "Blue"  ),
            (BlockColorType.Green,  new Color(.20f,.80f,.30f), "Green" ),
            (BlockColorType.Yellow, new Color(1.00f,.85f,.10f),"Yellow"),
            (BlockColorType.Purple, new Color(.60f,.20f,.90f), "Purple"),
            (BlockColorType.Orange, new Color(1.00f,.55f,.10f),"Orange"),
        };
        private static Color PC(BlockColorType t) =>
            Pal.FirstOrDefault(x => x.t == t).c;

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
            LoadCfg();
            RefreshList();
            if (_type == null) InitGrid();
            if (_knots.Count == 0) ApplyPreset();
            if (_groups.Count == 0) DefaultGroups();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            DestroyPreview();
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

            string folder = _cfg.levelSavePath.TrimEnd('/');
            foreach (var gid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(gid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponent<LevelRoot>() == null) continue;
                _paths.Add(path);
                _labels.Add(Path.GetFileNameWithoutExtension(path));
            }

            var s = _paths.Zip(_labels, (p, l) => (p, l)).OrderBy(x => x.l).ToList();
            _paths  = s.Select(x => x.p).ToList();
            _labels = s.Select(x => x.l).ToList();
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
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawLeft();
            VDiv();
            DrawCenter();
            VDiv();
            DrawRight();
            EditorGUILayout.EndHorizontal();
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
            EditorGUILayout.BeginVertical(GUILayout.Width(ListW), GUILayout.ExpandHeight(true));
            Hdr("LEVELS");
            GUI.backgroundColor = new Color(.45f,.85f,.5f);
            if (GUILayout.Button("+ New Level", GUILayout.Height(26))) NewLevel();
            GUI.backgroundColor = Color.white;
            GUILayout.Space(3);

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _labels.Count; i++)
            {
                bool active = _activeIdx == i;
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = active ? new Color(.4f,.65f,1f) : new Color(.24f,.24f,.26f);
                if (GUILayout.Button(_labels[i], GUILayout.Height(22)))
                { _activeIdx = i; LoadLevel(i); }
                GUI.backgroundColor = new Color(.9f,.3f,.3f);
                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(22)))
                    DeleteLevel(i);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndScrollView();

            GUILayout.Space(6);
            Hdr("SETTINGS");
            _levelIndex = EditorGUILayout.IntField ("Index",  _levelIndex);
            _levelName  = EditorGUILayout.TextField("Name",   _levelName);
            _goalType   = (LevelGoalType)EditorGUILayout.EnumPopup("Goal", _goalType);
            if (_goalType != LevelGoalType.ClearAllBlocks)
                _goalAmount = EditorGUILayout.IntField("Amount", _goalAmount);

            EditorGUILayout.EndVertical();
        }

        // ── Center panel ──────────────────────────────────────────────────────
        private void DrawCenter()
        {
            // Reserve space for toolbar (~21px) and footer buttons (~52px)
            const float toolbarH = 21f;
            const float footerH  = 52f;
            float scrollH = Mathf.Max(80f, position.height - toolbarH - footerH);

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _midScroll = EditorGUILayout.BeginScrollView(_midScroll,
                GUILayout.ExpandWidth(true), GUILayout.Height(scrollH));

            DrawSplineSection();
            DrawGridSection();
            DrawGroupsSection();

            EditorGUILayout.EndScrollView();

            // ── Action buttons — always visible below scroll ──────────────────
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(.3f,.85f,.45f);
            if (GUILayout.Button("  ✓  SAVE PREFAB  ", GUILayout.Height(34)))
                SavePrefab();
            GUI.backgroundColor = new Color(.25f,.6f,1f);
            if (GUILayout.Button("  ▶  TEST IN SCENE  ", GUILayout.Height(34)))
                TestInScene();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SPLINE SECTION
        // ═════════════════════════════════════════════════════════════════════
        private void DrawSplineSection()
        {
            Hdr("TRACK SPLINE");

            // Preset buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Preset:", GUILayout.Width(46));
            string[] presetNames = { "Oval", "Wide", "Rectangle" };
            for (int i = 0; i < presetNames.Length; i++)
            {
                GUI.backgroundColor = _splinePreset == i ? new Color(.4f,.7f,1f) : new Color(.3f,.3f,.33f);
                if (GUILayout.Button(presetNames[i], GUILayout.Height(22)))
                { _splinePreset = i; ApplyPreset(); }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Edit button
            if (!_editingSpline)
            {
                GUI.backgroundColor = new Color(.4f,.7f,1f);
                if (GUILayout.Button("✏  Edit Spline  (Scene View)", GUILayout.Height(26)))
                    StartSplineEdit();
                GUI.backgroundColor = Color.white;
            }
            else
            {
                // Active mode: info + exit button
                EditorGUILayout.HelpBox(
                    "● Drag knots in the Scene View\n" +
                    "● Shift+Click = add knot\n" +
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
            EditorGUILayout.BeginHorizontal();
            _showSafeArea = EditorGUILayout.ToggleLeft("Show Safe Area Guide", _showSafeArea, GUILayout.Width(160));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);
        }

        // ── Start spline edit mode ────────────────────────────────────────────
        private void StartSplineEdit()
        {
            _editingSpline = true;
            _selKnot       = -1;
            _addKnotMode   = false;

            DestroyPreview();

            // Lightweight preview: just a Track GO with SplineContainer
            _previewGo = new GameObject("[SplinePreview]");
            var track = new GameObject("Track");
            track.transform.SetParent(_previewGo.transform, false);
            var sc = track.AddComponent<SplineContainer>();
            WriteKnotsToContainer(sc);

            // Select track and frame it in the scene
            Selection.activeGameObject = track;
            SceneView.lastActiveSceneView?.FrameSelected();
            SceneView.RepaintAll();
        }

        private void StopSplineEdit(bool save)
        {
            _editingSpline = false;
            _addKnotMode   = false;

            if (save && _previewGo != null)
            {
                var sc = _previewGo.GetComponentInChildren<SplineContainer>();
                if (sc != null) ReadKnotsFromContainer(sc);
            }

            DestroyPreview();
            SceneView.RepaintAll();
            Repaint();
        }

        private void DestroyPreview()
        {
            if (_previewGo != null)
            {
                DestroyImmediate(_previewGo);
                _previewGo = null;
            }
        }

        // ── Scene View handles ────────────────────────────────────────────────
        private void OnSceneGUI(SceneView sv)
        {
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

            // FireRange guide box (red)
            float frWidth = _cfg.gridCellSize * Mathf.Max(_gridCols, 4) + 2f;
            Handles.color = new Color(1f, .25f, .25f, .5f);
            Handles.DrawWireCube(new Vector3(0f, .5f, FIRE_Z), new Vector3(frWidth, 1.5f, 3f));
            Handles.color = new Color(1f, .25f, .25f, .2f);
            var fc = GetWireCubeVerts(new Vector3(0, 0.01f, FIRE_Z), new Vector3(frWidth, 0, 3f));
            Handles.DrawSolidRectangleWithOutline(fc, new Color(1f,.25f,.25f,.08f), Color.clear);

            // Slot indicators (yellow)
            int slots = _cfg.slotCount;
            float tw = (slots - 1) * cs;
            Handles.color = new Color(1f, .9f, .1f, .8f);
            for (int i = 0; i < slots; i++)
            {
                var p = new Vector3(-tw * .5f + i * cs, 0f, FIRE_Z - 2f);
                float sz = HandleUtility.GetHandleSize(p) * .12f;
                Handles.SphereHandleCap(0, p, Quaternion.identity, sz, EventType.Repaint);
            }

            // Grid cell outlines
            if (_type == null) return;
            float hw = (_gridCols - 1) * cs * .5f;
            float hd = (_gridRows - 1) * cs * .5f;
            for (int r = 0; r < _gridRows; r++)
            for (int c = 0; c < _gridCols; c++)
            {
                var pos = new Vector3(-hw + c * cs, 0f, FIRE_Z - 4f - hd + r * cs);
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
                    int insertAt = FindInsertIndex(hitPos);
                    _knots.Insert(insertAt, hitPos);
                    _selKnot = insertAt;
                    SyncPreviewSpline();
                    e.Use(); Repaint(); return;
                }
            }

            // Delete → remove selected knot
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete)
            {
                if (_selKnot > 0 && _knots.Count > 3)
                {
                    _knots.RemoveAt(_selKnot);
                    _selKnot = Mathf.Min(_selKnot, _knots.Count - 1);
                    SyncPreviewSpline();
                    e.Use(); Repaint(); return;
                }
            }

            // Handle per knot
            for (int i = 0; i < _knots.Count; i++)
            {
                bool isAnchor = (i == 0);
                bool isSel    = (_selKnot == i);
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
                        np.y = 0f;
                        if (isAnchor) np.z = FIRE_Z;
                        _knots[i] = np;
                        _selKnot  = i;
                        SyncPreviewSpline();
                        Repaint();
                    }
                }
            }

        }

        private void DrawSplineCurveHandles()
        {
            if (_knots.Count < 2) return;
            Handles.color = new Color(.35f, .65f, 1f, .85f);

            for (int i = 0; i < _knots.Count; i++)
            {
                int  nxt  = (i + 1) % _knots.Count;
                int  prv  = (i - 1 + _knots.Count) % _knots.Count;
                int  nxt2 = (nxt + 1) % _knots.Count;

                Vector3 t0 = (_knots[nxt]  - _knots[prv])  * .33f;
                Vector3 t1 = (_knots[nxt2] - _knots[i])    * .33f;

                Handles.DrawBezier(_knots[i], _knots[nxt],
                    _knots[i] + t0, _knots[nxt] - t1,
                    new Color(.35f, .65f, 1f, .85f), null, 2.5f);
            }
        }

        // ── Spline helpers ────────────────────────────────────────────────────
        private void ApplyPreset()
        {
            float hw = _splineWidth * .5f;
            float fz = FIRE_Z;
            float d  = _splineDepth;

            _knots.Clear();
            switch (_splinePreset)
            {
                case 0: // Oval
                    _knots.Add(new Vector3( 0,  0, fz      ));
                    _knots.Add(new Vector3(+hw, 0, fz+d*.5f));
                    _knots.Add(new Vector3( 0,  0, fz+d    ));
                    _knots.Add(new Vector3(-hw, 0, fz+d*.5f));
                    break;
                case 1: // Wide
                    _knots.Add(new Vector3( 0,  0, fz       ));
                    _knots.Add(new Vector3(+hw, 0, fz+d*.25f));
                    _knots.Add(new Vector3( 0,  0, fz+d     ));
                    _knots.Add(new Vector3(-hw, 0, fz+d*.25f));
                    break;
                case 2: // Rectangle — 6 knots, knot 0 always at center-front (X=0)
                    _knots.Add(new Vector3(  0,  0, fz        ));
                    _knots.Add(new Vector3(+hw,  0, fz+d*.15f ));
                    _knots.Add(new Vector3(+hw,  0, fz+d*.85f ));
                    _knots.Add(new Vector3(  0,  0, fz+d      ));
                    _knots.Add(new Vector3(-hw,  0, fz+d*.85f ));
                    _knots.Add(new Vector3(-hw,  0, fz+d*.15f ));
                    break;
            }

            SyncPreviewSpline();
            SceneView.RepaintAll();
            // Frame scene so the new preset shape is immediately visible
            SceneView.lastActiveSceneView?.FrameSelected();
            Repaint();
        }

        private void CopySplineFrom(string srcPath)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
            if (go == null) return;

            List<Vector3> newKnots = null;

            var lr = go.GetComponent<LevelRoot>();
            if (lr != null && lr.splineKnots.Count >= 3)
            {
                newKnots = new List<Vector3>(lr.splineKnots);
            }
            else
            {
                var sc = go.GetComponentInChildren<SplineContainer>();
                if (sc == null) return;
                newKnots = new List<Vector3>();
                var xform = sc.transform;
                foreach (var k in sc.Spline)
                    newKnots.Add(xform.TransformPoint(k.Position));
            }

            if (newKnots == null || newKnots.Count < 3) return;

            // Shift front knot to FIRE_Z
            float minZ = newKnots.Min(v => v.z);
            float offset = FIRE_Z - minZ;
            for (int i = 0; i < newKnots.Count; i++)
                newKnots[i] = new Vector3(newKnots[i].x, 0f, newKnots[i].z + offset);

            // Lock anchor Z exactly
            if (newKnots.Count > 0)
                newKnots[0] = new Vector3(newKnots[0].x, 0f, FIRE_Z);

            _knots = newKnots;
            SyncPreviewSpline();
            SceneView.RepaintAll();
            Repaint();
        }

        private void SyncPreviewSpline()
        {
            if (_previewGo == null) return;
            var sc = _previewGo.GetComponentInChildren<SplineContainer>();
            if (sc != null) WriteKnotsToContainer(sc);
            SceneView.RepaintAll();
        }

        private void WriteKnotsToContainer(SplineContainer sc)
        {
            var spline = sc.Spline;
            spline.Clear();
            foreach (var k in _knots)
                spline.Add(new BezierKnot(new float3(k.x, k.y, k.z)));
            spline.Closed = true;
            for (int i = 0; i < spline.Count; i++)
                spline.SetTangentMode(i, TangentMode.AutoSmooth);
        }

        private void ReadKnotsFromContainer(SplineContainer sc)
        {
            var xform = sc.transform;
            _knots.Clear();
            foreach (var k in sc.Spline)
            {
                Vector3 w = xform.TransformPoint(k.Position);
                w.y = 0f;
                _knots.Add(w);
            }
            // Lock anchor Z
            if (_knots.Count > 0)
                _knots[0] = new Vector3(_knots[0].x, 0f, FIRE_Z);
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
            Hdr("SHOOTER GRID");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Cols", GUILayout.Width(42));
            int nc = EditorGUILayout.IntSlider(_gridCols, 1, MaxCols);
            GUILayout.Label("Rows", GUILayout.Width(34));
            int nr = EditorGUILayout.IntSlider(_gridRows, 1, MaxRows);
            EditorGUILayout.EndHorizontal();
            if (nc != _gridCols || nr != _gridRows)
            { _gridCols = nc; _gridRows = nr; ResizeGrid(); }

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
            EditorGUILayout.LabelField("  Click = select   |   Assign type & color in right panel",
                EditorStyles.miniLabel);
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
                : PC(col);
            if (t == GridCellType.Door) bg = Color.Lerp(bg, Color.black, .5f);

            // Labels
            string lbl1 = t == GridCellType.Empty ? "+"
                        : t == GridCellType.Door   ? "DOOR"
                        : col.ToString().Substring(0, 3).ToUpper();
            string lbl2 = t == GridCellType.Empty ? ""
                        : t == GridCellType.Door   ? $"×{_doors[c,r]}"
                        : _shots[c,r] < 0 ? $"×{_cfg?.defaultShots ?? 3}"
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

            // Click = select + auto-promote empty to ShooterBlock for immediate color access
            Event e = Event.current;
            if (e.type == EventType.MouseDown && cell.Contains(e.mousePosition))
            {
                _selC = c; _selR = r; _selKnot = -1;
                if (_type[c, r] == GridCellType.Empty)
                    _type[c, r] = GridCellType.ShooterBlock;
                e.Use(); Repaint();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  GROUPS SECTION
        // ═════════════════════════════════════════════════════════════════════
        private void DrawGroupsSection()
        {
            Hdr("CONVEYOR GROUPS");
            for (int i = _groups.Count - 1; i >= 0; i--)
            {
                var g = _groups[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = PC(g.color);
                GUILayout.Box("", GUILayout.Width(20), GUILayout.Height(20));
                GUI.backgroundColor = prev;
                g.color     = (BlockColorType)EditorGUILayout.EnumPopup(g.color, GUILayout.Width(72));
                GUILayout.Label("Rows",  GUILayout.Width(32)); g.rowCount  = EditorGUILayout.IntField(g.rowCount,  GUILayout.Width(36));
                GUILayout.Label("Lanes", GUILayout.Width(36)); g.laneCount = EditorGUILayout.IntField(g.laneCount, GUILayout.Width(28));
                if (GUILayout.Button("✕", GUILayout.Width(20), GUILayout.Height(20))) _groups.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ Group", GUILayout.Height(22)))
                _groups.Add(new LevelConveyorGroup
                    { color = BlockColorType.Red,
                      rowCount  = _cfg?.rowsPerGroup ?? 20,
                      laneCount = _cfg?.laneCount    ?? 5 });
            GUILayout.Space(8);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  RIGHT PANEL (inspector)
        // ═════════════════════════════════════════════════════════════════════
        private void DrawRight()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(RightW), GUILayout.ExpandHeight(true));

            // Knot inspector (when in spline edit mode and a knot is selected)
            if (_editingSpline && _selKnot >= 0 && _selKnot < _knots.Count)
            {
                DrawKnotInspector();
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
            GUILayout.Space(20);
            EditorGUILayout.HelpBox(
                "Click a grid cell or\na spline knot to inspect.", MessageType.None);
            EditorGUILayout.EndVertical();
        }

        // ── Knot inspector ────────────────────────────────────────────────────
        private void DrawKnotInspector()
        {
            int  i    = _selKnot;
            bool anch = (i == 0);
            Hdr($"KNOT #{i}{(anch ? "  🔒" : "")}");

            Vector3 k = _knots[i];

            EditorGUI.BeginChangeCheck();
            float nx = EditorGUILayout.FloatField("X", k.x);
            GUILayout.BeginHorizontal();
            float nz = EditorGUILayout.FloatField("Z", anch ? FIRE_Z : k.z);
            if (anch) EditorGUILayout.LabelField("(locked)", EditorStyles.miniLabel, GUILayout.Width(48));
            GUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck() && !anch)
            {
                _knots[i] = new Vector3(nx, 0f, nz);
                SyncPreviewSpline();
                SceneView.RepaintAll();
            }

            if (anch) EditorGUILayout.HelpBox($"FireRange anchor\nZ = {FIRE_Z:F1} locked\nX is free", MessageType.None);

            GUILayout.Space(8);

            if (!anch && _knots.Count > 3)
            {
                GUI.backgroundColor = new Color(1f,.4f,.4f);
                if (GUILayout.Button("Delete Knot", GUILayout.Height(24)))
                {
                    _knots.RemoveAt(i);
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

            bool isBlock = _type[c, r] == GridCellType.ShooterBlock;
            bool isDoor  = _type[c, r] == GridCellType.Door;

            // ── Shooter Block ─────────────────────────────────────────────────
            if (isBlock)
            {
                // Color palette — shown immediately, no extra click needed
                GUILayout.Label("Color:", EditorStyles.miniLabel);
                for (int i = 0; i < Pal.Length; i += 2)
                {
                    EditorGUILayout.BeginHorizontal();
                    for (int j = i; j < Mathf.Min(i + 2, Pal.Length); j++)
                    {
                        var entry = Pal[j];
                        bool isSel = _color[c, r] == entry.t;
                        GUI.backgroundColor = isSel ? entry.c : Color.Lerp(entry.c, Color.black, .4f);
                        var st = new GUIStyle(GUI.skin.button) { fontStyle = isSel ? FontStyle.Bold : FontStyle.Normal };
                        if (isSel) st.normal.textColor = Color.white;
                        if (GUILayout.Button(entry.n, st, GUILayout.Height(28)))
                        { _color[c, r] = entry.t; Repaint(); }
                        GUI.backgroundColor = Color.white;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                GUILayout.Space(6);

                // Shot count: default 100, range 50-200
                GUILayout.Label("Shot Count:", EditorStyles.miniLabel);
                bool usedef = _shots[c, r] < 0;
                bool nd = EditorGUILayout.Toggle("Default", usedef);
                if (nd != usedef) _shots[c, r] = nd ? -1 : (_cfg?.defaultShots ?? 100);
                if (!nd) _shots[c, r] = EditorGUILayout.IntSlider(_shots[c, r], 50, 200);

                GUILayout.Space(8);

                // Type/Clear — compact, at the bottom
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(.5f,.3f,.9f);
                if (GUILayout.Button("→ Door", GUILayout.Height(20)))
                { _type[c, r] = GridCellType.Door; Repaint(); }
                GUI.backgroundColor = new Color(.5f,.18f,.18f);
                if (GUILayout.Button("Set Empty", GUILayout.Height(20)))
                { _type[c, r] = GridCellType.Empty; _selC = -1; _selR = -1; Repaint(); }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            // ── Door ──────────────────────────────────────────────────────────
            else if (isDoor)
            {
                GUILayout.Label("Blocks from door:", EditorStyles.miniLabel);
                _doors[c, r] = EditorGUILayout.IntSlider(_doors[c, r], 1, 15);

                GUILayout.Space(4);
                GUILayout.Label("Door color:", EditorStyles.miniLabel);
                for (int i = 0; i < Pal.Length; i += 2)
                {
                    EditorGUILayout.BeginHorizontal();
                    for (int j = i; j < Mathf.Min(i + 2, Pal.Length); j++)
                    {
                        var entry = Pal[j];
                        bool isSel = _color[c, r] == entry.t;
                        GUI.backgroundColor = isSel ? entry.c : Color.Lerp(entry.c, Color.black, .4f);
                        var st = new GUIStyle(GUI.skin.button) { fontStyle = isSel ? FontStyle.Bold : FontStyle.Normal };
                        if (isSel) st.normal.textColor = Color.white;
                        if (GUILayout.Button(entry.n, st, GUILayout.Height(28)))
                        { _color[c, r] = entry.t; Repaint(); }
                        GUI.backgroundColor = Color.white;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                GUILayout.Space(8);

                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(.35f,.55f,1f);
                if (GUILayout.Button("→ Shooter Block", GUILayout.Height(20)))
                { _type[c, r] = GridCellType.ShooterBlock; Repaint(); }
                GUI.backgroundColor = new Color(.5f,.18f,.18f);
                if (GUILayout.Button("Set Empty", GUILayout.Height(20)))
                { _type[c, r] = GridCellType.Empty; _selC = -1; _selR = -1; Repaint(); }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  LEVEL MANAGEMENT
        // ═════════════════════════════════════════════════════════════════════
        private void NewLevel()
        {
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
            _gridCols   = 4; _gridRows = 2;
            _splinePreset = 0; _splineWidth = 3.5f; _splineDepth = 5f;
            _activeIdx  = -1; _selC = -1; _selR = -1; _selKnot = -1;
            StopSplineEdit(save: false);
            InitGrid();
            _groups.Clear(); DefaultGroups();
            ApplyPreset();
            Repaint();
        }

        private void LoadLevel(int idx)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(_paths[idx]);
            if (go == null) return;
            var lr = go.GetComponent<LevelRoot>();
            if (lr == null) return;

            StopSplineEdit(save: false);

            _levelIndex   = lr.levelIndex;
            _levelName    = lr.levelName;
            _goalType     = lr.goalType;
            _goalAmount   = lr.goalAmount;
            _gridCols     = Mathf.Clamp(lr.gridCols, 1, MaxCols);
            _gridRows     = Mathf.Clamp(lr.gridRows, 1, MaxRows);
            _splineWidth  = lr.splineWidth;
            _splineDepth  = lr.splineDepth;
            _splinePreset = lr.splinePreset;

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
                _groups.Add(new LevelConveyorGroup { color=g.color, rowCount=g.rowCount, laneCount=g.laneCount });

            if (lr.splineKnots.Count >= 3)
                _knots = new List<Vector3>(lr.splineKnots);
            else
                ApplyPreset();

            _selC = -1; _selR = -1; _selKnot = -1;
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

            if (_activeIdx == idx) { _activeIdx = -1; NewLevel(); }
            else if (_activeIdx > idx) _activeIdx--;

            RefreshList();
            Repaint();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SAVE PREFAB
        // ═════════════════════════════════════════════════════════════════════
        private void SavePrefab()
        {
            if (_cfg == null) { EditorUtility.DisplayDialog("Error","LevelEditorConfig not found!","OK"); return; }

            if (_editingSpline) StopSplineEdit(save: true);

            string dir  = _cfg.levelSavePath.TrimEnd('/');
            string name = $"Level_{_levelIndex:000}";
            string path = dir + "/" + name + ".prefab";
            EnsureDir(dir);

            var root = new GameObject(name);
            var lr   = root.AddComponent<LevelRoot>();
            WriteDesignData(lr);
            BuildHierarchy(root.transform, lr);

            // Save generated track mesh as a persistent asset so prefab can reference it
            SaveTrackMeshAsset(root.transform, dir, name);

            PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
            DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshList();

            if (ok)
            {
                _activeIdx = _paths.IndexOf(path);
                Debug.Log($"[LevelEditor] Saved: {path}");
            }
            else Debug.LogError($"[LevelEditor] Failed: {path}");
        }

        private void WriteDesignData(LevelRoot lr)
        {
            lr.levelIndex   = _levelIndex;
            lr.levelName    = _levelName;
            lr.goalType     = _goalType;
            lr.goalAmount   = _goalAmount;
            lr.gridCols     = _gridCols;
            lr.gridRows     = _gridRows;
            lr.splineWidth  = _splineWidth;
            lr.splineDepth  = _splineDepth;
            lr.splinePreset = _splinePreset;
            lr.splineKnots  = new List<Vector3>(_knots);

            lr.cells.Clear();
            for (int c = 0; c < _gridCols; c++)
            for (int r = 0; r < _gridRows; r++)
                lr.cells.Add(new LevelGridCell
                {
                    col=c, row=r, type=_type[c,r], color=_color[c,r],
                    shotCount=_shots[c,r], doorCount=_doors[c,r]
                });

            lr.groups.Clear();
            foreach (var g in _groups)
                lr.groups.Add(new LevelConveyorGroup { color=g.color, rowCount=g.rowCount, laneCount=g.laneCount });
        }

        // ═════════════════════════════════════════════════════════════════════
        //  BUILD HIERARCHY
        // ═════════════════════════════════════════════════════════════════════
        private void BuildHierarchy(Transform root, LevelRoot lr)
        {
            float cs   = _cfg.gridCellSize;
            float slotZ = FIRE_Z - 2f;
            float gridZ = FIRE_Z - 4f;

            // ── Track ──
            var trackGo = Go(root, "Track");
            var sc = trackGo.AddComponent<SplineContainer>();
            WriteKnotsToContainer(sc);
            var cc = trackGo.AddComponent<ConveyorController>();
            cc.speed = _cfg.conveyorSpeed;
            lr.conveyorController = cc;

            // Track mesh — ConveyorTrackMeshBuilder (RequireComponent auto-adds MeshFilter + MeshRenderer)
            var meshBuilder = trackGo.AddComponent<ConveyorTrackMeshBuilder>();
            meshBuilder.beltHalfWidth = 0.45f;
            meshBuilder.wallAboveBelt = 0.18f;
            meshBuilder.railHeight    = 1f;
            meshBuilder.railWidth     = 0.1f;
            meshBuilder.bevelSize     = 0.02f;
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

            if (_cfg.trackSegmentPrefab != null)
            {
                var tr = trackGo.AddComponent<ConveyorTrackRenderer>();
                tr.segmentPrefab = _cfg.trackSegmentPrefab;
                if (_cfg.arrowPrefab != null)
                {
                    tr.arrowPrefab  = _cfg.arrowPrefab;
                    tr.arrowSpacing = _cfg.arrowSpacing;
                }
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
                            bGo.GetComponent<ConveyorBlock3D>()?.SetGroupIndex(row, lane);
                        }
                    }
            }

            // ── FireRange ── (always anchored at FIRE_Z)
            GameObject frGo;
            if (_cfg.fireRangePrefab != null)
            {
                frGo = (GameObject)PrefabUtility.InstantiatePrefab(_cfg.fireRangePrefab, root);
                frGo.name = "FireRange";
            }
            else
            {
                frGo = Go(root, "FireRange");
                var fc = frGo.AddComponent<BoxCollider>();
                fc.isTrigger = true;
                fc.size = new Vector3(1.8f, 2f, 0.8f);
                frGo.AddComponent<FireRange>();
            }
            frGo.transform.localPosition = new Vector3(0f, 0f, FIRE_Z);
            lr.fireRange = frGo.GetComponent<FireRange>();

            // ── SlotDeck ──
            var deckGo = Go(root, "SlotDeck");
            deckGo.transform.localPosition = new Vector3(0f, 0f, slotZ);
            var ss = deckGo.AddComponent<SlotSystem>();
            if (_cfg.slotIndicatorPrefab != null) ss.slotIndicatorPrefab = _cfg.slotIndicatorPrefab;
            lr.slotSystem = ss;

            int   slots = _cfg.slotCount;
            float tw    = (slots - 1) * cs;
            for (int i = 0; i < slots; i++)
                Go(deckGo.transform, $"Slot_{i}").transform.localPosition =
                    new Vector3(-tw*.5f + i*cs, 0f, 0f);

            // ── ShooterGrid ──
            var sgGo = Go(root, "ShooterGrid");
            sgGo.transform.localPosition = new Vector3(0f, 0f, gridZ);
            var sg = sgGo.AddComponent<ShooterGrid>();
            if (_cfg.shooterBlockPrefab != null)
                sg.shooterBlockPrefab = _cfg.shooterBlockPrefab.GetComponent<ShooterBlock>();
            lr.shooterGrid = sg;

            float hw = (_gridCols - 1) * cs * .5f;
            float hd = (_gridRows - 1) * cs * .5f;

            for (int r = 0; r < _gridRows; r++)
            for (int c = 0; c < _gridCols; c++)
            {
                var pos = new Vector3(-hw + c*cs, 0f, -hd + r*cs);
                string nm = $"Cell_r{r}_c{c}";

                switch (_type[c, r])
                {
                    case GridCellType.ShooterBlock when _cfg.shooterBlockPrefab != null:
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(_cfg.shooterBlockPrefab, sgGo.transform);
                        go.name = nm; go.transform.localPosition = pos;
                        int sh = _shots[c,r] >= 0 ? _shots[c,r] : _cfg.defaultShots;
                        var sb = go.GetComponent<ShooterBlock>();
                        sb?.EditorSetup(_color[c,r], sh, c, r);
                        // Apply material directly so prefab shows colors in editor
                        if (sb?.blockRenderer != null && _gameCfg != null)
                        {
                            var mat = _gameCfg.GetMaterial(_color[c,r]);
                            if (mat != null) sb.blockRenderer.sharedMaterial = mat;
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
                    case GridCellType.Empty when _cfg.wallElementPrefab != null:
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(_cfg.wallElementPrefab, sgGo.transform);
                        go.name = nm; go.transform.localPosition = pos;
                        go.GetComponent<WallElement>()?.SetGridPosition(c, r);
                        break;
                    }
                }
            }

            // ── Ground ──
            if (_cfg.groundPrefab != null)
            {
                var gnd = (GameObject)PrefabUtility.InstantiatePrefab(_cfg.groundPrefab, root);
                gnd.name = "Ground";
                gnd.transform.localPosition = new Vector3(0f, -.01f, FIRE_Z + _splineDepth * .3f);
            }
            else
            {
                var gnd = GameObject.CreatePrimitive(PrimitiveType.Plane);
                gnd.name = "Ground";
                gnd.transform.SetParent(root, false);
                gnd.transform.localPosition = new Vector3(0f, -.01f, FIRE_Z + _splineDepth * .3f);
                gnd.transform.localScale    = new Vector3(2f, 1f, 2f);
                DestroyImmediate(gnd.GetComponent<MeshCollider>());
            }
        }

        // ── Track mesh asset ──────────────────────────────────────────────────
        private static void SaveTrackMeshAsset(Transform root, string dir, string name)
        {
            var track = root.Find("Track");
            if (track == null) return;
            var mf = track.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            string meshPath = $"{dir}/{name}_TrackMesh.asset";
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

        // ── Test In Scene ─────────────────────────────────────────────────────
        private void TestInScene()
        {
            SavePrefab();

            string dir  = _cfg.levelSavePath.TrimEnd('/');
            string path = dir + $"/Level_{_levelIndex:000}.prefab";
            var prefab  = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;

            // Kill all DOTween tweens first to prevent MissingReference on destroyed transforms
            DOTween.KillAll();

            // Remove existing LevelRoot instances from scene
            foreach (var lr in FindObjectsByType<LevelRoot>(FindObjectsSortMode.None))
                DestroyImmediate(lr.gameObject);

            // Assign prefab to LevelManager if present
            var lm = FindFirstObjectByType<LevelManager>();
            if (lm != null)
            {
                if (lm.levelPrefabs == null || lm.levelPrefabs.Length == 0)
                    lm.levelPrefabs = new LevelRoot[1];
                lm.levelPrefabs[0] = prefab.GetComponent<LevelRoot>();
                EditorUtility.SetDirty(lm);
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
            var pt=_type; var pc=_color; var ps=_shots; var pd=_doors;
            _type  = new GridCellType  [_gridCols, _gridRows];
            _color = new BlockColorType[_gridCols, _gridRows];
            _shots = new int           [_gridCols, _gridRows];
            _doors = new int           [_gridCols, _gridRows];
            for (int c=0;c<_gridCols;c++) for (int r=0;r<_gridRows;r++)
            {
                _shots[c,r]=-1; _doors[c,r]=5;
                if (pt==null||c>=pt.GetLength(0)||r>=pt.GetLength(1)) continue;
                _type[c,r]=pt[c,r]; _color[c,r]=pc[c,r]; _shots[c,r]=ps[c,r]; _doors[c,r]=pd[c,r];
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
