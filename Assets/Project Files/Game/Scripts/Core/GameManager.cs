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

            // Trigger LevelWin on active level root animator if present
            if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevelRoot != null)
            {
                var animator = LevelManager.Instance.CurrentLevelRoot.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = LevelManager.Instance.CurrentLevelRoot.GetComponentInChildren<Animator>();
                }

                if (animator != null)
                {
                    animator.SetTrigger("LevelWin");
                }
            }

            // Award level win coins
            if (config != null)
            {
                SaveManager.Coins += config.winRewardCoins;
            }

            SaveManager.CurrentLevel++;
            OnLevelWin?.Invoke();
        }

        public void TriggerFail()
        {
            // Allow triggering fail if state is Paused (due to active revival popup check)
            if (State != GameState.Playing && State != GameState.Paused) return;
            Debug.Log("[FAIL] *** TriggerFail called — game over ***");
            SetState(GameState.Fail);
            OnLevelFail?.Invoke();
        }

        /// <summary>
        /// Checks if there are any active conveyor blocks left in the scene.
        /// If none, the level is won.
        /// </summary>
        public void CheckWinCondition()
        {
            if (State != GameState.Playing) return;

            var blocks = FindObjectsByType<ConveyorBlock3D>(FindObjectsSortMode.None);
            foreach (var b in blocks)
            {
                if (b != null && !b.IsDestroyed)
                {
                    return; // At least one block is still alive
                }
            }

            // All blocks are destroyed - Win!
            TriggerWin();
        }

        public void CheckFailCondition()
        {
            if (State != GameState.Playing) return;

            if (IsDeadlocked() || AreAllShootersDepleted())
                HandleFailState();
        }

        private bool IsDeadlocked()
        {
            if (SlotSystem.Instance == null) { Debug.Log("[FAIL] IsDeadlocked: SlotSystem null"); return false; }
            if (ConveyorController.Instance == null) { Debug.Log("[FAIL] IsDeadlocked: ConveyorController null"); return false; }

            // Projectiles in flight mean blocks are still being hit — conveyor state is unsettled.
            // NotifyAllProjectilesLanded will re-run this check once the last projectile lands.
            if (ProjectilePool.Instance != null && ProjectilePool.Instance.ActiveCount > 0)
            {
                Debug.Log($"[FAIL] IsDeadlocked: {ProjectilePool.Instance.ActiveCount} projectile(s) in flight → defer");
                return false;
            }

            if (SlotSystem.Instance.HasEmptySlot)
            {
                Debug.Log($"[FAIL] IsDeadlocked: slots not full ({SlotSystem.Instance.GetSlottedBlocks().Count}/{SlotSystem.Instance.MaxSlots})");
                return false;
            }

            var mainColors = ConveyorController.Instance.GetLiveColorSet();
            if (mainColors.Count == 0) { Debug.Log("[FAIL] IsDeadlocked: main conveyor empty → win path"); return false; }

            var slottedBlocks = SlotSystem.Instance.GetSlottedBlocks();
            var activeSlotColors = new HashSet<BlockColorType>();
            foreach (var b in slottedBlocks)
                if (!b.IsDepleted) activeSlotColors.Add(b.ColorType);

            // 1. Check main conveyor for a match
            foreach (var color in activeSlotColors)
            {
                if (mainColors.Contains(color))
                {
                    Debug.Log($"[FAIL] IsDeadlocked: match on main conveyor ({color}) → not deadlocked");
                    return false;
                }
            }

            // 2. No match on main conveyor.
            //    A branch can prevent deadlock only when it both has a matching color AND
            //    the conveyor has a real gap available at its merge point right now.
            var branchPaths = FindObjectsByType<BranchPath>(FindObjectsSortMode.None);
            foreach (var bp in branchPaths)
            {
                if (bp.IsFullyMerged) continue;
                if (bp.CanBringMatchingColor(activeSlotColors))
                {
                    Debug.Log($"[FAIL] IsDeadlocked: branch '{bp.name}' has matching color AND gap available → not deadlocked yet");
                    return false;
                }
            }

            var slotDesc = new System.Text.StringBuilder();
            var convDesc = new System.Text.StringBuilder();
            foreach (var b in slottedBlocks) slotDesc.Append(b.IsDepleted ? $"{b.ColorType}(dep) " : $"{b.ColorType} ");
            foreach (var c in mainColors)    convDesc.Append($"{c} ");
            Debug.LogWarning($"[FAIL] DEADLOCK DETECTED — Slots:[{slotDesc}] Conveyor:[{convDesc}]");
            return true;
        }

        private bool AreAllShootersDepleted()
        {
            if (ProjectilePool.Instance != null && ProjectilePool.Instance.ActiveCount > 0) return false;
            if (ShooterGrid.Instance != null && ShooterGrid.Instance.GetActiveBlocks().Count > 0) return false;
            if (SlotSystem.Instance != null && SlotSystem.Instance.GetSlottedBlocks().Count > 0) return false;

            if (ConveyorController.Instance != null && ConveyorController.Instance.GetLiveColorSet().Count > 0)
            {
                Debug.LogWarning("[FAIL] ALL SHOOTERS DEPLETED — main conveyor still has blocks");
                return true;
            }

            var branchPaths = FindObjectsByType<BranchPath>(FindObjectsSortMode.None);
            foreach (var bp in branchPaths)
            {
                if (!bp.IsFullyMerged)
                {
                    Debug.LogWarning("[FAIL] ALL SHOOTERS DEPLETED — branch still has blocks");
                    return true;
                }
            }

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
