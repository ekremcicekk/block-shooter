#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.UI;
using TMPro;

namespace BlockShooter.Editor
{
    public static class SceneSetup
    {
        [MenuItem("BlockShooter/Setup Game Scene", false, 0)]
        public static void SetupGameScene()
        {
            if (!EditorUtility.DisplayDialog("Setup Game Scene",
                "Bu işlem mevcut Game sahnesini sıfırlayıp yeniden kuracak.\nDevam edilsin mi?",
                "Evet, Kur", "İptal"))
                return;

            // Open Game scene
            string scenePath = "Assets/Project Files/Game/Scenes/Game.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Clear all existing objects
            var roots = scene.GetRootGameObjects();
            foreach (var r in roots)
                Object.DestroyImmediate(r);

            // ── Build Hierarchy ────────────────────────────────────────────────
            SetupLighting();
            SetupCamera();
            SetupBackground();
            var managers = SetupManagers();
            SetupTrack();
            SetupFireRange();
            SetupShooterGrid();
            SetupProjectilePool();
            SetupBoosters();
            SetupUI();

            EditorSceneManager.SaveScene(scene);
            EditorUtility.DisplayDialog("Tamamlandı!",
                "Game sahnesi başarıyla kuruldu.\nGameConfig ScriptableObject'i GameManager'a atamayı unutma!",
                "Tamam");
        }

        // ── Lighting ──────────────────────────────────────────────────────────
        static void SetupLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.9f);
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            // Ambient
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.45f, 0.5f);
        }

        // ── Camera ────────────────────────────────────────────────────────────
        static void SetupCamera()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.18f, 0.22f, 0.32f);
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            // Top-down angled view — matches the reference game screenshots
            camGo.transform.position = new Vector3(0f, 14f, -5f);
            camGo.transform.rotation = Quaternion.Euler(62f, 0f, 0f);

            camGo.AddComponent<AudioListener>();
        }

        // ── Background ────────────────────────────────────────────────────────
        static void SetupBackground()
        {
            // Ground plane
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.05f, 2f);
            ground.transform.localScale = new Vector3(4f, 1f, 5f);
            Object.DestroyImmediate(ground.GetComponent<MeshCollider>());

            var mr = ground.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Shooter area platform
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "ShooterPlatform";
            platform.transform.position = new Vector3(0f, -0.08f, -4f);
            platform.transform.localScale = new Vector3(7f, 0.15f, 5f);
            Object.DestroyImmediate(platform.GetComponent<BoxCollider>());
        }

        // ── Managers ──────────────────────────────────────────────────────────
        static GameObject SetupManagers()
        {
            var root = new GameObject("[Managers]");

            // GameManager
            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(root.transform);
            gmGo.AddComponent<GameManager>();

            // LevelManager
            var lmGo = new GameObject("LevelManager");
            lmGo.transform.SetParent(root.transform);
            lmGo.AddComponent<LevelManager>();

            // ScoreManager
            var smGo = new GameObject("ScoreManager");
            smGo.transform.SetParent(root.transform);
            smGo.AddComponent<ScoreManager>();

            // GameBootstrap
            var bs = new GameObject("GameBootstrap");
            bs.transform.SetParent(root.transform);
            bs.AddComponent<GameBootstrap>();

            return root;
        }

        // ── Conveyor Track ────────────────────────────────────────────────────
        static void SetupTrack()
        {
            var trackRoot = new GameObject("[ConveyorTrack]");
            trackRoot.transform.position = new Vector3(0f, 0.15f, 3f);

            // SplineContainer
            var splineContainer = trackRoot.AddComponent<SplineContainer>();

            // Mesh components
            trackRoot.AddComponent<MeshFilter>();
            var mr = trackRoot.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var mc = trackRoot.AddComponent<MeshCollider>();

            // TrackMesh
            var trackMesh = trackRoot.AddComponent<ConveyorTrackMesh>();
            trackMesh.trackWidth = 2.5f;
            trackMesh.wallHeight = 0.28f;
            trackMesh.wallThickness = 0.08f;
            trackMesh.segments = 120;

            // PathController
            var pathCtrl = trackRoot.AddComponent<ConveyorPathController>();
            pathCtrl.speed = 1.5f;
            pathCtrl.loop = true;

            // Build default Rounded-Rect spline shape
            BuildDefaultSpline(splineContainer);
            trackMesh.Rebuild();

            EditorUtility.SetDirty(trackRoot);
        }

        static void BuildDefaultSpline(SplineContainer container)
        {
            var spline = container.Spline;
            spline.Clear();
            spline.Closed = true;

            // Rounded rect: width=4, height=6, corner=1.2
            float w = 2f, h = 3f, r = 1.2f;
            float kappa = r * 0.5523f;

            // 8 knots (4 corners × 2 control points each, approximated with 8 knots)
            var knots = new[]
            {
                // Top-left → Top-right (top edge)
                MakeKnot(new Vector3(-w, 0,  h + r), new Vector3(-kappa, 0, 0), new Vector3( kappa, 0, 0)),
                MakeKnot(new Vector3( w, 0,  h + r), new Vector3(-kappa, 0, 0), new Vector3( kappa, 0, 0)),
                // Top-right corner
                MakeKnot(new Vector3( w + r, 0,  h), new Vector3(0, 0, kappa),  new Vector3(0, 0, -kappa)),
                // Right edge
                MakeKnot(new Vector3( w + r, 0, -h), new Vector3(0, 0, kappa),  new Vector3(0, 0, -kappa)),
                // Bottom-right → Bottom-left
                MakeKnot(new Vector3( w, 0, -h - r), new Vector3( kappa, 0, 0), new Vector3(-kappa, 0, 0)),
                MakeKnot(new Vector3(-w, 0, -h - r), new Vector3( kappa, 0, 0), new Vector3(-kappa, 0, 0)),
                // Bottom-left corner
                MakeKnot(new Vector3(-w - r, 0, -h), new Vector3(0, 0, -kappa), new Vector3(0, 0,  kappa)),
                // Left edge
                MakeKnot(new Vector3(-w - r, 0,  h), new Vector3(0, 0, -kappa), new Vector3(0, 0,  kappa)),
            };

            foreach (var k in knots)
                spline.Add(k, TangentMode.Broken);
        }

        static BezierKnot MakeKnot(Vector3 pos, Vector3 tanIn, Vector3 tanOut)
            => new BezierKnot(pos, tanIn, tanOut, Quaternion.identity);

        // ── FireRange ─────────────────────────────────────────────────────────
        static void SetupFireRange()
        {
            var go = new GameObject("FireRange");
            go.transform.position = new Vector3(0f, 0.2f, -0.5f);

            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(8f, 1.5f, 3f);

            go.AddComponent<FireRange>();

            // Visual gizmo only in editor
            var layer = LayerMask.NameToLayer("Ignore Raycast");
            go.layer = layer;
        }

        // ── Shooter Grid ──────────────────────────────────────────────────────
        static void SetupShooterGrid()
        {
            var gridRoot = new GameObject("[ShooterGrid]");
            gridRoot.transform.position = new Vector3(0f, 0.1f, -4f);

            var grid = gridRoot.AddComponent<ShooterGrid>();
            grid.gridOrigin = new Vector2(-1.8f, -0.5f);

            // Placeholder grid slots (visual only)
            for (int col = 0; col < 4; col++)
            {
                for (int row = 0; row < 2; row++)
                {
                    var slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    slot.name = $"GridSlot_{col}_{row}";
                    slot.transform.SetParent(gridRoot.transform);
                    slot.transform.localPosition = new Vector3(
                        (col - 1.5f) * 1.25f, 0f, (row - 0.5f) * 1.25f);
                    slot.transform.localScale = new Vector3(1.1f, 0.12f, 1.1f);
                    Object.DestroyImmediate(slot.GetComponent<BoxCollider>());

                    var mr = slot.GetComponent<MeshRenderer>();
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        // ── Projectile Pool ───────────────────────────────────────────────────
        static void SetupProjectilePool()
        {
            var go = new GameObject("ProjectilePool");
            go.AddComponent<ProjectilePool>();
            // Note: projectilePrefab must be assigned manually in Inspector
        }

        // ── Boosters ──────────────────────────────────────────────────────────
        static void SetupBoosters()
        {
            var root = new GameObject("[Boosters]");
            root.AddComponent<BoosterManager>();

            var bomb = new GameObject("BombBooster");
            bomb.transform.SetParent(root.transform);
            bomb.AddComponent<BombBooster>();

            var rainbow = new GameObject("RainbowBooster");
            rainbow.transform.SetParent(root.transform);
            rainbow.AddComponent<RainbowBooster>();

            var freeze = new GameObject("FreezeBooster");
            freeze.transform.SetParent(root.transform);
            freeze.AddComponent<FreezeBooster>();
        }

        // ── UI ────────────────────────────────────────────────────────────────
        static void SetupUI()
        {
            // Main Canvas
            var canvasGo = new GameObject("UI_Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844); // iPhone 14 resolution
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // ── HUD ──
            var hud = MakePanel(canvasGo.transform, "HUD_Panel",
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, 0));
            hud.GetComponent<Image>().enabled = false;
            var hudCtrl = hud.AddComponent<HUDController>();

            // Level Text
            var levelTxt = MakeText(hud.transform, "LevelText", "Level 1",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(200f, 50f), 24);
            hudCtrl.levelText = levelTxt.GetComponent<TextMeshProUGUI>();

            // Score Text
            var scoreTxt = MakeText(hud.transform, "ScoreText", "0",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-80f, -50f), new Vector2(140f, 45f), 22);
            hudCtrl.scoreText = scoreTxt.GetComponent<TextMeshProUGUI>();

            // Progress Bar
            var progressGo = new GameObject("ProgressBar");
            progressGo.transform.SetParent(hud.transform, false);
            var progressBar = progressGo.AddComponent<Slider>();
            var progressRect = progressGo.GetComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0.1f, 1f);
            progressRect.anchorMax = new Vector2(0.9f, 1f);
            progressRect.anchoredPosition = new Vector2(0f, -105f);
            progressRect.sizeDelta = new Vector2(0f, 18f);
            SetupSliderComponents(progressGo);
            hudCtrl.progressBar = progressBar;

            // Pause Button
            var pauseBtn = MakeButton(hud.transform, "PauseButton", "II",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(45f, -45f), new Vector2(60f, 60f));

            // Pause Panel (hidden by default)
            var pausePanel = MakePanel(hud.transform, "PausePanel",
                new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            pausePanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.7f);
            pausePanel.SetActive(false);
            hudCtrl.pausePanel = pausePanel;
            hudCtrl.hudPanel = hud;

            var resumeBtn = MakeButton(pausePanel.transform, "ResumeButton", "DEVAM",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 60f));

            // ── Booster Bar ──
            var boosterBar = new GameObject("BoosterBar");
            boosterBar.transform.SetParent(hud.transform, false);
            var bbRect = boosterBar.AddComponent<RectTransform>();
            bbRect.anchorMin = new Vector2(0.5f, 0f);
            bbRect.anchorMax = new Vector2(0.5f, 0f);
            bbRect.anchoredPosition = new Vector2(0f, 80f);
            bbRect.sizeDelta = new Vector2(320f, 70f);

            var hlg = boosterBar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20f;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            string[] boosterNames = { "BOMB", "RAINBOW", "FREEZE" };
            BoosterType[] boosterTypes = { BoosterType.Bomb, BoosterType.Rainbow, BoosterType.Freeze };
            Color[] boosterColors =
            {
                new Color(1f, 0.3f, 0.3f),
                new Color(1f, 0.9f, 0.2f),
                new Color(0.3f, 0.7f, 1f)
            };

            for (int i = 0; i < 3; i++)
            {
                var btn = MakeButton(boosterBar.transform, $"Booster_{boosterNames[i]}", boosterNames[i],
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(88f, 64f));
                btn.GetComponent<Image>().color = boosterColors[i];
                var boosterUI = btn.AddComponent<BoosterButtonUI>();
                boosterUI.boosterType = boosterTypes[i];
                boosterUI.button = btn.GetComponent<Button>();

                // Count text
                var countTxt = MakeText(btn.transform, "CountText", "0",
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-5f, -5f), new Vector2(28f, 28f), 14);
                boosterUI.countText = countTxt.GetComponent<TextMeshProUGUI>();

                // Lock overlay
                var lockOverlay = new GameObject("LockOverlay");
                lockOverlay.transform.SetParent(btn.transform, false);
                var lockImg = lockOverlay.AddComponent<Image>();
                lockImg.color = new Color(0, 0, 0, 0.6f);
                var lockRect = lockOverlay.GetComponent<RectTransform>();
                lockRect.anchorMin = Vector2.zero;
                lockRect.anchorMax = Vector2.one;
                lockRect.offsetMin = lockRect.offsetMax = Vector2.zero;
                boosterUI.lockOverlay = lockOverlay;
            }

            // ── Win Panel ──
            var winPanel = MakePanel(canvasGo.transform, "WinPanel",
                new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            winPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            winPanel.SetActive(false);

            var winCard = MakePanel(winPanel.transform, "WinCard",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 420f));
            winCard.GetComponent<Image>().color = new Color(0.15f, 0.7f, 0.3f);

            var winCtrl = winPanel.AddComponent<WinPanel>();
            winCtrl.panel = winCard;

            var winTitle = MakeText(winCard.transform, "WinTitle", "LEVEL COMPLETE!",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(280f, 55f), 28);
            winCtrl.levelText = winTitle.GetComponent<TextMeshProUGUI>();

            var winScore = MakeText(winCard.transform, "ScoreText", "0",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(200f, 45f), 22);
            winCtrl.scoreText = winScore.GetComponent<TextMeshProUGUI>();

            var nextBtn = MakeButton(winCard.transform, "NextButton", "NEXT LEVEL",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(240f, 58f));
            nextBtn.GetComponent<Image>().color = new Color(1f, 0.85f, 0.1f);
            winCtrl.nextButton = nextBtn.GetComponent<Button>();

            // Stars
            winCtrl.stars = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                var star = new GameObject($"Star_{i}");
                star.transform.SetParent(winCard.transform, false);
                var starImg = star.AddComponent<Image>();
                starImg.color = Color.yellow;
                var starRect = star.GetComponent<RectTransform>();
                starRect.anchorMin = starRect.anchorMax = new Vector2(0.5f, 0.75f);
                starRect.anchoredPosition = new Vector2((i - 1) * 75f, 0f);
                starRect.sizeDelta = new Vector2(60f, 60f);
                winCtrl.stars[i] = star;
            }

            // ── Fail Panel ──
            var failPanel = MakePanel(canvasGo.transform, "FailPanel",
                new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            failPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            failPanel.SetActive(false);

            var failCard = MakePanel(failPanel.transform, "FailCard",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 350f));
            failCard.GetComponent<Image>().color = new Color(0.75f, 0.18f, 0.18f);

            var failCtrl = failPanel.AddComponent<FailPanel>();
            failCtrl.panel = failCard;

            var failTitle = MakeText(failCard.transform, "FailTitle", "LEVEL FAILED",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(280f, 55f), 28);
            failCtrl.levelText = failTitle.GetComponent<TextMeshProUGUI>();

            var retryBtn = MakeButton(failCard.transform, "RetryButton", "YENİDEN DENE",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(240f, 58f));
            retryBtn.GetComponent<Image>().color = new Color(1f, 0.55f, 0.1f);
            failCtrl.retryButton = retryBtn.GetComponent<Button>();

            var homeBtn2 = MakeButton(failCard.transform, "HomeButton", "ANA MENÜ",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(200f, 48f));
            failCtrl.homeButton = homeBtn2.GetComponent<Button>();

            // ── Combo UI ──
            var comboGo = new GameObject("ComboUI");
            comboGo.transform.SetParent(hud.transform, false);
            var comboRect = comboGo.AddComponent<RectTransform>();
            comboRect.anchorMin = comboRect.anchorMax = new Vector2(0.5f, 0.5f);
            comboRect.anchoredPosition = new Vector2(0f, 60f);
            comboRect.sizeDelta = new Vector2(250f, 55f);
            var comboUI = comboGo.AddComponent<ComboUI>();
            comboUI.panel = comboGo;
            comboGo.SetActive(false);

            var comboTxt = MakeText(comboGo.transform, "ComboText", "x3 COMBO!",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 55f), 26);
            comboUI.comboText = comboTxt.GetComponent<TextMeshProUGUI>();

            // ── Progress Tracker ──
            hud.AddComponent<ProgressTracker>();

            // EventSystem
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // ── UI Helpers ────────────────────────────────────────────────────────
        static GameObject MakePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.15f, 0.92f);
            return go;
        }

        static GameObject MakeText(Transform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, float fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return go;
        }

        static GameObject MakeButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.5f, 0.9f);
            var btn = go.AddComponent<Button>();
            var cb = btn.colors;
            cb.highlightedColor = new Color(0.3f, 0.65f, 1f);
            cb.pressedColor = new Color(0.15f, 0.4f, 0.75f);
            btn.colors = cb;

            var txtGo = MakeText(go.transform, "Label", label,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 18f);
            return go;
        }

        static void SetupSliderComponents(GameObject sliderGo)
        {
            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(sliderGo.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;

            // Fill Area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderGo.transform, false);
            var faRect = fillArea.AddComponent<RectTransform>();
            faRect.anchorMin = new Vector2(0, 0.25f);
            faRect.anchorMax = new Vector2(1, 0.75f);
            faRect.offsetMin = new Vector2(5, 0);
            faRect.offsetMax = new Vector2(-5, 0);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.8f, 0.4f);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

            var slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
        }
    }
}
#endif
