using UnityEngine;
using UnityEngine.UI;

namespace BlockShooter
{
    /// <summary>
    /// Attached to a full-screen dark UI Image.
    /// Uses the Custom/UISpotlightMask shader to cut a circular hole around the active tutorial target in local space.
    /// Resolution, DPI, and Canvas Scale independent.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class TutorialSpotlightOverlay : MonoBehaviour
    {
        [Header("Spotlight Config")]
        [Tooltip("Target radius of the focus hole in local UI coordinates.")]
        [SerializeField] private float _targetRadius = 75f;
        
        [Tooltip("Softness/feather width of the spotlight edge in local UI coordinates.")]
        [SerializeField] private float _feather = 30f;
        
        [Tooltip("How fast the spotlight shrinks/focuses onto the target.")]
        [SerializeField] private float _focusSpeed = 8f;
        
        [Tooltip("How fast the dark overlay fades in and out.")]
        [SerializeField] private float _fadeSpeed = 5f;

        private Image _image;
        private RectTransform _rectTransform;
        private Material _instancedMaterial;
        private float _currentRadius = 1000f;
        private float _currentAlpha = 0f;
        private Vector2 _currentLocalPos;
        private bool _isInitialized;

        private static readonly int SpotlightProp = Shader.PropertyToID("_SpotlightCenter");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private Color _baseOverlayColor;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized) return;
            
            _image = GetComponent<Image>();
            _rectTransform = transform as RectTransform;
            
            if (_image != null && _image.material != null)
            {
                // Create a dynamic instance of the material to avoid modifying the asset on disk
                _instancedMaterial = new Material(_image.material);
                _image.material = _instancedMaterial;
                _baseOverlayColor = _instancedMaterial.GetColor(ColorProp);
            }
            
            _currentRadius = Screen.width + Screen.height; // Start fully open
            _currentAlpha = 0f;
            SetAlpha(0f);
            
            _isInitialized = true;
        }

        private void OnDestroy()
        {
            if (_instancedMaterial != null)
            {
                Destroy(_instancedMaterial);
            }
        }

        private void OnEnable()
        {
            Initialize();
            // Start fully open
            _currentRadius = Screen.width + Screen.height;
            _currentAlpha = 0f;
            SetAlpha(0f);
        }

        private void Update()
        {
            if (_instancedMaterial == null || _rectTransform == null) return;

            bool isTutorialRunning = TutorialManager.Instance != null && 
                                     TutorialManager.Instance.IsRunning && 
                                     TutorialManager.Instance.ActiveTarget != null;

            if (isTutorialRunning)
            {
                TutorialTarget target = TutorialManager.Instance.ActiveTarget;
                Vector3 targetWorldPos = target.GetWorldPosition();
                
                // Get the UI canvas camera or null if overlay
                var canvas = _rectTransform.GetComponentInParent<Canvas>();
                var cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

                // Project 3D target coordinates to 2D Screen pixel coordinates
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam ?? Camera.main, targetWorldPos);
                
                // If it's a UI target, we project its UI world position instead
                if (target.UiTarget != null && target.UiTarget != target.transform)
                {
                    var targetCanvas = target.UiTarget.GetComponentInParent<Canvas>();
                    var targetCam = (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? targetCanvas.worldCamera : null;
                    screenPoint = RectTransformUtility.WorldToScreenPoint(targetCam ?? Camera.main, target.UiTarget.position);
                }

                // Convert screen coordinates to local coordinate space of our overlay RectTransform
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, screenPoint, cam, out Vector2 targetLocalPos);

                // If starting a new focus, snap the position or transition smoothly
                if (_currentAlpha < 0.05f)
                {
                    _currentLocalPos = targetLocalPos;
                    _currentRadius = Screen.width + Screen.height; // Start wide
                }
                else
                {
                    _currentLocalPos = Vector2.Lerp(_currentLocalPos, targetLocalPos, Time.deltaTime * _focusSpeed);
                }

                // Smoothly focus spotlight radius and fade in overlay alpha
                _currentRadius = Mathf.Lerp(_currentRadius, _targetRadius, Time.deltaTime * _focusSpeed);
                _currentAlpha = Mathf.Lerp(_currentAlpha, 1f, Time.deltaTime * _fadeSpeed);
            }
            else
            {
                // Fade out overlay and open spotlight radius back up
                _currentRadius = Mathf.Lerp(_currentRadius, Screen.width + Screen.height, Time.deltaTime * _focusSpeed);
                _currentAlpha = Mathf.Lerp(_currentAlpha, 0f, Time.deltaTime * _fadeSpeed);
            }

            // Apply calculated values to the material properties in local space
            SetAlpha(_currentAlpha);
            _instancedMaterial.SetVector(SpotlightProp, new Vector4(_currentLocalPos.x, _currentLocalPos.y, _currentRadius, _feather));
        }

        private void SetAlpha(float alpha)
        {
            if (_image != null)
            {
                // Enable or disable the component based on alpha to optimize drawing and raycasts
                _image.enabled = (alpha > 0.01f);
                
                Color col = _baseOverlayColor;
                col.a *= alpha;
                _instancedMaterial.SetColor(ColorProp, col);
            }
        }
    }
}
