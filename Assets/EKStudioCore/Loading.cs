using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using EKStudio.Monetization;
using EKStudio.IAP;

namespace EKStudio
{
    public class Loading : MonoBehaviour
    {


        [Header("Ads Configuration")]
        [Tooltip("Assign AdConfiguration from Inspector for guaranteed initialization.\nLeave empty to auto-load from Resources as fallback.")]
        public AdConfiguration adConfiguration;

        [Header("IAP Configuration")]
        [Tooltip("Assign IAPSettings from Inspector (located in Project Files/Data/)")]
        public IAPSettings iapSettings;

        [Header("Loading Timing")]
        [Tooltip("Minimum time to wait before starting game (seconds)\nGame will proceed when BOTH loading time is complete AND GDPR consent is given (if required)")]
        [Range(0f, 10f)]
        public float loadingDuration = 4f;

        [Header("UI References")]
        [Tooltip("Optional: Slider UI element to show progress")]
        public UnityEngine.UI.Slider progressSlider;

        [Tooltip("Optional: Image UI element (filled type) to show progress")]
        public UnityEngine.UI.Image progressImage;

        [Tooltip("Optional: TMP_Text UI element to show progress percentage")]
        public TMPro.TMP_Text progressText;

        [Header("Events")]
        [Tooltip("Fired when the loading completes and the target scene is about to be loaded.")]
        public UnityEngine.Events.UnityEvent OnLoadingComplete;

        private bool adsInitialized = false;
        private bool loadingTimeComplete = false;
        private bool gdprConsentGiven = false;
        private bool isWaitingForGDPR = false;
        private float currentProgress = 0f;

        void Start()
        {
            // Initialize progress UI to 0
            UpdateProgressUI(0f);

            // Initialize Ads with hybrid approach (Inspector + Resources fallback)
            InitializeAdsManager();

            // Initialize IAP System
            InitializeIAPManager();

            // Check if GDPR consent dialog will be shown
            CheckAndWaitForGDPRConsent();
        }

        void Update()
        {
            if (!loadingTimeComplete)
            {
                if (loadingDuration > 0f)
                {
                    currentProgress += Time.deltaTime / loadingDuration;
                }
                else
                {
                    currentProgress = 1f;
                }

                currentProgress = Mathf.Clamp01(currentProgress);
                UpdateProgressUI(currentProgress);

                if (currentProgress >= 1f)
                {
                    OnLoadingTimeComplete();
                }
            }
        }

        private void UpdateProgressUI(float progress)
        {
            if (progressSlider != null)
                progressSlider.value = progress;

            if (progressImage != null)
                progressImage.fillAmount = progress;

            if (progressText != null)
                progressText.text = $"Loading... {Mathf.RoundToInt(progress * 100)}%";
        }

        /// <summary>
        /// Called when loading timer/progress reaches 100%
        /// Attempts to proceed if all conditions are met
        /// </summary>
        private void OnLoadingTimeComplete()
        {
            loadingTimeComplete = true;
            Debug.Log($"[Loading] ⏱ Loading time complete ({loadingDuration}s)");

            // Try to start game if all conditions met
            TryStartGame();
        }

        /// <summary>
        /// Check if GDPR dialog will be shown and wait for user consent
        /// If GDPR is not enabled or already shown, proceed immediately
        /// </summary>
        private void CheckAndWaitForGDPRConsent()
        {
            // If AdConfiguration not loaded, proceed without GDPR check
            if (adConfiguration == null)
            {
                Debug.Log("[Loading] AdConfiguration not available, GDPR check skipped");
                gdprConsentGiven = true;
                TryStartGame();
                return;
            }

            // Check if GDPR dialog should be shown
            bool shouldShowGDPR = GDPRManager.ShouldShowGDPRDialog(adConfiguration);

            if (shouldShowGDPR)
            {
                // GDPR dialog will be shown
                // Subscribe to dialog closed event and wait for user consent
                isWaitingForGDPR = true;
                GDPRCanvas.OnGDPRDialogClosed += OnGDPRConsentReceived;
                TryShowGDPRDialog();

                Debug.Log("[Loading] ⏳ Waiting for GDPR consent...");
            }
            else
            {
                // GDPR already shown or not enabled, mark as complete
                gdprConsentGiven = true;
                Debug.Log("[Loading] ✓ GDPR consent not required or already given");
                TryStartGame();
            }
        }

        /// <summary>
        /// Called when user accepts GDPR consent
        /// Attempts to proceed if all conditions are met
        /// </summary>
        private void OnGDPRConsentReceived()
        {
            // Unsubscribe from event
            GDPRCanvas.OnGDPRDialogClosed -= OnGDPRConsentReceived;

            gdprConsentGiven = true;
            isWaitingForGDPR = false;

            Debug.Log("[Loading] ✓ GDPR consent received");

            // Try to start game if all conditions met
            TryStartGame();
        }

        /// <summary>
        /// Instantiates and initializes GDPR dialog
        /// </summary>
        private void TryShowGDPRDialog()
        {
            if (adConfiguration == null)
            {
                Debug.LogWarning("[Loading] ⚠ Cannot show GDPR: AdConfiguration is null");
                return;
            }
            if (adConfiguration.GDPRCanvasPrefab == null)
            {
                Debug.LogWarning("[Loading] ⚠ Cannot show GDPR: GDPRCanvasPrefab is not assigned in AdConfiguration");
                return;
            }
            var instance = Instantiate(adConfiguration.GDPRCanvasPrefab);
            DontDestroyOnLoad(instance.gameObject);
            instance.Initialize(adConfiguration);
            Debug.Log("[Loading] 🛡 GDPR dialog shown");
        }

        /// <summary>
        /// Attempts to start the game if all conditions are met:
        /// - Loading time is complete
        /// - GDPR consent is given (or not required)
        /// </summary>
        private void TryStartGame()
        {
            // Check if both conditions are met
            if (loadingTimeComplete && gdprConsentGiven)
            {
                Debug.Log("[Loading] ✅ All conditions met, starting game...");

                // Now that GDPR is accepted, show banner ad
                ShowBannerAfterConsent();

                StartGame();
            }
            else
            {
                // Log what we're still waiting for
                if (!loadingTimeComplete)
                    Debug.Log($"[Loading] ⏳ Still waiting for loading time ({loadingDuration}s)...");
                if (!gdprConsentGiven)
                    Debug.Log("[Loading] ⏳ Still waiting for GDPR consent...");
            }
        }

        /// <summary>
        /// Shows banner ad after GDPR consent is given
        /// Banner will persist across all scenes via DontDestroyOnLoad
        /// </summary>
        private void ShowBannerAfterConsent()
        {
            if (adConfiguration == null)
                return;

            if (adConfiguration.BannerType != AdProvider.Disable)
            {
                Debug.Log($"[Loading] 📢 GDPR accepted, showing banner (Type: {adConfiguration.BannerType})...");
                // Use new AdvertisingSystem API
                AdvertisingSystem.DisplayBanner();
                Debug.Log("[Loading] ✓ Banner displayed");
            }
            else
            {
                Debug.Log("[Loading] Banner Type is Disable, skipping banner");
            }
        }

        void OnDestroy()
        {
            // Make sure to unsubscribe to prevent memory leaks
            if (isWaitingForGDPR)
            {
                GDPRCanvas.OnGDPRDialogClosed -= OnGDPRConsentReceived;
            }
        }

        /// <summary>
        /// IAP Initialization System:
        /// 1. Check if IAPBootstrapper already initialized (fallback)
        /// 2. Use Inspector reference (primary - from Project Files/Data/)
        /// 3. Manual initialization with DontDestroyOnLoad
        /// </summary>
        private void InitializeIAPManager()
        {
            // Check if IAPManager already exists (from IAPBootstrapper)
            if (IAPManager.Instance != null)
            {
                Debug.Log("[Loading] ✓ IAPManager already initialized");

                // If settings assigned in Inspector, override existing settings
                if (iapSettings != null && IAPManager.Instance.iapSettings != iapSettings)
                {
                    IAPManager.Instance.iapSettings = iapSettings;
                    Debug.Log("[Loading] ℹ IAPSettings updated from Inspector reference");
                }

                return;
            }

            // Validate Inspector reference
            if (iapSettings == null)
            {
                Debug.LogWarning("[Loading] ⚠ IAPSettings not assigned in Inspector!\n" +
                                 "Assign IAPSettings from 'Project Files/Data/' in Loading scene Inspector.\n" +
                                 "IAP System will not be available.");
                return;
            }

            Debug.Log("[Loading] ✓ IAPSettings assigned from Inspector");

            // Create IAPManager manually
            GameObject iapGO = new GameObject("IAPManager(Loading)");
            DontDestroyOnLoad(iapGO);

            IAPManager iapManager = iapGO.AddComponent<IAPManager>();
            iapManager.iapSettings = iapSettings;

            Debug.Log("[Loading] ✅ IAPManager created and will persist across scenes");
        }

        /// <summary>
        /// Hybrid Ads Initialization System:
        /// 1. Check if AdsBootstrapper already initialized (primary)
        /// 2. Use Inspector reference if assigned (secondary)
        /// 3. Try to load from Resources as fallback (tertiary)
        /// </summary>
        private void InitializeAdsManager()
        {
            // Check if AdvertisingSystem already initialized
            var existingModule = Object.FindFirstObjectByType<AdvertisingInitializer>();
            if (existingModule != null)
            {
                Debug.Log("[Loading] ✓ AdvertisingSystem already initialized by Bootstrapper");
                adsInitialized = true;
                return;
            }

            // Load AdConfiguration (Inspector reference preferred, then Resources as fallback)
            if (adConfiguration == null)
            {
                // Try common resource names
                adConfiguration = Resources.Load<AdConfiguration>("AdConfiguration");
                if (adConfiguration == null)
                    adConfiguration = Resources.Load<AdConfiguration>("AdsSettings");
                if (adConfiguration == null)
                {
                    Debug.LogWarning("[Loading] ⚠ AdConfiguration not assigned in Inspector and not found in Resources!\n" +
                                     "Assign AdConfiguration in Loading scene Inspector or place it in a Resources folder.");
                    return;
                }
                else
                {
                    Debug.Log("[Loading] ℹ AdConfiguration loaded from Resources (fallback)");
                }
            }
            else
            {
                Debug.Log("[Loading] ✓ AdConfiguration assigned from Inspector");
            }

            // Create initializer for AdvertisingSystem manually
            GameObject adsInitGO = new GameObject("AdvertisingInitializer(Loading)");
            DontDestroyOnLoad(adsInitGO);
            var initializer = adsInitGO.AddComponent<AdvertisingInitializer>();
            initializer.Settings = adConfiguration;
            initializer.LoadAdOnStart = true;

            adsInitialized = true;
            Debug.Log("[Loading] ✓ AdvertisingSystem initialized manually (will persist across scenes)");
        }

        public void StartGame()
        {
            OnLoadingComplete?.Invoke();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        }
    }
}
