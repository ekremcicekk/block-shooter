using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace BlockShooter
{
    /// <summary>
    /// Manages the row of firing slots that shooter blocks occupy when selected.
    ///
    /// Slots are evenly spaced along the X axis. When a ShooterBlock is tapped
    /// and an empty slot is available, the block animates to the slot position
    /// and begins auto-shooting. ExtraSlot booster calls AddExtraSlot() to widen
    /// the row by one.
    ///
    /// Scene setup: place this on a "SlotSystem" GameObject.
    /// The component auto-builds slot positions at runtime using slotOrigin + slotSpacing.
    /// </summary>
    public class SlotSystem : MonoBehaviour
    {
        public static SlotSystem Instance { get; private set; }

        [Header("Slot Layout")]
        [Tooltip("World position of the left-most slot")]
        public Transform slotOrigin;
        [Tooltip("Horizontal gap between slot centres")]
        public float slotSpacing = 1.1f;
        [Tooltip("Default number of slots at level start")]
        public int defaultSlotCount = 4;

        [Header("Animation")]
        public float moveToSlotDuration = 0.35f;

        // runtime state
        private int _maxSlots;
        private readonly List<Vector3>      _slotPositions  = new();
        private readonly List<ShooterBlock> _occupiedSlots  = new();   // null = empty

        public int  MaxSlots       => _maxSlots;
        public bool HasEmptySlot   => EmptySlotIndex() >= 0;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start() => RebuildSlots(defaultSlotCount);

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Moves a block into the next free slot. Returns false if no slot available.</summary>
        public bool TrySlotBlock(ShooterBlock block)
        {
            int idx = EmptySlotIndex();
            if (idx < 0) return false;

            _occupiedSlots[idx] = block;
            Vector3 target = _slotPositions[idx];

            block.transform.DOMove(target, moveToSlotDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(() => block.OnArrivedInSlot());

            return true;
        }

        /// <summary>Called when a slotted block is depleted/removed.</summary>
        public void ReleaseSlot(ShooterBlock block)
        {
            for (int i = 0; i < _occupiedSlots.Count; i++)
                if (_occupiedSlots[i] == block) { _occupiedSlots[i] = null; return; }
        }

        /// <summary>ExtraSlot booster: add one more slot to the right.</summary>
        public void AddExtraSlot()
        {
            RebuildSlots(_maxSlots + 1);

            // Visual pulse on the new slot indicator (optional feedback)
            // The new slot is the rightmost — animate something if you have slot visuals
        }

        /// <summary>Returns all blocks currently occupying slots (non-null).</summary>
        public List<ShooterBlock> GetSlottedBlocks()
        {
            var result = new List<ShooterBlock>();
            foreach (var b in _occupiedSlots)
                if (b != null) result.Add(b);
            return result;
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void RebuildSlots(int count)
        {
            _maxSlots = count;

            // Compute origin in world space
            Vector3 origin = slotOrigin != null ? slotOrigin.position : transform.position;

            // Centre the row
            float totalWidth = (count - 1) * slotSpacing;
            Vector3 start = origin - Vector3.right * (totalWidth * 0.5f);

            _slotPositions.Clear();
            for (int i = 0; i < count; i++)
                _slotPositions.Add(start + Vector3.right * (i * slotSpacing));

            // Grow the occupied list if needed (new slots start empty)
            while (_occupiedSlots.Count < count)
                _occupiedSlots.Add(null);
        }

        private int EmptySlotIndex()
        {
            for (int i = 0; i < _maxSlots; i++)
                if (i < _occupiedSlots.Count && _occupiedSlots[i] == null) return i;
            return -1;
        }
    }
}
