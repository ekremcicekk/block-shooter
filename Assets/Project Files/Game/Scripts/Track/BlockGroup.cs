using System;
using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// A group of pre-placed ConveyorBlock3D children arranged in a row × lane grid.
    /// The Level Editor tool creates the child hierarchy; this script registers them at runtime.
    /// </summary>
    public class BlockGroup : MonoBehaviour
    {
        [Header("Config")]
        public BlockColorType colorType;
        public int   rowCount    = 20;
        public int   laneCount   = 5;
        public float laneSpacing = 0.22f;
        public float rowSpacing  = 0.22f;

        public int   RowCount    => rowCount;
        public int   LaneCount   => laneCount;
        public float LaneSpacing => laneSpacing;
        public float SplineLength => rowCount * rowSpacing;

        private ConveyorBlock3D[,] _blocks;
        private int _aliveCount;

        public int  AliveCount => _aliveCount;
        public bool IsEmpty    => _aliveCount <= 0;

        public event Action<BlockGroup> OnGroupCleared;

        /// <summary>
        /// Scans ConveyorBlock3D children (using their serialized RowIndex/LaneIndex),
        /// registers them, and applies color. Called by ConveyorController.Initialize().
        /// </summary>
        public void Initialize()
        {
            _blocks = new ConveyorBlock3D[rowCount, laneCount];
            _aliveCount = 0;

            Color c = GameManager.Instance.config.GetColor(colorType);

            foreach (var block in GetComponentsInChildren<ConveyorBlock3D>(true))
            {
                int r = block.RowIndex;
                int l = block.LaneIndex;
                if (r < 0 || r >= rowCount || l < 0 || l >= laneCount) continue;

                _blocks[r, l] = block;
                block.Initialize(colorType, c);
                block.OnDestroyed += HandleBlockDestroyed;
                _aliveCount++;
            }
        }

        public ConveyorBlock3D GetBlock(int row, int lane)
        {
            if (_blocks == null) return null;
            if (row < 0 || row >= rowCount || lane < 0 || lane >= laneCount) return null;
            return _blocks[row, lane];
        }

        private void HandleBlockDestroyed(ConveyorBlock3D block)
        {
            block.OnDestroyed -= HandleBlockDestroyed;
            _aliveCount--;
            if (_aliveCount <= 0)
                OnGroupCleared?.Invoke(this);
        }

        public void SetVisible(bool visible)
        {
            if (_blocks == null) return;
            foreach (var b in _blocks)
                if (b != null) b.gameObject.SetActive(visible);
        }

        public void DestroyBlocksInBounds(Bounds bounds)
        {
            if (_blocks == null) return;
            foreach (var b in _blocks)
            {
                if (b == null || b.IsDestroyed || !b.gameObject.activeSelf) continue;
                if (bounds.Contains(b.transform.position))
                    b.TriggerDestroy();
            }
        }
    }
}
