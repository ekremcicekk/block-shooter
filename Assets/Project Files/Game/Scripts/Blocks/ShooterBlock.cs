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
    /// </summary>
    public class ShooterBlock : MonoBehaviour
    {
        // ── Visuals ────────────────────────────────────────────────────────────
        [Header("Visuals")]
        public Renderer blockRenderer;
        public MeshRenderer glowRenderer;
        public TextMeshPro  shotCountText;
        public ParticleSystem muzzleFlash;
        public ParticleSystem depletedParticle;
        public ParticleSystem slotArrivalParticle; // played once when the block arrives at its slot
        public GameObject accessibleIndicator;   // optional highlight ring shown when selectable
        public Animator bodyAnimator;

        [Header("Shoot Point")]
        [Tooltip("The body mesh transform that rotates to face the target before firing")]
        public Transform bodyMesh;
        public Transform shootPoint;

        [Header("Mystery Shooter Settings")]
        [SerializeField] private bool _isMystery;

        // ── State ──────────────────────────────────────────────────────────────
        // Serialized so the Level Editor can bake color/shots/position into the prefab.
        [SerializeField] private BlockColorType _colorType;
        [SerializeField] private int   _shotCount = 100;
        [SerializeField] private int   _gridColumn;
        [SerializeField] private int   _gridRow;

        public enum BlockState { InGrid, MovingToSlot, InSlot, Depleted }
        public BlockState State { get; private set; } = BlockState.InGrid;

        private bool    _isAccessible;
        private bool    _isShooting;
        private bool    _isPerformingSuperShooter;
        private Coroutine _shootCoroutine;
        private int     _visibleShotsLeft;
        private int     _invisibleShotsLeft;
        private float   _recoilX;
        private Tween   _recoilTween;
        private Tween   _scaleTween;
        private Tween   _bodyScaleTween;

        private static readonly int ColorProp    = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionProp = Shader.PropertyToID("_EmissionColor");

        // ── Properties ────────────────────────────────────────────────────────
        public BlockColorType ColorType  => _colorType;
        public bool IsDepleted           => State == BlockState.Depleted;
        public bool IsInSlot             => State == BlockState.InSlot;
        public int  GridColumn           => _gridColumn;
        public int  GridRow              => _gridRow;
        public int  ShotCount            => _shotCount;
        public bool isMystery            { get => _isMystery; set => _isMystery = value; }
        public bool IsAccessible         => _isAccessible;
        public bool IsFrozen             => TryGetComponent<FreezeBlockFeature>(out var f) && f.isFrozen;
        public bool IsShooting           => _isShooting;

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
            _visibleShotsLeft = UnityEngine.Random.Range(1, 3);
            _invisibleShotsLeft = 0;
            UpdateShotCountUI();
            if (shotCountText != null) shotCountText.gameObject.SetActive(false);
            
            // Only apply color if this block is not currently in a mystery state.
            // Mystery block materials are managed by MysteryBlockFeature.
            if (!_isMystery)
            {
                ApplyColor();
            }

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

        /// <summary>
        /// Called by MysteryBlockFeature to reveal the block's true visuals.
        /// </summary>
        public void RevealFromFeature()
        {
            ApplyColor();
            UpdateShotCountUI();
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: bakes values into serialized fields and applies the material so the prefab shows the correct color.</summary>
        public void EditorSetup(BlockColorType colorType, int shotCount, int col, int row, bool isMystery = false)
        {
            _colorType  = colorType;
            _shotCount  = shotCount;
            _gridColumn = col;
            _gridRow    = row;
            _isMystery  = isMystery;

            // Try to sync with MysteryBlockFeature if present on the prefab instance
            var feature = GetComponent<MysteryBlockFeature>();
            if (feature != null)
            {
                if (feature.mysteryVisual != null) feature.mysteryVisual.SetActive(isMystery);
                if (feature.baseVisual != null) feature.baseVisual.SetActive(!isMystery);
            }

            if (!isMystery)
            {
                EditorApplyMaterial();
            }
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

            if (IsFrozen)
            {
                if (bodyAnimator != null)
                {
                    bodyAnimator.SetTrigger("ShooterShake");
                }
                else
                {
                    transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 3, 0.5f);
                }
                return;
            }

            // SuperShooter selection mode: pick a slotted block
            if (BoosterManager.Instance != null && BoosterManager.Instance.IsAwaitingSuperShooterTarget)
            {
                // Only slotted blocks can be selected for SuperShooter
                // (InGrid blocks shouldn't be selectable in this mode)
                return;
            }

            // MoveShooter selection mode: pick any InGrid block
            if (BoosterManager.Instance != null && BoosterManager.Instance.IsAwaitingMoveShooterTarget)
            {
                if (SlotSystem.Instance == null || !SlotSystem.Instance.HasEmptySlot)
                {
                    transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 3, 0.5f);
                    return;
                }

                BoosterManager.Instance.CompleteMoveShooter(this);
                MoveToSlot();
                return;
            }

            if (!_isAccessible)
            {
                if (bodyAnimator != null)
                {
                    bodyAnimator.SetTrigger("ShooterShake");
                }
                else
                {
                    transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 3, 0.5f);
                }
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
            if (shotCountText != null) shotCountText.gameObject.SetActive(true);

            // Immediately reveal mystery state when block starts moving to a slot
            if (_isMystery)
            {
                var feature = GetComponent<MysteryBlockFeature>();
                if (feature != null)
                {
                    feature.Reveal();
                }
            }

            ShooterGrid.Instance?.OnBlockLeftGrid(this);
            SlotSystem.Instance.TrySlotBlock(this);
            OnSlotted?.Invoke(this);
        }

        /// <summary>Called by SlotSystem when the block arrives at its slot position.</summary>
        public void OnArrivedInSlot()
        {
            State = BlockState.InSlot;

            // Play the slot arrival particle effect at the slot's ground position.
            if (slotArrivalParticle != null)
            {
                slotArrivalParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                slotArrivalParticle.Play();
            }

            if (bodyAnimator != null)
            {
                bodyAnimator.SetTrigger("ShooterArrived");
                
                // Disable the animator after 0.5 seconds so the arrival animation can play,
                // and then we can manually rotate bodyMesh without animator conflict.
                DOVirtual.DelayedCall(0.5f, () =>
                {
                    if (State == BlockState.InSlot && !IsDepleted && !_isPerformingSuperShooter)
                    {
                        if (bodyAnimator != null) bodyAnimator.enabled = false;
                    }
                });
            }
            TryStartGroupRoutine();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.CheckFailCondition();
            }
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

            if (bodyAnimator != null && bodyAnimator.enabled)
                bodyAnimator.enabled = false;

            _shootCoroutine = StartCoroutine(ShootGroupRoutine(group));
        }

        private BlockGroup FindMatchingGroup()
        {
            if (FireRange.Instance == null) return null;

            // Find the most urgent block in range that matches our color requirements
            var targetBlock = FireRange.Instance.GetFirstTarget(_colorType);

            if (targetBlock != null && !targetBlock.IsDestroyed)
            {
                return targetBlock.GetComponentInParent<BlockGroup>();
            }

            return null;
        }

        private void StopShooting()
        {
            _isShooting = false;
            if (_shootCoroutine != null) { StopCoroutine(_shootCoroutine); _shootCoroutine = null; }
        }

        // Fires at every block in the group row-by-row (Row_0 first), lane N-1 → 0.
        // Waits at the ROW level: as soon as any block from the row enters FireRange,
        // fires all lanes in order. This eliminates diagonal-entry timing issues
        // (outer lanes that exit slightly before inner lanes are still caught by the
        // homing projectile within the laneDelay window). Rows that looped past wait
        // for natural re-entry rather than being chased across the track.
        private IEnumerator ShootGroupRoutine(BlockGroup group)
        {
            float laneDelay = GameManager.Instance != null && GameManager.Instance.config != null 
                ? GameManager.Instance.config.fireRate 
                : 0.15f;
            const float rowTimeout = 15f;

            int startRow = FindStartRow(group);

            for (int row = startRow; row < group.RowCount && !IsDepleted; row++)
            {
                // If there are no live blocks left in this row, skip it immediately to prevent freezing
                if (!RowHasLiveBlocks(group, row)) continue;

                // If the row has already passed/exited the fire range, skip it
                if (RowIsPastFireRange(group, row)) continue;

                // Wait until at least one live block from this row is in FireRange.
                float waited = 0f;
                while (!RowHasBlockInRange(group, row) && RowHasLiveBlocks(group, row) && !RowIsPastFireRange(group, row) && waited < rowTimeout && !IsDepleted)
                {
                    yield return null;
                    waited += Time.deltaTime;
                }

                if (!RowHasBlockInRange(group, row)) continue;

                // Fire all lanes in the row — highest index first.
                bool firedAny = false;
                for (int lane = group.LaneCount - 1; lane >= 0 && !IsDepleted; lane--)
                {
                    var block = group.GetBlock(row, lane);
                    if (block == null || block.IsDestroyed || block.IsTargeted) continue;

                    if (firedAny)
                        yield return new WaitForSeconds(laneDelay / UIManager.SpeedMultiplier);

                    firedAny = true;
                    FireAt(block);
                }
            }

            _isShooting = false;
            _shootCoroutine = null;

            if (!IsDepleted)
            {
                yield return null;
                TryStartGroupRoutine();
            }
        }

        private bool RowHasBlockInRange(BlockGroup group, int row)
        {
            if (FireRange.Instance == null) return false;
            for (int lane = 0; lane < group.LaneCount; lane++)
            {
                var b = group.GetBlock(row, lane);
                if (b != null && !b.IsDestroyed && FireRange.Instance.ContainsBlock(b))
                    return true;
            }
            return false;
        }

        private bool RowHasLiveBlocks(BlockGroup group, int row)
        {
            for (int lane = 0; lane < group.LaneCount; lane++)
            {
                var b = group.GetBlock(row, lane);
                if (b != null && !b.IsDestroyed)
                    return true;
            }
            return false;
        }

        private bool RowIsPastFireRange(BlockGroup group, int row)
        {
            if (FireRange.Instance == null) return false;

            bool hasLiveBlocks = false;
            for (int lane = 0; lane < group.LaneCount; lane++)
            {
                var b = group.GetBlock(row, lane);
                if (b != null && !b.IsDestroyed)
                {
                    hasLiveBlocks = true;
                    // If any live block in the row has NOT entered yet or is still inside the range,
                    // the row is NOT past the fire range yet.
                    if (!b.HasEnteredFireRange || FireRange.Instance.ContainsBlock(b))
                        return false;
                }
            }
            return hasLiveBlocks;
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


        private void FireAt(ConveyorBlock3D target)
        {
            if (bodyAnimator != null && bodyAnimator.enabled)
                bodyAnimator.enabled = false;

            if (ProjectilePool.Instance == null || target == null) return;

            // Claim this block so no other shooter wastes a shot on it
            target.SetTargeted(true);

            Vector3 targetCenter = target.transform.position + Vector3.up * 0.3f;
            BlockColorType projColor = _colorType;

            // Determine if this shot should be visible based on the organic burst visibility pattern
            bool isProjectileVisible = true;
            if (_visibleShotsLeft > 0)
            {
                isProjectileVisible = true;
                _visibleShotsLeft--;
                if (_visibleShotsLeft <= 0)
                {
                    _invisibleShotsLeft = UnityEngine.Random.Range(1, 3); // 1-2 invisible shots
                }
            }
            else if (_invisibleShotsLeft > 0)
            {
                isProjectileVisible = false;
                _invisibleShotsLeft--;
                if (_invisibleShotsLeft <= 0)
                {
                    _visibleShotsLeft = UnityEngine.Random.Range(1, 4); // 1-3 visible shots
                }
            }

            // Rotate body mesh to face target. In SuperShooter mode, allow pitch/tilt rotation to look downwards in 3D.
            if (bodyMesh != null)
            {
                if (_isPerformingSuperShooter)
                {
                    // SuperShooter mode: allow full 3D LookRotation in local space
                    Vector3 lookDir = targetCenter - bodyMesh.position;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized);
                        float targetX = targetRot.eulerAngles.x;
                        float targetY = targetRot.eulerAngles.y;

                        // Immediately face target incorporating the current _recoilX offset
                        bodyMesh.localRotation = Quaternion.Euler(targetX + _recoilX, targetY, 0f);

                        // Trigger recoil tween if visible and not already playing
                        if (isProjectileVisible && (_recoilTween == null || !_recoilTween.IsActive() || _recoilTween.IsComplete()))
                        {
                            _recoilTween = DOTween.To(() => _recoilX, x => _recoilX = x, -10f, 0.03f)
                                .SetEase(Ease.OutQuad)
                                .OnUpdate(() =>
                                {
                                    if (target != null && bodyMesh != null)
                                    {
                                        Vector3 tCenter = target.transform.position + Vector3.up * 0.3f;
                                        Vector3 curLookDir = tCenter - bodyMesh.position;
                                        if (curLookDir.sqrMagnitude > 0.001f)
                                        {
                                            Quaternion curTargetRot = Quaternion.LookRotation(curLookDir.normalized);
                                            bodyMesh.localRotation = Quaternion.Euler(curTargetRot.eulerAngles.x + _recoilX, curTargetRot.eulerAngles.y, 0f);
                                        }
                                    }
                                })
                                .OnComplete(() =>
                                {
                                    _recoilTween = DOTween.To(() => _recoilX, x => _recoilX = x, 0f, 0.07f)
                                        .SetEase(Ease.InQuad)
                                        .OnUpdate(() =>
                                        {
                                            if (target != null && bodyMesh != null)
                                            {
                                                Vector3 tCenter = target.transform.position + Vector3.up * 0.3f;
                                                Vector3 curLookDir = tCenter - bodyMesh.position;
                                                if (curLookDir.sqrMagnitude > 0.001f)
                                                {
                                                    Quaternion curTargetRot = Quaternion.LookRotation(curLookDir.normalized);
                                                    bodyMesh.localRotation = Quaternion.Euler(curTargetRot.eulerAngles.x + _recoilX, curTargetRot.eulerAngles.y, 0f);
                                                }
                                            }
                                        });
                                });
                        }
                    }
                }
                else
                {
                    // Original working code path for normal slot shooting
                    Vector3 lookDir = target.transform.position - bodyMesh.position;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        float targetY = Quaternion.LookRotation(lookDir.normalized).eulerAngles.y;

                        // Immediately face target incorporating the current _recoilX offset
                        bodyMesh.localRotation = Quaternion.Euler(_recoilX, targetY, 0f);

                        // Trigger recoil tween if visible and not already playing
                        if (isProjectileVisible && (_recoilTween == null || !_recoilTween.IsActive() || _recoilTween.IsComplete()))
                        {
                            _recoilTween = DOTween.To(() => _recoilX, x => _recoilX = x, -10f, 0.03f)
                                .SetEase(Ease.OutQuad)
                                .OnUpdate(() =>
                                {
                                    if (target != null && bodyMesh != null)
                                    {
                                        Vector3 curLookDir = target.transform.position - bodyMesh.position;
                                        curLookDir.y = 0f;
                                        if (curLookDir.sqrMagnitude > 0.001f)
                                        {
                                            float curTargetY = Quaternion.LookRotation(curLookDir.normalized).eulerAngles.y;
                                            bodyMesh.localRotation = Quaternion.Euler(_recoilX, curTargetY, 0f);
                                        }
                                    }
                                })
                                .OnComplete(() =>
                                {
                                    _recoilTween = DOTween.To(() => _recoilX, x => _recoilX = x, 0f, 0.07f)
                                        .SetEase(Ease.InQuad)
                                        .OnUpdate(() =>
                                        {
                                            if (target != null && bodyMesh != null)
                                            {
                                                Vector3 curLookDir = target.transform.position - bodyMesh.position;
                                                curLookDir.y = 0f;
                                                if (curLookDir.sqrMagnitude > 0.001f)
                                                {
                                                    float curTargetY = Quaternion.LookRotation(curLookDir.normalized).eulerAngles.y;
                                                    bodyMesh.localRotation = Quaternion.Euler(_recoilX, curTargetY, 0f);
                                                }
                                            }
                                        });
                                });
                        }
                    }
                }
            }

            Vector3 spawnPos = shootPoint != null ? shootPoint.position : transform.position + Vector3.up * 0.3f;
            targetCenter = target.transform.position + Vector3.up * 0.3f;
            Vector3 dir = (targetCenter - spawnPos).normalized;

            float pSpeed = GameManager.Instance.config.projectileSpeed * UIManager.SpeedMultiplier;
            if (_isPerformingSuperShooter) pSpeed *= 1.8f;

            Projectile proj = ProjectilePool.Instance.Get(spawnPos);
            proj.Launch(projColor, pSpeed, ProjectilePool.Instance, dir, target, isProjectileVisible);

            if (muzzleFlash != null) muzzleFlash.Play();
            
            if (!_isPerformingSuperShooter)
            {
                if (_scaleTween == null || !_scaleTween.IsActive() || _scaleTween.IsComplete())
                {
                    transform.localScale = Vector3.one;
                    _scaleTween = transform.DOPunchScale(Vector3.one * 0.08f, 0.12f, 1, 0.5f);
                }
            }
            else
            {
                if (bodyMesh != null)
                {
                    if (_bodyScaleTween == null || !_bodyScaleTween.IsActive() || _bodyScaleTween.IsComplete())
                    {
                        bodyMesh.localScale = Vector3.one;
                        _bodyScaleTween = bodyMesh.DOPunchScale(Vector3.one * 0.12f, 0.1f, 1, 0.5f);
                    }
                }
            }

            _shotCount--;
            UpdateShotCountUI();
            if (_shotCount <= 0 && !_isPerformingSuperShooter) Deplete();
        }

        // kept for external callers (e.g. SuperShooter)
        public void FireProjectile()
        {
            var target = FireRange.Instance?.GetFirstTarget(_colorType);
            if (target != null) FireAt(target);
        }

        // ── SuperShooter: float, zoom camera, and fire at matching blocks sequentially ──

        public void StartSuperShooter()
        {
            if (State != BlockState.InSlot || IsDepleted) return;
            _isPerformingSuperShooter = true;
            StopShooting();
            
            // Reset root rotation and disable animator so manual rotations on bodyMesh work
            transform.DOKill(false);
            transform.localRotation = Quaternion.identity;
            
            if (_recoilTween != null) _recoilTween.Kill();
            _recoilX = 0f;

            if (_scaleTween != null) _scaleTween.Kill();
            if (_bodyScaleTween != null) _bodyScaleTween.Kill();
            transform.localScale = Vector3.one;
            if (bodyMesh != null) bodyMesh.localScale = Vector3.one;

            if (bodyAnimator != null) bodyAnimator.enabled = false;

            State = BlockState.MovingToSlot; // Prevents normal update shoot cycles
            StartCoroutine(SuperShooterRoutine());
        }

        private IEnumerator SuperShooterRoutine()
        {
            float originalCameraSize = Camera.main != null ? Camera.main.orthographicSize : 10f;

            // 1. Camera Zoom to orthographic size 8
            Camera.main?.DOOrthoSize(8f, 0.35f).SetEase(Ease.OutQuad);

            // 2. Jump up to floating position
            Vector3 originalPos = transform.position;
            Vector3 floatPos = originalPos + Vector3.up * 2.5f + Vector3.back * 1.2f;

            transform.DOScale(Vector3.one * 1.15f, 0.35f).SetEase(Ease.OutQuad);

            yield return transform.DOJump(floatPos, jumpPower: 1.5f, numJumps: 1, duration: 0.4f)
                .SetEase(Ease.OutQuad)
                .WaitForCompletion();

            // Floating bob animation loop
            var hoverTween = transform.DOMoveY(floatPos.y + 0.12f, 0.35f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);

            // 3. Shoot at matching conveyor blocks sequentially up to _shotCount
            var targets = ConveyorController.Instance != null 
                ? ConveyorController.Instance.GetOrderedBlocks(_colorType)
                : new System.Collections.Generic.List<ConveyorBlock3D>();

            targets.RemoveAll(t => t == null || t.IsDestroyed || t.IsTargeted);

            int shotsToFire = Mathf.Min(_shotCount, targets.Count);
            if (shotsToFire > 0)
            {
                for (int i = 0; i < shotsToFire; i++)
                {
                    var target = targets[i];
                    if (target == null || target.IsDestroyed) continue;

                    FireAt(target);
                    yield return new WaitForSeconds(0.02f / UIManager.SpeedMultiplier);
                }

                // Brief delay to let final projectiles hit their targets
                yield return new WaitForSeconds(0.15f);
            }
            else
            {
                yield return new WaitForSeconds(0.3f);
            }

            hoverTween.Kill();

            // 4. Parallel return movement, rotation reset, and camera restore
            Camera.main?.DOOrthoSize(originalCameraSize, 0.35f).SetEase(Ease.OutQuad);
            transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.InQuad);
            if (bodyMesh != null)
            {
                bodyMesh.DOLocalRotate(Vector3.zero, 0.35f).SetEase(Ease.OutQuad);
            }

            yield return transform.DOMove(originalPos, 0.35f)
                .SetEase(Ease.OutQuad)
                .WaitForCompletion();

            // 5. Trigger depletion
            _isPerformingSuperShooter = false;
            Deplete();
        }

        // ── Accessibility ─────────────────────────────────────────────────────

        public void SetAccessible(bool accessible)
        {
            if (_isMystery) accessible = false;
            if (IsFrozen) accessible = false;

            bool changed = (_isAccessible != accessible);
            _isAccessible = accessible;
            if (accessibleIndicator != null)
                accessibleIndicator.SetActive(accessible && State == BlockState.InGrid);

            if (changed && accessible && State == BlockState.InGrid)
            {
                if (bodyAnimator != null)
                {
                    bodyAnimator.SetTrigger("ShooterUnlock");
                }
            }
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

            if (bodyAnimator != null)
            {
                bodyAnimator.enabled = true; // Re-enable animator so it can play the deplete animation
                bodyAnimator.SetTrigger("ShooterDeplete");
                DOVirtual.DelayedCall(0.5f, () => gameObject.SetActive(false));
            }
            else
            {
                // Fallback to standard tween if no animator is present
                transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack)
                    .OnComplete(() => gameObject.SetActive(false));
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
            if (bodyMesh != null) DOTween.Kill(bodyMesh);
            if (_recoilTween != null) _recoilTween.Kill();
            if (_scaleTween != null) _scaleTween.Kill();
            if (_bodyScaleTween != null) _bodyScaleTween.Kill();
            StopShooting();
        }
    }
}
