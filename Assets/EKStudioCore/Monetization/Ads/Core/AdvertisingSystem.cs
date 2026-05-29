#pragma warning disable 0649
#pragma warning disable 0162

using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;
using EKStudio.Monetization.Networks;

namespace EKStudio.Monetization
{
    [Define("MODULE_ADMOB", "GoogleMobileAds.Editor.GoogleMobileAdsSettings", new string[] { "Assets/GoogleMobileAds/GoogleMobileAds.dll" })]
    [Define("MODULE_UNITYADS", "UnityEngine.Advertisements.Advertisement", new string[] { "Packages/com.unity.ads/Runtime/Advertisement/Advertisement.cs" })]
    [Define("MODULE_LEVELPLAY", "Unity.Services.LevelPlay.LevelPlay", new string[] { "Unity.Services.LevelPlay" })]
    public static class AdvertisingSystem
    {
        // Next time we should attempt an auto interstitial request if it's not loaded
        private static float _nextInterstitialRequestTime = 0f;
        private const float AUTO_REQUEST_INTERVAL_SECONDS = 5f;
        private const int INIT_ATTEMPTS_AMOUNT = 30;
        private const string FIRST_LAUNCH_PREFS = "ek_initial_app_launch";
        private const string NO_ADS_PREF_NAME = "ek_advertisement_removal_state";
        private const string NO_ADS_ACTIVE_HASH = "7f3d9e8a2c1b4f6a9d5e8c7b2a4f6d9e";

        private static readonly AdNetworkProvider[] AD_PROVIDERS = new AdNetworkProvider[]
        {
            new MockAdNetwork(AdProvider.Mock),
#if MODULE_ADMOB
            new AdMobNetwork(AdProvider.AdMob),
#endif
#if MODULE_UNITYADS
            new UnityAdsNetwork(AdProvider.UnityAds),
#endif
#if MODULE_LEVELPLAY
            new LevelPlayNetwork(AdProvider.LevelPlay),
#endif
        };

        private static bool isModuleInitialised;
        private static AdConfiguration settings;
        private static double lastInterstitialTime;
        private static AdNetworkProvider.RewardedAdCallback RewardedAdCallback;
        private static AdNetworkProvider.InterstitialAdCallback interstitalCallback;
        private static List<System.Action> mainThreadEvents = new List<System.Action>();
        private static int mainThreadEventsCount;
        private static bool isFirstAdLoaded = false;
        private static bool waitingForRewardVideoCallback;
        private static bool isBannerActive = true;
        private static Coroutine loadingCoroutine;
        private static AdEventExecutor eventExecutor;
        private static bool areAdsEnabled;
        private static Dictionary<AdProvider, AdNetworkProvider> advertisingActiveModules = new Dictionary<AdProvider, AdNetworkProvider>();
        private static AdvertisingInitializer initModule;
        
        // Banner deferral across Loading scene
        private static bool _deferBannerUntilAfterLoading = false;
        private static bool _hookedSceneChangeForBanner = false;
        
        // Interstitial timing reset when leaving Loading scene
        private static bool _shouldResetInterstitialTimingOnSceneChange = false;
        private static bool _hookedSceneChangeForInterstitialTiming = false;
        private static bool _isFirstLaunchSession = false; // Track first launch for this app session

        // Events
        public static event System.Action ForcedAdDisabled;
        public static event System.Action<AdProvider> AdProviderInitialised;
        public static event System.Action<AdProvider, AdType> AdLoaded;
        public static event System.Action<AdProvider, AdType> AdDisplayed;
        public static event System.Action<AdProvider, AdType> AdClosed;
        public static event System.Func<bool> InterstitialConditions;

        public static AdConfiguration Settings => settings;
        public static AdvertisingInitializer InitModule => initModule;

        #region Initialization
        public static void Initialise(AdvertisingInitializer AdvertisingInitializer, bool loadOnStart)
        {
            if (isModuleInitialised)
            {
                if (AdvertisingInitializer?.Settings?.SystemLogs == true)
                    Debug.LogWarning("[AdvertisingSystem]: Module already exists!");
                return;
            }

            isModuleInitialised = true;
            isFirstAdLoaded = false;
            initModule = AdvertisingInitializer;
            settings = AdvertisingInitializer.Settings;
            
            // Check if ads should be enabled based on IAP or settings
            // Don't force enable ads - let the system decide based on user preferences
            areAdsEnabled = !PlayerPrefs.HasKey(NO_ADS_PREF_NAME) || 
                           PlayerPrefs.GetString(NO_ADS_PREF_NAME) != NO_ADS_ACTIVE_HASH;

            if (settings == null)
            {
                Debug.LogError("[AdvertisingSystem]: Settings don't exist!");
                return;
            }

            SetupInterstitialTiming();
            SetupActiveModules();
            ValidateSettings();
            InitialiseModules(loadOnStart);
        }

        private static void SetupInterstitialTiming()
        {
            // Check if this is the first app launch EVER (persisted across sessions)
            bool isFirstLaunch = !PlayerPrefs.HasKey(FIRST_LAUNCH_PREFS);
            
            // Save the first launch state for this session
            _isFirstLaunchSession = isFirstLaunch;
            
            // Mark as launched (do this once at the start)
            if (isFirstLaunch)
            {
                PlayerPrefs.SetInt(FIRST_LAUNCH_PREFS, 1);
                PlayerPrefs.Save();
            }
            
            // Check if we're in Loading scene - if so, defer timing setup until we leave
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool isInLoadingScene = !string.IsNullOrEmpty(activeScene.name) && activeScene.name == "Loading";
            
            if (isInLoadingScene)
            {
                // We're in Loading scene - timing will be set when leaving
                _shouldResetInterstitialTimingOnSceneChange = true;
                
                if (!_hookedSceneChangeForInterstitialTiming)
                {
                    UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChangedForInterstitialTiming;
                    _hookedSceneChangeForInterstitialTiming = true;
                }
                
                // Set a high placeholder value to prevent premature auto-show
                lastInterstitialTime = Time.realtimeSinceStartup + 9999f;
                
                if (settings.SystemLogs)
                    Debug.Log($"[AdvertisingSystem]: In Loading scene. Interstitial timing will be set after leaving. First launch: {isFirstLaunch}");
            }
            else
            {
                // Not in Loading scene - setup timing immediately
                float delay = _isFirstLaunchSession ? settings.InterstitialFirstStartDelay : settings.InterstitialStartDelay;
                
                if (delay <= 0)
                    lastInterstitialTime = 0;
                else
                    lastInterstitialTime = Time.realtimeSinceStartup + delay;
                
                if (settings.SystemLogs)
                    Debug.Log($"[AdvertisingSystem]: Interstitial timing set. First launch: {_isFirstLaunchSession}, Delay: {delay}s, Next show: {lastInterstitialTime}");
            }
        }

        private static void SetupActiveModules()
        {
            advertisingActiveModules = new Dictionary<AdProvider, AdNetworkProvider>();
            for (int i = 0; i < AD_PROVIDERS.Length; i++)
            {
                if (IsModuleEnabled(AD_PROVIDERS[i].ProviderType))
                {
                    advertisingActiveModules.Add(AD_PROVIDERS[i].ProviderType, AD_PROVIDERS[i]);
                }
            }
        }

        private static void ValidateSettings()
        {
            if (settings.SystemLogs)
            {
                if (settings.BannerType != AdProvider.Disable && !advertisingActiveModules.ContainsKey(settings.BannerType))
                    Debug.LogWarning($"[AdvertisingSystem]: Banner type ({settings.BannerType}) is selected, but isn't active!");

                if (settings.InterstitialType != AdProvider.Disable && !advertisingActiveModules.ContainsKey(settings.InterstitialType))
                    Debug.LogWarning($"[AdvertisingSystem]: Interstitial type ({settings.InterstitialType}) is selected, but isn't active!");

                if (settings.RewardedVideoType != AdProvider.Disable && !advertisingActiveModules.ContainsKey(settings.RewardedVideoType))
                    Debug.LogWarning($"[AdvertisingSystem]: Rewarded Video type ({settings.RewardedVideoType}) is selected, but isn't active!");
            }
        }

        private static void InitialiseModules(bool loadAds)
        {
            foreach (var advertisingModule in advertisingActiveModules.Keys)
            {
                InitialiseModule(advertisingModule);
            }

            if (loadAds)
            {
                TryToLoadFirstAds();
            }
        }

        private static void InitialiseModule(AdProvider advertisingModule)
        {
            if (advertisingActiveModules.ContainsKey(advertisingModule))
            {
                if (!advertisingActiveModules[advertisingModule].IsInitialised())
                {
                    if (settings.SystemLogs)
                        Debug.Log($"[AdvertisingSystem]: Module {advertisingModule} trying to initialise!");

                    advertisingActiveModules[advertisingModule].Initialise(settings);
                }
                else
                {
                    if (settings.SystemLogs)
                        Debug.Log($"[AdvertisingSystem]: Module {advertisingModule} is already initialised!");
                }
            }
            else
            {
                if (settings.SystemLogs)
                    Debug.LogWarning($"[AdvertisingSystem]: Module {advertisingModule} is disabled!");
            }
        }
        #endregion

        #region Public API
        public static bool IsInterstitialReady()
        {
            if (settings == null)
            {
                return false;
            }
            
            return IsInterstitialReady(settings.InterstitialType);
        }

        public static bool IsInterstitialReady(AdProvider advertisingModule)
        {
            if (!areAdsEnabled || !IsModuleActive(advertisingModule))
                return false;

            return advertisingActiveModules[advertisingModule].IsInterstitialReady();
        }

        public static void LoadInterstitialAd()
        {
            AdProvider advertisingModule = settings.InterstitialType;

            if (!areAdsEnabled || !IsModuleActive(advertisingModule) || 
                !advertisingActiveModules[advertisingModule].IsInitialised())
                return;

            // Always request new interstitial, even if one is already loaded
            // This ensures we have fresh ads for subsequent level transitions
            
            advertisingActiveModules[advertisingModule].LoadInterstitialAd();
        }

        public static void DisplayInterstitialAd(AdNetworkProvider.InterstitialAdCallback callback, bool ignoreConditions = false)
        {
            if (settings == null)
            {
                callback?.Invoke(false);
                return;
            }
            
            AdProvider advertisingModule = settings.InterstitialType;
            interstitalCallback = callback;

            // Never show interstitial in Loading scene (ads can initialize but not display)
            if (!ignoreConditions)
            {
                string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (!string.IsNullOrEmpty(currentSceneName) && currentSceneName == "Loading")
                {
                    ExecuteInterstitialCallback(false);
                    return;
                }
            }

            // Check if ads are enabled and module is active
            if (!areAdsEnabled || !IsModuleActive(advertisingModule) || 
                !advertisingActiveModules[advertisingModule].IsInitialised())
            {
                ExecuteInterstitialCallback(false);
                return;
            }

            // Check timing conditions only if not ignoring them
            if (!ignoreConditions && (!CheckInterstitialTime() || !CheckExtraInterstitialCondition()))
            {
                ExecuteInterstitialCallback(false);
                return;
            }

            // If ad is not loaded, try to request it first
            if (!advertisingActiveModules[advertisingModule].IsInterstitialReady())
            {
                LoadInterstitialAd();
                ExecuteInterstitialCallback(false);
                return;
            }

            advertisingActiveModules[advertisingModule].DisplayInterstitialAd(callback);
        }

        public static bool IsRewardBasedVideoLoaded()
        {
            AdProvider advertisingModule = settings.RewardedVideoType;

            if (!IsModuleActive(advertisingModule) || !advertisingActiveModules[advertisingModule].IsInitialised())
                return false;

            return advertisingActiveModules[advertisingModule].IsRewardedAdReady();
        }

        public static void RequestRewardBasedVideo()
        {
            AdProvider advertisingModule = settings.RewardedVideoType;

            if (!IsModuleActive(advertisingModule) || !advertisingActiveModules[advertisingModule].IsInitialised())
                return;

            // Always request new rewarded video, even if one is already loaded
            // This ensures we have fresh ads for subsequent requests
            
            advertisingActiveModules[advertisingModule].LoadRewardedAd();
        }

        public static void ShowRewardBasedVideo(AdNetworkProvider.RewardedAdCallback callback, bool showErrorMessage = true)
        {
            AdProvider advertisingModule = settings.RewardedVideoType;
            RewardedAdCallback = callback;
            waitingForRewardVideoCallback = true;

            if (!IsModuleActive(advertisingModule) || !advertisingActiveModules[advertisingModule].IsInitialised())
            {
                ExecuteRewardVideoCallback(false);
                if (showErrorMessage)
                    ShowErrorMessage();
                return;
            }

            // If ad is not loaded, try to request it first
            if (!advertisingActiveModules[advertisingModule].IsRewardedAdReady())
            {
                RequestRewardBasedVideo();
                ExecuteRewardVideoCallback(false);
                if (showErrorMessage)
                    ShowErrorMessage();
                return;
            }

            advertisingActiveModules[advertisingModule].DisplayRewardedAd(callback);
        }

        public static void DisplayBanner()
        {
            if (!isBannerActive) return;
            
            if (settings == null)
            {
                return;
            }

            AdProvider advertisingModule = settings.BannerType;

            if (!areAdsEnabled || !IsModuleActive(advertisingModule) || !advertisingActiveModules[advertisingModule].IsInitialised())
            {
                return;
            }

            advertisingActiveModules[advertisingModule].DisplayBanner();
        }

        public static void ConcealBanner()
        {
            AdProvider advertisingModule = settings.BannerType;

            if (!IsModuleActive(advertisingModule) || !advertisingActiveModules[advertisingModule].IsInitialised())
                return;

            advertisingActiveModules[advertisingModule].ConcealBanner();
        }

        public static void RemoveBanner()
        {
            AdProvider advertisingModule = settings.BannerType;

            if (!IsModuleActive(advertisingModule) || !advertisingActiveModules[advertisingModule].IsInitialised())
                return;

            advertisingActiveModules[advertisingModule].RemoveBanner();
        }

        public static void EnableBanner()
        {
            isBannerActive = true;
            DisplayBanner();
        }

        public static void DisableBanner()
        {
            isBannerActive = false;
            ConcealBanner();
        }
        #endregion

        #region Utility Methods
        public static bool IsModuleEnabled(AdProvider advertisingModule)
        {
            if (advertisingModule == AdProvider.Disable)
                return false;

            return (Settings.BannerType == advertisingModule || 
                    Settings.InterstitialType == advertisingModule || 
                    Settings.RewardedVideoType == advertisingModule);
        }

        public static bool IsModuleActive(AdProvider advertisingModule)
        {
            return advertisingActiveModules.ContainsKey(advertisingModule);
        }

        public static bool IsModuleInititalized(AdProvider advertisingModule)
        {
            if (advertisingActiveModules.ContainsKey(advertisingModule))
            {
                return advertisingActiveModules[advertisingModule].IsInitialised();
            }
            return false;
        }

        // Ensure a single AdEventExecutor instance exists
        private static void EnsureEventExecutor()
        {
            if (eventExecutor == null)
            {
                GameObject executorGO = new GameObject("AdvertisingEventExecutor");
                Object.DontDestroyOnLoad(executorGO);
                eventExecutor = executorGO.AddComponent<AdEventExecutor>();
            }
        }

        public static void CallEventInMainThread(System.Action callback)
        {
            if (callback != null)
            {
                EnsureEventExecutor();
                mainThreadEvents.Add(callback);
                mainThreadEventsCount++;
            }
        }

        public static void ShowErrorMessage()
        {
            if (settings != null && settings.SystemLogs)
                Debug.Log("[AdvertisingSystem]: Network error. Please try again later");
        }
        #endregion

        #region Event Handlers
        public static void OnProviderInitialised(AdProvider advertisingModule)
        {
            AdProviderInitialised?.Invoke(advertisingModule);

            // Auto-request interstitial as soon as its provider is initialized
            if (settings != null && advertisingModule == settings.InterstitialType && areAdsEnabled)
            {
                if (settings.SystemLogs)
                    Debug.Log("[AdvertisingSystem] Interstitial provider initialized, requesting interstitial immediately");
                LoadInterstitialAd();
            }
        }

        public static void OnProviderAdLoaded(AdProvider advertisingModule, AdType advertisingType)
        {
            AdLoaded?.Invoke(advertisingModule, advertisingType);
        }

        public static void OnProviderAdDisplayed(AdProvider advertisingModule, AdType advertisingType)
        {
            AdDisplayed?.Invoke(advertisingModule, advertisingType);

            if (advertisingType == AdType.Interstitial || advertisingType == AdType.RewardedVideo)
            {
                ResetInterstitialDelayTime();
            }
        }

        public static void OnProviderAdClosed(AdProvider advertisingModule, AdType advertisingType)
        {
            AdClosed?.Invoke(advertisingModule, advertisingType);

            if (advertisingType == AdType.Interstitial || advertisingType == AdType.RewardedVideo)
            {
                ResetInterstitialDelayTime();
            }
        }

        public static void ExecuteInterstitialCallback(bool result)
        {
            if (interstitalCallback != null)
            {
                var callback = interstitalCallback;
                interstitalCallback = null; // Clear immediately to prevent double-invoke
                CallEventInMainThread(() => callback.Invoke(result));
            }
        }

        public static void ExecuteRewardVideoCallback(bool result)
        {
            if (RewardedAdCallback != null && waitingForRewardVideoCallback)
            {
                var callback = RewardedAdCallback;
                RewardedAdCallback = null; // Clear immediately to prevent double-invoke
                waitingForRewardVideoCallback = false;
                CallEventInMainThread(() => callback.Invoke(result));
            }
        }
        #endregion

        #region IAP Integration
        public static bool AreAdsEnabled()
        {
            return areAdsEnabled;
        }

        public static void DisableAds()
        {
            PlayerPrefs.SetString(NO_ADS_PREF_NAME, NO_ADS_ACTIVE_HASH);
            areAdsEnabled = false;
            ForcedAdDisabled?.Invoke();
            RemoveBanner();
        }
        #endregion

        #region Internal Methods
        private static void Update()
        {
            if (mainThreadEventsCount > 0)
            {
                for (int i = 0; i < mainThreadEventsCount; i++)
                {
                    mainThreadEvents[i]?.Invoke();
                }
                mainThreadEvents.Clear();
                mainThreadEventsCount = 0;
            }

            // Auto-show interstitial timing check
            if (settings != null && settings.AutoShowInterstitial)
            {
                // Never auto-show in Loading scene
                string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                bool isLoadingScene = !string.IsNullOrEmpty(currentSceneName) && currentSceneName == "Loading";
                
                // Check if it's time to show interstitial
                if (!isLoadingScene && lastInterstitialTime < Time.realtimeSinceStartup)
                {
                    DisplayInterstitialAd(null);
                    ResetInterstitialDelayTime();
                }
            }

            // Auto-reload interstitials if not loaded yet (provider initialized & enabled)
            // This prevents missing interstitials on first tries due to slow init or no-fill retries
            if (settings != null)
            {
                AdProvider interstitialProvider = settings.InterstitialType;
                if (interstitialProvider != AdProvider.Disable && IsModuleActive(interstitialProvider))
                {
                    // Request again every AUTO_REQUEST_INTERVAL_SECONDS while not loaded
                    if (!advertisingActiveModules[interstitialProvider].IsInterstitialReady())
                    {
                        if (Time.realtimeSinceStartup >= _nextInterstitialRequestTime)
                        {
                            LoadInterstitialAd();
                            _nextInterstitialRequestTime = Time.realtimeSinceStartup + AUTO_REQUEST_INTERVAL_SECONDS;
                        }
                    }
                }
            }
        }

        public static void TryToLoadFirstAds()
        {
            if (loadingCoroutine == null)
            {
                // Reuse the same AdEventExecutor instance
                EnsureEventExecutor();
                loadingCoroutine = eventExecutor.StartCoroutine(TryToLoadAdsCoroutine());
            }
        }

        private static IEnumerator TryToLoadAdsCoroutine()
        {
            int initAttemps = 0;
            yield return new WaitForSeconds(1.0f);

            // Continue trying while ads not loaded AND we haven't exceeded max attempts
            while (!isFirstAdLoaded && initAttemps < INIT_ATTEMPTS_AMOUNT)
            {
                if (LoadFirstAds())
                    break;

                yield return new WaitForSeconds(1.0f * (initAttemps + 1));
                initAttemps++;
            }

            if (settings.SystemLogs)
            {
                if (isFirstAdLoaded)
                    Debug.Log("[AdvertisingSystem]: First ads have loaded!");
                else
                    Debug.LogWarning($"[AdvertisingSystem]: Failed to load ads after {initAttemps} attempts.");
            }
        }

        private static bool LoadFirstAds()
        {
            if (isFirstAdLoaded)
                return true;

            bool isRewardedVideoModuleInititalized = AdvertisingSystem.IsModuleInititalized(AdvertisingSystem.Settings.RewardedVideoType);
            bool isInterstitialModuleInitialized = AdvertisingSystem.IsModuleInititalized(AdvertisingSystem.Settings.InterstitialType);
            bool isBannerModuleInitialized = AdvertisingSystem.IsModuleInititalized(AdvertisingSystem.Settings.BannerType);

            bool isRewardedVideoActive = AdvertisingSystem.Settings.RewardedVideoType != AdProvider.Disable;
            bool isInterstitialActive = AdvertisingSystem.Settings.InterstitialType != AdProvider.Disable;
            bool isBannerActive = AdvertisingSystem.Settings.BannerType != AdProvider.Disable;

            if ((!isRewardedVideoActive || isRewardedVideoModuleInititalized) && 
                (!isInterstitialActive || isInterstitialModuleInitialized) && 
                (!isBannerActive || isBannerModuleInitialized))
            {
                if (isRewardedVideoActive)
                    AdvertisingSystem.RequestRewardBasedVideo();

                if (isInterstitialActive && areAdsEnabled)
                    AdvertisingSystem.LoadInterstitialAd();

                if (isBannerActive && areAdsEnabled)
                    ShowBannerOrDeferForLoading();

                isFirstAdLoaded = true;
                return true;
            }

            return false;
        }

            // Ensure banner never appears in Loading scene. If we're in Loading, defer display
            // until the next active scene, then show automatically once.
            private static void ShowBannerOrDeferForLoading()
            {
                try
                {
                    var active = SceneManager.GetActiveScene();
                    string sceneName = active.name;

                    if (!string.IsNullOrEmpty(sceneName) && sceneName == "Loading")
                    {
                        _deferBannerUntilAfterLoading = true;
                        if (!_hookedSceneChangeForBanner)
                        {
                            SceneManager.activeSceneChanged += OnActiveSceneChangedForBanner;
                            _hookedSceneChangeForBanner = true;
                        }
                        if (settings?.SystemLogs == true)
                            Debug.Log("[AdvertisingSystem]: Banner display deferred (will show after Loading scene)");
                        return;
                    }

                    // Not in Loading scene: show immediately
                    DisplayBanner();
                }
                catch (System.Exception e)
                {
                    if (settings?.SystemLogs == true)
                        Debug.LogWarning($"[AdvertisingSystem]: Banner show failed: {e.Message}");
                }
            }

            private static void OnActiveSceneChangedForBanner(Scene oldScene, Scene newScene)
            {
                if (!_deferBannerUntilAfterLoading)
                    return;

                // Skip if still in Loading
                if (!string.IsNullOrEmpty(newScene.name) && newScene.name == "Loading")
                    return;

                _deferBannerUntilAfterLoading = false;
                
                try
                {
                    DisplayBanner();
                    if (settings?.SystemLogs == true)
                        Debug.Log($"[AdvertisingSystem]: Banner displayed in scene '{newScene.name}'");
                }
                catch (System.Exception e)
                {
                    if (settings?.SystemLogs == true)
                        Debug.LogWarning($"[AdvertisingSystem]: Deferred banner show failed: {e.Message}");
                }
                finally
                {
                    if (_hookedSceneChangeForBanner)
                    {
                        SceneManager.activeSceneChanged -= OnActiveSceneChangedForBanner;
                        _hookedSceneChangeForBanner = false;
                    }
                }
            }

        private static void OnSceneChangedForInterstitialTiming(Scene oldScene, Scene newScene)
        {
            if (!_shouldResetInterstitialTimingOnSceneChange)
                return;

            // Skip if still in Loading scene
            if (!string.IsNullOrEmpty(newScene.name) && newScene.name == "Loading")
                return;

            _shouldResetInterstitialTimingOnSceneChange = false;

            // Set interstitial timing now that we've left Loading scene
            // Use the session-cached first launch value, not PlayerPrefs
            if (settings != null)
            {
                float delay = _isFirstLaunchSession ? settings.InterstitialFirstStartDelay : settings.InterstitialStartDelay;
                
                if (delay <= 0)
                    lastInterstitialTime = 0;
                else
                    lastInterstitialTime = Time.realtimeSinceStartup + delay;
                
                if (settings.SystemLogs)
                    Debug.Log($"[AdvertisingSystem]: Interstitial timing set after leaving Loading. First launch: {_isFirstLaunchSession}, Delay: {delay}s, Next show: {lastInterstitialTime}");
            }

            // Unhook event to prevent multiple triggers
            if (_hookedSceneChangeForInterstitialTiming)
            {
                SceneManager.activeSceneChanged -= OnSceneChangedForInterstitialTiming;
                _hookedSceneChangeForInterstitialTiming = false;
            }
        }

        private static bool CheckInterstitialTime()
        {
            if (settings.SystemLogs)
                Debug.Log($"[AdvertisingSystem]: Interstitial Time: {lastInterstitialTime}; Time.realtimeSinceStartup: {Time.realtimeSinceStartup}");

            // If lastInterstitialTime is 0, always allow interstitial ads
            if (lastInterstitialTime <= 0)
                return true;
                
            return lastInterstitialTime < Time.realtimeSinceStartup;
        }

        public static bool CheckExtraInterstitialCondition()
        {
            if (InterstitialConditions != null)
            {
                bool state = true;
                System.Delegate[] listDelegates = InterstitialConditions.GetInvocationList();
                for (int i = 0; i < listDelegates.Length; i++)
                {
                    if (!(bool)listDelegates[i].DynamicInvoke())
                    {
                        state = false;
                        break;
                    }
                }

                if (settings.SystemLogs)
                    Debug.Log($"[AdvertisingSystem]: Extra condition interstitial state: {state}");

                return state;
            }

            return true;
        }

        public static void SetInterstitialDelayTime(float time)
        {
            lastInterstitialTime = Time.realtimeSinceStartup + time;
        }

        public static void ResetInterstitialDelayTime()
        {
            // If interstitialShowingDelay is 0, set lastInterstitialTime to 0 so ads can show immediately
            if (settings.InterstitialShowingDelay <= 0)
            {
                lastInterstitialTime = 0;
            }
            else
            {
                lastInterstitialTime = Time.realtimeSinceStartup + settings.InterstitialShowingDelay;
            }
        }
        #endregion

        private class AdEventExecutor : MonoBehaviour
        {
            private void Update()
            {
                AdvertisingSystem.Update();
            }
            
            private void OnDestroy()
            {
                // Clean up when destroyed
                if (loadingCoroutine != null)
                {
                    StopCoroutine(loadingCoroutine);
                    loadingCoroutine = null;
                }
            }
        }
    }
}
