using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// A group of 3D conveyor blocks (5 wide × rows deep) moving as one unit on a spline.
    /// Total = laneCount × rowCount blocks. Default: 5×20 = 100.
    /// </summary>
    public class BlockGroup : MonoBehaviour
    {
        [Header("Config")]
        public BlockColorType colorType;
        public int laneCount = 5;
        public int rowCount = 20;
        public float blockSize = 0.38f;
        public float laneSpacing = 0.42f;
        public float rowSpacing = 0.42f;

        [Header("Prefab")]
        public ConveyorBlock3D blockPrefab;

        private readonly List<ConveyorBlock3D> _blocks = new();
        private int _aliveCount;

        public int AliveCount => _aliveCount;
        public bool IsEmpty => _aliveCount <= 0;
        public float SplineLength => rowCount * rowSpacing;

        public event Action<BlockGroup> OnGroupCleared;

        public void Initialize(BlockColorType color, ConveyorBlock3D prefab, int lanes, int rows)
        {
            colorType = color;
            blockPrefab = prefab;
            laneCount = lanes;
            rowCount = rows;

            SpawnBlocks();
        }

        private void SpawnBlocks()
        {
            ClearBlocks();
            Color c = GameManager.Instance.config.GetColor(colorType);

            for (int row = 0; row < rowCount; row++)
            {
                for (int lane = 0; lane < laneCount; lane++)
                {
                    ConveyorBlock3D block = Instantiate(blockPrefab, transform);
                    block.Initialize(colorType, c);
                    block.OnDestroyed += HandleBlockDestroyed;
                    _blocks.Add(block);
                }
            }

            _aliveCount = _blocks.Count;
        }

        // Returns the block at (row, lane), or null if destroyed/missing.
        public ConveyorBlock3D GetBlock(int row, int lane)
        {
            int idx = row * laneCount + lane;
            return (idx >= 0 && idx < _blocks.Count) ? _blocks[idx] : null;
        }

        private void HandleBlockDestroyed(ConveyorBlock3D block)
        {
            block.OnDestroyed -= HandleBlockDestroyed;
            _aliveCount--;

            if (_aliveCount <= 0)
                OnGroupCleared?.Invoke(this);
        }

        private void ClearBlocks()
        {
            foreach (var b in _blocks)
                if (b != null) Destroy(b.gameObject);
            _blocks.Clear();
            _aliveCount = 0;
        }

        public void SetVisible(bool visible)
        {
            foreach (var b in _blocks)
                if (b != null) b.gameObject.SetActive(visible);
        }

        private void OnDestroy() => ClearBlocks();
    }
}
