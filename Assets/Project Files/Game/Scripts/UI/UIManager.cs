using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.SceneManagement;

namespace BlockShooter
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("HUD Panel")]
        public TextMeshProUGUI levelText;
        public Image progressBarFill;
        public TextMeshProUGUI progressText;
        public TextMeshProUGUI coinText;
        public Button settingsBtn;
        public GameObject settingsPanel;
        public Button resumeButton;
        public Button speedBtn;
        public GameObject speedX1Group;
        public GameObject speedX2Group;

        [Header("Win Panel")]
        public GameObject winPanel;
        public TextMeshProUGUI winCoinText;
        public Button winNextButton;

        [Header("Fail Panel")]
        public GameObject failPanel;
        public Button failRetryButton;
        public Button failHomeButton;

        [Header("Keep Playing Panel")]
        public GameObject keepPlayingPanel;
        public Button playOnButton;
        public Button watchAdButton;

        // Runtime states
        public bool HasRevivedThisLevel { get; private set; }
        private bool _isSpeedX2;
        private int _currentDisplayCoins;
        private Tween _coinTween;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Reset timescales on load just in case
            Time.timeScale = 1f;
        }

        private void Start()
        {
            // Reset revive state
            HasRevivedThisLevel = false;

            // Ensure speed is reset to x1 at level start
            _isSpeedX2 = false;
            if (ConveyorController.Instance != null)
            {
                ConveyorController.Instance.SetSpeedMultiplier(1f);
            }
            if (speedX1Group != null) speedX1Group.SetActive(true);
            if (speedX2Group != null)
            {
                speedX2Group.SetActive(false);
                speedX2Group.transform.localScale = Vector3.zero;
            }

            // Set dynamic button listeners in code
            settingsBtn?.onClick.AddListener(OpenSettings);
            resumeButton?.onClick.AddListener(CloseSettings);
            speedBtn?.onClick.AddListener(ToggleSpeed);

            winNextButton?.onClick.AddListener(OnNextLevel);

            failRetryButton?.onClick.AddListener(OnRetryLevel);
            failHomeButton?.onClick.AddListener(OnHome);

            playOnButton?.onClick.AddListener(OnPlayOnClicked);
            watchAdButton?.onClick.AddListener(OnWatchAdClicked);

            // Dynamically search and bind decline button in KeepPlaying panel to keep serialized fields clean
            if (keepPlayingPanel != null)
            {
                Transform declineTrans = keepPlayingPanel.transform.Find("TryAgain_BTN");
                if (declineTrans == null) declineTrans = keepPlayingPanel.transform.Find("TryAgain");
                
                if (declineTrans != null)
                {
                    Button declineBtn = declineTrans.GetComponent<Button>();
                    if (declineBtn != null)
                    {
                        declineBtn.onClick.AddListener(OnDeclineKeepPlaying);
                    }
                }
            }

            // Setup level info
            if (levelText != null)
                levelText.text = SaveManager.CurrentLevel.ToString();

            // Reset panel active states
            settingsPanel?.SetActive(false);
            winPanel?.SetActive(false);
            failPanel?.SetActive(false);
            keepPlayingPanel?.SetActive(false);

            // Initialize coins count
            UpdateCoinUI(animate: false);
        }

        private void OnEnable()
        {
            GameManager.OnLevelWin += ShowWinPanel;
            GameManager.OnLevelFail += ShowFailPanel;
        }

        private void OnDisable()
        {
            GameManager.OnLevelWin -= ShowWinPanel;
            GameManager.OnLevelFail -= ShowFailPanel;
        }

        // ── HUD Panel Control ──────────────────────────────────────────────────

        public void SetProgress(float value)
        {
            if (progressBarFill != null)
                progressBarFill.fillAmount = value;

            if (progressText != null)
                progressText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        public void UpdateCoinUI(bool animate = true)
        {
            int targetCoins = SaveManager.Coins;
            if (!animate || coinText == null)
            {
                _currentDisplayCoins = targetCoins;
                if (coinText != null) coinText.text = _currentDisplayCoins.ToString();
                return;
            }

            _coinTween?.Kill();
            _coinTween = DOTween.To(() => _currentDisplayCoins, x =>
            {
                _currentDisplayCoins = x;
                coinText.text = _currentDisplayCoins.ToString();
            }, targetCoins, 0.4f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        private void ToggleSpeed()
        {
            _isSpeedX2 = !_isSpeedX2;

            if (ConveyorController.Instance != null)
            {
                ConveyorController.Instance.SetSpeedMultiplier(_isSpeedX2 ? 2f : 1f);
            }

            // Subtle mechanical punch on the parent button
            transform.DOKill();
            transform.localScale = Vector3.one;
            transform.DOPunchScale(Vector3.one * 0.05f, 0.15f, 1, 0.5f).SetUpdate(true);

            if (_isSpeedX2)
            {
                // Transition to X2
                if (speedX1Group != null) speedX1Group.SetActive(false);
                if (speedX2Group != null)
                {
                    speedX2Group.SetActive(true);
                    speedX2Group.transform.DOKill();
                    speedX2Group.transform.localScale = Vector3.one * 0.8f;
                    speedX2Group.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
                }
            }
            else
            {
                // Transition to X1
                if (speedX2Group != null) speedX2Group.SetActive(false);
                if (speedX1Group != null)
                {
                    speedX1Group.SetActive(true);
                    speedX1Group.transform.DOKill();
                    speedX1Group.transform.localScale = Vector3.one * 0.8f;
                    speedX1Group.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
                }
            }
        }

        // ── Settings Panel Control ─────────────────────────────────────────────

        private void OpenSettings()
        {
            if (settingsPanel == null) return;
            settingsPanel.SetActive(true);
            settingsPanel.transform.localScale = Vector3.zero;
            settingsPanel.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);

            // Pause the gameplay time
            Time.timeScale = 0f;
            GameManager.Instance?.SetState(GameState.Paused);
        }

        private void CloseSettings()
        {
            if (settingsPanel == null) return;
            settingsPanel.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() =>
                {
                    settingsPanel.SetActive(false);
                    // Resume the gameplay time
                    Time.timeScale = 1f;
                    GameManager.Instance?.SetState(GameState.Playing);
                });
        }

        // ── Win Panel Control ──────────────────────────────────────────────────

        private void ShowWinPanel()
        {
            if (winPanel == null) return;

            if (winCoinText != null && GameManager.Instance != null && GameManager.Instance.config != null)
                winCoinText.text = $"+{GameManager.Instance.config.winRewardCoins}";

            winPanel.SetActive(true);
            winPanel.transform.localScale = Vector3.zero;
            winPanel.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void OnNextLevel()
        {
            DOTween.KillAll();
            LevelManager.Instance?.LoadNextLevel();
        }

        private void OnHome()
        {
            DOTween.KillAll();
            LevelManager.Instance?.LoadMainMenu();
        }

        // ── Fail Panel Control ─────────────────────────────────────────────────

        private void ShowFailPanel()
        {
            if (failPanel == null) return;

            failPanel.SetActive(true);
            failPanel.transform.localScale = Vector3.zero;
            failPanel.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void OnRetryLevel()
        {
            DOTween.KillAll();
            LevelManager.Instance?.RestartLevel();
        }

        // ── Keep Playing Panel (Revival) Control ──────────────────────────────

        public void ShowKeepPlayingPanel()
        {
            if (keepPlayingPanel == null) return;
            
            keepPlayingPanel.SetActive(true);
            keepPlayingPanel.transform.localScale = Vector3.zero;
            keepPlayingPanel.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void OnPlayOnClicked()
        {
            int cost = 100;
            if (GameManager.Instance != null && GameManager.Instance.config != null)
            {
                cost = GameManager.Instance.config.playOnCost;
            }

            if (SaveManager.Coins >= cost)
            {
                SaveManager.Coins -= cost;
                UpdateCoinUI(animate: true);

                // Add an extra slot to let the user keep playing
                if (SlotSystem.Instance != null)
                {
                    SlotSystem.Instance.AddExtraSlot();
                }

                // Hide KeepPlaying panel
                keepPlayingPanel.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack).SetUpdate(true)
                    .OnComplete(() =>
                    {
                        keepPlayingPanel.SetActive(false);
                        
                        // Mark revived so they cannot revive again
                        HasRevivedThisLevel = true;

                        // Unfreeze conveyor & resume
                        if (ConveyorController.Instance != null)
                        {
                            ConveyorController.Instance.IsFrozen = false;
                        }
                    });
            }
            else
            {
                // Shake KeepPlaying window or button to show insufficient coins
                playOnButton?.transform.DOPunchPosition(Vector3.right * 15f, 0.3f, 5, 0.5f).SetUpdate(true);
            }
        }

        private void OnWatchAdClicked()
        {
            // Clickable but does nothing for now
        }

        private void OnDeclineKeepPlaying()
        {
            // Decline revive, hide panel and trigger actual level fail
            keepPlayingPanel.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() =>
                {
                    keepPlayingPanel.SetActive(false);
                    GameManager.Instance?.TriggerFail();
                });
        }
    }
}
