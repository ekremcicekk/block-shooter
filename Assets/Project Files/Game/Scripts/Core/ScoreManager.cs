using System;
using UnityEngine;

namespace BlockShooter
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        public int Score { get; private set; }
        public int BlocksDestroyed { get; private set; }
        public int ComboCount { get; private set; }

        public static event Action<int> OnScoreChanged;
        public static event Action<int> OnComboChanged;

        private float _lastDestroyTime;
        private const float ComboWindow = 1.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void AddBlockDestroyed()
        {
            BlocksDestroyed++;

            if (Time.time - _lastDestroyTime < ComboWindow)
                ComboCount++;
            else
                ComboCount = 1;

            _lastDestroyTime = Time.time;

            int config_score = GameManager.Instance != null ? GameManager.Instance.config.scorePerBlock : 10;
            int multiplier = ComboCount >= 3 ? GameManager.Instance.config.scoreComboMultiplier : 1;
            AddScore(config_score * multiplier);
            OnComboChanged?.Invoke(ComboCount);
        }

        private void AddScore(int amount)
        {
            Score += amount;
            OnScoreChanged?.Invoke(Score);
        }

        public void Reset()
        {
            Score = 0;
            BlocksDestroyed = 0;
            ComboCount = 0;
        }
    }
}
