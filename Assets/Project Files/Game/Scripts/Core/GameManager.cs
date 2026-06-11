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

        /// <summary>
        /// Evaluates both fail conditions and acts accordingly.
        /// Call this whenever game state changes in a way that could produce a deadlock or depletion.
        /// </summary>
        public void CheckFailCondition()
        {
            if (State != GameState.Playing) return;

            if (IsDeadlocked() || AreAllShootersDepleted())
                HandleFailState();
        }

        // Returns true when all slots are full and no slotted block can match any live conveyor block.
        private bool IsDeadlocked()
        {
            if (SlotSystem.Instance == null || SlotSystem.Instance.HasEmptySlot) return false;

            var conveyorColors = GetLiveConveyorColors();
            if (conveyorColors.Count == 0) return false; // win path — CheckWinCondition handles this

            foreach (var block in SlotSystem.Instance.GetSlottedBlocks())
            {
                if (!block.IsDepleted && conveyorColors.Contains(block.ColorType))
                    return false;
            }

            return true;
        }

        // Returns true when there are no shooter blocks left (grid + slots) but conveyor still has blocks.
        private bool AreAllShootersDepleted()
        {
            if (ProjectilePool.Instance != null && ProjectilePool.Instance.ActiveCount > 0) return false;
            if (ShooterGrid.Instance != null && ShooterGrid.Instance.GetActiveBlocks().Count > 0) return false;
            if (SlotSystem.Instance != null && SlotSystem.Instance.GetSlottedBlocks().Count > 0) return false;

            return GetLiveConveyorColors().Count > 0;
        }

        private HashSet<BlockColorType> GetLiveConveyorColors()
        {
            var colors = new HashSet<BlockColorType>();
            var blocks = FindObjectsByType<ConveyorBlock3D>(FindObjectsSortMode.None);
            foreach (var b in blocks)
                if (b != null && !b.IsDestroyed && b.gameObject.activeInHierarchy)
                    colors.Add(b.ColorType);
            return colors;
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
