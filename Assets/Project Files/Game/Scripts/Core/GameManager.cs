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

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            SetState(GameState.Playing);
            OnLevelStart?.Invoke();
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

            if (IsDeadlocked() || AreAllShootersDepleted())
                HandleFailState();
        }

        // ── Fail detection ────────────────────────────────────────────────────

        private bool IsDeadlocked()
        {
            if (SlotSystem.Instance == null || ConveyorController.Instance == null) return false;

            // While projectiles are still in flight, conveyor state is changing.
            // NotifyAllProjectilesLanded will call us again once all have landed.
            if (ProjectilePool.Instance != null && ProjectilePool.Instance.ActiveCount > 0) return false;

            // Deadlock requires every slot to be occupied
            if (SlotSystem.Instance.HasEmptySlot) return false;

            var mainColors = ConveyorController.Instance.GetLiveColorSet();

            // Empty conveyor is a win condition, not a deadlock
            if (mainColors.Count == 0) return false;

            // Collect colors of non-depleted slotted shooters
            var slotColors = new HashSet<BlockColorType>();
            foreach (var b in SlotSystem.Instance.GetSlottedBlocks())
                if (!b.IsDepleted) slotColors.Add(b.ColorType);

            // Match found on main conveyor → not deadlocked
            foreach (var color in slotColors)
                if (mainColors.Contains(color)) return false;

            // Branch can resolve the deadlock if it still has a matching color queued.
            // The looping conveyor always creates a mergeT gap as groups pass, so gap
            // availability is irrelevant — only the color matters.
            foreach (var bp in FindObjectsByType<BranchPath>(FindObjectsSortMode.None))
                if (!bp.IsFullyMerged && bp.HasMatchingColorInQueue(slotColors)) return false;

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
