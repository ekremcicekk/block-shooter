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
            if (!Application.isPlaying) return;
            if (Instance != null && Instance != this) Destroy(Instance.gameObject);
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
                // Do NOT clear IsTargeted here. If a projectile is already in flight toward
                // this block, clearing the flag would let another shooter re-target the same
                // block, causing a wasted shot when the first projectile destroys it first.
                // The in-flight projectile's ReturnToPool() clears IsTargeted if it misses.
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
        /// Returns the block in range closest to this FireRange's transform (i.e. the
        /// most "urgent" block — the one about to exit the zone). Sorting by actual
        /// world distance is reliable regardless of RowIndex or group ordering.
        /// </summary>
        public ConveyorBlock3D GetFirstTarget(BlockColorType colorType)
        {
            ConveyorBlock3D best     = null;
            float           bestDist = float.MaxValue;
            Vector3         origin   = transform.position;
            foreach (var b in _blocksInRange)
            {
                if (b == null || b.IsDestroyed || b.ColorType != colorType) continue;
                float d = Vector3.SqrMagnitude(b.transform.position - origin);
                if (d < bestDist) { bestDist = d; best = b; }
            }
            return best;
        }

        public ConveyorBlock3D GetFirstTarget()
        {
            ConveyorBlock3D best     = null;
            float           bestDist = float.MaxValue;
            Vector3         origin   = transform.position;
            foreach (var b in _blocksInRange)
            {
                if (b == null || b.IsDestroyed) continue;
                float d = Vector3.SqrMagnitude(b.transform.position - origin);
                if (d < bestDist) { bestDist = d; best = b; }
            }
            return best;
        }
    }
}
