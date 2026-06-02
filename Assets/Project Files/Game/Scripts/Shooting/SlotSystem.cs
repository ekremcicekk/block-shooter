using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace BlockShooter
{
    /// <summary>
    /// Manages the row of firing slots between the conveyor track and the shooter grid.
    ///
    /// Slot positions are defined by child GameObjects named "Slot_0", "Slot_1", etc.
    /// Initialize() reads those transforms at level start; the Level Editor places them.
    ///
    /// Empty slots show a visual placeholder (slotIndicatorPrefab).
    /// </summary>
    public class SlotSystem : MonoBehaviour
    {
        public static SlotSystem Instance { get; private set; }

        [Header("Visuals")]
        [Tooltip("Prefab shown when slot is empty (gray rounded square). Falls back to a cube if null.")]
        public GameObject slotIndicatorPrefab;

        [Header("Animation")]
        public float moveToSlotDuration = 0.35f;

        // ── Runtime state ─────────────────────────────────────────────────────
        private int _maxSlots;
        private readonly List<Vector3>      _slotPositions = new();
        private readonly List<ShooterBlock> _occupied      = new();
        private readonly List<GameObject>   _indicators    = new();

        private float   _slotSpacing = 1.2f;
        private Vector3 _extraSlotDir = Vector3.right;

        public int  MaxSlots     => _maxSlots;
        public bool HasEmptySlot => EmptySlotIndex() >= 0;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (!Application.isPlaying) return;
            if (Instance != null && Instance != this) Destroy(Instance.gameObject);
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Initialization ────────────────────────────────────────────────────

        /// <summary>
        /// Reads child Slot_N transforms and builds slot positions.
        /// Called by LevelRoot.Initialize().
        /// </summary>
        public void Initialize()
        {
            // Clean up only runtime-added indicators (e.g. from extra slots)
            foreach (var ind in _indicators)
            {
                if (ind != null && ind.transform.parent == transform)
                {
                    Destroy(ind);
                }
            }

            _slotPositions.Clear();
            _occupied.Clear();
            _indicators.Clear();

            // Collect and sort child slot markers
            var slotTransforms = new List<Transform>();
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("Slot_"))
                    slotTransforms.Add(child);
            }
            slotTransforms.Sort((a, b) =>
                string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            // Derive spacing and direction for AddExtraSlot
            if (slotTransforms.Count >= 2)
            {
                Vector3 delta = slotTransforms[1].position - slotTransforms[0].position;
                _slotSpacing  = delta.magnitude;
                _extraSlotDir = delta.normalized;
            }

            foreach (var t in slotTransforms)
            {
                _slotPositions.Add(t.position);
                _occupied.Add(null);

                GameObject ind = null;
                if (t.childCount > 0)
                {
                    ind = t.GetChild(0).gameObject;
                    ind.SetActive(true);
                }
                else
                {
                    ind = slotIndicatorPrefab != null
                        ? Instantiate(slotIndicatorPrefab, t.position, Quaternion.identity, t)
                        : CreateDefaultIndicator(t);
                }
                _indicators.Add(ind);
            }

            _maxSlots = _slotPositions.Count;
        }

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

        /// <summary>ExtraSlot booster — appends one more slot at the end of the row.</summary>
        public void AddExtraSlot()
        {
            Vector3 newPos = _slotPositions.Count > 0
                ? _slotPositions[_slotPositions.Count - 1] + _extraSlotDir * _slotSpacing
                : transform.position;

            _slotPositions.Add(newPos);
            _occupied.Add(null);
            _maxSlots++;

            GameObject ind = slotIndicatorPrefab != null
                ? Instantiate(slotIndicatorPrefab, newPos, Quaternion.identity, transform)
                : CreateDefaultIndicator(transform);
            if (slotIndicatorPrefab == null)
            {
                ind.transform.position = newPos;
            }
            Vector3 originalScale = ind.transform.localScale;
            ind.transform.localScale = Vector3.zero;
            ind.transform.DOScale(originalScale, 0.4f).SetEase(Ease.OutBack);
            _indicators.Add(ind);
        }

        public List<ShooterBlock> GetSlottedBlocks()
        {
            var result = new List<ShooterBlock>();
            foreach (var b in _occupied)
                if (b != null) result.Add(b);
            return result;
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void SetIndicatorVisible(int idx, bool visible)
        {
            if (idx >= 0 && idx < _indicators.Count && _indicators[idx] != null)
                _indicators[idx].SetActive(visible);
        }

        private GameObject CreateDefaultIndicator(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "SlotIndicator";
            go.transform.SetParent(parent, false);
            Destroy(go.GetComponent<Collider>());

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
