using System;
using UnityEngine;

namespace BlockShooter
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

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
