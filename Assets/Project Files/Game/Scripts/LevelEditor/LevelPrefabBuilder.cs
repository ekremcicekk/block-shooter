#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Splines;
using Unity.Mathematics;
using BlockShooter;

namespace EKStudio.Editor
{
    public class LevelPrefabBuilder
    {
        public static void BuildHierarchy(Transform root, LevelRoot lr, LevelEditorConfig cfg, GameConfig gameCfg)
        {
            // Assign config references to LevelRoot for runtime spawning
            lr.conveyorBlockPrefab = cfg.conveyorBlockPrefab;
            lr.shooterBlockPrefab = cfg.shooterBlockPrefab;
            lr.gridCellSize = cfg.gridCellSize;
            lr.laneSpacing = cfg.laneSpacing;
            lr.rowSpacing = cfg.rowSpacing;
            lr.beltHalfWidth = cfg.beltHalfWidth;
            lr.railWidth = cfg.railWidth;

            float cs = cfg.gridCellSize;
            float slotZ = -1.5f;
            float gridZ = -2.5f;

            // Create ConveyorSystem parent group
            var conveyorSys = Go(root, "ConveyorSystem");

            // ── Track ──
            var trackGo = Go(conveyorSys.transform, "Track");
            trackGo.transform.localPosition = new Vector3(0f, 0f, 0f);
            var sc = trackGo.AddComponent<SplineContainer>();
            float trackRailHeight = cfg.railHeight;
            
            // Build main closed track spline
            WriteKnotsToContainer(sc, lr.splineKnots, lr.splineTangentsIn, lr.splineTangentsOut, lr.splineTangentModes, trackRailHeight, 0f);
            sc.Spline.Closed = true;

            var cc = trackGo.AddComponent<ConveyorController>();
            cc.speed = 1.5f; // Default fallback speed
            lr.conveyorController = cc;

            // Track mesh — ConveyorTrackMeshBuilder
            var meshBuilder = trackGo.AddComponent<ConveyorTrackMeshBuilder>();
            meshBuilder.resolution = cfg.trackResolution;
            meshBuilder.openZoneHalfT = lr.openZoneHalfT;
            meshBuilder.beltHalfWidth = cfg.beltHalfWidth;
            meshBuilder.wallAboveBelt = cfg.wallAboveBelt;
            meshBuilder.railHeight = trackRailHeight;
            meshBuilder.railWidth = cfg.railWidth;
            meshBuilder.bevelSize = cfg.trackBevelSize;
            meshBuilder.BuildMesh();

            var mr = trackGo.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterials = new Material[]
                {
                    cfg.trackSideMaterial,
                    cfg.trackBeltMaterial,
                };
            }

            if (cfg.arrowPrefab != null)
            {
                cc.arrowPrefab = cfg.arrowPrefab;
                cc.arrowSpacing = cfg.arrowSpacing;
            }

            // Block groups
            var groupsGo = Go(trackGo.transform, "Groups");
            // Blocks are spawned at runtime/preview to minimize prefab size.

            // ── Branch Paths ──
            if (lr.branches != null && lr.branches.Count > 0)
            {
                var branchesGroupGo = Go(conveyorSys.transform, "Branches");
                int branchIdx = 0;
                foreach (var b in lr.branches)
                {
                    var branchGo = Go(branchesGroupGo.transform, b.branchName);
                    branchGo.transform.localPosition = new Vector3(0f, 0f, 0f);

                    var bSc = branchGo.AddComponent<SplineContainer>();
                    WriteKnotsToContainer(bSc, b.splineKnots, b.splineTangentsIn, b.splineTangentsOut, b.splineTangentModes, trackRailHeight, 0f);

                    var bp = branchGo.AddComponent<BranchPath>();
                    bp.mergeT = b.mergeT;
                    bp.data = b;

                    var bMeshBuilder = branchGo.AddComponent<ConveyorTrackMeshBuilder>();
                    bMeshBuilder.resolution = cfg.trackResolution;
                    bMeshBuilder.openZoneEnabled = false;
                    bMeshBuilder.beltHalfWidth = cfg.beltHalfWidth;
                    bMeshBuilder.wallAboveBelt = cfg.wallAboveBelt;
                    bMeshBuilder.railHeight = trackRailHeight;
                    bMeshBuilder.railWidth = cfg.railWidth;
                    bMeshBuilder.bevelSize = cfg.trackBevelSize;

                    // Calculate main conveyor wall plane at merge point
                    if (sc != null && b.splineKnots != null && b.splineKnots.Count >= 2)
                    {
                        sc.Spline.Evaluate(b.mergeT, out var mPos, out var mTan, out var mUp);
                        Vector3 worldMergePos = trackGo.transform.TransformPoint(mPos);
                        Vector3 worldMergeTan = trackGo.transform.TransformDirection((Vector3)mTan).normalized;
                        Vector3 worldMergeUp = trackGo.transform.TransformDirection((Vector3)mUp).normalized;
                        if (worldMergeUp.sqrMagnitude < 0.001f) worldMergeUp = Vector3.up;
                        Vector3 worldMergeRight = Vector3.Cross(worldMergeUp, worldMergeTan).normalized;

                        Vector3 branchLast = b.splineKnots[b.splineKnots.Count - 1];
                        Vector3 branchSecondLast = b.splineKnots[b.splineKnots.Count - 2];
                        Vector3 toBranch = (branchSecondLast - branchLast).normalized;
                        float dot = Vector3.Dot(toBranch, worldMergeRight);

                        Vector3 wallNormal = dot < 0f ? -worldMergeRight : worldMergeRight;
                        float wallOffset = cfg.beltHalfWidth + cfg.railWidth;
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
                            cfg.trackSideMaterial,
                            cfg.trackBeltMaterial,
                        };
                    }

                    // Calculate branch spline length
                    float branchSplineLen = SplineUtility.CalculateLength(bSc.Spline, branchGo.transform.localToWorldMatrix);
                    float mainTrackHalfWidth = cfg.beltHalfWidth + cfg.railWidth;
                    float safetyOffset = mainTrackHalfWidth + cfg.rowSpacing + 0.1f;
                    float mergeStopT = branchSplineLen > 0f
                        ? Mathf.Clamp01(1.0f - (safetyOffset / branchSplineLen))
                        : 0.95f;

                    var bGroupsGo = Go(branchGo.transform, "Groups");
                    // Blocks are spawned at runtime/preview to minimize prefab size.
                    branchIdx++;
                }
            }

            var gameplayLogic = Go(root, "GameplayLogic");
            var boardPlatform = Go(root, "BoardPlatform");

            // ── FireRange ──
            GameObject frGo;
            if (cfg.fireRangePrefab != null)
            {
                frGo = (GameObject)PrefabUtility.InstantiatePrefab(cfg.fireRangePrefab, trackGo.transform);
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
            frGo.transform.localPosition = new Vector3(0f, 0f, 0f);
            if (PrefabUtility.IsPartOfPrefabInstance(frGo))
                PrefabUtility.RecordPrefabInstancePropertyModifications(frGo.transform);
            lr.fireRange = frGo.GetComponent<FireRange>();

            // ── SlotDeck ──
            var deckGo = Go(boardPlatform.transform, "SlotDeck");
            deckGo.transform.localPosition = new Vector3(0f, 0f, slotZ);
            var ss = deckGo.AddComponent<SlotSystem>();
            if (cfg.slotIndicatorPrefab != null) ss.slotIndicatorPrefab = cfg.slotIndicatorPrefab;
            lr.slotSystem = ss;

            int slots = cfg.slotCount;
            float tw = (slots - 1) * cfg.slotSpacing;
            for (int i = 0; i < slots; i++)
            {
                var slotGo = Go(deckGo.transform, $"Slot_{i}");
                slotGo.transform.localPosition = new Vector3(-tw * .5f + i * cfg.slotSpacing, 0f, 0f);

                if (cfg.slotIndicatorPrefab != null)
                {
                    var indGo = (GameObject)PrefabUtility.InstantiatePrefab(cfg.slotIndicatorPrefab, slotGo.transform);
                    indGo.name = "SlotIndicator";
                    indGo.transform.localPosition = Vector3.zero;
                    indGo.transform.localRotation = Quaternion.identity;
                }
            }

            // ── ShooterGrid ──
            var sgGo = Go(boardPlatform.transform, "ShooterGrid");
            sgGo.transform.localPosition = new Vector3(0f, 0f, gridZ);
            var sg = sgGo.AddComponent<ShooterGrid>();
            if (cfg.shooterBlockPrefab != null)
                sg.shooterBlockPrefab = cfg.shooterBlockPrefab.GetComponent<ShooterBlock>();
            lr.shooterGrid = sg;

            float hw = (lr.gridCols - 1) * cs * .5f;

            for (int r = 0; r < lr.gridRows; r++)
            for (int c = 0; c < lr.gridCols; c++)
            {
                var pos = new Vector3(-hw + c * cs, 0f, (r - lr.gridRows + 0.5f) * cs);
                string nm = $"Cell_r{r}_c{c}";

                var cell = lr.cells.Find(x => x.col == c && x.row == r);
                if (cell == null) continue;

                switch (cell.type)
                {
                    case GridCellType.ShooterBlock when cfg.shooterBlockPrefab != null:
                        {
                            var go = (GameObject)PrefabUtility.InstantiatePrefab(cfg.shooterBlockPrefab, sgGo.transform);
                            go.name = nm; go.transform.localPosition = pos;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                            int sh = Mathf.Max(1, cell.shotCount);
                            var sb = go.GetComponent<ShooterBlock>();
                            sb?.EditorSetup(cell.color, sh, c, r, isMystery: false);
                            if (sb?.blockRenderer != null && gameCfg != null)
                            {
                                var mat = gameCfg.GetMaterial(cell.color);
                                if (mat != null) sb.blockRenderer.sharedMaterial = mat;
                            }
                            break;
                        }
                    case GridCellType.MysteryShooter:
                        {
                            var prefab = cfg.shooterBlockPrefab;
                            if (prefab != null)
                            {
                                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, sgGo.transform);
                                go.name = nm; go.transform.localPosition = pos;
                                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                                int sh = Mathf.Max(1, cell.shotCount);
                                var sb = go.GetComponent<ShooterBlock>();
                                sb?.EditorSetup(cell.color, sh, c, r, isMystery: true);
                            }
                            break;
                        }
                    case GridCellType.FreezeShooter:
                        {
                            var prefab = cfg.shooterBlockPrefab;
                            if (prefab != null)
                            {
                                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, sgGo.transform);
                                go.name = nm; go.transform.localPosition = pos;
                                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                                int sh = Mathf.Max(1, cell.shotCount);
                                var sb = go.GetComponent<ShooterBlock>();
                                sb?.EditorSetup(cell.color, sh, c, r, isMystery: false);
                                if (sb?.blockRenderer != null && gameCfg != null)
                                {
                                    var mat = gameCfg.GetMaterial(cell.color);
                                    if (mat != null) sb.blockRenderer.sharedMaterial = mat;
                                }

                                var f = go.GetComponent<FreezeBlockFeature>();
                                if (f == null) f = go.AddComponent<FreezeBlockFeature>();
                                f.isFrozen = true;
                                f.freezeCount = cell.freezeCount;
                                f.SyncVisualsEditor();
                            }
                            break;
                        }
                    case GridCellType.Door:
                        {
                            var go = Go(sgGo.transform, nm); go.transform.localPosition = pos;
                            var d = go.AddComponent<BlockDoor>();
                            d.blockCount = cell.doorCount;
                            d.spawnColors = new List<BlockColorType> { cell.color };
                            break;
                        }
                }
            }

            // ── Shooter Deck Mesh ──
            var isEmpty = new bool[lr.gridCols, lr.gridRows];
            for (int c = 0; c < lr.gridCols; c++)
            for (int r = 0; r < lr.gridRows; r++)
            {
                var cell = lr.cells.Find(x => x.col == c && x.row == r);
                isEmpty[c, r] = (cell == null || cell.type == GridCellType.Empty);
            }

            var deckMeshGo = Go(boardPlatform.transform, "ShooterDeck");
            deckMeshGo.transform.localPosition = new Vector3(0f, 0f, gridZ);
            var deckBuilder = deckMeshGo.AddComponent<ShooterDeckMeshBuilder>();
            deckBuilder.gridCols = lr.gridCols;
            deckBuilder.gridRows = lr.gridRows;
            deckBuilder.cellSize = cs;
            deckBuilder.tileHeight = cfg.deckTileHeight;
            deckBuilder.sideWingWidth = cfg.sideWingWidth;
            deckBuilder.backDepth = cfg.backDepth;
            deckBuilder.bevelSize = cfg.bevelSize;
            deckBuilder.bevelSegments = cfg.bevelSegments;
            deckBuilder.BuildMesh(isEmpty);
            var deckMr = deckMeshGo.GetComponent<MeshRenderer>();
            deckMr.sharedMaterials = new Material[]
            {
                cfg.deckTopMaterial,
                cfg.deckWallMaterial,
            };

            // ── Ground ──
            if (cfg.groundPrefab != null)
            {
                var envGo = Go(root, "Environment");
                var gnd = (GameObject)PrefabUtility.InstantiatePrefab(cfg.groundPrefab, envGo.transform);
                gnd.name = "Ground";
                gnd.transform.localPosition = Vector3.zero;
                PrefabUtility.RecordPrefabInstancePropertyModifications(gnd.transform);
            }
        }

        public void SaveTrackMeshAsset(Transform root, LevelRoot lr, string name, List<LevelRoot> levelPrefabs)
        {
            var track = FindDeepChild(root, "Track");
            if (track == null) return;
            var mf = track.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            // Reuse track mesh if identical spline shape exists
            if (levelPrefabs != null)
            {
                foreach (var prefab in levelPrefabs)
                {
                    if (prefab == null || prefab.gameObject.name == name) continue;

                    var otherLr = prefab;
                    if (otherLr != null && IsSplineEqual(lr, otherLr))
                    {
                        var otherTrack = FindDeepChild(prefab.transform, "Track");
                        if (otherTrack != null)
                        {
                            var otherMf = otherTrack.GetComponent<MeshFilter>();
                            if (otherMf != null && otherMf.sharedMesh != null)
                            {
                                mf.sharedMesh = otherMf.sharedMesh;
                                return;
                            }
                        }
                    }
                }
            }

            const string meshDir = "Assets/Project Files/Game/Models/LevelMesh";
            EnsureDir(meshDir);
            string meshPath = $"{meshDir}/{name}_TrackMesh.asset";
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

        public void SaveDeckMeshAsset(Transform root, LevelRoot lr, string name, List<LevelRoot> levelPrefabs)
        {
            var deck = FindDeepChild(root, "ShooterDeck");
            if (deck == null) return;
            var mf = deck.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            // Reuse deck mesh if identical grid structure exists
            if (levelPrefabs != null)
            {
                foreach (var prefab in levelPrefabs)
                {
                    if (prefab == null || prefab.gameObject.name == name) continue;

                    var otherLr = prefab;
                    if (otherLr != null && AreDecksEqual(lr, otherLr))
                    {
                        var otherDeck = FindDeepChild(prefab.transform, "ShooterDeck");
                        if (otherDeck != null)
                        {
                            var otherMf = otherDeck.GetComponent<MeshFilter>();
                            if (otherMf != null && otherMf.sharedMesh != null)
                            {
                                mf.sharedMesh = otherMf.sharedMesh;
                                return;
                            }
                        }
                    }
                }
            }

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

        public void SaveBranchMeshAssets(Transform root, LevelRoot lr, string name, List<LevelRoot> levelPrefabs)
        {
            var branches = FindDeepChild(root, "Branches");
            if (branches == null) return;

            // Reuse branch meshes if identical branches shape exists
            if (levelPrefabs != null)
            {
                foreach (var prefab in levelPrefabs)
                {
                    if (prefab == null || prefab.gameObject.name == name) continue;

                    var otherLr = prefab;
                    if (otherLr != null && AreBranchesEqual(lr, otherLr))
                    {
                        var otherBranches = FindDeepChild(prefab.transform, "Branches");
                        if (otherBranches != null && otherBranches.childCount == branches.childCount)
                        {
                            bool allAssigned = true;
                            for (int i = 0; i < branches.childCount; i++)
                            {
                                var mf = branches.GetChild(i).GetComponent<MeshFilter>();
                                var otherMf = otherBranches.GetChild(i).GetComponent<MeshFilter>();
                                if (mf != null && otherMf != null && otherMf.sharedMesh != null)
                                {
                                    mf.sharedMesh = otherMf.sharedMesh;
                                }
                                else
                                {
                                    allAssigned = false;
                                }
                            }
                            if (allAssigned) return;
                        }
                    }
                }
            }

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

        public static void WriteKnotsToContainer(SplineContainer sc, List<Vector3> knots, List<Vector3> tangentsIn, List<Vector3> tangentsOut, List<int> tangentModes, float yOffset = 0f, float zOffset = 0f)
        {
            var spline = sc.Spline;
            spline.Clear();
            for (int i = 0; i < knots.Count; i++)
            {
                var k = knots[i];
                var tanIn = i < tangentsIn.Count ? (float3)tangentsIn[i] : float3.zero;
                var tanOut = i < tangentsOut.Count ? (float3)tangentsOut[i] : float3.zero;
                spline.Add(new BezierKnot(new float3(k.x, k.y + yOffset, k.z + zOffset), tanIn, tanOut));
            }
            spline.Closed = false;
            for (int i = 0; i < spline.Count; i++)
            {
                var mode = i < tangentModes.Count ? (TangentMode)tangentModes[i] : TangentMode.AutoSmooth;
                spline.SetTangentMode(i, mode);
            }
        }

        private static bool IsSplineEqual(LevelRoot a, LevelRoot b)
        {
            if (a == null || b == null) return false;
            if (a.splineKnots.Count != b.splineKnots.Count) return false;
            if (a.splineTangentsIn.Count != b.splineTangentsIn.Count) return false;
            if (a.splineTangentsOut.Count != b.splineTangentsOut.Count) return false;
            if (a.splineTangentModes.Count != b.splineTangentModes.Count) return false;

            for (int i = 0; i < a.splineKnots.Count; i++)
            {
                if (Vector3.Distance(a.splineKnots[i], b.splineKnots[i]) > 0.001f) return false;
                if (Vector3.Distance(a.splineTangentsIn[i], b.splineTangentsIn[i]) > 0.001f) return false;
                if (Vector3.Distance(a.splineTangentsOut[i], b.splineTangentsOut[i]) > 0.001f) return false;
                if (a.splineTangentModes[i] != b.splineTangentModes[i]) return false;
            }

            if (Mathf.Abs(a.splineWidth - b.splineWidth) > 0.001f) return false;
            if (Mathf.Abs(a.splineDepth - b.splineDepth) > 0.001f) return false;
            if (a.splinePreset != b.splinePreset) return false;
            if (Mathf.Abs(a.openZoneHalfT - b.openZoneHalfT) > 0.001f) return false;

            return true;
        }

        private static bool AreDecksEqual(LevelRoot a, LevelRoot b)
        {
            if (a == null || b == null) return false;
            if (a.gridCols != b.gridCols || a.gridRows != b.gridRows) return false;
            if (a.cells.Count != b.cells.Count) return false;

            for (int i = 0; i < a.cells.Count; i++)
            {
                var cellA = a.cells[i];
                var cellB = b.cells.Find(c => c.col == cellA.col && c.row == cellA.row);
                if (cellB == null) return false;

                bool isEmptyA = (cellA.type == GridCellType.Empty);
                bool isEmptyB = (cellB.type == GridCellType.Empty);
                if (isEmptyA != isEmptyB) return false;
            }

            return true;
        }

        private static bool AreBranchesEqual(LevelRoot a, LevelRoot b)
        {
            if (a == null || b == null) return false;
            if (a.branches.Count != b.branches.Count) return false;

            for (int i = 0; i < a.branches.Count; i++)
            {
                var brA = a.branches[i];
                var brB = b.branches[i];

                if (brA.branchName != brB.branchName) return false;
                if (Mathf.Abs(brA.mergeT - brB.mergeT) > 0.001f) return false;
                if (brA.connectFromLeft != brB.connectFromLeft) return false;

                if (brA.splineKnots.Count != brB.splineKnots.Count) return false;
                for (int j = 0; j < brA.splineKnots.Count; j++)
                {
                    if (Vector3.Distance(brA.splineKnots[j], brB.splineKnots[j]) > 0.001f) return false;
                    if (Vector3.Distance(brA.splineTangentsIn[j], brB.splineTangentsIn[j]) > 0.001f) return false;
                    if (Vector3.Distance(brA.splineTangentsOut[j], brB.splineTangentsOut[j]) > 0.001f) return false;
                    if (brA.splineTangentModes[j] != brB.splineTangentModes[j]) return false;
                }
            }

            return true;
        }

        private static GameObject Go(Transform p, string n)
        {
            var g = new GameObject(n);
            g.transform.SetParent(p, false);
            return g;
        }

        private static void EnsureDir(string path)
        {
            string[] pts = path.Split('/');
            string cur = pts[0];
            for (int i = 1; i < pts.Length; i++)
            {
                string nxt = cur + "/" + pts[i];
                if (!AssetDatabase.IsValidFolder(nxt)) AssetDatabase.CreateFolder(cur, pts[i]);
                cur = nxt;
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
    }
}
#endif
