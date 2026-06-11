using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace BlockShooter
{
    public enum GridCellType { Empty, ShooterBlock, Door, MysteryShooter, FreezeShooter }

    [Serializable]
    public class LevelGridCell
    {
        public int            col, row;
        public GridCellType   type      = GridCellType.ShooterBlock;
        public BlockColorType color     = BlockColorType.Red;
        public int            shotCount = -1;
        public int            doorCount = 3;
        public int            freezeCount = 3;
    }

    [Serializable]
    public class LevelConveyorGroup
    {
        public BlockColorType color     = BlockColorType.Red;
        public int            rowCount  = 20;
        public int            laneCount = 5;
    }

    [Serializable]
    public class BranchPathData
    {
        public string branchName = "Branch_0";
        public float mergeT = 0.5f;
        public bool connectFromLeft = false;

        public List<Vector3> splineKnots = new();
        public List<Vector3> splineTangentsIn = new();
        public List<Vector3> splineTangentsOut = new();
        public List<int> splineTangentModes = new();

        public List<LevelConveyorGroup> groups = new();
    }

    public class LevelRoot : MonoBehaviour
    {
        [Header("Sub-Systems")]
        public ConveyorController conveyorController;
        public ShooterGrid        shooterGrid;
        public SlotSystem         slotSystem;
        public FireRange          fireRange;

        [Header("Runtime Spawner Config")]
        public GameObject conveyorBlockPrefab;
        public GameObject shooterBlockPrefab;
        public float gridCellSize = 1.2f;
        public float laneSpacing = 0.22f;
        public float rowSpacing = 0.22f;
        public float beltHalfWidth = 0.45f;
        public float railWidth = 0.1f;

        // ── Design data (written by Level Editor) ─────────────────────────────
        public bool isHardLevel = false;
        [Header("Camera Config")]
        public float cameraSize = 9f;
        public float cameraZ = -10f;
        [HideInInspector] public int   gridCols     = 4;
        [HideInInspector] public int   gridRows     = 2;
        [HideInInspector] public float splineWidth  = 6f;
        [HideInInspector] public float splineDepth  = 10f;
        [HideInInspector] public int   splinePreset = 0;

        [HideInInspector] public List<Vector3>           splineKnots      = new();
        [HideInInspector] public List<Vector3>           splineTangentsIn  = new();
        [HideInInspector] public List<Vector3>           splineTangentsOut = new();
        [HideInInspector] public List<int>               splineTangentModes = new(); // TangentMode enum int values
        [HideInInspector] public float openZoneHalfT = 0.08f;
        [HideInInspector] public List<LevelGridCell>     cells       = new();
        [HideInInspector] public List<LevelConveyorGroup> groups     = new();
        [HideInInspector] public List<BranchPathData>     branches   = new();

        public void Initialize()
        {
            SpawnBlocksRuntime();

            if (conveyorController != null) conveyorController.Initialize();
            if (shooterGrid        != null) shooterGrid.Initialize();
            if (slotSystem         != null) slotSystem.Initialize();
        }

        private Material GetColorMaterial(BlockColorType color)
        {
            if (GameManager.Instance != null && GameManager.Instance.config != null)
            {
                return GameManager.Instance.config.GetMaterial(color);
            }
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:GameConfig");
            if (guids.Length > 0)
            {
                var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
                return cfg?.GetMaterial(color);
            }
#endif
            return null;
        }

        public void SpawnBlocksRuntime()
        {
            if (shooterGrid == null) shooterGrid = GetComponentInChildren<ShooterGrid>(true);
            if (conveyorController == null) conveyorController = GetComponentInChildren<ConveyorController>(true);

            // 1. Spawn Grid Blocks
            if (shooterGrid != null)
            {
                Transform sgGo = shooterGrid.transform;
                // Clear any existing children
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    var temp = new List<GameObject>();
                    foreach (Transform c in sgGo) temp.Add(c.gameObject);
                    foreach (var g in temp) DestroyImmediate(g);
                }
                else
                #endif
                {
                    foreach (Transform c in sgGo) Destroy(c.gameObject);
                }

                float cs = gridCellSize;
                float hw = (gridCols - 1) * cs * 0.5f;

                for (int r = 0; r < gridRows; r++)
                for (int c = 0; c < gridCols; c++)
                {
                    var pos = new Vector3(-hw + c * cs, 0f, (r - gridRows + 0.5f) * cs);
                    string nm = $"Cell_r{r}_c{c}";

                    var cell = cells.Find(x => x.col == c && x.row == r);
                    if (cell == null) continue;

                    switch (cell.type)
                    {
                        case GridCellType.ShooterBlock:
                        case GridCellType.MysteryShooter:
                        case GridCellType.FreezeShooter:
                            if (shooterBlockPrefab != null)
                            {
                                GameObject go = Instantiate(shooterBlockPrefab, sgGo);
                                go.name = nm;
                                go.transform.localPosition = pos;
                                int sh = Mathf.Max(1, cell.shotCount);
                                var sb = go.GetComponent<ShooterBlock>();
                                sb?.EditorSetup(cell.color, sh, c, r, isMystery: (cell.type == GridCellType.MysteryShooter));

                                if (sb?.blockRenderer != null)
                                {
                                    var mat = GetColorMaterial(cell.color);
                                    if (mat != null) sb.blockRenderer.sharedMaterial = mat;
                                }

                                if (cell.type == GridCellType.FreezeShooter)
                                {
                                    var f = go.GetComponent<FreezeBlockFeature>();
                                    if (f == null) f = go.AddComponent<FreezeBlockFeature>();
                                    f.isFrozen = true;
                                    f.freezeCount = cell.freezeCount;
                                    #if UNITY_EDITOR
                                    if (!Application.isPlaying) f.SyncVisualsEditor();
                                    #endif
                                }
                            }
                            break;

                        case GridCellType.Door:
                            {
                                GameObject go = new GameObject(nm);
                                go.transform.SetParent(sgGo, false);
                                go.transform.localPosition = pos;
                                var d = go.AddComponent<BlockDoor>();
                                d.blockCount = cell.doorCount;
                                d.spawnColors = new List<BlockColorType> { cell.color };
                                break;
                            }
                    }
                }
            }

            // 2. Spawn Conveyor Blocks (Main Spline)
            Transform trackGroupsParent = conveyorController != null ? conveyorController.transform.Find("Groups") : null;
            if (trackGroupsParent != null)
            {
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    var temp = new List<GameObject>();
                    foreach (Transform c in trackGroupsParent) temp.Add(c.gameObject);
                    foreach (var g in temp) DestroyImmediate(g);
                }
                else
                #endif
                {
                    foreach (Transform c in trackGroupsParent) Destroy(c.gameObject);
                }

                if (conveyorBlockPrefab != null)
                {
                    foreach (var gd in groups)
                    {
                        GameObject gGo = new GameObject($"Group_{gd.color}");
                        gGo.transform.SetParent(trackGroupsParent, false);
                        var bg = gGo.AddComponent<BlockGroup>();
                        bg.colorType = gd.color;
                        bg.rowCount = gd.rowCount;
                        bg.laneCount = gd.laneCount;
                        bg.laneSpacing = laneSpacing;
                        bg.rowSpacing = rowSpacing;

                        for (int row = 0; row < gd.rowCount; row++)
                        {
                            GameObject rowGo = new GameObject($"Row_{row}");
                            rowGo.transform.SetParent(gGo.transform, false);
                            for (int lane = 0; lane < gd.laneCount; lane++)
                            {
                                GameObject bGo = Instantiate(conveyorBlockPrefab, rowGo.transform);
                                bGo.name = $"Block_{lane}";
                                bGo.transform.localPosition = Vector3.zero;
                                var cb = bGo.GetComponent<ConveyorBlock3D>();
                                if (cb != null)
                                {
                                    cb.SetGroupIndex(row, lane);
                                    var mat = GetColorMaterial(gd.color);
                                    if (mat != null && cb.blockRenderer != null)
                                    {
                                        cb.blockRenderer.sharedMaterial = mat;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 3. Spawn Branch Conveyor Blocks
            Transform conveyorSystem = conveyorController != null ? conveyorController.transform.parent : null;
            Transform branchesParent = conveyorSystem != null ? conveyorSystem.Find("Branches") : null;
            if (branchesParent != null && conveyorBlockPrefab != null)
            {
                foreach (var b in branches)
                {
                    Transform branchGo = branchesParent.Find(b.branchName);
                    if (branchGo == null) continue;

                    Transform bGroupsGo = branchGo.Find("Groups");
                    if (bGroupsGo == null) continue;

                    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        var temp = new List<GameObject>();
                        foreach (Transform c in bGroupsGo) temp.Add(c.gameObject);
                        foreach (var g in temp) DestroyImmediate(g);
                    }
                    else
                    #endif
                    {
                        foreach (Transform c in bGroupsGo) Destroy(c.gameObject);
                    }

                    var bSc = branchGo.GetComponent<SplineContainer>();
                    if (bSc == null) continue;

                    float branchSplineLen = SplineUtility.CalculateLength(bSc.Spline, branchGo.localToWorldMatrix);
                    float mainTrackHalfWidth = beltHalfWidth + railWidth;
                    float safetyOffset = mainTrackHalfWidth + rowSpacing + 0.1f;
                    float mergeStopT = branchSplineLen > 0f ? Mathf.Clamp01(1.0f - (safetyOffset / branchSplineLen)) : 0.95f;

                    int globalRowIdx = 0;
                    foreach (var gd in b.groups)
                    {
                        GameObject gGo = new GameObject($"Group_{gd.color}");
                        gGo.transform.SetParent(bGroupsGo, false);
                        var bg = gGo.AddComponent<BlockGroup>();
                        bg.colorType = gd.color;
                        bg.rowCount = gd.rowCount;
                        bg.laneCount = 5;
                        bg.laneSpacing = laneSpacing;
                        bg.rowSpacing = rowSpacing;

                        for (int row = 0; row < gd.rowCount; row++)
                        {
                            float rowT = mergeStopT - (globalRowIdx * rowSpacing) / branchSplineLen;
                            rowT = Mathf.Clamp01(rowT);

                            bSc.Spline.Evaluate(rowT, out var spPos, out var spTan, out var spUp);
                            Vector3 worldPos = branchGo.TransformPoint(spPos);
                            Vector3 fwd = branchGo.TransformDirection((Vector3)spTan).normalized;
                            Vector3 upDir = branchGo.TransformDirection((Vector3)spUp).normalized;
                            if (upDir == Vector3.zero) upDir = Vector3.up;
                            Vector3 right = Vector3.Cross(upDir, fwd).normalized;
                            Quaternion rot = fwd != Vector3.zero ? Quaternion.LookRotation(fwd, upDir) : Quaternion.identity;

                            GameObject rowGo = new GameObject($"Row_{row}");
                            rowGo.transform.SetParent(gGo.transform, false);

                            for (int lane = 0; lane < 5; lane++)
                            {
                                GameObject bGo = Instantiate(conveyorBlockPrefab, rowGo.transform);
                                bGo.name = $"Block_{lane}";
                                float xOff = (lane - 2f) * laneSpacing;
                                bGo.transform.position = worldPos + right * xOff;
                                bGo.transform.rotation = rot;

                                var cb = bGo.GetComponent<ConveyorBlock3D>();
                                if (cb != null)
                                {
                                    cb.SetGroupIndex(row, lane);
                                    var mat = GetColorMaterial(gd.color);
                                    if (mat != null && cb.blockRenderer != null)
                                    {
                                        cb.blockRenderer.sharedMaterial = mat;
                                    }
                                }
                            }
                            globalRowIdx++;
                        }
                    }
                }
            }
        }
    }
}
