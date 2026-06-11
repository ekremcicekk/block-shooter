using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Visual layer for tutorial hints.
    /// Keeps the prepared tutorial UI together and only shows/hides it.
    /// </summary>
    public class TutorialCanvasController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Tutorial UI Root")]
        [SerializeField] private GameObject _tutorialRoot;

        [Header("Optional Hand UI (screen-space)")]
        [SerializeField] private RectTransform _handRect;

        [Header("Spotlight Overlay")]
        [SerializeField] private TutorialSpotlightOverlay _spotlightOverlay;

        private Transform _trackedWorldTarget;
        private Vector3 _trackedWorldOffset;

        private void Awake()
        {
            Hide();
        }

        private void LateUpdate()
        {
            if (_trackedWorldTarget != null)
            {
                UpdateHandPosition(_trackedWorldTarget.position + _trackedWorldOffset);
            }
        }

        public void Show()
        {
            HideHand();

            if (_tutorialRoot != null)
            {
                _tutorialRoot.SetActive(true);
            }

            if (_spotlightOverlay != null)
            {
                _spotlightOverlay.gameObject.SetActive(true);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        public void Hide()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            if (_tutorialRoot != null)
            {
                _tutorialRoot.SetActive(false);
            }

            if (_spotlightOverlay != null)
            {
                _spotlightOverlay.gameObject.SetActive(false);
            }
            HideHand();
        }

        // Positions the hand RectTransform over a world-space position.
        public void ShowHandAtWorldPosition(Vector3 worldPos)
        {
            _trackedWorldTarget = null;
            _trackedWorldOffset = Vector3.zero;
            UpdateHandPosition(worldPos);
        }

        public void ShowHandFollowTarget(Transform worldTarget, Vector3 worldOffset = default)
        {
            _trackedWorldTarget = worldTarget;
            _trackedWorldOffset = worldOffset;

            if (_trackedWorldTarget != null)
            {
                UpdateHandPosition(_trackedWorldTarget.position + _trackedWorldOffset);
            }
        }

        private void UpdateHandPosition(Vector3 worldPos)
        {
            if (_handRect == null)
                return;

            var canvas = _handRect.GetComponentInParent<Canvas>();
            var cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam ?? Camera.main, worldPos);

            var canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            Vector2 localPoint;
            if (canvasRect != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, cam, out localPoint);
                _handRect.anchoredPosition = localPoint;
            }
            else
            {
                _handRect.position = screenPoint;
            }

            _handRect.gameObject.SetActive(true);
        }

        public void HideHand()
        {
            _trackedWorldTarget = null;
            _trackedWorldOffset = Vector3.zero;

            if (_handRect != null)
            {
                _handRect.gameObject.SetActive(false);
            }
        }
    }
}