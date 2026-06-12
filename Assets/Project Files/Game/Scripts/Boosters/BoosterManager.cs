using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace BlockShooter
{
    /// <summary>
    /// Manages the three boosters. No child objects needed — attach to [Managers].
    ///
    /// ExtraSlot  — adds one more firing slot for the rest of the level.
    /// SuperShooter — enters selection mode; player taps a slotted block;
    ///              that block floats, zooms camera, and fires at matching-color blocks.
    /// MoveShooter — enters selection mode; player picks any block on the grid and sends it directly to an empty slot.
    /// </summary>
    public class BoosterManager : MonoBehaviour
    {
        public static BoosterManager Instance { get; private set; }

        [Header("Initial unlock reward")]
        public int initialBoosterCount = 2;

        // SuperShooter awaits a tap on a slotted block
        public bool IsAwaitingSuperShooterTarget { get; private set; }
        // MoveShooter awaits a tap on any grid block
        public bool IsAwaitingMoveShooterTarget { get; private set; }

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start() => CheckUnlocks();

        // ── Public API ────────────────────────────────────────────────────────

        public bool IsBoosterUnlocked(BoosterType type)
        {
            if (GameManager.Instance == null || GameManager.Instance.config == null) return true;
            var config = GameManager.Instance.config;

            int unlockLevel = type switch
            {
                BoosterType.ExtraSlot    => config.extraSlotUnlockLevel,
                BoosterType.SuperShooter => config.superShooterUnlockLevel,
                BoosterType.MoveShooter  => config.moveShooterUnlockLevel,
                _ => 999
            };
            return SaveManager.CurrentLevel >= unlockLevel;
        }

        public bool ActivateBooster(BoosterType type)
        {
            if (!IsBoosterUnlocked(type)) return false;

            if (type == BoosterType.SuperShooter && IsAwaitingSuperShooterTarget)
            {
                CancelSuperShooterSelection();
                return true;
            }
            if (type == BoosterType.MoveShooter && IsAwaitingMoveShooterTarget)
            {
                CancelMoveShooterSelection();
                return true;
            }

            // Usability validation before consuming booster
            if (type == BoosterType.ExtraSlot)
            {
                if (SlotSystem.Instance == null || SlotSystem.Instance.MaxSlots >= 5) return false;
            }
            else if (type == BoosterType.SuperShooter)
            {
                if (SlotSystem.Instance == null) return false;
                bool hasNonShootingBlock = false;

                var mainColors = ConveyorController.Instance != null 
                    ? ConveyorController.Instance.GetLiveColorSet() 
                    : new System.Collections.Generic.HashSet<BlockColorType>();

                foreach (var b in SlotSystem.Instance.GetSlottedBlocks())
                {
                    if (b != null && !b.IsShooting && mainColors.Contains(b.ColorType))
                    {
                        hasNonShootingBlock = true;
                        break;
                    }
                }
                if (!hasNonShootingBlock) return false;
            }
            else if (type == BoosterType.MoveShooter)
            {
                if (SlotSystem.Instance == null || !SlotSystem.Instance.HasEmptySlot) return false;
                if (ShooterGrid.Instance == null || !ShooterGrid.Instance.HasLockedBlocks()) return false;
            }

            // Only consume ExtraSlot immediately. Others are consumed only on successful choice.
            if (type == BoosterType.ExtraSlot)
            {
                if (!SaveManager.UseBooster(type)) return false;
            }
            else
            {
                if (SaveManager.GetBoosterCount(type) <= 0) return false;
            }

            switch (type)
            {
                case BoosterType.ExtraSlot:  ActivateExtraSlot();  break;
                case BoosterType.SuperShooter: ActivateSuperShooter(); break;
                case BoosterType.MoveShooter: ActivateMoveShooter(); break;
            }
            return true;
        }

        // ── ExtraSlot ─────────────────────────────────────────────────────────
        // Adds one more firing slot. Permanent for the level.

        private void ActivateExtraSlot()
        {
            SlotSystem.Instance?.AddExtraSlot();
            Camera.main?.DOShakePosition(0.15f, 0.08f, 5, 90);
        }


        // ── SuperShooter ──────────────────────────────────────────────────────
        // Enters selection mode. Player must tap a slotted block.
        // That block floats into the air, zooms the camera, and shoots matching conveyor blocks.

        private void ActivateSuperShooter()
        {
            if (SlotSystem.Instance == null) return;
            var slotted = SlotSystem.Instance.GetSlottedBlocks();
            
            var mainColors = ConveyorController.Instance != null 
                ? ConveyorController.Instance.GetLiveColorSet() 
                : new System.Collections.Generic.HashSet<BlockColorType>();

            // Filter candidates: only slotted blocks that are NOT currently shooting and have matching colors on main conveyor
            var candidates = new System.Collections.Generic.List<ShooterBlock>();
            foreach (var b in slotted)
            {
                bool isTutorialTarget = false;
                if (global::BlockShooter.TutorialManager.Instance != null && global::BlockShooter.TutorialManager.Instance.IsRunning)
                {
                    var activeTarget = global::BlockShooter.TutorialManager.Instance.ActiveTarget;
                    if (activeTarget != null)
                    {
                        if (activeTarget.Matches(b.transform))
                        {
                            isTutorialTarget = true;
                        }
                        else
                        {
                            var slotTransform = SlotSystem.Instance.GetSlotTransform(b);
                            if (slotTransform != null && activeTarget.Matches(slotTransform))
                            {
                                isTutorialTarget = true;
                            }
                        }

                        if (!isTutorialTarget)
                        {
                            var bTargets = b.GetComponentsInChildren<TutorialTarget>();
                            foreach (var t in bTargets)
                            {
                                if (t != null && string.Equals(t.TargetId, activeTarget.TargetId, System.StringComparison.OrdinalIgnoreCase))
                                {
                                    isTutorialTarget = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (b != null && !b.IsShooting && (isTutorialTarget || mainColors.Contains(b.ColorType)))
                {
                    candidates.Add(b);
                }
            }

            if (candidates.Count == 0) return;

            IsAwaitingSuperShooterTarget = true;

            // Highlight ONLY the eligible candidate blocks so player knows to tap one
            foreach (var b in candidates)
                b.transform.DOPunchScale(Vector3.one * 0.2f, 0.4f, 4, 0.5f);

            StartCoroutine(WaitForSuperShooterTarget(candidates));
        }

        private IEnumerator WaitForSuperShooterTarget(System.Collections.Generic.List<ShooterBlock> candidates)
        {
            float timeout = 8f;
            float elapsed = 0f;

            while (elapsed < timeout && IsAwaitingSuperShooterTarget)
            {
                elapsed += Time.deltaTime;

                if (Input.GetMouseButtonDown(0))
                {
                    var hit = RaycastBlock();
                    if (hit != null)
                    {
                        // Validate click with tutorial manager if running
                        if (global::BlockShooter.TutorialManager.Instance != null && global::BlockShooter.TutorialManager.Instance.IsRunning)
                        {
                            TutorialTarget matchedTarget = null;
                            var activeTarget = global::BlockShooter.TutorialManager.Instance.ActiveTarget;
                            if (activeTarget != null)
                            {
                                var hitTargets = hit.GetComponentsInChildren<TutorialTarget>();
                                foreach (var t in hitTargets)
                                {
                                    if (t != null && string.Equals(t.TargetId, activeTarget.TargetId, System.StringComparison.OrdinalIgnoreCase))
                                    {
                                        matchedTarget = t;
                                        break;
                                    }
                                }
                                if (matchedTarget == null)
                                {
                                    var hitParentTargets = hit.GetComponentsInParent<TutorialTarget>();
                                    foreach (var t in hitParentTargets)
                                    {
                                        if (t != null && string.Equals(t.TargetId, activeTarget.TargetId, System.StringComparison.OrdinalIgnoreCase))
                                        {
                                            matchedTarget = t;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (global::BlockShooter.TutorialManager.Instance.TryHandleTargetClick(matchedTarget, hit.transform))
                            {
                                // Wrong target: block click and continue waiting
                                continue;
                            }
                        }

                        if (candidates.Contains(hit))
                        {
                            IsAwaitingSuperShooterTarget = false;
                            SaveManager.UseBooster(BoosterType.SuperShooter); // Consume count here!
                            ResetSuperShooterCandidatesScale(candidates);
                            hit.StartSuperShooter();

                            var area = FindFirstObjectByType<BoosterAreaUI>();
                            if (area != null) area.RefreshUI();
                            yield break;
                        }
                        else if (hit.State == ShooterBlock.BlockState.InGrid)
                        {
                            // Tapped a block still in the grid -> cancel selection, no refund needed
                            IsAwaitingSuperShooterTarget = false;
                            ResetSuperShooterCandidatesScale(candidates);

                            var area = FindFirstObjectByType<BoosterAreaUI>();
                            if (area != null) area.RefreshUI();
                            yield break;
                        }
                    }
                }
                yield return null;
            }

            if (IsAwaitingSuperShooterTarget)
            {
                IsAwaitingSuperShooterTarget = false;
            }
            ResetSuperShooterCandidatesScale(candidates);

            var areaUi = FindFirstObjectByType<BoosterAreaUI>();
            if (areaUi != null) areaUi.RefreshUI();
        }

        private void ResetSuperShooterCandidatesScale(System.Collections.Generic.List<ShooterBlock> candidates)
        {
            foreach (var b in candidates)
            {
                if (b != null)
                {
                    b.transform.DOKill(true);
                    b.transform.localScale = Vector3.one;
                }
            }
        }

        public void CancelSuperShooterSelection()
        {
            if (!IsAwaitingSuperShooterTarget) return;
            IsAwaitingSuperShooterTarget = false;
            // No refund needed since it was not consumed yet!
            if (SlotSystem.Instance != null)
            {
                ResetSuperShooterCandidatesScale(SlotSystem.Instance.GetSlottedBlocks());
            }

            var area = FindFirstObjectByType<BoosterAreaUI>();
            if (area != null) area.RefreshUI();
        }

        // ── MoveShooter ───────────────────────────────────────────────────────
        // Enters selection mode. Player can pick any block on the grid
        // (including locked blocks) and send it directly to an empty slot.

        private void ActivateMoveShooter()
        {
            if (ShooterGrid.Instance == null || SlotSystem.Instance == null) return;
            if (!SlotSystem.Instance.HasEmptySlot) return;

            // Only activate if there is at least one locked/blocked block on the grid
            if (!ShooterGrid.Instance.HasLockedBlocks()) return;

            IsAwaitingMoveShooterTarget = true;

            // Manage indicators and hover bobbing based on true accessibility
            foreach (var b in ShooterGrid.Instance.GetActiveBlocks())
            {
                if (b.State == ShooterBlock.BlockState.InGrid)
                {
                    if (!b.IsAccessible)
                    {
                        // Enable selection indicator and start hover for normally locked blocks
                        if (b.accessibleIndicator != null) b.accessibleIndicator.SetActive(true);
                        b.StartMoveShooterHover();
                    }
                    else
                    {
                        // Hide selection indicator for accessible blocks (they remain open visually, but no indicator)
                        if (b.accessibleIndicator != null) b.accessibleIndicator.SetActive(false);
                    }
                }
            }
        }

        private void ResetMoveShooterCandidatesScale()
        {
            if (ShooterGrid.Instance == null) return;

            foreach (var b in ShooterGrid.Instance.GetActiveBlocks())
            {
                if (b != null && b.State == ShooterBlock.BlockState.InGrid)
                {
                    b.transform.DOKill(true);
                    
                    if (b.IsHovering)
                    {
                        b.StopMoveShooterHover(shouldRebindToLocked: true);
                    }

                    // Reset indicator to match the true accessibility state
                    if (b.accessibleIndicator != null)
                    {
                        b.accessibleIndicator.SetActive(b.IsAccessible);
                    }
                }
            }
        }

        public void CancelMoveShooterSelection()
        {
            if (!IsAwaitingMoveShooterTarget) return;
            IsAwaitingMoveShooterTarget = false;
            // No refund needed since it was not consumed yet!
            ResetMoveShooterCandidatesScale();

            var area = FindFirstObjectByType<BoosterAreaUI>();
            if (area != null) area.RefreshUI();
        }

        public void CompleteMoveShooter(ShooterBlock block)
        {
            IsAwaitingMoveShooterTarget = false;
            SaveManager.UseBooster(BoosterType.MoveShooter); // Consume count here!
            ResetMoveShooterCandidatesScale();

            var area = FindFirstObjectByType<BoosterAreaUI>();
            if (area != null) area.RefreshUI();
        }

        private ShooterBlock RaycastBlock()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                return hit.collider.GetComponentInParent<ShooterBlock>();
            return null;
        }

        // ── Unlock rewards ────────────────────────────────────────────────────

        private void CheckUnlocks()
        {
            if (GameManager.Instance == null || GameManager.Instance.config == null) return;
            var config = GameManager.Instance.config;

            int level = SaveManager.CurrentLevel;
            TryGiveInitial(BoosterType.ExtraSlot,    config.extraSlotUnlockLevel,  level);
            TryGiveInitial(BoosterType.SuperShooter, config.superShooterUnlockLevel, level);
            TryGiveInitial(BoosterType.MoveShooter,  config.moveShooterUnlockLevel, level);
        }

        private void TryGiveInitial(BoosterType type, int unlockLevel, int currentLevel)
        {
            string key = $"BoosterSeen_{type}";
            if (currentLevel >= unlockLevel && PlayerPrefs.GetInt(key, 0) == 0)
            {
                SaveManager.AddBooster(type, initialBoosterCount);
                PlayerPrefs.SetInt(key, 1);
                PlayerPrefs.Save();
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Give All Boosters (Debug)")]
        private void GiveAllBoosters()
        {
            SaveManager.AddBooster(BoosterType.ExtraSlot, 3);
            SaveManager.AddBooster(BoosterType.SuperShooter, 3);
            SaveManager.AddBooster(BoosterType.MoveShooter, 3);
        }
#endif
    }
}
