using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using DG.Tweening;

namespace BlockShooter
{
    /// <summary>
    /// Moves conveyor blocks along a branch spline toward the main conveyor.
    /// When a row reaches the stop point it waits for a gap on the main
    /// conveyor (T-range check only — no geometry side calculation) then
    /// jumps the blocks onto the main conveyor with a DOTween arc.
    /// </summary>
    public class BranchPath : MonoBehaviour
    {
        public BranchPathData data;
        public float mergeT = 0.5f;

        private SplineContainer _splineContainer;
        private float           _splineLength;
        private float           _stopT = 0.92f;

        private readonly List<RowEntry> _rows = new();

        private struct RowEntry
        {
            public ConveyorBlock3D[] Blocks;
            public BlockColorType    Color;
            public float             CurrentT;
            public float             RowSpacing;
            public bool              Merged;
            public BlockGroup        MergedGroup;
        }

        // ── Init ─────────────────────────────────────────────────────────────

        public void Initialize()
        {
            _splineContainer = GetComponent<SplineContainer>();
            _splineLength    = SplineUtility.CalculateLength(
                _splineContainer.Spline, transform.localToWorldMatrix);
            mergeT = data != null ? data.mergeT : mergeT;

            // Stop blocks before they visually enter the main track outer wall.
            // The branch spline last knot sits at the main track center at mergeT.
            // Outermost lane block is displaced 2 * laneSpacing laterally, so we
            // add that on top of the half-width to prevent clipping.
            float halfWidth   = GetMainHalfWidth();
            float laneSpacing = GetLaneSpacing();
            float safeOffset  = halfWidth + 2f * laneSpacing + 0.05f;
            _stopT = _splineLength > 0f
                ? Mathf.Clamp01(1f - safeOffset / _splineLength)
                : 0.90f;

            _rows.Clear();

            var groups = new List<BlockGroup>(GetComponentsInChildren<BlockGroup>(true));
            groups.Sort((a, b) => string.Compare(a.name, b.name,
                System.StringComparison.Ordinal));

            int globalRow = 0;
            foreach (var group in groups)
            {
                group.Initialize();

                for (int r = 0; r < group.RowCount; r++)
                {
                    var blocks = new List<ConveyorBlock3D>();
                    for (int l = 0; l < group.LaneCount; l++)
                    {
                        var b = group.GetBlock(r, l);
                        if (b != null) blocks.Add(b);
                    }

                    if (blocks.Count == 0) continue;

                    float t0 = _stopT - (globalRow * group.rowSpacing) / _splineLength;
                    _rows.Add(new RowEntry
                    {
                        Blocks     = blocks.ToArray(),
                        Color      = group.colorType,
                        CurrentT   = Mathf.Clamp01(t0),
                        RowSpacing = group.rowSpacing,
                        Merged     = false,
                        MergedGroup = null
                    });
                    globalRow++;
                }
            }

            foreach (var row in _rows)
                PlaceRow(row);
        }

        // ── Update ────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!GameManager.Instance.IsPlaying || _rows.Count == 0) return;

            var cc = ConveyorController.Instance;
            if (cc == null) return;

            float speed = cc.IsFrozen ? 0f : cc.speed;
            float delta = _splineLength > 0f ? (speed / _splineLength) * Time.deltaTime : 0f;

            // Advance rows
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.Merged) continue;

                // Front row stops at _stopT; each following row keeps spacing behind the one ahead
                float maxT = _stopT;
                if (i > 0)
                {
                    var prev = _rows[i - 1];
                    maxT = Mathf.Min(_stopT,
                        prev.CurrentT - row.RowSpacing / _splineLength);
                }

                row.CurrentT  = Mathf.Min(row.CurrentT + delta, maxT);
                _rows[i]      = row;
                PlaceRow(row);
            }

            // Try to merge the front-most unmerged row that has reached the stop
            TryMergeFrontRow();

            // Purge fully-cleared merged rows
            for (int i = _rows.Count - 1; i >= 0; i--)
            {
                var row = _rows[i];
                if (row.Merged && row.MergedGroup != null && row.MergedGroup.IsEmpty)
                {
                    cc.RemoveGroup(row.MergedGroup);
                    _rows.RemoveAt(i);
                }
            }
        }

        // ── Merge ─────────────────────────────────────────────────────────────

        private void TryMergeFrontRow()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.Merged) continue;

                // Not at stop yet
                if (row.CurrentT < _stopT - 0.001f) break;

                // Pure T-range gap check — no geometry/side calculation needed
                var cc = ConveyorController.Instance;
                float halfRowT = (row.RowSpacing * 0.5f) / cc.SplineWorldLength;
                if (!cc.IsRangeEmpty(mergeT - halfRowT, mergeT + halfRowT)) break;

                DoMerge(i);
                break; // one row per frame
            }
        }

        private void DoMerge(int idx)
        {
            var row = _rows[idx];
            var cc  = ConveyorController.Instance;

            // Create a 1-row BlockGroup on the main conveyor
            var go    = new GameObject("BranchMergedRow");
            var group = go.AddComponent<BlockGroup>();
            group.colorType   = row.Color;
            group.rowCount    = 1;
            group.laneCount   = 5;
            group.laneSpacing = GetLaneSpacing();
            group.rowSpacing  = row.RowSpacing;
            group.Initialize();

            cc.InsertGroupAt(group, mergeT);
            cc.ForceUpdateGroupPosition(group);

            foreach (var block in row.Blocks)
            {
                if (block == null || block.IsDestroyed) continue;

                Vector3    fromPos = block.transform.position;
                Quaternion fromRot = block.transform.rotation;

                block.transform.SetParent(group.transform, true);
                block.SetGroupIndex(0, block.LaneIndex);
                group.RegisterMergedBlock(block, block.LaneIndex);

                cc.ForceUpdateGroupPosition(group);

                Vector3    toPos = block.transform.position;
                Quaternion toRot = block.transform.rotation;

                block.transitionOffset    = fromPos - toPos;
                block.transitionRotOffset = Quaternion.Inverse(toRot) * fromRot;

                // Restore visual position before tween starts
                block.transform.position = fromPos;
                block.transform.rotation = fromRot;

                AnimateMerge(block, block.transitionOffset, block.transitionRotOffset);
            }

            row.MergedGroup = group;
            row.Merged      = true;
            _rows[idx]      = row;
        }

        private static void AnimateMerge(ConveyorBlock3D block,
            Vector3 startOffset, Quaternion startRot)
        {
            DOTween.Kill(block);
            const float duration   = 0.18f;
            const float jumpHeight = 0.35f;

            DOTween.To(t =>
            {
                if (block == null) return;
                block.transitionOffset = new Vector3(
                    Mathf.Lerp(startOffset.x, 0f, t),
                    Mathf.Lerp(startOffset.y, 0f, t) + Mathf.Sin(t * Mathf.PI) * jumpHeight,
                    Mathf.Lerp(startOffset.z, 0f, t));
            }, 0f, 1f, duration).SetEase(Ease.OutQuad).SetId(block);

            DOTween.To(t =>
            {
                if (block == null) return;
                block.transitionRotOffset = Quaternion.Slerp(startRot, Quaternion.identity, t);
            }, 0f, 1f, duration).SetEase(Ease.OutQuad).SetId(block);
        }

        // ── Placement ─────────────────────────────────────────────────────────

        private void PlaceRow(RowEntry row)
        {
            if (_splineContainer == null || _splineLength <= 0f || row.Merged) return;

            _splineContainer.Spline.Evaluate(
                row.CurrentT, out var pos, out var tangent, out var up);

            Vector3    worldPos = transform.TransformPoint(pos);
            Vector3    fwd      = transform.TransformDirection((Vector3)tangent).normalized;
            Vector3    upDir    = transform.TransformDirection((Vector3)up).normalized;
            if (upDir == Vector3.zero) upDir = Vector3.up;
            Vector3    right    = Vector3.Cross(upDir, fwd).normalized;
            Quaternion rot      = fwd != Vector3.zero
                ? Quaternion.LookRotation(fwd, upDir)
                : Quaternion.identity;

            float laneSpacing = GetLaneSpacing();
            foreach (var block in row.Blocks)
            {
                if (block == null || block.IsDestroyed) continue;
                int   lane = block.LaneIndex;
                float xOff = (lane - 2f) * laneSpacing;
                block.transform.SetPositionAndRotation(worldPos + right * xOff, rot);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private float GetMainHalfWidth()
        {
            var builder = ConveyorController.Instance?.GetComponent<ConveyorTrackMeshBuilder>();
            if (builder != null) return builder.beltHalfWidth + builder.railWidth;
            var lr = GetComponentInParent<LevelRoot>();
            if (lr?.conveyorController != null)
            {
                var b2 = lr.conveyorController.GetComponent<ConveyorTrackMeshBuilder>();
                if (b2 != null) return b2.beltHalfWidth + b2.railWidth;
            }
            return 0.55f;
        }

        private float GetLaneSpacing()
        {
            foreach (var g in GetComponentsInChildren<BlockGroup>(true))
                if (g != null && g.laneSpacing > 0.01f) return g.laneSpacing;

            var cc = ConveyorController.Instance;
            if (cc != null)
                foreach (var g in cc.GetComponentsInChildren<BlockGroup>(true))
                    if (g != null && g.GetComponentInParent<BranchPath>() == null
                        && g.laneSpacing > 0.01f) return g.laneSpacing;

            return 0.18f;
        }
    }
}
