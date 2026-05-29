using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace BlockShooter
{
    public class FailPanel : MonoBehaviour
    {
        [Header("UI")]
        public GameObject panel;
        public TextMeshProUGUI levelText;
        public Button retryButton;
        public Button homeButton;

        private void OnEnable()
        {
            GameManager.OnLevelFail += Show;
        }

        private void OnDisable()
        {
            GameManager.OnLevelFail -= Show;
        }

        private void Start()
        {
            panel?.SetActive(false);
            retryButton?.onClick.AddListener(OnRetry);
            homeButton?.onClick.AddListener(OnHome);
        }

        private void Show()
        {
            panel?.SetActive(true);
            if (levelText != null)
                levelText.text = $"Level {SaveManager.CurrentLevel}";

            panel.transform.localScale = Vector3.zero;
            panel.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void OnRetry() => LevelManager.Instance?.RestartLevel();
        private void OnHome() => LevelManager.Instance?.LoadMainMenu();
    }
}
