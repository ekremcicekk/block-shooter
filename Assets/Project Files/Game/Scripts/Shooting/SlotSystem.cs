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
        private readonly List<Transform>    _slotTransforms = new();

        private float   _slotSpacing = 1.2f;
        private Vector3 _extraSlotDir = Vector3.right;

        public int  MaxSlots     => _maxSlots;
        public int  InitialSlotsCount { get; private set; }
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
            _slotTransforms.Clear();

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
                // Force slot Y coordinate to 0.3f
                Vector3 pos = t.position;
                pos.y = 0.3f;
                t.position = pos;

                _slotPositions.Add(pos);
                _occupied.Add(null);
                _slotTransforms.Add(t);

                GameObject ind = null;
                if (t.childCount > 0)
                {
                    ind = t.GetChild(0).gameObject;
                    ind.SetActive(true);
                }
                else
                {
                    ind = slotIndicatorPrefab != null
                        ? Instantiate(slotIndicatorPrefab, pos, Quaternion.identity, t)
                        : CreateDefaultIndicator(t);
                }
                _indicators.Add(ind);
            }

            _maxSlots = _slotPositions.Count;
            InitialSlotsCount = _maxSlots;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Moves block to the next empty slot. Returns false if none available.</summary>
        public bool TrySlotBlock(ShooterBlock block)
        {
            int idx = EmptySlotIndex();
            if (idx < 0) return false;

            _occupied[idx] = block;
            // SlotIndicator remains active

            Vector3 targetPos = _slotPositions[idx];
            targetPos.y += 0.05f; // Offset Y slightly to sit on top of the SlotIndicator

            block.transform.DOJump(targetPos, jumpPower: 0.8f, numJumps: 1, duration: moveToSlotDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => block.OnArrivedInSlot());

            block.transform.DOScale(Vector3.one, moveToSlotDuration)
                .SetEase(Ease.OutQuad);

            return true;
        }

        /// <summary>Called when a slotted block depletes.</summary>
        public void ReleaseSlot(ShooterBlock block)
        {
            for (int i = 0; i < _occupied.Count; i++)
            {
                if (_occupied[i] != block) continue;
                _occupied[i] = null;
                // SlotIndicator is already active
                return;
            }
        }

        /// <summary>ExtraSlot booster — appends one more slot and dynamically re-centers all slots with a smooth animation.</summary>
        public void AddExtraSlot()
        {
            if (_maxSlots >= 5) return;

            int count = _slotTransforms.Count + 1;
            float tw = (count - 1) * _slotSpacing;

            // 1. Re-center and animate existing slots
            for (int i = 0; i < _slotTransforms.Count; i++)
            {
                Transform t = _slotTransforms[i];
                Vector3 targetLocalPos = new Vector3(-tw * 0.5f + i * _slotSpacing, t.localPosition.y, 0f);
                
                // Animate slot transform
                t.DOLocalMove(targetLocalPos, 0.4f).SetEase(Ease.OutQuad);

                // Update slot position tracker (world position)
                Vector3 newWorldPos = t.parent.TransformPoint(targetLocalPos);
                newWorldPos.y = 0.3f;
                _slotPositions[i] = newWorldPos;

                // Animate occupied shooter block if present
                if (_occupied[i] != null)
                {
                    Vector3 blockTargetPos = newWorldPos;
                    blockTargetPos.y += 0.05f; // offset Y slightly to sit on indicator
                    _occupied[i].transform.DOMove(blockTargetPos, 0.4f).SetEase(Ease.OutQuad);
                }
            }

            // 2. Create the new slot
            var slotGo = new GameObject($"Slot_{count - 1}");
            slotGo.transform.SetParent(transform, false);
            
            // Position new slot at its centered position, preserving local Y height
            float defaultLocalY = _slotTransforms.Count > 0 ? _slotTransforms[0].localPosition.y : 0.3f;
            Vector3 newSlotLocalPos = new Vector3(-tw * 0.5f + (count - 1) * _slotSpacing, defaultLocalY, 0f);
            slotGo.transform.localPosition = newSlotLocalPos;
            
            _slotTransforms.Add(slotGo.transform);

            Vector3 newWorldPosNewSlot = transform.TransformPoint(newSlotLocalPos);
            newWorldPosNewSlot.y = 0.3f;
            _slotPositions.Add(newWorldPosNewSlot);
            _occupied.Add(null);
            _maxSlots++;

            // 3. Instantiate and animate new slot indicator
            GameObject ind = slotIndicatorPrefab != null
                ? Instantiate(slotIndicatorPrefab, slotGo.transform)
                : CreateDefaultIndicator(slotGo.transform);
            
            ind.name = "SlotIndicator";
            ind.transform.localPosition = Vector3.zero;
            ind.transform.localRotation = Quaternion.identity;

            // Find and play ExtraSlotExplosion particle system inside the new indicator
            var explosion = ind.transform.Find("ExtraSlotExplosion")?.GetComponent<ParticleSystem>();
            if (explosion == null)
            {
                foreach (var ps in ind.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (ps.name == "ExtraSlotExplosion")
                    {
                        explosion = ps;
                        break;
                    }
                }
            }
            if (explosion != null)
            {
                explosion.gameObject.SetActive(true);
                explosion.Play();
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
