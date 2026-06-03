using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

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
        }

        public void Initialize()
        {
            _splineContainer = GetComponent<SplineContainer>();
            _splineLength = SplineUtility.CalculateLength(_splineContainer.Spline, transform.localToWorldMatrix);
            mergeT = data != null ? data.mergeT : mergeT;

            // Calculate stop T: blocks should stop at the outer edge of the main conveyor
            // The last knot is at the center of the main track, so we offset by the track half-width
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

            if (_splineLength > 0f)
            {
                _mergeStopT = Mathf.Clamp01(1.0f - (mainTrackHalfWidth / _splineLength));
            }

            var blockGroups = GetComponentsInChildren<BlockGroup>(true);
            var groupList = new List<BlockGroup>(blockGroups);
            groupList.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

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
                            OriginalGroup = group
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
                // Cap at _mergeStopT for the front row, or behind the row in front
                float maxT = _mergeStopT;
                if (i > 0)
                {
                    float spacingInT = _rows[i].RowSpacing / _splineLength;
                    maxT = _rows[i - 1].CurrentT - spacingInT;
                }

                entry.CurrentT = Mathf.Min(entry.CurrentT + delta, maxT);
                _rows[i] = entry;

                PlaceRowAtT(entry);
            }

            var frontRow = _rows[0];
            if (frontRow.CurrentT >= _mergeStopT - 0.001f)
            {
                float checkHalfSize = (1.5f * frontRow.RowSpacing) / ConveyorController.Instance.SplineWorldLength;
                bool canMerge = ConveyorController.Instance.IsRangeEmpty(mergeT - checkHalfSize, mergeT + checkHalfSize);

                if (canMerge)
                {
                    MergeRow(frontRow);
                    _rows.RemoveAt(0);
                }
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
            float laneSpacing = row.OriginalGroup != null ? row.OriginalGroup.laneSpacing : 0.22f;

            foreach (var block in row.Blocks)
            {
                if (block == null || block.IsDestroyed) continue;
                int lane = block.LaneIndex;
                float xOff = (lane - (totalLanes - 1) * 0.5f) * laneSpacing;
                block.transform.SetPositionAndRotation(worldPos + right * xOff, rot);
            }
        }

        private void MergeRow(BranchRowEntry row)
        {
            GameObject tempGo = new GameObject("MergedRowGroup");
            var newGroup = tempGo.AddComponent<BlockGroup>();
            newGroup.colorType = row.ColorType;
            newGroup.rowCount = 1;
            newGroup.laneCount = 5;
            newGroup.laneSpacing = row.OriginalGroup != null ? row.OriginalGroup.laneSpacing : 0.22f;
            newGroup.rowSpacing = row.RowSpacing;

            foreach (var block in row.Blocks)
            {
                if (block == null || block.IsDestroyed) continue;
                block.transform.SetParent(tempGo.transform, true);
                block.SetGroupIndex(0, block.LaneIndex);
            }

            newGroup.Initialize();
            ConveyorController.Instance.InsertGroupAt(newGroup, mergeT);
        }
    }
}

