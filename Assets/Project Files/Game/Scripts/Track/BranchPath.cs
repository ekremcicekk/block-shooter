using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using DG.Tweening;

namespace BlockShooter
{
    public class BranchPath : MonoBehaviour
    {
        public BranchPathData data;
        public float mergeT = 0.5f;

        private SplineContainer _splineContainer;
        private float _splineLength;
        private float _mergeStopT = 0.95f; // T value where blocks stop (outer wall of main conveyor)
        private readonly List<BranchRowEntry> _rows = new();

        public struct BranchRowEntry
        {
            public ConveyorBlock3D[] Blocks;
            public BlockColorType ColorType;
            public float CurrentT; // Position along branch spline (0.0 to 1.0)
            public float RowSpacing;
            public BlockGroup OriginalGroup;
            public BlockGroup MergedGroup;
            public bool[] MergedLanes;
        }

        public void Initialize()
        {
            _splineContainer = GetComponent<SplineContainer>();
            _splineLength = SplineUtility.CalculateLength(_splineContainer.Spline, transform.localToWorldMatrix);
            mergeT = data != null ? data.mergeT : mergeT;

            // Calculate stop T: blocks should stop at the outer edge of the main conveyor
            // The last knot is at the center of the main track, so we offset by the track half-width
            // plus half the block's row spacing and a small safety margin to prevent clipping
            float mainTrackHalfWidth = 0f;
            var mainTrackBuilder = FindFirstMainTrackBuilder();
            if (mainTrackBuilder != null)
            {
                mainTrackHalfWidth = mainTrackBuilder.beltHalfWidth + mainTrackBuilder.railWidth;
            }
            else
            {
                mainTrackHalfWidth = 0.55f; // fallback
            }

            float firstRowSpacing = 0.18f; // fallback
            var blockGroups = GetComponentsInChildren<BlockGroup>(true);
            var groupList = new List<BlockGroup>(blockGroups);
            groupList.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
            if (groupList.Count > 0)
            {
                firstRowSpacing = groupList[0].rowSpacing;
            }
            float laneSpacing = GetMainTrackLaneSpacing();

            // Align the closest possible block (lane 0 or 4, which is offset by 2 * laneSpacing)
            // exactly 0.05m outside the conveyor outer wall
            float safetyOffset = mainTrackHalfWidth + 2f * laneSpacing + 0.05f;

            if (_splineLength > 0f)
            {
                _mergeStopT = Mathf.Clamp01(1.0f - (safetyOffset / _splineLength));
            }



            _rows.Clear();

            int globalRowIndex = 0;
            foreach (var group in groupList)
            {
                group.Initialize();

                for (int r = 0; r < group.RowCount; r++)
                {
                    var laneBlocks = new List<ConveyorBlock3D>();
                    for (int l = 0; l < group.LaneCount; l++)
                    {
                        var b = group.GetBlock(r, l);
                        if (b != null) laneBlocks.Add(b);
                    }

                    if (laneBlocks.Count > 0)
                    {
                        float rowSpacing = group.rowSpacing;
                        // Start from _mergeStopT backwards instead of 1.0
                        float initialT = _mergeStopT - (globalRowIndex * rowSpacing) / _splineLength;
                        initialT = Mathf.Clamp01(initialT);

                        _rows.Add(new BranchRowEntry
                        {
                            Blocks = laneBlocks.ToArray(),
                            ColorType = group.colorType,
                            CurrentT = initialT,
                            RowSpacing = rowSpacing,
                            OriginalGroup = group,
                            MergedGroup = null,
                            MergedLanes = new bool[5]
                        });
                        globalRowIndex++;
                    }
                }
            }

            foreach (var row in _rows)
            {
                PlaceRowAtT(row);
            }
        }

        private ConveyorTrackMeshBuilder FindFirstMainTrackBuilder()
        {
            var cc = ConveyorController.Instance;
            if (cc != null)
            {
                return cc.GetComponent<ConveyorTrackMeshBuilder>();
            }
            // Fallback: search sibling or parent
            var lr = GetComponentInParent<LevelRoot>();
            if (lr != null && lr.conveyorController != null)
            {
                return lr.conveyorController.GetComponent<ConveyorTrackMeshBuilder>();
            }
            return null;
        }

        private void Update()
        {
            if (!GameManager.Instance.IsPlaying || _rows.Count == 0) return;

            float speed = ConveyorController.Instance.speed;
            if (ConveyorController.Instance.IsFrozen) speed = 0f;
            float delta = (speed / _splineLength) * Time.deltaTime;

            for (int i = 0; i < _rows.Count; i++)
            {
                var entry = _rows[i];
                float maxT = 1.0f;
                if (i == 0)
                {
                    maxT = (entry.MergedGroup != null) ? 1.0f : _mergeStopT;
                }
                else
                {
                    float spacingInT = _rows[i].RowSpacing / _splineLength;
                    maxT = _rows[i - 1].CurrentT - spacingInT;
                    if (entry.MergedGroup == null)
                    {
                        maxT = Mathf.Min(maxT, _mergeStopT);
                    }
                }

                entry.CurrentT = Mathf.Min(entry.CurrentT + delta, maxT);
                _rows[i] = entry;

                PlaceRowAtT(entry);
            }

            // Check and merge blocks for all rows that have reached the stop point or are already merging
            for (int i = 0; i < _rows.Count; i++)
            {
                var entry = _rows[i];
                if (entry.CurrentT >= _mergeStopT - 0.001f || entry.MergedGroup != null)
                {
                    CheckAndMergeBlocks(ref entry);
                    _rows[i] = entry;
                }
            }

            // Remove fully merged rows from the front
            int rowsBefore = _rows.Count;
            while (_rows.Count > 0 && AllLanesMerged(_rows[0]))
            {
                var firstRow = _rows[0];
                if (firstRow.MergedGroup != null && firstRow.MergedGroup.IsEmpty)
                {
                    ConveyorController.Instance.RemoveGroup(firstRow.MergedGroup);
                }
                _rows.RemoveAt(0);
            }

            if (rowsBefore > 0 && _rows.Count == 0)
            {
                // Branch fully merged — new blocks now on conveyor, check for potential deadlock
                GameManager.Instance?.CheckFailCondition();
            }
        }

        private void PlaceRowAtT(BranchRowEntry row)
        {
            if (_splineContainer == null || _splineLength <= 0f) return;

            _splineContainer.Spline.Evaluate(row.CurrentT, out var pos, out var tangent, out var up);

            Vector3 worldPos = transform.TransformPoint(pos);
            Vector3 fwd = transform.TransformDirection((Vector3)tangent).normalized;
            Vector3 upDir = transform.TransformDirection((Vector3)up).normalized;
            if (upDir == Vector3.zero) upDir = Vector3.up;
            Vector3 right = Vector3.Cross(upDir, fwd).normalized;
            Quaternion rot = fwd != Vector3.zero ? Quaternion.LookRotation(fwd, upDir) : Quaternion.identity;

            const int totalLanes = 5;
            float laneSpacing = GetMainTrackLaneSpacing();

            foreach (var block in row.Blocks)
            {
                if (block == null || block.IsDestroyed) continue;
                if (row.MergedLanes != null && row.MergedLanes[block.LaneIndex]) continue; // skip merged
                
                int lane = block.LaneIndex;
                float xOff = (lane - (totalLanes - 1) * 0.5f) * laneSpacing;
                block.transform.SetPositionAndRotation(worldPos + right * xOff, rot);
            }
        }

        private bool AllLanesMerged(BranchRowEntry row)
        {
            if (row.MergedLanes == null) return false;
            foreach (var block in row.Blocks)
            {
                if (block == null || block.IsDestroyed) continue;
                if (!row.MergedLanes[block.LaneIndex]) return false;
            }
            return true;
        }

        private void CheckAndMergeBlocks(ref BranchRowEntry row)
        {
            if (row.MergedLanes == null) row.MergedLanes = new bool[5];

            // If no blocks have merged yet, check if we can start the merge
            if (row.MergedGroup == null)
            {
                // Only start merging if the row has reached the merge stop point
                if (row.CurrentT < _mergeStopT - 0.001f) return;

                // Check if the conveyor is clear for the lanes that have active blocks in our row
                float checkHalfSize = row.RowSpacing / ConveyorController.Instance.SplineWorldLength;
                bool canMerge = true;
                foreach (var block in row.Blocks)
                {
                    if (block == null || block.IsDestroyed) continue;
                    if (!ConveyorController.Instance.IsRangeEmptyForLane(mergeT - checkHalfSize, mergeT + checkHalfSize, block.LaneIndex))
                    {
                        canMerge = false;
                        break;
                    }
                }

                if (!canMerge) return; // wait until the conveyor is clear

                Debug.Log($"[BranchPath] Merge STARTING on {gameObject.name}: Conveyor is clear. Creating MergedGroup at mergeT {mergeT:F3} for color {row.ColorType}.");

                // Create the MergedGroup immediately to start the merging state
                GameObject tempGo = new GameObject("MergedRowGroup");
                var newGroup = tempGo.AddComponent<BlockGroup>();
                newGroup.colorType = row.ColorType;
                newGroup.rowCount = 1;
                newGroup.laneCount = 5;
                newGroup.laneSpacing = GetMainTrackLaneSpacing();
                newGroup.rowSpacing = row.RowSpacing;
                newGroup.Initialize();

                row.MergedGroup = newGroup;
                ConveyorController.Instance.InsertGroupAt(newGroup, mergeT);
            }

            // Now that MergedGroup is created, we are in the merging state.
            // As the row moves forward (up to maxT = 1.0f), blocks will cross the conveyor boundary.
            // We check and merge each block when it crosses the boundary.
            float safetyThreshold = Mathf.Lerp(_mergeStopT, 1.0f, 0.8f);
            bool forceMergeAll = (row.CurrentT >= safetyThreshold);

            for (int lane = 0; lane < 5; lane++)
            {
                var block = GetBlockFromEntry(row, lane);
                if (block == null || block.IsDestroyed) continue;

                if (row.MergedLanes[lane]) continue;

                Vector3 worldPos = GetBlockBranchPosition(row, lane);
                if (forceMergeAll || IsPositionInsideConveyor(worldPos))
                {
                    MergeBlock(ref row, block, lane);
                    row.MergedLanes[lane] = true;
                }
            }
        }

        private ConveyorBlock3D GetBlockFromEntry(BranchRowEntry row, int lane)
        {
            foreach (var b in row.Blocks)
            {
                if (b != null && b.LaneIndex == lane) return b;
            }
            return null;
        }

        private Vector3 GetBlockBranchPosition(BranchRowEntry row, int lane)
        {
            if (_splineContainer == null) return Vector3.zero;
            _splineContainer.Spline.Evaluate(row.CurrentT, out var pos, out var tangent, out var up);

            Vector3 worldPos = transform.TransformPoint(pos);
            Vector3 fwd = transform.TransformDirection((Vector3)tangent).normalized;
            Vector3 upDir = transform.TransformDirection((Vector3)up).normalized;
            if (upDir == Vector3.zero) upDir = Vector3.up;
            Vector3 right = Vector3.Cross(upDir, fwd).normalized;
            
            float laneSpacing = GetMainTrackLaneSpacing();
            float xOff = (lane - 2f) * laneSpacing;
            return worldPos + right * xOff;
        }

        private bool IsPositionInsideConveyor(Vector3 worldPos)
        {
            var cc = ConveyorController.Instance;
            if (cc == null) return false;

            var mainSpline = cc.SplineContainer;
            if (mainSpline == null) return false;

            var mainTrackBuilder = FindFirstMainTrackBuilder();
            if (mainTrackBuilder == null) return false;

            Vector3 mainLocalPos = mainSpline.transform.InverseTransformPoint(worldPos);

            SplineUtility.GetNearestPoint(
                mainSpline.Spline,
                mainLocalPos,
                out var nearestLocal,
                out float _,
                8,
                4
            );

            Vector3 worldNearest = mainSpline.transform.TransformPoint((Vector3)nearestLocal);

            // Use absolute distance from the nearest main-track center point.
            // The old directional-projection approach required branchOnRightSide to be
            // correct, but that flag is unreliable (wrong tangent direction at some mergeT
            // values causes all blocks to be classified as "outside"). A simple radial
            // distance works for branches from either side.
            float dist = Vector3.Distance(worldPos, worldNearest);
            float R    = mainTrackBuilder.beltHalfWidth + mainTrackBuilder.railWidth;

            return dist < R;
        }

        private void MergeBlock(ref BranchRowEntry row, ConveyorBlock3D block, int lane)
        {
            // MergedGroup is already created in CheckAndMergeBlocks
            Vector3 prevPosition = block.transform.position;
            Quaternion prevRotation = block.transform.rotation;

            block.transform.SetParent(row.MergedGroup.transform, true);
            block.SetGroupIndex(0, lane);
            row.MergedGroup.RegisterMergedBlock(block, lane);

            // Force update Conveyor positions so target position is updated
            ConveyorController.Instance.ForceUpdateGroupPosition(row.MergedGroup);

            Vector3 targetWorldPos = block.transform.position;
            Quaternion targetWorldRot = block.transform.rotation;

            block.transitionOffset = prevPosition - targetWorldPos;
            block.transitionRotOffset = Quaternion.Inverse(targetWorldRot) * prevRotation;

            // Immediately set the position and rotation back to the transition starting state
            // to prevent 1-frame visual flicker
            block.transform.position = prevPosition;
            block.transform.rotation = prevRotation;

            // Get MergedGroup reference to a local variable to use inside lambda expressions,
            // as ref parameters like 'row' cannot be captured by closures.
            var mergedGroup = row.MergedGroup;

            float duration = 0.16f; // Snappy jump transition duration (0.16s for a clean arc)
            float jumpHeight = 0.35f; // Higher peak height to jump clearly over the outer wall (0.35m)
            Vector3 startOffset = block.transitionOffset;
            Quaternion initialRotOffset = block.transitionRotOffset;

            // Clean up any existing tweens on this block to avoid collisions
            DOTween.Kill(block);

            // Jump tween for position offset (uses Ease.OutQuad for quick leap and smooth landing)
            DOTween.To(tVal => {
                if (block == null || mergedGroup == null) return;
                
                // Interpolate X and Z offsets linearly to 0
                float x = Mathf.Lerp(startOffset.x, 0f, tVal);
                float z = Mathf.Lerp(startOffset.z, 0f, tVal);
                
                // Parabolic vertical arc added to the Y offset
                float yNormal = Mathf.Lerp(startOffset.y, 0f, tVal);
                float arc = Mathf.Sin(tVal * Mathf.PI) * jumpHeight;
                float y = yNormal + arc;
                
                block.transitionOffset = new Vector3(x, y, z);
            }, 0f, 1f, duration).SetEase(Ease.OutQuad).SetId(block);

            // Slerp tween for rotation offset (uses Ease.OutQuad to match position snappiness)
            DOTween.To(tVal => {
                if (block == null) return;
                block.transitionRotOffset = Quaternion.Slerp(initialRotOffset, Quaternion.identity, tVal);
            }, 0f, 1f, duration).SetEase(Ease.OutQuad).SetId(block);

            Debug.Log($"[BranchPath] Merged block {block.name} (lane {lane}) on {gameObject.name} onto conveyor. Jump initiated.");
        }

        private float GetMainTrackLaneSpacing()
        {
            // 1. Try to read from the branch path's own groups first (they have the correct level lane spacing)
            var branchGroups = GetComponentsInChildren<BlockGroup>(true);
            if (branchGroups != null && branchGroups.Length > 0)
            {
                foreach (var g in branchGroups)
                {
                    if (g != null && g.laneSpacing > 0.01f)
                    {
                        return g.laneSpacing;
                    }
                }
            }

            // 2. Try to read from any active block groups on the main conveyor controller
            var cc = ConveyorController.Instance;
            if (cc != null)
            {
                var mainGroups = cc.GetComponentsInChildren<BlockGroup>(true);
                foreach (var g in mainGroups)
                {
                    if (g != null && g.GetComponentInParent<BranchPath>() == null && g.laneSpacing > 0.01f)
                    {
                        return g.laneSpacing;
                    }
                }
            }
            return 0.18f; // Fallback to level default
        }
    }
}

