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
        private BlockColorType _colorType;
        private int   _shotCount;
        private bool  _isRainbowMode;

        public enum BlockState { InGrid, MovingToSlot, InSlot, Depleted }
        public BlockState State { get; private set; } = BlockState.InGrid;

        private bool _isAccessible;
        private bool _isShooting;
        private Coroutine _shootCoroutine;
        private Queue<ConveyorBlock3D> _targetQueue = new();

        private static readonly int ColorProp    = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionProp = Shader.PropertyToID("_EmissionColor");

        // ── Properties ────────────────────────────────────────────────────────
        public BlockColorType ColorType  => _colorType;
        public bool IsDepleted           => State == BlockState.Depleted;
        public bool IsInSlot             => State == BlockState.InSlot;
        public int  GridColumn           { get; private set; }
        public int  GridRow              { get; private set; }

        // Events
        public event Action<ShooterBlock> OnSlotted;
        public event Action<ShooterBlock> OnDepleted;

        // ── Init ──────────────────────────────────────────────────────────────

        public void Initialize(BlockColorType colorType, int shotCount, int col, int row)
        {
            _colorType = colorType;
            _shotCount = shotCount;
            GridColumn = col;
            GridRow    = row;
            State      = BlockState.InGrid;
            _isAccessible = false;

            ApplyColor();
            UpdateShotCountUI();
            SetAccessible(false);
        }

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
            BuildTargetQueue();
            if (_targetQueue.Count > 0) StartShooting();
        }

        // Builds queue from blocks CURRENTLY in FireRange, sorted by arrival order (highest RowIndex first).
        private void BuildTargetQueue()
        {
            _targetQueue.Clear();
            if (FireRange.Instance == null) return;

            var sorted = new List<ConveyorBlock3D>();
            foreach (var b in FireRange.Instance.BlocksInRange)
            {
                if (b == null || b.IsDestroyed || !b.gameObject.activeSelf) continue;
                if (!_isRainbowMode && b.ColorType != _colorType) continue;
                sorted.Add(b);
            }
            sorted.Sort((a, b) =>
            {
                int cmp = b.RowIndex.CompareTo(a.RowIndex);
                return cmp != 0 ? cmp : a.LaneIndex.CompareTo(b.LaneIndex);
            });
            foreach (var b in sorted) _targetQueue.Enqueue(b);
        }

        // Called whenever a new block enters FireRange — adds it to the shoot queue.
        private void OnFireRangeBlockEntered(ConveyorBlock3D block)
        {
            if (State != BlockState.InSlot || IsDepleted) return;
            if (!_isRainbowMode && block.ColorType != _colorType) return;
            if (block == null || block.IsDestroyed) return;
            _targetQueue.Enqueue(block);
            if (!_isShooting) StartShooting();
        }

        // ── Shooting (only active while InSlot) ───────────────────────────────

        private void Update()
        {
            if (!GameManager.Instance.IsPlaying) return;
            if (State != BlockState.InSlot || IsDepleted) return;
            // Safety net: restart if queue has items but shooting stopped (e.g. rainbow toggle)
            if (_targetQueue.Count > 0 && !_isShooting) StartShooting();
        }

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
            while (!IsDepleted)
            {
                // Skip dead/inactive blocks at front of queue
                while (_targetQueue.Count > 0)
                {
                    var front = _targetQueue.Peek();
                    if (front == null || front.IsDestroyed || !front.gameObject.activeSelf)
                        _targetQueue.Dequeue();
                    else break;
                }

                if (_targetQueue.Count == 0)
                    break; // OnFireRangeBlockEntered will restart when next block enters

                ConveyorBlock3D target = _targetQueue.Dequeue();
                FireAt(target);

                yield return new WaitForSeconds(GameManager.Instance.config.fireRate);
            }

            _isShooting = false;
            _shootCoroutine = null;
        }

        private void FireAt(ConveyorBlock3D target)
        {
            if (ProjectilePool.Instance == null || target == null) return;

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

            _targetQueue.Clear();

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
            if (!active) ApplyColor();
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
            Color c = GameManager.Instance.config.GetColor(_colorType);
            if (blockRenderer != null)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(ColorProp, c);
                blockRenderer.SetPropertyBlock(mpb);
            }
            if (glowRenderer != null)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(ColorProp, new Color(c.r, c.g, c.b, 0.4f));
                mpb.SetColor(EmissionProp, c * 0.6f);
                glowRenderer.SetPropertyBlock(mpb);
            }
        }

        private BlockColorType GetAnyActiveColor()
        {
            if (_targetQueue.Count > 0) return _targetQueue.Peek().ColorType;
            if (FireRange.Instance != null)
                foreach (var b in FireRange.Instance.BlocksInRange)
                    return b.ColorType;
            return _colorType;
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
