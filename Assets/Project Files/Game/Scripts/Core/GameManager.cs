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
            if (State != GameState.Playing) return;
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
        /// Checks if all slots are occupied and none of the slotted shooter blocks
        /// can target any remaining blocks on the conveyor (deadlock).
        /// </summary>
        public void CheckFailCondition()
        {
            if (State != GameState.Playing) return;

            if (SlotSystem.Instance == null) return;

            // 1. Slots must be completely full
            if (SlotSystem.Instance.HasEmptySlot) return;

            var slottedBlocks = SlotSystem.Instance.GetSlottedBlocks();
            if (slottedBlocks.Count == 0) return;

            // 2. Find all live blocks on the conveyor
            var conveyorBlocks = FindObjectsByType<ConveyorBlock3D>(FindObjectsSortMode.None);
            bool hasConveyorBlocks = false;
            var availableColors = new HashSet<BlockColorType>();

            foreach (var cb in conveyorBlocks)
            {
                if (cb != null && !cb.IsDestroyed)
                {
                    hasConveyorBlocks = true;
                    availableColors.Add(cb.ColorType);
                }
            }

            // If there are no conveyor blocks left, it's a win, not a fail.
            if (!hasConveyorBlocks) return;

            // 3. Check if ANY slotted block can target ANY available color
            bool canShootAny = false;
            foreach (var sb in slottedBlocks)
            {
                if (sb == null || sb.IsDepleted) continue;

                // A normal block can shoot if its color exists on the conveyor
                if (availableColors.Contains(sb.ColorType))
                {
                    canShootAny = true;
                    break;
                }
            }

            // If none of the slotted blocks can shoot any block on the conveyor, it is a deadlock -> Fail!
            if (!canShootAny)
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
