using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace BlockShooter
{
    /// <summary>
    /// Manages the three boosters. No child objects needed — attach to [Managers].
    ///
    /// ExtraSlot  — adds one more firing slot for the rest of the level.
    /// FreePick   — for `freePickData.duration` seconds, ALL grid blocks become selectable.
    /// SuperShooter — enters selection mode; player taps a slotted block;
    ///              that block floats, zooms camera, and fires at matching-color blocks.
    /// </summary>
    public class BoosterManager : MonoBehaviour
    {
        public static BoosterManager Instance { get; private set; }

        [Header("Booster Config (ScriptableObjects — optional)")]
        public BoosterData extraSlotData;
        public BoosterData freePickData;
        [UnityEngine.Serialization.FormerlySerializedAs("colorBlastData")]
        public BoosterData superShooterData;
        public BoosterData moveShooterData;

        [Header("Initial unlock reward")]
        public int initialBoosterCount = 2;

        // SuperShooter awaits a tap on a slotted block
        public bool IsAwaitingSuperShooterTarget { get; private set; }
        // MoveShooter awaits a tap on any grid block
        public bool IsAwaitingMoveShooterTarget { get; private set; }

        private Coroutine _freePickCoroutine;

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
            int unlockLevel = type switch
            {
                BoosterType.ExtraSlot  => extraSlotData  != null ? extraSlotData.unlockLevel  : GameManager.Instance.config.extraSlotUnlockLevel,
                BoosterType.FreePick   => freePickData   != null ? freePickData.unlockLevel   : GameManager.Instance.config.freePickUnlockLevel,
                BoosterType.SuperShooter => superShooterData != null ? superShooterData.unlockLevel : GameManager.Instance.config.superShooterUnlockLevel,
                BoosterType.MoveShooter => moveShooterData != null ? moveShooterData.unlockLevel : GameManager.Instance.config.moveShooterUnlockLevel,
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

            if (!SaveManager.UseBooster(type)) return false;

            switch (type)
            {
                case BoosterType.ExtraSlot:  ActivateExtraSlot();  break;
                case BoosterType.FreePick:   ActivateFreePick();   break;
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

        // ── FreePick ──────────────────────────────────────────────────────────
        // For a limited time, all grid blocks become selectable regardless of row.
        // After the player picks a block (or time runs out), normal rules resume.

        private void ActivateFreePick()
        {
            if (_freePickCoroutine != null)
            {
                StopCoroutine(_freePickCoroutine);
                ShooterGrid.Instance?.SetFreePickMode(false);
            }

            float duration = freePickData != null ? freePickData.duration : 8f;
            _freePickCoroutine = StartCoroutine(FreePickRoutine(duration));
        }

        private IEnumerator FreePickRoutine(float duration)
        {
            ShooterGrid.Instance?.SetFreePickMode(true);

            // End early if the player picks a block (watch for a slotted event)
            float elapsed = 0f;
            int slotCountBefore = SlotSystem.Instance != null ? SlotSystem.Instance.GetSlottedBlocks().Count : 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                // If a new block was slotted, the pick was made — end early
                int slotCountNow = SlotSystem.Instance != null ? SlotSystem.Instance.GetSlottedBlocks().Count : 0;
                if (slotCountNow > slotCountBefore) break;

                yield return null;
            }

            ShooterGrid.Instance?.SetFreePickMode(false);
            _freePickCoroutine = null;
        }

        // ── SuperShooter ──────────────────────────────────────────────────────
        // Enters selection mode. Player must tap a slotted block.
        // That block floats into the air, zooms the camera, and shoots matching conveyor blocks.

        private void ActivateSuperShooter()
        {
            if (SlotSystem.Instance == null) return;
            var slotted = SlotSystem.Instance.GetSlottedBlocks();
            if (slotted.Count == 0) return;

            IsAwaitingSuperShooterTarget = true;

            // Highlight slotted blocks so player knows to tap one
            foreach (var b in slotted)
                b.transform.DOPunchScale(Vector3.one * 0.2f, 0.4f, 4, 0.5f);

            StartCoroutine(WaitForSuperShooterTarget(slotted));
        }

        private IEnumerator WaitForSuperShooterTarget(System.Collections.Generic.List<ShooterBlock> candidates)
        {
            float timeout = superShooterData != null ? superShooterData.duration : 8f;
            float elapsed = 0f;

            while (elapsed < timeout && IsAwaitingSuperShooterTarget)
            {
                elapsed += Time.deltaTime;

                if (Input.GetMouseButtonDown(0))
                {
                    var hit = RaycastBlock();
                    if (hit != null)
                    {
                        if (candidates.Contains(hit))
                        {
                            IsAwaitingSuperShooterTarget = false;
                            ResetSuperShooterCandidatesScale(candidates);
                            hit.StartSuperShooter();
                            yield break;
                        }
                        else if (hit.State == ShooterBlock.BlockState.InGrid)
                        {
                            // Tapped a block still in the grid -> cancel selection and refund
                            IsAwaitingSuperShooterTarget = false;
                            SaveManager.AddBooster(BoosterType.SuperShooter, 1);
                            ResetSuperShooterCandidatesScale(candidates);
                            yield break;
                        }
                    }
                }
                yield return null;
            }

            if (IsAwaitingSuperShooterTarget)
            {
                IsAwaitingSuperShooterTarget = false;
                SaveManager.AddBooster(BoosterType.SuperShooter, 1);
            }
            ResetSuperShooterCandidatesScale(candidates);
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
            SaveManager.AddBooster(BoosterType.SuperShooter, 1);
            if (SlotSystem.Instance != null)
            {
                ResetSuperShooterCandidatesScale(SlotSystem.Instance.GetSlottedBlocks());
            }
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

            // Punch scale/highlight all currently locked blocks to suggest selection
            foreach (var b in ShooterGrid.Instance.GetActiveBlocks())
            {
                if (b.State == ShooterBlock.BlockState.InGrid && !b.IsAccessible)
                {
                    b.transform.DOPunchScale(Vector3.one * 0.15f, 0.4f, 4, 0.5f);
                }
            }

            StartCoroutine(WaitForMoveShooterTarget());
        }

        private IEnumerator WaitForMoveShooterTarget()
        {
            float timeout = moveShooterData != null ? moveShooterData.duration : 8f;
            float elapsed = 0f;

            while (elapsed < timeout && IsAwaitingMoveShooterTarget)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (IsAwaitingMoveShooterTarget)
            {
                IsAwaitingMoveShooterTarget = false;
                SaveManager.AddBooster(BoosterType.MoveShooter, 1);
            }
            ResetMoveShooterCandidatesScale();
        }

        private void ResetMoveShooterCandidatesScale()
        {
            if (ShooterGrid.Instance == null) return;
            foreach (var b in ShooterGrid.Instance.GetActiveBlocks())
            {
                if (b != null && b.State == ShooterBlock.BlockState.InGrid && !b.IsAccessible)
                {
                    b.transform.DOKill(true);
                    b.transform.localScale = Vector3.one;
                }
            }
        }

        public void CancelMoveShooterSelection()
        {
            if (!IsAwaitingMoveShooterTarget) return;
            IsAwaitingMoveShooterTarget = false;
            SaveManager.AddBooster(BoosterType.MoveShooter, 1);
            ResetMoveShooterCandidatesScale();
        }

        public void CompleteMoveShooter(ShooterBlock block)
        {
            IsAwaitingMoveShooterTarget = false;
            ResetMoveShooterCandidatesScale();
        }

        private ShooterBlock RaycastBlock()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                return hit.collider.GetComponent<ShooterBlock>();
            return null;
        }

        // ── Unlock rewards ────────────────────────────────────────────────────

        private void CheckUnlocks()
        {
            int level = SaveManager.CurrentLevel;
            TryGiveInitial(BoosterType.ExtraSlot,  extraSlotData  != null ? extraSlotData.unlockLevel  : GameManager.Instance.config.extraSlotUnlockLevel,  level);
            TryGiveInitial(BoosterType.FreePick,   freePickData   != null ? freePickData.unlockLevel   : GameManager.Instance.config.freePickUnlockLevel,   level);
            TryGiveInitial(BoosterType.SuperShooter, superShooterData != null ? superShooterData.unlockLevel : GameManager.Instance.config.superShooterUnlockLevel, level);
            TryGiveInitial(BoosterType.MoveShooter, moveShooterData != null ? moveShooterData.unlockLevel : GameManager.Instance.config.moveShooterUnlockLevel, level);
        }

        private void TryGiveInitial(BoosterType type, int unlockLevel, int currentLevel)
        {
            string key = $"BoosterSeen_{type}";
            if (currentLevel >= unlockLevel && PlayerPrefs.GetInt(key, 0) == 0)
            {
                SaveManager.AddBooster(type, initialBoosterCount);
                PlayerPrefs.SetInt(key, 1);
                PlayerPrefs.Save();
                BoosterUnlockUI.Instance?.ShowUnlock(type);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Give All Boosters (Debug)")]
        private void GiveAllBoosters()
        {
            SaveManager.AddBooster(BoosterType.ExtraSlot, 3);
            SaveManager.AddBooster(BoosterType.FreePick, 3);
            SaveManager.AddBooster(BoosterType.SuperShooter, 3);
            SaveManager.AddBooster(BoosterType.MoveShooter, 3);
        }
#endif
    }
}
