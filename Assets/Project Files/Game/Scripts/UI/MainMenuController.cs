using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

namespace BlockShooter
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("UI Elements")]
        public TextMeshProUGUI levelText;
        public Button playButton;
        public Button settingsButton;
        public GameObject settingsPanel;

        [Header("Animations")]
        public RectTransform logoRect;
        public RectTransform playButtonRect;

        private void Start()
        {
            if (levelText != null)
                levelText.text = $"Level {SaveManager.CurrentLevel}";

            playButton?.onClick.AddListener(StartGame);
            settingsButton?.onClick.AddListener(ToggleSettings);

            AnimateEntrance();
        }

        private void AnimateEntrance()
        {
            if (logoRect != null)
            {
                logoRect.anchoredPosition += Vector2.up * 100f;
                logoRect.DOAnchorPosY(logoRect.anchoredPosition.y - 100f, 0.6f).SetEase(Ease.OutBack);
            }

            if (playButtonRect != null)
            {
                playButtonRect.localScale = Vector3.zero;
                playButtonRect.DOScale(Vector3.one, 0.5f).SetDelay(0.3f).SetEase(Ease.OutBack);
            }
        }

        private void StartGame()
        {
            DOTween.KillAll();
            SceneManager.LoadScene(1);
        }

        private void ToggleSettings()
        {
            if (settingsPanel == null) return;
            bool active = !settingsPanel.activeSelf;
            settingsPanel.SetActive(active);
            if (active)
            {
                settingsPanel.transform.localScale = Vector3.zero;
                settingsPanel.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            }
        }
    }
}
