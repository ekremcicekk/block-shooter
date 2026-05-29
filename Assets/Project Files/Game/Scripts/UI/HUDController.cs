using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BlockShooter
{
    public class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }

        [Header("Level Info")]
        public TextMeshProUGUI levelText;
        public Slider progressBar;

        [Header("Score")]
        public TextMeshProUGUI scoreText;

        [Header("Panels")]
        public GameObject hudPanel;
        public GameObject pausePanel;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            GameManager.OnStateChanged += HandleStateChanged;
            ScoreManager.OnScoreChanged += UpdateScore;
        }

        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
            ScoreManager.OnScoreChanged -= UpdateScore;
        }

        private void Start()
        {
            if (levelText != null)
                levelText.text = $"Level {SaveManager.CurrentLevel}";
            UpdateScore(0);
        }

        private void HandleStateChanged(GameState state)
        {
            hudPanel?.SetActive(state == GameState.Playing);
            pausePanel?.SetActive(state == GameState.Paused);
        }

        private void UpdateScore(int score)
        {
            if (scoreText != null)
                scoreText.text = score.ToString("N0");
        }

        public void SetProgress(float value)
        {
            if (progressBar != null)
                progressBar.value = value;
        }

        public void OnPauseButton()
        {
            if (GameManager.Instance.State == GameState.Playing)
            {
                GameManager.Instance.SetState(GameState.Paused);
                Time.timeScale = 0f;
            }
        }

        public void OnResumeButton()
        {
            if (GameManager.Instance.State == GameState.Paused)
            {
                GameManager.Instance.SetState(GameState.Playing);
                Time.timeScale = 1f;
            }
        }
    }
}
