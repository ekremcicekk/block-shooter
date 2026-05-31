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
            TryStartGroupRoutine();
        }

        // ── Shooting (only active while InSlot) ───────────────────────────────

        private void Update()
        {
            if (!GameManager.Instance.IsPlaying) return;
            if (State != BlockState.InSlot || IsDepleted) return;
            if (!_isShooting) TryStartGroupRoutine();
        }

        private void TryStartGroupRoutine()
        {
            if (_isShooting || IsDepleted) return;
            var group = FindMatchingGroup();
            if (group == null) return;
            _isShooting = true;
            _shootCoroutine = StartCoroutine(ShootGroupRoutine(group));
        }

        private BlockGroup FindMatchingGroup()
        {
            if (ConveyorController.Instance == null) return null;
            foreach (var bg in ConveyorController.Instance.GetComponentsInChildren<BlockGroup>(true))
            {
                if (bg.IsEmpty) continue;
                if (!_isRainbowMode && bg.colorType != _colorType) continue;
                return bg;
            }
            return null;
        }

        private void StopShooting()
        {
            _isShooting = false;
            if (_shootCoroutine != null) { StopCoroutine(_shootCoroutine); _shootCoroutine = null; }
        }

        // Fires at every block in the group, row-by-row (Row_0 first), lane-by-lane
        // (highest lane index first for the wave direction). Waits for each block to
        // enter FireRange before firing — the natural conveyor speed creates the inter-row gap.
        private IEnumerator ShootGroupRoutine(BlockGroup group)
        {
            const float laneDelay    = 0.04f;
            const float blockTimeout = 8f;

            // If the shooter arrived late (some rows already exited FireRange),
            // skip ahead to the earliest row that is currently inside FireRange.
            int startRow = FindStartRow(group);

            for (int row = startRow; row < group.RowCount && !IsDepleted; row++)
            {
                bool firedInRow = false;

                // Lane_N-1 → Lane_0 so the highest-index lane is destroyed first.
                for (int lane = group.LaneCount - 1; lane >= 0 && !IsDepleted; lane--)
                {
                    var block = group.GetBlock(row, lane);
                    if (block == null || block.IsDestroyed) continue;

                    // Wait until the block enters FireRange (or times out / is destroyed).
                    float waited = 0f;
                    while (waited < blockTimeout && !block.IsDestroyed)
                    {
                        if (FireRange.Instance != null && FireRange.Instance.ContainsBlock(block)) break;
                        // A later row from this group entered range → this row already passed.
                        if (HasLaterRowInRange(group, row)) break;
                        yield return null;
                        waited += Time.deltaTime;
                    }

                    if (block.IsDestroyed || block.IsTargeted) continue;
                    if (FireRange.Instance == null || !FireRange.Instance.ContainsBlock(block)) continue;

                    if (firedInRow)
                        yield return new WaitForSeconds(laneDelay);

                    firedInRow = true;
                    FireAt(block);
                }
                // No explicit rowDelay — the physical gap between rows on the conveyor
                // naturally produces the inter-row pause as the next row enters FireRange.
            }

            _isShooting = false;
            _shootCoroutine = null;

            if (!IsDepleted)
                TryStartGroupRoutine();
        }

        // Returns the smallest RowIndex of this group's blocks that are currently in FireRange.
        // Falls back to 0 if none are in range yet (group hasn't arrived).
        private int FindStartRow(BlockGroup group)
        {
            if (FireRange.Instance == null) return 0;
            int minRow = int.MaxValue;
            foreach (var b in FireRange.Instance.BlocksInRange)
            {
                if (b == null || b.IsDestroyed) continue;
                if (b.transform.IsChildOf(group.transform))
                    minRow = Mathf.Min(minRow, b.RowIndex);
            }
            return minRow < int.MaxValue ? minRow : 0;
        }

        // Returns true when a row with index > currentRow from this group is already in FireRange,
        // meaning currentRow has already passed through and should be skipped.
        private bool HasLaterRowInRange(BlockGroup group, int currentRow)
        {
            if (FireRange.Instance == null) return false;
            foreach (var b in FireRange.Instance.BlocksInRange)
            {
                if (b == null || b.IsDestroyed) continue;
                if (b.RowIndex > currentRow && b.transform.IsChildOf(group.transform))
                    return true;
            }
            return false;
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
            DOTween.Kill(transform);
            StopShooting();
        }
    }
}
