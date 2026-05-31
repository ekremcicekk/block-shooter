using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

namespace BlockShooter
{
    /// <summary>
    /// A single shooter block in the grid or in a firing slot.
    ///
    /// Lifecycle:
    ///   InGrid (not shooting) → player taps → MovingToSlot → InSlot (auto-shoots) → Depleted
    ///
    /// Accessibility:
    ///   Controlled by ShooterGrid. A block is accessible when no un-slotted,
    ///   non-depleted block exists in front of it in the same column.
    ///   FreePick booster temporarily makes ALL blocks accessible.
    /// </summary>
    public class ShooterBlock : MonoBehaviour
    {
        // ── Visuals ────────────────────────────────────────────────────────────
        [Header("Visuals")]
        public MeshRenderer blockRenderer;
        public MeshRenderer glowRenderer;
        public TextMeshPro  shotCountText;
        public ParticleSystem muzzleFlash;
        public ParticleSystem depletedParticle;
        public GameObject accessibleIndicator;   // optional highlight ring shown when selectable

        [Header("Shoot Point")]
        [Tooltip("The body mesh transform that rotates to face the target before firing")]
        public Transform bodyMesh;
        public Transform shootPoint;

        // ── State ──────────────────────────────────────────────────────────────
        // Serialized so the Level Editor can bake color/shots/position into the prefab.
        [SerializeField] private BlockColorType _colorType;
        [SerializeField] private int   _shotCount = 100;
        [SerializeField] private int   _gridColumn;
        [SerializeField] private int   _gridRow;
        private bool  _isRainbowMode;

        public enum BlockState { InGrid, MovingToSlot, InSlot, Depleted }
        public BlockState State { get; private set; } = BlockState.InGrid;

        private bool    _isAccessible;
        private bool    _isShooting;
        private Coroutine _shootCoroutine;

        private static readonly int ColorProp    = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionProp = Shader.PropertyToID("_EmissionColor");

        // ── Properties ────────────────────────────────────────────────────────
        public BlockColorType ColorType  => _colorType;
        public bool IsDepleted           => State == BlockState.Depleted;
        public bool IsInSlot             => State == BlockState.InSlot;
        public int  GridColumn           => _gridColumn;
        public int  GridRow              => _gridRow;

        // Events
        public event Action<ShooterBlock> OnSlotted;
        public event Action<ShooterBlock> OnDepleted;

        // ── Init ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Runtime initialization for pre-placed blocks. Reads serialized fields set by the Level Editor.
        /// </summary>
        public void Initialize()
        {
            State         = BlockState.InGrid;
            _isAccessible = false;
            ApplyColor();
            UpdateShotCountUI();
            SetAccessible(false);
        }

        /// <summary>
        /// Full initialization used when spawning blocks dynamically at runtime (e.g. from BlockDoor).
        /// </summary>
        public void Initialize(BlockColorType colorType, int shotCount, int col, int row)
        {
            _colorType  = colorType;
            _shotCount  = shotCount;
            _gridColumn = col;
            _gridRow    = row;
            Initialize();
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: bakes values into serialized fields and applies the material so the prefab shows the correct color.</summary>
        public void EditorSetup(BlockColorType colorType, int shotCount, int col, int row)
        {
            _colorType  = colorType;
            _shotCount  = shotCount;
            _gridColumn = col;
            _gridRow    = row;
            EditorApplyMaterial();
        }

        private void EditorApplyMaterial()
        {
            if (blockRenderer == null) return;
            var guids = UnityEditor.AssetDatabase.FindAssets("t:GameConfig");
            if (guids.Length == 0) return;
            var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
            var mat = cfg?.GetMaterial(_colorType);
            if (mat != null) blockRenderer.sharedMaterial = mat;
        }
#endif

        // ── Tap handling ──────────────────────────────────────────────────────

        private void OnMouseDown()
        {
            if (!GameManager.Instance.IsPlaying) return;
            if (State != BlockState.InGrid) return;

            // ColorBlast selection mode: pick a slotted block
            if (BoosterManager.Instance != null && BoosterManager.Instance.IsAwaitingColorBlastTarget)
            {
                // Only slotted blocks can be selected for ColorBlast
                // (InGrid blocks shouldn't be selectable in this mode)
                return;
            }

            if (!_isAccessible)
            {
                transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 3, 0.5f);
                return;
            }

            if (SlotSystem.Instance == null || !SlotSystem.Instance.HasEmptySlot)
            {
                transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 3, 0.5f);
                return;
            }

            MoveToSlot();
        }

        private void MoveToSlot()
        {
            State = BlockState.MovingToSlot;
            SetAccessible(false);

            ShooterGrid.Instance?.OnBlockLeftGrid(this);
            SlotSystem.Instance.TrySlotBlock(this);
            OnSlotted?.Invoke(this);
        }

        /// <summary>Called by SlotSystem when the block arrives at its slot position.</summary>
        public void OnArrivedInSlot()
        {
            State = BlockState.InSlot;
            if (FireRange.Instance != null)
                FireRange.Instance.OnBlockEntered += OnFireRangeBlockEntered;
            // Start shooting immediately if a target is already in range
            if (HasTarget()) StartShooting();
        }

        // Called whenever a new block enters FireRange
        private void OnFireRangeBlockEntered(ConveyorBlock3D block)
        {
            if (State != BlockState.InSlot || IsDepleted) return;
            if (!_isRainbowMode && block.ColorType != _colorType) return;
            if (!_isShooting) StartShooting();
        }

        // ── Shooting (only active while InSlot) ───────────────────────────────

        private void Update()
        {
            if (!GameManager.Instance.IsPlaying) return;
            if (State != BlockState.InSlot || IsDepleted) return;
            // Safety net: restart if a target is available but coroutine stopped
            if (!_isShooting && HasTarget()) StartShooting();
        }

        private bool HasTarget() => GetVolleyTargets().Count > 0;

        private void StartShooting()
        {
            _isShooting = true;
            _shootCoroutine = StartCoroutine(ShootRoutine());
        }

        private void StopShooting()
        {
            _isShooting = false;
            if (_shootCoroutine != null) { StopCoroutine(_shootCoroutine); _shootCoroutine = null; }
        }

        private IEnumerator ShootRoutine()
        {
            const float laneDelay = 0.04f; // stagger between lanes within a row
            const float rowDelay  = 0.08f; // gap between rows — the Mexican-wave timing

            while (!IsDepleted)
            {
                var targets = GetVolleyTargets();
                if (targets.Count == 0) break;

                int  lastRow   = -1;
                bool firedAny  = false;

                foreach (var t in targets)
                {
                    if (IsDepleted) break;
                    if (t == null || t.IsDestroyed || t.IsTargeted) continue;

                    if (lastRow >= 0)
                    {
                        bool newRow = t.RowIndex != lastRow;
                        yield return new WaitForSeconds(newRow ? rowDelay : laneDelay);
                    }

                    lastRow = t.RowIndex;
                    FireAt(t);
                    firedAny = true;
                }

                if (!firedAny) break;

                yield return new WaitForSeconds(rowDelay);
            }

            _isShooting = false;
            _shootCoroutine = null;
        }

        // Returns matching, un-targeted blocks currently in fire range.
        // Order: Row_0 first (ascending RowIndex), Block_0 first within each row (ascending LaneIndex).
        private List<ConveyorBlock3D> GetVolleyTargets()
        {
            var list = new List<ConveyorBlock3D>();
            if (FireRange.Instance == null) return list;

            foreach (var b in FireRange.Instance.BlocksInRange)
            {
                if (b == null || b.IsDestroyed || b.IsTargeted) continue;
                if (!_isRainbowMode && b.ColorType != _colorType) continue;
                list.Add(b);
            }

            if (list.Count < 2) return list;

            list.Sort((a, b) =>
            {
                int rowCmp = a.RowIndex.CompareTo(b.RowIndex); // Row_0 first
                if (rowCmp != 0) return rowCmp;
                return a.LaneIndex.CompareTo(b.LaneIndex);     // Block_0 first within row
            });

            return list;
        }

        private void FireAt(ConveyorBlock3D target)
        {
            if (ProjectilePool.Instance == null || target == null) return;

            // Claim this block so no other shooter wastes a shot on it
            target.SetTargeted(true);

            BlockColorType projColor = _isRainbowMode ? target.ColorType : _colorType;

            // Rotate body mesh on Y axis only — no pitch/roll
            if (bodyMesh != null)
            {
                Vector3 lookDir = target.transform.position - bodyMesh.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                    bodyMesh.rotation = Quaternion.LookRotation(lookDir.normalized);
            }

            Vector3 spawnPos = shootPoint != null ? shootPoint.position : transform.position + Vector3.up * 0.3f;
            Vector3 dir = (target.transform.position - spawnPos).normalized;

            Projectile proj = ProjectilePool.Instance.Get(spawnPos);
            proj.Launch(projColor, GameManager.Instance.config.projectileSpeed, ProjectilePool.Instance, dir, target);

            if (muzzleFlash != null) muzzleFlash.Play();
            transform.DOKill(false);
            transform.localScale = Vector3.one;
            transform.DOPunchScale(Vector3.one * 0.08f, 0.1f, 1, 0.5f);

            _shotCount--;
            UpdateShotCountUI();
            if (_shotCount <= 0) Deplete();
        }

        // kept for external callers (e.g. ColorBlast)
        public void FireProjectile()
        {
            var target = _isRainbowMode
                ? FireRange.Instance?.GetFirstTarget()
                : FireRange.Instance?.GetFirstTarget(_colorType);
            if (target != null) FireAt(target);
        }

        // ── ColorBlast: fire at ALL matching blocks simultaneously ─────────────

        public void FireColorBlast()
        {
            if (State != BlockState.InSlot || IsDepleted) return;
            StartCoroutine(ColorBlastRoutine());
        }

        private IEnumerator ColorBlastRoutine()
        {
            StopShooting();

            // Fire one projectile per matching block in range
            var targets = new System.Collections.Generic.List<ConveyorBlock3D>(
                FireRange.Instance != null ? FireRange.Instance.BlocksInRange : System.Array.Empty<ConveyorBlock3D>());

            Vector3 spawnPos = shootPoint != null ? shootPoint.position : transform.position + Vector3.up * 0.3f;

            foreach (var t in targets)
            {
                if (t == null || t.IsDestroyed) continue;
                if (!_isRainbowMode && t.ColorType != _colorType) continue;

                Vector3 dir = (t.transform.position - spawnPos).normalized;
                Projectile proj = ProjectilePool.Instance?.Get(spawnPos);
                proj?.Launch(_colorType, GameManager.Instance.config.projectileSpeed * 1.5f,
                    ProjectilePool.Instance, dir);

                if (muzzleFlash != null) muzzleFlash.Play();
                yield return new WaitForSeconds(0.05f);
            }

            Deplete();
        }

        // ── Accessibility ─────────────────────────────────────────────────────

        public void SetAccessible(bool accessible)
        {
            _isAccessible = accessible;
            if (accessibleIndicator != null)
                accessibleIndicator.SetActive(accessible && State == BlockState.InGrid);
        }

        // ── Deplete ───────────────────────────────────────────────────────────

        private void Deplete()
        {
            State = BlockState.Depleted;
            StopShooting();
            if (FireRange.Instance != null)
                FireRange.Instance.OnBlockEntered -= OnFireRangeBlockEntered;

            if (depletedParticle != null) depletedParticle.Play();

            // Notify systems immediately so slot/grid update right away
            SlotSystem.Instance?.ReleaseSlot(this);
            ShooterGrid.Instance?.OnBlockDepleted(this);
            OnDepleted?.Invoke(this);

            // Animate out then hide — slot indicator reappears via ReleaseSlot above
            transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack)
                .OnComplete(() => gameObject.SetActive(false));
        }

        // ── Rainbow mode ──────────────────────────────────────────────────────

        public void SetRainbowMode(bool active)
        {
            _isRainbowMode = active;
            if (!active)
            {
                ApplyColor();
            }
            else if (blockRenderer != null)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(ColorProp, Color.white);
                blockRenderer.SetPropertyBlock(mpb);
            }
        }

        public void RefillShots(int amount)
        {
            _shotCount += amount;
            if (IsDepleted && _shotCount > 0) { State = BlockState.InGrid; ApplyColor(); }
            UpdateShotCountUI();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ApplyColor()
        {
            var config = GameManager.Instance?.config;
            if (config == null) return;

            var mat = config.GetMaterial(_colorType);
            if (mat != null && blockRenderer != null)
            {
                blockRenderer.sharedMaterial = mat;
                blockRenderer.SetPropertyBlock(null);
            }
            else if (blockRenderer != null)
            {
                Color c = config.GetColor(_colorType);
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(ColorProp, c);
                blockRenderer.SetPropertyBlock(mpb);
            }

            if (glowRenderer != null)
            {
                Color c = config.GetColor(_colorType);
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(ColorProp, new Color(c.r, c.g, c.b, 0.4f));
                mpb.SetColor(EmissionProp, c * 0.6f);
                glowRenderer.SetPropertyBlock(mpb);
            }
        }

        private void UpdateShotCountUI()
        {
            if (shotCountText != null) shotCountText.text = _shotCount.ToString();
        }

        private void OnDisable()
        {
            if (FireRange.Instance != null)
                FireRange.Instance.OnBlockEntered -= OnFireRangeBlockEntered;
            DOTween.Kill(transform);
            StopShooting();
        }
    }
}
