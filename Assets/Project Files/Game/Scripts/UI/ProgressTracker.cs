using UnityEngine;

namespace BlockShooter
{
    public class ProgressTracker : MonoBehaviour
    {
        private int _totalBlocks;
        private int _destroyedBlocks;

        private void OnEnable()
        {
            ScoreManager.OnScoreChanged += HandleScoreChanged;
        }

        private void OnDisable()
        {
            ScoreManager.OnScoreChanged -= HandleScoreChanged;
        }

        public void SetTotalBlocks(int total)
        {
            _totalBlocks = Mathf.Max(1, total);
            _destroyedBlocks = 0;
            UpdateHUD();
        }

        private void HandleScoreChanged(int score)
        {
            _destroyedBlocks = ScoreManager.Instance != null ? ScoreManager.Instance.BlocksDestroyed : 0;
            UpdateHUD();
        }

        private void UpdateHUD()
        {
            float progress = _totalBlocks > 0 ? (float)_destroyedBlocks / _totalBlocks : 0f;
            UIManager.Instance?.SetProgress(progress);
        }
    }
}
