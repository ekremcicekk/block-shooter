using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace BlockShooter
{
    /// <summary>
    /// Manages the row of firing slots between the conveyor track and the shooter grid.
    ///
    /// Empty slots show a visual placeholder (slotIndicatorPrefab).
    /// When a ShooterBlock is tapped, it animates to the next free slot and starts shooting.
    ///
    /// Scene positioning:
    ///   slotOrigin Z ≈ 0  (between track at Z>0 and grid at Z=-3.5 to -1)
    ///   slotSpacing   = 1.2  (matches gridCellSize)
    ///   defaultSlotCount = 4
    /// </summary>
    public class SlotSystem : MonoBehaviour
    {
        public static SlotSystem Instance { get; private set; }

        [Header("Slot Layout")]
        [Tooltip("Centre of the slot row in world space")]
        public Transform slotOrigin;
        [Tooltip("Horizontal gap between slot centres — match GameConfig.gridCellSize")]
        public float slotSpacing = 1.2f;
        [Tooltip("Default number of slots at level start")]
        public int defaultSlotCount = 4;

        [Header("Visuals")]
        [Tooltip("Prefab shown when slot is empty (gray rounded square). If null, a primitive cube is used.")]
        public GameObject slotIndicatorPrefab;
        [Tooltip("Scale applied to each slot indicator")]
        public Vector3 indicatorScale = new Vector3(0.9f, 0.1f, 0.9f);

        [Header("Animation")]
        public float moveToSlotDuration = 0.35f;

        // ── Runtime state ─────────────────────────────────────────────────────
        private int _maxSlots;
        private readonly List<Vector3>      _slotPositions = new();
        private readonly List<ShooterBlock> _occupied      = new();  // null = empty
        private readonly List<GameObject>   _indicators    = new();  // one per slot

        public int  MaxSlots     => _maxSlots;
        public bool HasEmptySlot => EmptySlotIndex() >= 0;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start() => RebuildSlots(defaultSlotCount);

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Moves block to the next empty slot. Returns false if none available.</summary>
        public bool TrySlotBlock(ShooterBlock block)
        {
            int idx = EmptySlotIndex();
            if (idx < 0) return false;

            _occupied[idx] = block;
            SetIndicatorVisible(idx, false);

            block.transform.DOMove(_slotPositions[idx], moveToSlotDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(() => block.OnArrivedInSlot());

            return true;
        }

        /// <summary>Called when a slotted block depletes.</summary>
        public void ReleaseSlot(ShooterBlock block)
        {
            for (int i = 0; i < _occupied.Count; i++)
            {
                if (_occupied[i] != block) continue;
                _occupied[i] = null;
                SetIndicatorVisible(i, true);
                return;
            }
        }

        /// <summary>ExtraSlot booster — adds one more slot permanently for this level.</summary>
        public void AddExtraSlot()
        {
            int prev = _maxSlots;
            RebuildSlots(_maxSlots + 1);

            // Bounce-in the new indicator
            if (_indicators.Count > prev && _indicators[prev] != null)
            {
                _indicators[prev].transform.localScale = Vector3.zero;
                _indicators[prev].transform.DOScale(indicatorScale, 0.4f).SetEase(Ease.OutBack);
            }
        }

        public List<ShooterBlock> GetSlottedBlocks()
        {
            var result = new List<ShooterBlock>();
            foreach (var b in _occupied)
                if (b != null) result.Add(b);
            return result;
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void RebuildSlots(int count)
        {
            _maxSlots = count;

            Vector3 origin = slotOrigin != null ? slotOrigin.position : transform.position;
            float totalWidth = (count - 1) * slotSpacing;
            Vector3 start = origin - Vector3.right * (totalWidth * 0.5f);

            // Expand position + state lists
            while (_slotPositions.Count < count)
                _slotPositions.Add(Vector3.zero);
            while (_occupied.Count < count)
                _occupied.Add(null);

            // Reposition existing indicators and build new ones
            for (int i = 0; i < count; i++)
            {
                _slotPositions[i] = start + Vector3.right * (i * slotSpacing);

                if (i >= _indicators.Count)
                {
                    // Create new indicator
                    GameObject ind = slotIndicatorPrefab != null
                        ? Instantiate(slotIndicatorPrefab, _slotPositions[i], Quaternion.identity, transform)
                        : CreateDefaultIndicator(_slotPositions[i]);
                    ind.transform.localScale = indicatorScale;
                    _indicators.Add(ind);
                }
                else
                {
                    // Reposition existing
                    _indicators[i].transform.position = _slotPositions[i];
                }

                // Sync visibility with occupancy
                SetIndicatorVisible(i, _occupied[i] == null);
            }
        }

        private void SetIndicatorVisible(int idx, bool visible)
        {
            if (idx >= 0 && idx < _indicators.Count && _indicators[idx] != null)
                _indicators[idx].SetActive(visible);
        }

        private GameObject CreateDefaultIndicator(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "SlotIndicator";
            go.transform.position = pos;
            go.transform.SetParent(transform);

            // Destroy collider so it doesn't interfere with raycasts
            Destroy(go.GetComponent<Collider>());

            // Gray material
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat.shader.name == "Hidden/InternalErrorShader")
                    mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.55f, 0.55f, 0.60f, 1f);
                mr.material = mat;
            }
            return go;
        }

        private int EmptySlotIndex()
        {
            for (int i = 0; i < _maxSlots; i++)
                if (i < _occupied.Count && _occupied[i] == null) return i;
            return -1;
        }
    }
}
