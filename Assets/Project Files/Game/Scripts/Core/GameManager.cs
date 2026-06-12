using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private const float WinDelaySeconds = 1.2f;

        [Header("Config")]
        public GameConfig config;

        public GameState State { get; private set; } = GameState.Idle;

        public static event Action<GameState> OnStateChanged;
        public static event Action OnLevelWin;
        public static event Action OnLevelFail;
        public static event Action OnLevelStart;

        private Coroutine _failCoroutine;
        private float _failCheckTimer = 0f;
        private const float FailCheckInterval = 0.1f;
        private bool _isDeadlockActive = false;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Lock target frame rate to 60 FPS for smooth performance and to prevent stuttering
            Application.targetFrameRate = 60;
        }

        private void Start()
        {
            SetState(GameState.Playing);
            OnLevelStart?.Invoke();
        }

        private void Update()
        {
            if (State != GameState.Playing)
            {
                CancelFailSequence();
                return;
            }

            _failCheckTimer += Time.deltaTime;
            if (_failCheckTimer >= FailCheckInterval)
            {
                _failCheckTimer = 0f;
                CheckFailCondition();
            }
        }

        private void CancelFailSequence()
        {
            if (_failCoroutine != null)
            {
                StopCoroutine(_failCoroutine);
                _failCoroutine = null;
            }
            if (_isDeadlockActive)
            {
                _isDeadlockActive = false;
                Debug.Log("[FailCheck] Deadlock or fail state resolved. Fail timer canceled.");
            }
        }

        public void SetState(GameState newState)
        {
            State = newState;
            OnStateChanged?.Invoke(newState);
        }

        public void TriggerWin()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.Win);
            StartCoroutine(TriggerWinDelayed());
        }

        private IEnumerator TriggerWinDelayed()
        {
            yield return new WaitForSecondsRealtime(WinDelaySeconds);

            if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevelRoot != null)
            {
                var animator = LevelManager.Instance.CurrentLevelRoot.GetComponent<Animator>();
                if (animator == null)
                    animator = LevelManager.Instance.CurrentLevelRoot.GetComponentInChildren<Animator>();
                if (animator != null)
                    animator.SetTrigger("LevelWin");
            }

            if (config != null)
                SaveManager.Coins += config.winRewardCoins;

            SaveManager.CurrentLevel++;
            OnLevelWin?.Invoke();
        }

        public void TriggerFail()
        {
            if (State != GameState.Playing && State != GameState.Paused) return;
            SetState(GameState.Fail);
            OnLevelFail?.Invoke();
        }

        public void CheckWinCondition()
        {
            if (State != GameState.Playing) return;

            var blocks = FindObjectsByType<ConveyorBlock3D>(FindObjectsSortMode.None);
            foreach (var b in blocks)
                if (b != null && !b.IsDestroyed) return;

            TriggerWin();
        }

        public void CheckFailCondition()
        {
            if (State != GameState.Playing) return;

            bool isDeadlocked = IsDeadlocked();
            bool isAllDepleted = AreAllShootersDepleted();

            if (isDeadlocked || isAllDepleted)
            {
                if (_failCoroutine == null)
                {
                    if (isDeadlocked && !_isDeadlockActive)
                    {
                        _isDeadlockActive = true;
                        Debug.Log("[FailCheck] Deadlock confirmed. Firing fail sequence with 1.5s delay.");
                    }
                    else if (isAllDepleted && !_isDeadlockActive)
                    {
                        _isDeadlockActive = true;
                        Debug.Log("[FailCheck] All shooters depleted. Firing fail sequence with 1.5s delay.");
                    }
                    _failCoroutine = StartCoroutine(TriggerFailDelayedRoutine());
                }
            }
            else
            {
                CancelFailSequence();
            }
        }

        private IEnumerator TriggerFailDelayedRoutine()
        {
            yield return new WaitForSeconds(1.5f);

            // Re-verify after delay to ensure status didn't change (e.g. from a booster)
            if (State == GameState.Playing && (IsDeadlocked() || AreAllShootersDepleted()))
            {
                Debug.Log("[FailCheck] Fail sequence completed. Triggering fail state.");
                HandleFailState();
            }
            _failCoroutine = null;
            _isDeadlockActive = false;
        }

        // ── Fail detection ────────────────────────────────────────────────────

        private bool IsDeadlocked()
        {
            if (SlotSystem.Instance == null || ConveyorController.Instance == null) return false;

            // While projectiles are still in flight, the conveyor state is changing.
            if (ProjectilePool.Instance != null && ProjectilePool.Instance.ActiveCount > 0) return false;

            // Step 1: Check if player can shoot from slots
            var slotColors = new HashSet<BlockColorType>();
            foreach (var b in SlotSystem.Instance.GetSlottedBlocks())
            {
                if (b != null && !b.IsDepleted)
                    slotColors.Add(b.ColorType);
            }

            var mainColors = ConveyorController.Instance.GetLiveColorSet();

            // Empty main conveyor is a win state, not a deadlock
            if (mainColors.Count == 0) return false;

            bool canShoot = false;
            foreach (var color in slotColors)
            {
                if (mainColors.Contains(color))
                {
                    canShoot = true;
                    break;
                }
            }

            // Step 2: Check if player can pull new blocks from grid
            bool hasEmptySlot = SlotSystem.Instance.HasEmptySlot;
            bool hasAccessibleShooter = false;
            if (ShooterGrid.Instance != null)
            {
                foreach (var b in ShooterGrid.Instance.GetActiveBlocks())
                {
                    if (b != null && b.State == ShooterBlock.BlockState.InGrid && b.IsAccessible && !b.IsFrozen)
                    {
                        hasAccessibleShooter = true;
                        break;
                    }
                }
            }

            bool canPull = hasEmptySlot && hasAccessibleShooter;

            // If we can shoot from slots, or pull from the grid, we are not deadlocked
            if (canShoot || canPull) return false;

            // Step 3: Check branch merge potential
            // If the player cannot shoot or pull, but there are unmerged branches,
            // we are only safe if those branch blocks can actually merge into the conveyor.
            // If the conveyor is completely full, branches are blocked and cannot merge.
            bool hasUnmergedBranches = false;
            var branchPaths = FindObjectsByType<BranchPath>(FindObjectsSortMode.None);
            foreach (var bp in branchPaths)
            {
                if (!bp.IsFullyMerged)
                {
                    hasUnmergedBranches = true;
                    break;
                }
            }

            if (hasUnmergedBranches)
            {
                float requiredSpacing = 0.2f;
                foreach (var bp in branchPaths)
                {
                    if (!bp.IsFullyMerged)
                    {
                        var groups = bp.GetComponentsInChildren<BlockGroup>(true);
                        if (groups != null && groups.Length > 0)
                        {
                            requiredSpacing = groups[0].rowSpacing;
                            break;
                        }
                    }
                }

                // If the conveyor is not full, branch blocks will eventually merge, so we are not deadlocked yet
                if (!ConveyorController.Instance.IsConveyorFull(requiredSpacing))
                {
                    return false;
                }
            }

            // No possible moves, and no new blocks can merge -> Deadlock
            return true;
        }

        private bool AreAllShootersDepleted()
        {
            if (ProjectilePool.Instance != null && ProjectilePool.Instance.ActiveCount > 0) return false;
            if (ShooterGrid.Instance != null && ShooterGrid.Instance.GetActiveBlocks().Count > 0) return false;
            if (SlotSystem.Instance != null && SlotSystem.Instance.GetSlottedBlocks().Count > 0) return false;

            // All shooters are gone — fail only when conveyor / branch still has blocks
            if (ConveyorController.Instance != null && ConveyorController.Instance.GetLiveColorSet().Count > 0)
                return true;

            foreach (var bp in FindObjectsByType<BranchPath>(FindObjectsSortMode.None))
                if (!bp.IsFullyMerged) return true;

            return false;
        }

        private void HandleFailState()
        {
            if (State != GameState.Playing) return;

            bool canRevive = UIManager.Instance != null && !UIManager.Instance.HasRevivedThisLevel
                          && SlotSystem.Instance != null
                          && SlotSystem.Instance.MaxSlots <= SlotSystem.Instance.InitialSlotsCount;

            if (canRevive)
            {
                if (ConveyorController.Instance != null)
                    ConveyorController.Instance.IsFrozen = true;

                SetState(GameState.Paused);
                UIManager.Instance.ShowKeepPlayingPanel();
            }
            else
            {
                TriggerFail();
            }
        }

        public bool IsPlaying => State == GameState.Playing;
    }

    public enum GameState
    {
        Idle,
        Playing,
        Paused,
        Win,
        Fail
    }
}
