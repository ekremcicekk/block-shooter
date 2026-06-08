using UnityEngine;
using TMPro;
using DG.Tweening;

namespace BlockShooter
{
    /// <summary>
    /// Singleton manager attached to the Canvas to handle displaying warning popups
    /// with smooth DOTween scale and fade animations.
    /// </summary>
    public class WarningManager : MonoBehaviour
    {
        public static WarningManager Instance { get; private set; }

        [Header("UI References")]
        [Tooltip("The main container Panel of the warning popup (should have a CanvasGroup component).")]
        public GameObject warningPanel;

        [Tooltip("The centered box/window RectTransform that will scale up/down.")]
        public RectTransform warningBox;

        [Tooltip("The TMPro text component showing the warning message.")]
        public TextMeshProUGUI warningText;

        [Header("Animation Settings")]
        public float showDuration = 0.25f;
        public float delayDuration = 1.5f;
        public float hideDuration = 0.25f;

        private CanvasGroup _canvasGroup;
        private Sequence _activeSequence;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Ensure the panel starts disabled
            if (warningPanel != null)
            {
                _canvasGroup = warningPanel.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = warningPanel.AddComponent<CanvasGroup>();
                }
                warningPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Displays a warning popup with the specified message.
        /// Handles overriding any active warnings smoothly.
        /// </summary>
        public void ShowWarning(string message)
        {
            if (warningPanel == null || warningText == null || warningBox == null || _canvasGroup == null)
            {
                Debug.LogWarning("[WarningManager] References are not fully assigned!");
                return;
            }

            // Stop any active warning animation sequence
            if (_activeSequence != null && _activeSequence.IsActive())
            {
                _activeSequence.Kill();
            }

            // Kill existing tweens on the objects
            warningBox.DOKill(true);
            _canvasGroup.DOKill(true);

            // Set message and prepare initial animation state
            warningText.text = message;
            _canvasGroup.alpha = 0f;
            warningBox.localScale = Vector3.zero;
            warningPanel.SetActive(true);

            // Create a new sequence for the animation
            _activeSequence = DOTween.Sequence();

            // 1. Fade in and Scale up
            _activeSequence.Append(_canvasGroup.DOFade(1f, showDuration).SetEase(Ease.OutQuad));
            _activeSequence.Join(warningBox.DOScale(Vector3.one, showDuration).SetEase(Ease.OutBack));

            // 2. Wait for delay
            _activeSequence.AppendInterval(delayDuration);

            // 3. Fade out and Scale down
            _activeSequence.Append(_canvasGroup.DOFade(0f, hideDuration).SetEase(Ease.InQuad));
            _activeSequence.Join(warningBox.DOScale(Vector3.zero, hideDuration).SetEase(Ease.InBack));

            // 4. Cleanup
            _activeSequence.OnComplete(() =>
            {
                warningPanel.SetActive(false);
            });

            // Ensure update mode is independent of Time.timeScale (works when paused)
            _activeSequence.SetUpdate(true);
        }
    }
}
