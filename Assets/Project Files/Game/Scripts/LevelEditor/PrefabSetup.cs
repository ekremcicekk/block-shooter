#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;

namespace BlockShooter.Editor
{
    /// <summary>
    /// Creates the required runtime prefabs programmatically.
    /// Run via: BlockShooter > Create Prefabs
    /// </summary>
    public static class PrefabSetup
    {
        private const string PrefabPath = "Assets/Project Files/Game/Prefabs/";

        [MenuItem("BlockShooter/Create Runtime Prefabs", false, 1)]
        public static void CreateAllPrefabs()
        {
            CreateProjectilePrefab();
            CreateConveyorBlock3DPrefab();
            CreateShooterBlockPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Prefablar Oluşturuldu!",
                $"Prefablar şuraya kaydedildi:\n{PrefabPath}", "Tamam");
        }

        // ── Projectile ─────────────────────────────────────────────────────────
        static void CreateProjectilePrefab()
        {
            var go = new GameObject("Projectile");

            // Add Projectile first — RequireComponent auto-adds Rigidbody + SphereCollider
            var proj = go.AddComponent<Projectile>();

            // Configure Rigidbody (already added via RequireComponent)
            var rb = go.GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            // Configure SphereCollider (already added via RequireComponent)
            var col = go.GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.12f;

            // Sphere mesh child
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(go.transform, false);
            sphere.transform.localScale = Vector3.one * 0.22f;
            sphere.name = "BallMesh";
            Object.DestroyImmediate(sphere.GetComponent<SphereCollider>());

            var mr = sphere.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            proj.ballRenderer = mr;

            // Trail
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.startWidth = 0.15f;
            trail.endWidth = 0f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            proj.trail = trail;

            SavePrefab(go, "Projectile");
            Object.DestroyImmediate(go);
        }

        // ── ConveyorBlock3D ─────────────────────────────────────────────────────
        static void CreateConveyorBlock3DPrefab()
        {
            var go = new GameObject("ConveyorBlock3D");

            // Main cube mesh
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(go.transform, false);
            cube.transform.localScale = new Vector3(0.44f, 0.44f, 0.44f);
            cube.name = "BlockMesh";
            Object.DestroyImmediate(cube.GetComponent<BoxCollider>());

            // Add BoxCollider to parent for trigger detection
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.46f, 0.46f, 0.46f);
            col.isTrigger = false;

            var block = go.AddComponent<ConveyorBlock3D>();
            block.blockRenderer = cube.GetComponent<MeshRenderer>();

            var mr = cube.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            SavePrefab(go, "ConveyorBlock3D");
            Object.DestroyImmediate(go);
        }

        // ── ShooterBlock ────────────────────────────────────────────────────────
        static void CreateShooterBlockPrefab()
        {
            var go = new GameObject("ShooterBlock");

            // Body — slightly rounded look via scale
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(0.9f, 0.75f, 0.9f);
            body.name = "BodyMesh";
            Object.DestroyImmediate(body.GetComponent<SphereCollider>());

            // Glow ring (flat cylinder underneath)
            var glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glow.transform.SetParent(go.transform, false);
            glow.transform.localScale = new Vector3(1.05f, 0.04f, 1.05f);
            glow.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            glow.name = "GlowMesh";
            Object.DestroyImmediate(glow.GetComponent<CapsuleCollider>());

            // Shoot point
            var shootPoint = new GameObject("ShootPoint");
            shootPoint.transform.SetParent(go.transform, false);
            shootPoint.transform.localPosition = new Vector3(0f, 0.5f, 0.3f);

            // Shot count text (World Space Canvas)
            var canvasGo = new GameObject("ShotCanvas");
            canvasGo.transform.SetParent(go.transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            canvasGo.transform.localRotation = Quaternion.Euler(62f, 0f, 0f); // Match camera tilt
            canvasGo.transform.localScale = Vector3.one * 0.012f;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 5;

            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(100f, 50f);

            var textGo = new GameObject("ShotCountText");
            textGo.transform.SetParent(canvasGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.text = "100";
            tmp.fontSize = 42f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            // ShooterBlock component
            var block = go.AddComponent<ShooterBlock>();
            block.blockRenderer = body.GetComponent<MeshRenderer>();
            block.glowRenderer = glow.GetComponent<MeshRenderer>();
            block.shotCountText = tmp;
            block.shootPoint = shootPoint.transform;

            var bodyMr = body.GetComponent<MeshRenderer>();
            bodyMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var glowMr = glow.GetComponent<MeshRenderer>();
            glowMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            SavePrefab(go, "ShooterBlock");
            Object.DestroyImmediate(go);
        }

        static void SavePrefab(GameObject go, string name)
        {
            string fullPath = PrefabPath + name + ".prefab";
            bool success;
            PrefabUtility.SaveAsPrefabAsset(go, fullPath, out success);
            if (success) Debug.Log($"[PrefabSetup] Oluşturuldu: {fullPath}");
            else Debug.LogError($"[PrefabSetup] Başarısız: {fullPath}");
        }
    }
}
#endif
