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

        private float _failCheckTimer = 0f;

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

        private float _noFireTimer = 0f;

        private void Update()
        {
            if (State != GameState.Playing) return;

            // 1. Regular 0.5s check (color mismatch deadlock)
            _failCheckTimer += Time.deltaTime;
            if (_failCheckTimer >= 0.5f)
            {
                _failCheckTimer = 0f;
                CheckFailCondition();
            }

            // 2. Safety timeout check: if slots are full and no shooter is actively firing or projectiles in-flight,
            // we start a timeout. If it reaches 2 seconds, we force deadlock detection.
            if (SlotSystem.Instance != null)
            {
                var slottedBlocks = SlotSystem.Instance.GetSlottedBlocks();
                if (slottedBlocks.Count >= SlotSystem.Instance.MaxSlots)
                {
                    bool isAnyShooting = false;
                    foreach (var sb in slottedBlocks)
                    {
                        if (sb != null && sb.IsShooting)
                        {
                            isAnyShooting = true;
                            break;
                        }
                    }

                    if (ProjectilePool.Instance != null && ProjectilePool.Instance.ActiveCount > 0)
                    {
                        isAnyShooting = true;
                    }

                    if (!isAnyShooting)
                    {
                        _noFireTimer += Time.deltaTime;
                        if (_noFireTimer >= 2.0f)
                        {
                            _noFireTimer = 0f;
                            Debug.LogWarning("[GameManager] No-Fire safety timeout triggered. Deadlock assumed.");
                            HandleDeadlockState();
                        }
                    }
                    else
                    {
                        _noFireTimer = 0f;
                    }
                }
                else
                {
                    _noFireTimer = 0f;
                }
            }
            else
            {
                _noFireTimer = 0f;
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

            var slottedBlocks = SlotSystem.Instance.GetSlottedBlocks();

            // 1. Slots must be completely full
            if (slottedBlocks.Count < SlotSystem.Instance.MaxSlots) return;

            // 2. Find all live blocks on the conveyor
            var conveyorBlocks = FindObjectsByType<ConveyorBlock3D>(FindObjectsSortMode.None);
            bool hasConveyorBlocks = false;
            var availableColors = new HashSet<BlockColorType>();

            foreach (var cb in conveyorBlocks)
            {
                if (cb != null && !cb.IsDestroyed && cb.gameObject.activeInHierarchy)
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

                // A block can shoot if its color matches any block currently on the conveyor
                if (availableColors.Contains(sb.ColorType))
                {
                    canShootAny = true;
                    break;
                }
            }

            // If none of the slotted blocks can shoot any block on the conveyor, it is a deadlock -> Fail/Revive!
            if (!canShootAny)
            {
                Debug.LogWarning("[GameManager] Color mismatch deadlock detected.");
                HandleDeadlockState();
            }
        }

        private void HandleDeadlockState()
        {
            if (State != GameState.Playing) return;

            // Check for keep playing (revive) eligibility
            bool eligibleForRevive = SlotSystem.Instance != null &&
                                     SlotSystem.Instance.MaxSlots <= SlotSystem.Instance.InitialSlotsCount && 
                                     UIManager.Instance != null && 
                                     !UIManager.Instance.HasRevivedThisLevel;

            if (eligibleForRevive)
            {
                // Freeze conveyor while revival popup is shown
                if (ConveyorController.Instance != null)
                {
                    ConveyorController.Instance.IsFrozen = true;
                }

                // Pause the state so we do not trigger checks repeatedly during UI presentation
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
