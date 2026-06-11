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
        /// Checks if all slots are occupied and none of the slotted shooter blocks
        /// can target any remaining blocks on the conveyor (deadlock).
        /// </summary>
        public void CheckFailCondition(string trigger = "unknown")
        {
            if (State != GameState.Playing) return;
            if (SlotSystem.Instance == null) return;

            var slottedBlocks = SlotSystem.Instance.GetSlottedBlocks();
            int slotCount  = slottedBlocks.Count;
            int maxSlots   = SlotSystem.Instance.MaxSlots;

            // Slots not full yet — nothing to check
            if (slotCount < maxSlots)
            {
                Debug.Log($"[FAIL] Check [{trigger}] — slots {slotCount}/{maxSlots} not full → skip");
                return;
            }

            // Build conveyor color set
            var conveyorBlocks = FindObjectsByType<ConveyorBlock3D>(FindObjectsSortMode.None);
            var conveyorColors = new HashSet<BlockColorType>();
            foreach (var cb in conveyorBlocks)
                if (cb != null && !cb.IsDestroyed && cb.gameObject.activeInHierarchy)
                    conveyorColors.Add(cb.ColorType);

            if (conveyorColors.Count == 0)
            {
                Debug.Log($"[FAIL] Check [{trigger}] — slots {slotCount}/{maxSlots} FULL | no conveyor blocks → skip (win path)");
                return;
            }

            // Build slotted color list and check match
            var slotDesc    = new System.Text.StringBuilder();
            bool canShootAny = false;
            foreach (var sb in slottedBlocks)
            {
                if (sb == null) { slotDesc.Append("null "); continue; }
                slotDesc.Append(sb.ColorType);
                if (sb.IsDepleted) { slotDesc.Append("(dep) "); continue; }
                slotDesc.Append(' ');
                if (conveyorColors.Contains(sb.ColorType))
                    canShootAny = true;
            }

            var conveyorDesc = new System.Text.StringBuilder();
            foreach (var c in conveyorColors) { conveyorDesc.Append(c); conveyorDesc.Append(' '); }

            if (canShootAny)
            {
                Debug.Log($"[FAIL] Check [{trigger}] — slots FULL ({slotDesc}) | conveyor ({conveyorDesc}) → match found, OK");
                return;
            }

            Debug.LogWarning($"[FAIL] Check [{trigger}] — slots FULL ({slotDesc}) | conveyor ({conveyorDesc}) → NO MATCH → DEADLOCK");
            HandleDeadlockState();
        }

        private void HandleDeadlockState()
        {
            if (State != GameState.Playing) return;

            bool maxSlotsUnchanged = SlotSystem.Instance != null &&
                                     SlotSystem.Instance.MaxSlots <= SlotSystem.Instance.InitialSlotsCount;
            bool notRevived        = UIManager.Instance != null && !UIManager.Instance.HasRevivedThisLevel;
            bool eligibleForRevive = maxSlotsUnchanged && notRevived;

            Debug.LogWarning($"[FAIL] HandleDeadlock — maxSlotsUnchanged={maxSlotsUnchanged} notRevived={notRevived} → eligibleForRevive={eligibleForRevive}");

            if (eligibleForRevive)
            {
                if (ConveyorController.Instance != null)
                    ConveyorController.Instance.IsFrozen = true;

                SetState(GameState.Paused);
                UIManager.Instance.ShowKeepPlayingPanel();
                Debug.Log("[FAIL] HandleDeadlock → showing KeepPlaying panel, conveyor frozen");
            }
            else
            {
                Debug.Log("[FAIL] HandleDeadlock → calling TriggerFail");
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
