#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace BlockShooter.Editor
{
    /// <summary>
    /// Scene-view level editor for designing conveyor tracks.
    /// Accessible via BlockShooter > Track Level Editor menu.
    /// </summary>
    public class TrackLevelEditor : EditorWindow
    {
        // ── Tabs ──────────────────────────────────────────────────────────────
        private enum Tab { Track, Feeders, Grid, Export }
        private Tab _tab = Tab.Track;

        // ── Track settings ────────────────────────────────────────────────────
        private ConveyorTrackMesh _mainTrack;
        private TrackShapePreset _shapePreset = TrackShapePreset.RoundedRect;
        private float _presetWidth = 5f;
        private float _presetHeight = 7f;
        private float _presetCornerRadius = 1.5f;
        private float _trackWidth = 2.5f;
        private float _wallHeight = 0.28f;
        private int _meshSegments = 120;

        // ── Feeders ───────────────────────────────────────────────────────────
        private List<FeederEntry> _feeders = new();
        private Vector2 _feederScrollPos;

        // ── Grid ──────────────────────────────────────────────────────────────
        private int _gridCols = 4, _gridRows = 2;
        private GridCellType[,] _cellTypes;
        private BlockColorType[,] _cellColors;
        private int[,] _cellShots;
        private bool _gridInit;

        // ── Export ────────────────────────────────────────────────────────────
        private int _levelIndex = 1;
        private string _levelName = "Level_01";
        private LevelDifficulty _difficulty = LevelDifficulty.Normal;
        private float _conveyorSpeed = 1f;

        private Vector2 _scroll;

        private readonly Color[] _colorMap =
        {
            Color.gray,
            new Color(0.9f, 0.2f, 0.2f),
            new Color(0.2f, 0.5f, 0.9f),
            new Color(0.2f, 0.8f, 0.3f),
            new Color(1f, 0.85f, 0.1f),
            new Color(0.6f, 0.2f, 0.9f),
            new Color(1f, 0.55f, 0.1f)
        };

        [System.Serializable]
        private class FeederEntry
        {
            public FeederPath feederPath;
            public float connectionT = 0.5f;
            public List<BlockColorType> colors = new() { BlockColorType.Red };
            public bool foldout = true;
        }

        private enum TrackShapePreset
        {
            RoundedRect,
            Ellipse,
            Custom
        }

        [MenuItem("BlockShooter/Track Level Editor")]
        public static void Open() => GetWindow<TrackLevelEditor>("Track Level Editor").Show();

        private void OnEnable() => InitGrid();

        private void OnGUI()
        {
            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawHeader();
            DrawTabs();

            switch (_tab)
            {
                case Tab.Track: DrawTrackTab(); break;
                case Tab.Feeders: DrawFeedersTab(); break;
                case Tab.Grid: DrawGridTab(); break;
                case Tab.Export: DrawExportTab(); break;
            }

            GUILayout.EndScrollView();
        }

        // ── Header ─────────────────────────────────────────────────────────────
        private void DrawHeader()
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
                { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField("Block Shooter — Track Level Editor", style, GUILayout.Height(28));
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        private void DrawTabs()
        {
            string[] labels = { "Track Shape", "Feeder Paths", "Shooter Grid", "Export" };
            _tab = (Tab)GUILayout.Toolbar((int)_tab, labels, GUILayout.Height(26));
            EditorGUILayout.Space(6);
        }

        // ── Tab: Track Shape ───────────────────────────────────────────────────
        private void DrawTrackTab()
        {
            EditorGUILayout.LabelField("Main Conveyor Track", EditorStyles.boldLabel);

            _mainTrack = (ConveyorTrackMesh)EditorGUILayout.ObjectField(
                "Track Object", _mainTrack, typeof(ConveyorTrackMesh), true);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Shape Preset", EditorStyles.boldLabel);
            _shapePreset = (TrackShapePreset)EditorGUILayout.EnumPopup("Preset", _shapePreset);

            if (_shapePreset != TrackShapePreset.Custom)
            {
                _presetWidth = EditorGUILayout.FloatField("Width", _presetWidth);
                _presetHeight = EditorGUILayout.FloatField("Height", _presetHeight);
                if (_shapePreset == TrackShapePreset.RoundedRect)
                    _presetCornerRadius = EditorGUILayout.Slider("Corner Radius", _presetCornerRadius, 0.1f, Mathf.Min(_presetWidth, _presetHeight) * 0.5f);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Mesh Settings", EditorStyles.boldLabel);
            _trackWidth = EditorGUILayout.Slider("Track Width", _trackWidth, 0.5f, 5f);
            _wallHeight = EditorGUILayout.Slider("Wall Height", _wallHeight, 0.05f, 0.6f);
            _meshSegments = EditorGUILayout.IntSlider("Mesh Segments", _meshSegments, 20, 300);

            EditorGUILayout.Space(8);

            if (_mainTrack == null)
            {
                GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
                if (GUILayout.Button("Create Main Track in Scene", GUILayout.Height(36)))
                    CreateMainTrack();
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
                if (GUILayout.Button("Apply Shape to Track", GUILayout.Height(36)))
                    ApplyShapePreset();
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("Rebuild Mesh", GUILayout.Height(28)))
                {
                    _mainTrack.trackWidth = _trackWidth;
                    _mainTrack.wallHeight = _wallHeight;
                    _mainTrack.segments = _meshSegments;
                    _mainTrack.Rebuild();
                    EditorUtility.SetDirty(_mainTrack);
                }
            }

            DrawShapePreview();
        }

        private void CreateMainTrack()
        {
            var go = new GameObject("ConveyorMainTrack");
            go.AddComponent<SplineContainer>();
            var trackMesh = go.AddComponent<ConveyorTrackMesh>();
            var pathCtrl = go.AddComponent<ConveyorPathController>();
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<MeshCollider>();

            trackMesh.trackWidth = _trackWidth;
            trackMesh.wallHeight = _wallHeight;
            trackMesh.segments = _meshSegments;

            _mainTrack = trackMesh;
            Selection.activeGameObject = go;

            ApplyShapePreset();
            Undo.RegisterCreatedObjectUndo(go, "Create Main Track");
        }

        private void ApplyShapePreset()
        {
            if (_mainTrack == null) return;
            var splineContainer = _mainTrack.GetComponent<SplineContainer>();
            if (splineContainer == null) return;

            var spline = splineContainer.Spline;
            spline.Clear();

            switch (_shapePreset)
            {
                case TrackShapePreset.RoundedRect:
                    BuildRoundedRectSpline(spline, _presetWidth, _presetHeight, _presetCornerRadius);
                    break;
                case TrackShapePreset.Ellipse:
                    BuildEllipseSpline(spline, _presetWidth, _presetHeight);
                    break;
            }

            _mainTrack.trackWidth = _trackWidth;
            _mainTrack.wallHeight = _wallHeight;
            _mainTrack.segments = _meshSegments;
            _mainTrack.Rebuild();

            EditorUtility.SetDirty(splineContainer);
            EditorUtility.SetDirty(_mainTrack);
        }

        private void BuildRoundedRectSpline(Spline spline, float w, float h, float r)
        {
            spline.Closed = true;
            float hw = w * 0.5f - r;
            float hh = h * 0.5f - r;

            // 8 knots: 2 per corner (straight segments + curved tangents)
            // Top, Right, Bottom, Left
            float tan = r * 0.5523f; // Bezier approximation for quarter circle

            AddRoundedRectKnot(spline, new Vector3(-hw, 0, hh + r), new Vector3(-hw - tan, 0, hh + r), new Vector3(-hw + tan, 0, hh + r));
            AddRoundedRectKnot(spline, new Vector3(hw, 0, hh + r), new Vector3(hw - tan, 0, hh + r), new Vector3(hw + tan, 0, hh + r));
            AddRoundedRectKnot(spline, new Vector3(hw + r, 0, hh), new Vector3(hw + r, 0, hh + tan), new Vector3(hw + r, 0, hh - tan));
            AddRoundedRectKnot(spline, new Vector3(hw + r, 0, -hh), new Vector3(hw + r, 0, -hh + tan), new Vector3(hw + r, 0, -hh - tan));
            AddRoundedRectKnot(spline, new Vector3(hw, 0, -hh - r), new Vector3(hw + tan, 0, -hh - r), new Vector3(hw - tan, 0, -hh - r));
            AddRoundedRectKnot(spline, new Vector3(-hw, 0, -hh - r), new Vector3(-hw + tan, 0, -hh - r), new Vector3(-hw - tan, 0, -hh - r));
            AddRoundedRectKnot(spline, new Vector3(-hw - r, 0, -hh), new Vector3(-hw - r, 0, -hh - tan), new Vector3(-hw - r, 0, -hh + tan));
            AddRoundedRectKnot(spline, new Vector3(-hw - r, 0, hh), new Vector3(-hw - r, 0, hh - tan), new Vector3(-hw - r, 0, hh + tan));
        }

        private void AddRoundedRectKnot(Spline spline, Vector3 pos, Vector3 tangentIn, Vector3 tangentOut)
        {
            var knot = new BezierKnot(pos, tangentIn - pos, tangentOut - pos, Quaternion.identity);
            spline.Add(knot, TangentMode.Broken);
        }

        private void BuildEllipseSpline(Spline spline, float rx, float ry)
        {
            spline.Closed = true;
            float kappa = 0.5523f;

            spline.Add(new BezierKnot(new Vector3(0, 0, ry * 0.5f),
                new float3(-rx * 0.5f * kappa, 0, 0), new float3(rx * 0.5f * kappa, 0, 0)), TangentMode.Broken);
            spline.Add(new BezierKnot(new Vector3(rx * 0.5f, 0, 0),
                new float3(0, 0, ry * 0.5f * kappa), new float3(0, 0, -ry * 0.5f * kappa)), TangentMode.Broken);
            spline.Add(new BezierKnot(new Vector3(0, 0, -ry * 0.5f),
                new float3(rx * 0.5f * kappa, 0, 0), new float3(-rx * 0.5f * kappa, 0, 0)), TangentMode.Broken);
            spline.Add(new BezierKnot(new Vector3(-rx * 0.5f, 0, 0),
                new float3(0, 0, -ry * 0.5f * kappa), new float3(0, 0, ry * 0.5f * kappa)), TangentMode.Broken);
        }

        private void DrawShapePreview()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Shape Preview", EditorStyles.boldLabel);
            Rect previewRect = GUILayoutUtility.GetRect(220, 120);
            EditorGUI.DrawRect(previewRect, new Color(0.12f, 0.12f, 0.18f));

            Handles.BeginGUI();
            DrawShapeInRect(previewRect);
            Handles.EndGUI();
        }

        private void DrawShapeInRect(Rect r)
        {
            float cx = r.center.x, cy = r.center.y;
            float scale = Mathf.Min(r.width, r.height) / (Mathf.Max(_presetWidth, _presetHeight) + 2f);

            Handles.color = new Color(0.7f, 0.7f, 0.75f);
            if (_shapePreset == TrackShapePreset.Ellipse)
            {
                DrawEllipseGizmo(cx, cy, _presetWidth * scale * 0.5f, _presetHeight * scale * 0.5f);
            }
            else
            {
                DrawRoundedRectGizmo(cx, cy, _presetWidth * scale, _presetHeight * scale, _presetCornerRadius * scale);
            }
        }

        private void DrawEllipseGizmo(float cx, float cy, float rx, float ry)
        {
            int segs = 64;
            Vector3 prev = new Vector3(cx + rx, cy, 0);
            for (int i = 1; i <= segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                var next = new Vector3(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry, 0);
                Handles.DrawLine(prev, next);
                prev = next;
            }
        }

        private void DrawRoundedRectGizmo(float cx, float cy, float w, float h, float r)
        {
            float hw = w * 0.5f, hh = h * 0.5f;
            // straight edges
            Handles.DrawLine(new Vector3(cx - hw + r, cy - hh), new Vector3(cx + hw - r, cy - hh));
            Handles.DrawLine(new Vector3(cx - hw + r, cy + hh), new Vector3(cx + hw - r, cy + hh));
            Handles.DrawLine(new Vector3(cx - hw, cy - hh + r), new Vector3(cx - hw, cy + hh - r));
            Handles.DrawLine(new Vector3(cx + hw, cy - hh + r), new Vector3(cx + hw, cy + hh - r));
            // corners
            DrawArcGizmo(cx + hw - r, cy + hh - r, r, 0, 90);
            DrawArcGizmo(cx - hw + r, cy + hh - r, r, 90, 90);
            DrawArcGizmo(cx - hw + r, cy - hh + r, r, 180, 90);
            DrawArcGizmo(cx + hw - r, cy - hh + r, r, 270, 90);
        }

        private void DrawArcGizmo(float cx, float cy, float r, float startDeg, float spanDeg)
        {
            int segs = 12;
            float step = spanDeg / segs * Mathf.Deg2Rad;
            float start = startDeg * Mathf.Deg2Rad;
            for (int i = 0; i < segs; i++)
            {
                var a = new Vector3(cx + Mathf.Cos(start + i * step) * r, cy + Mathf.Sin(start + i * step) * r);
                var b = new Vector3(cx + Mathf.Cos(start + (i + 1) * step) * r, cy + Mathf.Sin(start + (i + 1) * step) * r);
                Handles.DrawLine(a, b);
            }
        }

        // ── Tab: Feeder Paths ──────────────────────────────────────────────────
        private void DrawFeedersTab()
        {
            EditorGUILayout.LabelField("Feeder / Input Paths", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Each feeder connects to the main track at a T value (0-1).\n" +
                "When that section of the main track is empty, the feeder sends its next block group.",
                MessageType.Info);

            _feederScrollPos = GUILayout.BeginScrollView(_feederScrollPos, GUILayout.MaxHeight(400));

            for (int i = 0; i < _feeders.Count; i++)
            {
                var f = _feeders[i];
                f.foldout = EditorGUILayout.BeginFoldoutHeaderGroup(f.foldout, $"Feeder {i + 1}");
                if (f.foldout)
                {
                    EditorGUI.indentLevel++;
                    f.feederPath = (FeederPath)EditorGUILayout.ObjectField("Feeder Path", f.feederPath, typeof(FeederPath), true);
                    f.connectionT = EditorGUILayout.Slider("Connection T on Main Track", f.connectionT, 0f, 1f);

                    EditorGUILayout.LabelField("Block Group Colors (in order):");
                    for (int j = 0; j < f.colors.Count; j++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        f.colors[j] = (BlockColorType)EditorGUILayout.EnumPopup($"  Group {j + 1}", f.colors[j]);
                        GUI.backgroundColor = Color.red;
                        if (GUILayout.Button("X", GUILayout.Width(22))) { f.colors.RemoveAt(j); j--; }
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();
                    }

                    if (GUILayout.Button("+ Add Color Group")) f.colors.Add(BlockColorType.Red);

                    GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
                    if (GUILayout.Button("Create Feeder Object in Scene")) CreateFeederInScene(f, i);
                    GUI.backgroundColor = Color.white;
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button($"Remove Feeder {i + 1}", GUILayout.Height(20))) { _feeders.RemoveAt(i); i--; }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.Space(4);
            }

            GUILayout.EndScrollView();

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
            if (GUILayout.Button("+ Add Feeder Path", GUILayout.Height(32))) _feeders.Add(new FeederEntry());
            GUI.backgroundColor = Color.white;
        }

        private void CreateFeederInScene(FeederEntry entry, int index)
        {
            var go = new GameObject($"FeederPath_{index + 1}");
            go.AddComponent<SplineContainer>();
            var feeder = go.AddComponent<FeederPath>();
            go.AddComponent<ConveyorTrackMesh>();
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<MeshCollider>();

            feeder.groupColors = new List<BlockColorType>(entry.colors);

            // Create ConnectionPoint
            var cpGo = new GameObject("ConnectionPoint");
            cpGo.transform.SetParent(go.transform);
            var cp = cpGo.AddComponent<ConnectionPoint>();
            cp.mainSplineT = entry.connectionT;
            cp.feederPath = feeder;

            if (_mainTrack != null)
            {
                var pathCtrl = _mainTrack.GetComponent<ConveyorPathController>();
                if (pathCtrl != null)
                {
                    feeder.mainPath = pathCtrl;
                    feeder.connectionPoint = cp;
                    pathCtrl.connectionPoints.Add(cp);
                    EditorUtility.SetDirty(pathCtrl);
                }
            }

            entry.feederPath = feeder;
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Feeder Path");
        }

        // ── Tab: Shooter Grid ──────────────────────────────────────────────────
        private void DrawGridTab()
        {
            EditorGUILayout.LabelField("Shooter Block Grid", EditorStyles.boldLabel);

            int newCols = EditorGUILayout.IntSlider("Columns", _gridCols, 1, 6);
            int newRows = EditorGUILayout.IntSlider("Rows", _gridRows, 1, 3);
            if (newCols != _gridCols || newRows != _gridRows)
            {
                _gridCols = newCols;
                _gridRows = newRows;
                InitGrid();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("Left-click: cycle Empty→ShooterBlock→Door\nRight-click: color/options menu", MessageType.None);

            float cellSize = 58f;
            for (int r = _gridRows - 1; r >= 0; r--)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(10);
                for (int c = 0; c < _gridCols; c++) DrawGridCell(c, r, cellSize);
                GUILayout.EndHorizontal();
                GUILayout.Space(3);
            }
        }

        private void InitGrid()
        {
            _cellTypes = new GridCellType[_gridCols, _gridRows];
            _cellColors = new BlockColorType[_gridCols, _gridRows];
            _cellShots = new int[_gridCols, _gridRows];

            for (int c = 0; c < _gridCols; c++)
                for (int r = 0; r < _gridRows; r++)
                {
                    _cellTypes[c, r] = GridCellType.ShooterBlock;
                    _cellColors[c, r] = BlockColorType.Red;
                    _cellShots[c, r] = 100;
                }

            _gridInit = true;
        }

        private void DrawGridCell(int col, int row, float size)
        {
            var ct = _cellTypes[col, row];
            var cc = _cellColors[col, row];

            Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            Color bg = ct == GridCellType.Empty ? new Color(0.15f, 0.15f, 0.15f)
                : ct == GridCellType.Door ? new Color(0.55f, 0.38f, 0.1f)
                : _colorMap[(int)cc];
            EditorGUI.DrawRect(rect, bg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), Color.black);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), Color.black);

            var style = new GUIStyle(EditorStyles.miniLabel)
                { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }, fontSize = 9 };
            string label = ct == GridCellType.Empty ? "EMPTY"
                : ct == GridCellType.Door ? "DOOR"
                : $"{cc.ToString()[..2]}\n{_cellShots[col, row]}";
            GUI.Label(rect, label, style);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 0)
                    _cellTypes[col, row] = ct switch
                    {
                        GridCellType.Empty => GridCellType.ShooterBlock,
                        GridCellType.ShooterBlock => GridCellType.Door,
                        _ => GridCellType.Empty
                    };
                else ShowGridContextMenu(col, row);
                Event.current.Use();
                Repaint();
            }
        }

        private void ShowGridContextMenu(int col, int row)
        {
            var menu = new GenericMenu();
            foreach (BlockColorType ct in System.Enum.GetValues(typeof(BlockColorType)))
            {
                if (ct == BlockColorType.None) continue;
                var cap = ct;
                menu.AddItem(new GUIContent($"Color/{ct}"), _cellColors[col, row] == ct,
                    () => { _cellColors[col, row] = cap; Repaint(); });
            }
            foreach (int s in new[] { 50, 100, 150, 200 })
            {
                var cap = s;
                menu.AddItem(new GUIContent($"Shots/{s}"), _cellShots[col, row] == s,
                    () => { _cellShots[col, row] = cap; Repaint(); });
            }
            menu.ShowAsContext();
        }

        // ── Tab: Export ────────────────────────────────────────────────────────
        private void DrawExportTab()
        {
            EditorGUILayout.LabelField("Export Level", EditorStyles.boldLabel);
            _levelIndex = EditorGUILayout.IntField("Level Index", _levelIndex);
            _levelName = EditorGUILayout.TextField("File Name", _levelName);
            _difficulty = (LevelDifficulty)EditorGUILayout.EnumPopup("Difficulty", _difficulty);
            _conveyorSpeed = EditorGUILayout.Slider("Conveyor Speed Multiplier", _conveyorSpeed, 0.5f, 3f);

            EditorGUILayout.Space(8);

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
            if (GUILayout.Button("Export LevelData ScriptableObject", GUILayout.Height(40)))
                ExportLevelData();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Apply All Settings to Scene Objects", GUILayout.Height(28)))
                ApplyToScene();
        }

        private void ExportLevelData()
        {
            var asset = ScriptableObject.CreateInstance<LevelData>();
            asset.levelIndex = _levelIndex;
            asset.levelName = _levelName;
            asset.difficulty = _difficulty;
            asset.conveyorSpeedMultiplier = _conveyorSpeed;

            asset.gridCells = new List<GridCellData>();
            if (_gridInit)
            {
                for (int c = 0; c < _gridCols; c++)
                    for (int r = 0; r < _gridRows; r++)
                    {
                        if (_cellTypes[c, r] == GridCellType.Empty) continue;
                        asset.gridCells.Add(new GridCellData
                        {
                            column = c, row = r,
                            cellType = _cellTypes[c, r],
                            color = _cellColors[c, r],
                            customShotCount = _cellShots[c, r]
                        });
                    }
            }

            asset.availableColors = new List<BlockColorType>();
            foreach (var cell in asset.gridCells)
                if (!asset.availableColors.Contains(cell.color))
                    asset.availableColors.Add(cell.color);

            string path = $"Assets/Project Files/Game/ScriptableObjects/Levels/{_levelName}.asset";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorUtility.DisplayDialog("Exported!", $"Saved to:\n{path}", "OK");
        }

        private void ApplyToScene()
        {
            if (_mainTrack != null)
            {
                _mainTrack.trackWidth = _trackWidth;
                _mainTrack.wallHeight = _wallHeight;
                _mainTrack.segments = _meshSegments;
                _mainTrack.Rebuild();
                EditorUtility.SetDirty(_mainTrack);
            }

            foreach (var f in _feeders)
            {
                if (f.feederPath == null) continue;
                var cp = f.feederPath.connectionPoint;
                if (cp != null) cp.mainSplineT = f.connectionT;
                f.feederPath.groupColors = new List<BlockColorType>(f.colors);
                EditorUtility.SetDirty(f.feederPath);
            }
        }
    }
}
#endif
