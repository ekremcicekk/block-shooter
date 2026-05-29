using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace BlockShooter
{
    public class WinPanel : MonoBehaviour
    {
        [Header("UI")]
        public GameObject panel;
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI scoreText;
        public Button nextButton;
        public Button homeButton;

        [Header("Stars")]
        public GameObject[] stars;
        public int starsEarned = 3;

        private void OnEnable()
        {
            GameManager.OnLevelWin += Show;
        }

        private void OnDisable()
        {
            GameManager.OnLevelWin -= Show;
        }

        private void Start()
        {
            panel?.SetActive(false);
            nextButton?.onClick.AddListener(OnNext);
            homeButton?.onClick.AddListener(OnHome);
        }

        private void Show()
        {
            panel?.SetActive(true);

            if (levelText != null)
                levelText.text = $"Level {SaveManager.CurrentLevel - 1} Complete!";
            if (scoreText != null && ScoreManager.Instance != null)
                scoreText.text = ScoreManager.Instance.Score.ToString("N0");

            AnimatePanel();
            AnimateStars();
        }

        private void AnimatePanel()
        {
            if (panel == null) return;
            panel.transform.localScale = Vector3.zero;
            panel.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void AnimateStars()
        {
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null) continue;
                bool earned = i < starsEarned;
                stars[i].SetActive(earned);
                if (earned)
                {
                    stars[i].transform.localScale = Vector3.zero;
                    stars[i].transform.DOScale(Vector3.one, 0.3f)
                        .SetDelay(0.3f + i * 0.15f)
                        .SetEase(Ease.OutBack)
                        .SetUpdate(true);
                }
            }
        }

        private void OnNext() => LevelManager.Instance?.LoadNextLevel();
        private void OnHome() => LevelManager.Instance?.LoadMainMenu();
    }
}
