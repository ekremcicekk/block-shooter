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
        private RectTransform _trackedUiTarget;
        private Vector3 _trackedWorldOffset;

        private void Awake()
        {
            Hide();
        }

        private void LateUpdate()
        {
            UpdatePosition();
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
            _trackedUiTarget = null;
            _trackedWorldOffset = Vector3.zero;
            
            if (_handRect != null)
            {
                var canvas = _handRect.GetComponentInParent<Canvas>();
                var cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
                var canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
                Vector2 localPoint;
                if (canvasRect != null)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, worldPos, cam, out localPoint);
                    _handRect.anchoredPosition = localPoint;
                }
                else
                {
                    _handRect.position = worldPos;
                }
                _handRect.gameObject.SetActive(true);
            }
        }

        public void ShowHandFollowTarget(TutorialTarget target, Vector3 worldOffset = default)
        {
            _trackedWorldTarget = null;
            _trackedUiTarget = null;
            _trackedWorldOffset = worldOffset;

            if (target != null)
            {
                if (target.UiTarget != null)
                {
                    _trackedUiTarget = target.UiTarget;
                }
                else
                {
                    _trackedWorldTarget = target.WorldTarget;
                }
            }

            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (_handRect == null) return;

            bool hasTarget = false;
            Vector2 screenPoint = Vector2.zero;

            var canvas = _handRect.GetComponentInParent<Canvas>();
            var cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

            if (_trackedUiTarget != null)
            {
                hasTarget = true;
                var targetCanvas = _trackedUiTarget.GetComponentInParent<Canvas>();
                Camera targetCam = null;
                if (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    targetCam = targetCanvas.worldCamera;
                }
                
                screenPoint = RectTransformUtility.WorldToScreenPoint(targetCam, _trackedUiTarget.position);
                screenPoint += (Vector2)_trackedWorldOffset;
            }
            else if (_trackedWorldTarget != null)
            {
                hasTarget = true;
                screenPoint = RectTransformUtility.WorldToScreenPoint(cam ?? Camera.main, _trackedWorldTarget.position + _trackedWorldOffset);
            }

            if (hasTarget)
            {
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
        }

        public void HideHand()
        {
            _trackedWorldTarget = null;
            _trackedUiTarget = null;
            _trackedWorldOffset = Vector3.zero;

            if (_handRect != null)
            {
                _handRect.gameObject.SetActive(false);
            }
        }
    }
}