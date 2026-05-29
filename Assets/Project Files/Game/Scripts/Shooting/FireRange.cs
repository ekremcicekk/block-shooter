using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    [RequireComponent(typeof(Collider))]
    public class FireRange : MonoBehaviour
    {
        public static FireRange Instance { get; private set; }

        private readonly HashSet<ConveyorBlock3D> _blocksInRange = new();

        public event Action<ConveyorBlock3D> OnBlockEntered;
        public event Action<ConveyorBlock3D> OnBlockExited;

        public IReadOnlyCollection<ConveyorBlock3D> BlocksInRange => _blocksInRange;

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

        public bool HasTargetFor(BlockColorType colorType)
        {
            foreach (var b in _blocksInRange)
                if (b.ColorType == colorType) return true;
            return false;
        }

        public ConveyorBlock3D GetClosestTarget(BlockColorType colorType, Vector3 from)
        {
            ConveyorBlock3D closest = null;
            float minDist = float.MaxValue;
            foreach (var b in _blocksInRange)
            {
                if (b.ColorType != colorType) continue;
                float d = Vector3.Distance(from, b.transform.position);
                if (d < minDist) { minDist = d; closest = b; }
            }
            return closest;
        }
    }
}
