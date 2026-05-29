using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    [RequireComponent(typeof(Collider))]
    public class FireRange : MonoBehaviour
    {
        public static FireRange Instance { get; private set; }

        // List preserves insertion order = conveyor arrival order (blocks enter in path sequence)
        private readonly List<ConveyorBlock3D> _blocksInRange = new();

        public event Action<ConveyorBlock3D> OnBlockEntered;
        public event Action<ConveyorBlock3D> OnBlockExited;

        public IReadOnlyList<ConveyorBlock3D> BlocksInRange => _blocksInRange;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent<ConveyorBlock3D>(out var block)) return;
            if (_blocksInRange.Contains(block)) return;
            _blocksInRange.Add(block);
            OnBlockEntered?.Invoke(block);
            block.OnDestroyed += HandleBlockDestroyed;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.TryGetComponent<ConveyorBlock3D>(out var block)) return;
            RemoveBlock(block);
        }

        private void HandleBlockDestroyed(ConveyorBlock3D block) => RemoveBlock(block);

        private void RemoveBlock(ConveyorBlock3D block)
        {
            if (_blocksInRange.Remove(block))
            {
                block.OnDestroyed -= HandleBlockDestroyed;
                OnBlockExited?.Invoke(block);
            }
        }

        /// <summary>Returns the world-space bounds of this trigger collider.</summary>
        public Bounds GetBounds() => GetComponent<Collider>().bounds;

        public bool HasTargetFor(BlockColorType colorType)
        {
            foreach (var b in _blocksInRange)
                if (!b.IsDestroyed && b.ColorType == colorType) return true;
            return false;
        }

        /// <summary>
        /// Returns the block in range with the lowest RowIndex (then LaneIndex) matching the color.
        /// This targets blocks in BlockGroup spawn order: row 0 first, left lane first.
        /// </summary>
        public ConveyorBlock3D GetFirstTarget(BlockColorType colorType)
        {
            ConveyorBlock3D best = null;
            foreach (var b in _blocksInRange)
            {
                if (b.IsDestroyed || b.ColorType != colorType) continue;
                if (best == null
                    || b.RowIndex < best.RowIndex
                    || (b.RowIndex == best.RowIndex && b.LaneIndex < best.LaneIndex))
                    best = b;
            }
            return best;
        }

        /// <summary>Returns the block with the lowest RowIndex/LaneIndex (any color).</summary>
        public ConveyorBlock3D GetFirstTarget()
        {
            ConveyorBlock3D best = null;
            foreach (var b in _blocksInRange)
            {
                if (b.IsDestroyed) continue;
                if (best == null
                    || b.RowIndex < best.RowIndex
                    || (b.RowIndex == best.RowIndex && b.LaneIndex < best.LaneIndex))
                    best = b;
            }
            return best;
        }
    }
}
