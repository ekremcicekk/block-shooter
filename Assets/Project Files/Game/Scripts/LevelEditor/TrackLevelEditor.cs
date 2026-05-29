#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace BlockShooter.Editor
{
    /// <summary>
    /// Track Level Editor — BlockShooter > Track Level Editor
    ///
    /// Workflow:
    ///   1. Create Track Prefab  →  gives a GameObject with SplineContainer + ConveyorTrackRenderer
    ///   2. Edit spline freely in Scene view (any shape — Unity Spline handles it)
    ///   3. Assign segment prefab + arrow prefab in Inspector
    ///   4. Add Feeder Paths if needed
    ///   5. Save as prefab  →  assign to LevelData.trackPrefab
    /// </summary>
    public class TrackLevelEditor : EditorWindow
    {
        private enum Tab { CreateTrack, Feeders, QuickShapes, Help }
        private Tab _tab = Tab.CreateTrack;

        // ── Create Track ──────────────────────────────────────────────────────
        private string _trackPrefabName = "Track_Level01";
        private GameObject _segmentPrefab;
        private GameObject _arrowPrefab;
        private int _arrowCount = 3;
        private float _blockSpeed = 1.5f;
        private ConveyorTrackRenderer _activeTrack;

        // Block prefab & group colors shown in CreateTrack tab
        private ConveyorBlock3D _blockPrefab;
        private readonly List<BlockColorType> _groupColors = new() { BlockColorType.Red, BlockColorType.Blue };

        // ── Feeders ───────────────────────────────────────────────────────────
        private readonly List<FeederEntry> _feeders = new();
        private Vector2 _feederScroll;

        // ── Quick Shapes ──────────────────────────────────────────────────────
        private TrackShape _quickShape = TrackShape.RoundedRect;
        private float _shapeW = 4f, _shapeH = 6f, _shapeR = 1.2f;
        private Vector2 _scroll;

        private class FeederEntry
        {
            public string name = "Feeder_1";
            public float connectionT = 0.5f;
            public List<BlockColorType> groupColors = new() { BlockColorType.Red };
            public GameObject feederSegmentPrefab;
            public bool foldout = true;
        }

        private enum TrackShape { RoundedRect, Ellipse, Figure8, Spiral }

        [MenuItem("BlockShooter/Track Level Editor")]
        public static void Open() => GetWindow<TrackLevelEditor>("Track Level Editor").minSize = new Vector2(360, 500);

        private void OnGUI()
        {
            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawHeader();
            DrawTabs();

            switch (_tab)
            {
                case Tab.CreateTrack:   DrawCreateTrackTab(); break;
                case Tab.Feeders:       DrawFeedersTab(); break;
                case Tab.QuickShapes:   DrawQuickShapesTab(); break;
                case Tab.Help:          DrawHelpTab(); break;
            }

            GUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            var s = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField("Block Shooter — Track Level Editor", s, GUILayout.Height(26));
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        private void DrawTabs()
        {
            _tab = (Tab)GUILayout.Toolbar((int)_tab,
                new[] { "Create Track", "Feeders", "Quick Shapes", "Help" },
                GUILayout.Height(26));
            EditorGUILayout.Space(6);
        }

        // ── Tab: Create Track ─────────────────────────────────────────────────
        private void DrawCreateTrackTab()
        {
            EditorGUILayout.LabelField("1. Track Prefab Settings", EditorStyles.boldLabel);

            _trackPrefabName = EditorGUILayout.TextField("Prefab Name", _trackPrefabName);
            _segmentPrefab   = (GameObject)EditorGUILayout.ObjectField("Segment Prefab", _segmentPrefab, typeof(GameObject), false);
            _arrowPrefab     = (GameObject)EditorGUILayout.ObjectField("Arrow Prefab",   _arrowPrefab,   typeof(GameObject), false);
            _arrowCount      = EditorGUILayout.IntSlider("Arrow Count", _arrowCount, 0, 8);
            _blockSpeed      = EditorGUILayout.FloatField("Block Speed", _blockSpeed);

            EditorGUILayout.Space(4);
            _blockPrefab = (ConveyorBlock3D)EditorGUILayout.ObjectField("Block Prefab", _blockPrefab, typeof(ConveyorBlock3D), false);
            EditorGUILayout.LabelField("Block Group Colors:", EditorStyles.boldLabel);
            for (int i = 0; i < _groupColors.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _groupColors[i] = (BlockColorType)EditorGUILayout.EnumPopup($"  Group {i + 1}", _groupColors[i]);
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("X", GUILayout.Width(22))) { _groupColors.RemoveAt(i); i--; }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ Renk Grubu Ekle")) _groupColors.Add(BlockColorType.Green);

            EditorGUILayout.HelpBox(
                "Segment Prefab: kısa, düz track parçası. +Z eksenine bakmalı, origin'de ortalanmalı.\n" +
                "Block Prefab: ConveyorBlock3D prefabı — track üzerindeki bloklar.\n" +
                "Block Group Colors: her renk için 5×20=100 blok grubu oluşturulur.",
                MessageType.Info);

            EditorGUILayout.Space(6);

            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("Create Track in Scene", GUILayout.Height(36)))
                CreateTrackInScene();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(6);

            _activeTrack = (ConveyorTrackRenderer)EditorGUILayout.ObjectField(
                "Active Track", _activeTrack, typeof(ConveyorTrackRenderer), true);

            if (_activeTrack != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("2. Edit spline in Scene view (any shape)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Unity Spline aracını kullan:\n" +
                    "• Knot ekle: Ctrl+click on spline\n" +
                    "• Knot sil: seç + Delete\n" +
                    "• Tangent modu: knot üzerinde sağ tık\n\n" +
                    "Herhangi bir şekil çizebilirsin — kare, elips, spiral, D harfi...",
                    MessageType.None);

                if (GUILayout.Button("Rebuild Track Mesh", GUILayout.Height(28)))
                {
                    _activeTrack.RebuildInEditor();
                    EditorUtility.SetDirty(_activeTrack);
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("3. Save as Prefab", EditorStyles.boldLabel);

                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
                if (GUILayout.Button("Save Track as Prefab", GUILayout.Height(36)))
                    SaveTrackAsPrefab();
                GUI.backgroundColor = Color.white;
            }
        }

        private void CreateTrackInScene()
        {
            var go = new GameObject($"ConveyorTrack_{_trackPrefabName}");
            go.transform.position = new Vector3(0f, 0.1f, 3f);

            var splineContainer = go.AddComponent<SplineContainer>();
            splineContainer.Spline.Closed = true;

            var renderer = go.AddComponent<ConveyorTrackRenderer>();
            renderer.segmentPrefab = _segmentPrefab;
            renderer.arrowPrefab   = _arrowPrefab;
            renderer.arrowCount    = _arrowCount;
            renderer.blockSpeed    = _blockSpeed;

            var pathCtrl = go.AddComponent<ConveyorPathController>();
            pathCtrl.speed       = _blockSpeed;
            pathCtrl.loop        = true;
            pathCtrl.blockPrefab = _blockPrefab;
            pathCtrl.groupColors = _groupColors.ToArray();

            // Apply default rounded-rect shape so it's not empty
            ApplyRoundedRect(splineContainer, _shapeW, _shapeH, _shapeR);

            _activeTrack = renderer;
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Conveyor Track");

            EditorUtility.DisplayDialog("Track Oluşturuldu",
                "Sahneye eklendi.\nSpline'ı Scene view'da serbestçe düzenleyebilirsin.\n" +
                "Bitince 'Save Track as Prefab' ile kaydet.", "Tamam");
        }

        private void SaveTrackAsPrefab()
        {
            if (_activeTrack == null) { EditorUtility.DisplayDialog("Hata", "Active Track seçili değil.", "Tamam"); return; }

            string dir = "Assets/Project Files/Game/Prefabs/Tracks";
            EnsureDirectory(dir);
            string path = $"{dir}/{_trackPrefabName}.prefab";

            bool success;
            PrefabUtility.SaveAsPrefabAsset(_activeTrack.gameObject, path, out success);

            if (success)
                EditorUtility.DisplayDialog("Kaydedildi!", $"{path}\n\nArtık LevelData.trackPrefab alanına atayabilirsin.", "Tamam");
            else
                EditorUtility.DisplayDialog("Hata", "Prefab kaydedilemedi.", "Tamam");
        }

        // ── Tab: Feeders ───────────────────────────────────────────────────────
        private void DrawFeedersTab()
        {
            EditorGUILayout.LabelField("Feeder / Girdi Pathleri", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Feeder path boş alan tespit edince blokları uçurarak ana conveyor'a gönderir.\n" +
                "Mesh bağlantısı YOK — bloklar direkt uçar.",
                MessageType.Info);

            _feederScroll = GUILayout.BeginScrollView(_feederScroll, GUILayout.MaxHeight(380));

            for (int i = 0; i < _feeders.Count; i++)
            {
                var f = _feeders[i];
                f.foldout = EditorGUILayout.BeginFoldoutHeaderGroup(f.foldout, $"Feeder {i + 1} — {f.name}");
                if (f.foldout)
                {
                    EditorGUI.indentLevel++;
                    f.name = EditorGUILayout.TextField("Name", f.name);
                    f.connectionT = EditorGUILayout.Slider("Connection T (main track)", f.connectionT, 0f, 1f);
                    f.feederSegmentPrefab = (GameObject)EditorGUILayout.ObjectField(
                        "Segment Prefab", f.feederSegmentPrefab, typeof(GameObject), false);

                    EditorGUILayout.LabelField("Block Group Colors (sırayla):");
                    for (int j = 0; j < f.groupColors.Count; j++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        f.groupColors[j] = (BlockColorType)EditorGUILayout.EnumPopup($"  Group {j + 1}", f.groupColors[j]);
                        GUI.backgroundColor = Color.red;
                        if (GUILayout.Button("X", GUILayout.Width(22))) { f.groupColors.RemoveAt(j); j--; }
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();
                    }
                    if (GUILayout.Button("+ Renk Grubu Ekle")) f.groupColors.Add(BlockColorType.Blue);

                    GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
                    if (GUILayout.Button("Create Feeder in Scene", GUILayout.Height(28)))
                        CreateFeederInScene(f);
                    GUI.backgroundColor = Color.white;

                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button($"Feeder {i + 1} Sil", GUILayout.Height(20))) { _feeders.RemoveAt(i); i--; }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.Space(3);
            }

            GUILayout.EndScrollView();

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
            if (GUILayout.Button("+ Feeder Ekle", GUILayout.Height(32)))
                _feeders.Add(new FeederEntry { name = $"Feeder_{_feeders.Count + 1}" });
            GUI.backgroundColor = Color.white;
        }

        private void CreateFeederInScene(FeederEntry entry)
        {
            if (_activeTrack == null) { Debug.LogWarning("[TrackLevelEditor] Önce bir ConveyorTrack oluştur/seç."); return; }

            var go = new GameObject(entry.name);
            go.AddComponent<SplineContainer>();

            var feeder = go.AddComponent<FlyingBlockFeeder>();
            feeder.mainPath       = _activeTrack.GetComponent<ConveyorPathController>();
            feeder.mainConnectionT = entry.connectionT;
            feeder.groupColors    = new List<BlockColorType>(entry.groupColors);

            var feederRenderer = go.AddComponent<ConveyorTrackRenderer>();
            feederRenderer.segmentPrefab = entry.feederSegmentPrefab != null
                ? entry.feederSegmentPrefab : _segmentPrefab;
            feederRenderer.arrowCount = 0; // feeders don't need arrows

            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Feeder Path");
        }

        // ── Tab: Quick Shapes ─────────────────────────────────────────────────
        private void DrawQuickShapesTab()
        {
            EditorGUILayout.LabelField("Hızlı Şekil Uygula", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Active Track'e seçilen şekli uygular.\n" +
                "Uyguladıktan sonra Scene view'da knot'ları serbestçe taşıyabilirsin.",
                MessageType.Info);

            _activeTrack = (ConveyorTrackRenderer)EditorGUILayout.ObjectField(
                "Target Track", _activeTrack, typeof(ConveyorTrackRenderer), true);

            EditorGUILayout.Space(4);
            _quickShape = (TrackShape)EditorGUILayout.EnumPopup("Shape", _quickShape);
            _shapeW = EditorGUILayout.FloatField("Width", _shapeW);
            _shapeH = EditorGUILayout.FloatField("Height", _shapeH);

            if (_quickShape == TrackShape.RoundedRect)
                _shapeR = EditorGUILayout.Slider("Corner Radius", _shapeR, 0.1f,
                    Mathf.Min(_shapeW, _shapeH) * 0.45f);

            EditorGUILayout.Space(6);
            DrawShapePreview();
            EditorGUILayout.Space(6);

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
            if (GUILayout.Button("Apply Shape", GUILayout.Height(36)))
                ApplyQuickShape();
            GUI.backgroundColor = Color.white;
        }

        private void ApplyQuickShape()
        {
            if (_activeTrack == null) { EditorUtility.DisplayDialog("Hata", "Target Track seçili değil.", "Tamam"); return; }
            var sc = _activeTrack.GetComponent<SplineContainer>();
            if (sc == null) return;

            switch (_quickShape)
            {
                case TrackShape.RoundedRect: ApplyRoundedRect(sc, _shapeW, _shapeH, _shapeR); break;
                case TrackShape.Ellipse:     ApplyEllipse(sc, _shapeW, _shapeH); break;
                case TrackShape.Figure8:     ApplyFigure8(sc, _shapeW, _shapeH); break;
                case TrackShape.Spiral:      ApplySpiral(sc, _shapeW, _shapeH); break;
            }

            _activeTrack.RebuildInEditor();
            EditorUtility.SetDirty(sc);
            EditorUtility.SetDirty(_activeTrack);
        }

        // ── Shape Builders ────────────────────────────────────────────────────

        private void ApplyRoundedRect(SplineContainer sc, float w, float h, float r)
        {
            var spline = sc.Spline;
            spline.Clear();
            spline.Closed = true;

            float hw = w * 0.5f - r, hh = h * 0.5f - r;
            float k = r * 0.5523f;

            AddKnot(spline, new float3(-hw, 0,  hh + r), new float3(-k, 0, 0), new float3( k, 0, 0));
            AddKnot(spline, new float3( hw, 0,  hh + r), new float3(-k, 0, 0), new float3( k, 0, 0));
            AddKnot(spline, new float3( hw + r, 0,  hh), new float3(0, 0,  k), new float3(0, 0, -k));
            AddKnot(spline, new float3( hw + r, 0, -hh), new float3(0, 0,  k), new float3(0, 0, -k));
            AddKnot(spline, new float3( hw, 0, -hh - r), new float3( k, 0, 0), new float3(-k, 0, 0));
            AddKnot(spline, new float3(-hw, 0, -hh - r), new float3( k, 0, 0), new float3(-k, 0, 0));
            AddKnot(spline, new float3(-hw - r, 0, -hh), new float3(0, 0, -k), new float3(0, 0,  k));
            AddKnot(spline, new float3(-hw - r, 0,  hh), new float3(0, 0, -k), new float3(0, 0,  k));
        }

        private void ApplyEllipse(SplineContainer sc, float rx, float ry)
        {
            var spline = sc.Spline;
            spline.Clear();
            spline.Closed = true;
            float hrx = rx * 0.5f, hry = ry * 0.5f;
            float k = 0.5523f;

            AddKnot(spline, new float3(0, 0,  hry), new float3(-hrx * k, 0, 0), new float3( hrx * k, 0, 0));
            AddKnot(spline, new float3( hrx, 0, 0), new float3(0, 0,  hry * k), new float3(0, 0, -hry * k));
            AddKnot(spline, new float3(0, 0, -hry), new float3( hrx * k, 0, 0), new float3(-hrx * k, 0, 0));
            AddKnot(spline, new float3(-hrx, 0, 0), new float3(0, 0, -hry * k), new float3(0, 0,  hry * k));
        }

        private void ApplyFigure8(SplineContainer sc, float rx, float ry)
        {
            var spline = sc.Spline;
            spline.Clear();
            spline.Closed = true;
            float hrx = rx * 0.5f, hry = ry * 0.5f;
            float k = 0.5523f;

            // Top loop
            AddKnot(spline, new float3(0, 0, 0),       new float3(-hrx * k, 0,  hry * 0.2f), new float3( hrx * k, 0, -hry * 0.2f));
            AddKnot(spline, new float3( hrx, 0,  hry * 0.5f), new float3(0, 0, -hry * k * 0.5f), new float3(0, 0, hry * k * 0.5f));
            AddKnot(spline, new float3(0, 0,  hry),    new float3( hrx * k, 0, 0), new float3(-hrx * k, 0, 0));
            AddKnot(spline, new float3(-hrx, 0,  hry * 0.5f), new float3(0, 0,  hry * k * 0.5f), new float3(0, 0, -hry * k * 0.5f));
            // Bottom loop
            AddKnot(spline, new float3(0, 0, 0),       new float3( hrx * k, 0,  hry * 0.2f), new float3(-hrx * k, 0, -hry * 0.2f));
            AddKnot(spline, new float3(-hrx, 0, -hry * 0.5f), new float3(0, 0,  hry * k * 0.5f), new float3(0, 0, -hry * k * 0.5f));
            AddKnot(spline, new float3(0, 0, -hry),    new float3(-hrx * k, 0, 0), new float3( hrx * k, 0, 0));
            AddKnot(spline, new float3( hrx, 0, -hry * 0.5f), new float3(0, 0, -hry * k * 0.5f), new float3(0, 0,  hry * k * 0.5f));
        }

        private void ApplySpiral(SplineContainer sc, float rx, float ry)
        {
            // Closed spiral-like path (D shape with extended tail, like Level 3 in reference)
            var spline = sc.Spline;
            spline.Clear();
            spline.Closed = false; // open path for spiral

            float hrx = rx * 0.5f, hry = ry * 0.5f;
            float k = 0.5523f;

            // Main loop (like reference game Level 3 "d" shape)
            AddKnot(spline, new float3(0, 0, -hry),    new float3( hrx * k, 0, 0), new float3(-hrx * k, 0, 0));
            AddKnot(spline, new float3(-hrx, 0, 0),    new float3(0, 0, -hry * k), new float3(0, 0,  hry * k));
            AddKnot(spline, new float3(0, 0,  hry),    new float3(-hrx * k, 0, 0), new float3( hrx * k, 0, 0));
            AddKnot(spline, new float3( hrx, 0, 0),    new float3(0, 0,  hry * k), new float3(0, 0, -hry * k));
            AddKnot(spline, new float3(0, 0, -hry * 0.2f), new float3( hrx * 0.3f, 0, 0), new float3(-hrx * 0.3f, 0, 0));
            // Tail going up-right (feeder input arm)
            AddKnot(spline, new float3( hrx * 0.6f, 0, hry * 0.8f), new float3(0, 0, -hry * 0.5f), new float3(0, 0, hry * 0.5f));
            AddKnot(spline, new float3( hrx * 1.2f, 0, hry * 1.8f), new float3(-hrx * 0.3f, 0, -hry * 0.3f), new float3(hrx * 0.3f, 0, hry * 0.3f));
        }

        private void AddKnot(Spline spline, float3 pos, float3 tanIn, float3 tanOut)
        {
            spline.Add(new BezierKnot(pos, tanIn, tanOut, quaternion.identity), TangentMode.Broken);
        }

        // ── Shape Preview ─────────────────────────────────────────────────────

        private void DrawShapePreview()
        {
            Rect r = GUILayoutUtility.GetRect(240, 110);
            EditorGUI.DrawRect(r, new Color(0.1f, 0.1f, 0.15f));
            Handles.BeginGUI();
            Handles.color = new Color(0.4f, 0.9f, 0.6f);

            float cx = r.center.x, cy = r.center.y;
            float scale = Mathf.Min(r.width, r.height) / (Mathf.Max(_shapeW, _shapeH) + 1f);
            float hw = _shapeW * 0.5f * scale, hh = _shapeH * 0.5f * scale;

            switch (_quickShape)
            {
                case TrackShape.RoundedRect:
                    float cr = _shapeR * scale;
                    Handles.DrawLine(new Vector3(cx - hw + cr, cy - hh), new Vector3(cx + hw - cr, cy - hh));
                    Handles.DrawLine(new Vector3(cx - hw + cr, cy + hh), new Vector3(cx + hw - cr, cy + hh));
                    Handles.DrawLine(new Vector3(cx - hw, cy - hh + cr), new Vector3(cx - hw, cy + hh - cr));
                    Handles.DrawLine(new Vector3(cx + hw, cy - hh + cr), new Vector3(cx + hw, cy + hh - cr));
                    DrawArc(cx + hw - cr, cy + hh - cr, cr, 0, 90);
                    DrawArc(cx - hw + cr, cy + hh - cr, cr, 90, 90);
                    DrawArc(cx - hw + cr, cy - hh + cr, cr, 180, 90);
                    DrawArc(cx + hw - cr, cy - hh + cr, cr, 270, 90);
                    break;
                case TrackShape.Ellipse:
                    DrawEllipse(cx, cy, hw, hh); break;
                case TrackShape.Figure8:
                    DrawEllipse(cx, cy - hh * 0.5f, hw, hh * 0.5f);
                    DrawEllipse(cx, cy + hh * 0.5f, hw, hh * 0.5f);
                    break;
                case TrackShape.Spiral:
                    DrawEllipse(cx - hw * 0.15f, cy, hw * 0.85f, hh * 0.85f);
                    Handles.DrawLine(new Vector3(cx + hw * 0.15f, cy), new Vector3(cx + hw * 1.0f, cy - hh * 0.8f));
                    break;
            }
            Handles.EndGUI();
        }

        private void DrawEllipse(float cx, float cy, float rx, float ry)
        {
            int n = 48;
            Vector3 prev = new Vector3(cx + rx, cy);
            for (int i = 1; i <= n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var next = new Vector3(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
                Handles.DrawLine(prev, next);
                prev = next;
            }
        }

        private void DrawArc(float cx, float cy, float r, float startDeg, float span)
        {
            int n = 8;
            float step = span / n * Mathf.Deg2Rad, start = startDeg * Mathf.Deg2Rad;
            for (int i = 0; i < n; i++)
            {
                Handles.DrawLine(
                    new Vector3(cx + Mathf.Cos(start + i * step) * r, cy + Mathf.Sin(start + i * step) * r),
                    new Vector3(cx + Mathf.Cos(start + (i + 1) * step) * r, cy + Mathf.Sin(start + (i + 1) * step) * r));
            }
        }

        // ── Tab: Help ─────────────────────────────────────────────────────────
        private void DrawHelpTab()
        {
            EditorGUILayout.HelpBox(
                "KULLANIM AKIŞI\n\n" +
                "1. Create Track sekmesi → 'Create Track in Scene'\n" +
                "2. Scene view'da spline'ı istediğin şekle getir\n" +
                "   • Herhangi bir kapalı/açık şekil olabilir\n" +
                "   • Ctrl+click ile knot ekle\n" +
                "   • Knot'u seçip Delete ile sil\n" +
                "3. Inspector'dan Segment Prefab ve Arrow Prefab ata\n" +
                "4. 'Rebuild Track Mesh' ile meshleri yenile\n" +
                "5. 'Save Track as Prefab' ile kaydet\n" +
                "6. LevelData → trackPrefab alanına bu prefabı ata\n\n" +
                "FEEDER PATHLERI\n\n" +
                "• Feeders sekmesinden ekle\n" +
                "• Bloklar mesh bağlantısı olmadan uçarak gelir\n" +
                "• mainConnectionT = ana track'te hangi noktaya bağlanacak\n\n" +
                "HIZLI ŞEKILLER\n\n" +
                "• Quick Shapes sekmesinden hazır preset uygula\n" +
                "• Uyguladıktan sonra Scene'de özgürce düzenle",
                MessageType.None);
        }

        // ── Utilities ─────────────────────────────────────────────────────────

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
