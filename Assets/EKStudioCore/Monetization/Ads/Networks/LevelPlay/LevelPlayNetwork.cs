using UnityEngine;
using System;

namespace EKStudio.Monetization.Networks
{
#if MODULE_LEVELPLAY
    using Unity.Services.LevelPlay;

    public class LevelPlayNetwork : AdNetworkProvider
    {
        private const int RETRY_ATTEMPT_DEFAULT_VALUE = 1;
        private int interstitialRetryAttempt = RETRY_ATTEMPT_DEFAULT_VALUE;
        private int rewardedRetryAttempt = RETRY_ATTEMPT_DEFAULT_VALUE;
        
        private LevelPlayBannerAd bannerAd;
        private LevelPlayInterstitialAd interstitialAd;
        private LevelPlayRewardedAd rewardedAd;
        private InterstitialAdCallback currentInterstitialCallback;
        private RewardedAdCallback currentRewardedCallback;

        public LevelPlayNetwork(AdProvider moduleType) : base(moduleType) { }

        public override void Initialise(AdConfiguration AdConfiguration)
        {
            this.AdConfiguration = AdConfiguration;

            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay trying to initialise..");

            try
            {
                // Enable test suite if in test mode
                if (AdConfiguration.TestMode)
                {
                    LevelPlay.SetMetaData("is_test_suite", "enable");
                }
                
                // Register event listeners BEFORE initializing
                LevelPlay.OnInitSuccess += OnLevelPlayInitialized;
                LevelPlay.OnInitFailed += OnLevelPlayInitializationFailed;
                
                // Initialize LevelPlay SDK
                LevelPlay.Init(GetAppKey());
                
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: LevelPlay initialization started!");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AdvertisingSystem]: LevelPlay initialization failed: {e.Message}");
                OnProviderInitialised(); // Still call this to prevent hanging
            }
        }

        private void OnLevelPlayInitialized(LevelPlayConfiguration config)
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay SDK initialized successfully");
            OnProviderInitialised();
        }

        private void OnLevelPlayInitializationFailed(LevelPlayInitError error)
        {
            Debug.LogError($"[AdvertisingSystem]: LevelPlay initialization failed: {error}");
            OnProviderInitialised(); // Still call this to prevent hanging
        }

        private string GetAppKey()
        {
#if UNITY_ANDROID
            return AdConfiguration.LevelPlaySettings.AndroidAppKey;
#elif UNITY_IOS
            return AdConfiguration.LevelPlaySettings.IOSAppKey;
#else
            return AdConfiguration.LevelPlaySettings.AndroidAppKey; // Default to Android
#endif
        }

        public override void DisplayBanner()
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay DisplayBanner called");

            if (bannerAd == null)
            {
                string bannerId = GetBannerID();
                bannerAd = new LevelPlayBannerAd(bannerId);
                
                // Set up banner events
                bannerAd.OnAdLoaded += OnBannerLoaded;
                bannerAd.OnAdLoadFailed += OnBannerLoadFailed;
                bannerAd.OnAdDisplayed += OnBannerDisplayed;
                bannerAd.OnAdDisplayFailed += OnBannerDisplayFailed;
                bannerAd.OnAdClicked += OnBannerClicked;
                bannerAd.OnAdCollapsed += OnBannerCollapsed;
                
                // Load the banner
                bannerAd.LoadAd();
            }
            else
            {
                bannerAd.ShowAd();
                AdvertisingSystem.OnProviderAdDisplayed(AdProvider.LevelPlay, AdType.Banner);
            }
        }

        public override void ConcealBanner()
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay ConcealBanner called");

            if (bannerAd != null)
            {
                bannerAd.HideAd();
                AdvertisingSystem.OnProviderAdClosed(AdProvider.LevelPlay, AdType.Banner);
            }
        }

        public override void RemoveBanner()
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay RemoveBanner called");

            if (bannerAd != null)
            {
                bannerAd.DestroyAd();
                bannerAd = null;
                AdvertisingSystem.OnProviderAdClosed(AdProvider.LevelPlay, AdType.Banner);
            }
        }

        public override bool IsInterstitialReady()
        {
            return interstitialAd != null && interstitialAd.IsAdReady();
        }

        public override bool IsRewardedAdReady()
        {
            return rewardedAd != null && rewardedAd.IsAdReady();
        }

        public override void LoadInterstitialAd()
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay LoadInterstitialAd called");

            // Always create new interstitial ad to ensure fresh ad loading
            if (interstitialAd != null)
            {
                // Clean up existing ad
                interstitialAd.OnAdLoaded -= OnInterstitialLoaded;
                interstitialAd.OnAdLoadFailed -= OnInterstitialLoadFailed;
                interstitialAd.OnAdDisplayed -= OnInterstitialDisplayed;
                interstitialAd.OnAdDisplayFailed -= OnInterstitialDisplayFailed;
                interstitialAd.OnAdClicked -= OnInterstitialClicked;
                interstitialAd.OnAdClosed -= OnInterstitialClosed;
                interstitialAd = null;
            }

            string interstitialId = GetInterstitialID();
            interstitialAd = new LevelPlayInterstitialAd(interstitialId);
            
            // Set up interstitial events
            interstitialAd.OnAdLoaded += OnInterstitialLoaded;
            interstitialAd.OnAdLoadFailed += OnInterstitialLoadFailed;
            interstitialAd.OnAdDisplayed += OnInterstitialDisplayed;
            interstitialAd.OnAdDisplayFailed += OnInterstitialDisplayFailed;
            interstitialAd.OnAdClicked += OnInterstitialClicked;
            interstitialAd.OnAdClosed += OnInterstitialClosed;
            
            // Load the interstitial
            interstitialAd.LoadAd();
        }

        public override void DisplayInterstitialAd(InterstitialAdCallback callback)
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay DisplayInterstitialAd called");

            currentInterstitialCallback = callback;

            if (interstitialAd != null && interstitialAd.IsAdReady())
            {
                interstitialAd.ShowAd();
                AdvertisingSystem.OnProviderAdDisplayed(AdProvider.LevelPlay, AdType.Interstitial);
            }
            else
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: LevelPlay interstitial ad not ready");
                callback?.Invoke(false);
            }
        }

        public override void LoadRewardedAd()
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay LoadRewardedAd called");

            // Always create new rewarded ad to ensure fresh ad loading
            if (rewardedAd != null)
            {
                // Clean up existing ad
                rewardedAd.OnAdLoaded -= OnRewardedLoaded;
                rewardedAd.OnAdLoadFailed -= OnRewardedLoadFailed;
                rewardedAd.OnAdDisplayed -= OnRewardedDisplayed;
                rewardedAd.OnAdDisplayFailed -= OnRewardedDisplayFailed;
                rewardedAd.OnAdRewarded -= OnRewardedRewarded;
                rewardedAd.OnAdClicked -= OnRewardedClicked;
                rewardedAd.OnAdClosed -= OnRewardedClosed;
                rewardedAd = null;
            }

            string rewardedId = GetRewardedVideoID();
            rewardedAd = new LevelPlayRewardedAd(rewardedId);
            
            // Set up rewarded video events
            rewardedAd.OnAdLoaded += OnRewardedLoaded;
            rewardedAd.OnAdLoadFailed += OnRewardedLoadFailed;
            rewardedAd.OnAdDisplayed += OnRewardedDisplayed;
            rewardedAd.OnAdDisplayFailed += OnRewardedDisplayFailed;
            rewardedAd.OnAdRewarded += OnRewardedRewarded;
            rewardedAd.OnAdClicked += OnRewardedClicked;
            rewardedAd.OnAdClosed += OnRewardedClosed;
            
            // Load the rewarded video
            rewardedAd.LoadAd();
        }

        public override void DisplayRewardedAd(RewardedAdCallback callback)
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay DisplayRewardedAd called");

            if (rewardedAd != null && rewardedAd.IsAdReady())
            {
                rewardedAd.ShowAd();
                AdvertisingSystem.OnProviderAdDisplayed(AdProvider.LevelPlay, AdType.RewardedVideo);
                
                // Store callback for reward event
                currentRewardedCallback = callback;
            }
            else
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: LevelPlay rewarded ad not ready");
                callback?.Invoke(false);
            }
        }

        private string GetBannerID()
        {
#if UNITY_ANDROID
            return AdConfiguration.LevelPlaySettings.AndroidBannerID;
#elif UNITY_IOS
            return AdConfiguration.LevelPlaySettings.IOSBannerID;
#else
            return AdConfiguration.LevelPlaySettings.AndroidBannerID; // Default to Android
#endif
        }

        private string GetBannerPosition()
        {
            // Convert BannerPosition enum to LevelPlay position string
            switch (AdConfiguration.LevelPlaySettings.BannerPosition)
            {
                case BannerPosition.Top:
                    return "TOP";
                case BannerPosition.Bottom:
                default:
                    return "BOTTOM";
            }
        }

        private string GetInterstitialID()
        {
#if UNITY_ANDROID
            return AdConfiguration.LevelPlaySettings.AndroidInterstitialID;
#elif UNITY_IOS
            return AdConfiguration.LevelPlaySettings.IOSInterstitialID;
#else
            return AdConfiguration.LevelPlaySettings.AndroidInterstitialID; // Default to Android
#endif
        }

        private string GetRewardedVideoID()
        {
#if UNITY_ANDROID
            return AdConfiguration.LevelPlaySettings.AndroidRewardedVideoID;
#elif UNITY_IOS
            return AdConfiguration.LevelPlaySettings.IOSRewardedVideoID;
#else
            return AdConfiguration.LevelPlaySettings.AndroidRewardedVideoID; // Default to Android
#endif
        }

        #region Banner Event Handlers
        private void OnBannerLoaded(LevelPlayAdInfo adInfo)
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay banner loaded");
            AdvertisingSystem.OnProviderAdLoaded(AdProvider.LevelPlay, AdType.Banner);
        }

        private void OnBannerLoadFailed(LevelPlayAdError error)
        {
            Debug.LogError($"[AdvertisingSystem]: LevelPlay banner load failed: {error}");
        }

        private void OnBannerDisplayed(LevelPlayAdInfo adInfo)
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay banner displayed");
        }

        private void OnBannerDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            Debug.LogError($"[AdvertisingSystem]: LevelPlay banner display failed: {error}");
        }

        private void OnBannerClicked(LevelPlayAdInfo adInfo)
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay banner clicked");
        }

        private void OnBannerCollapsed(LevelPlayAdInfo adInfo)
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay banner collapsed");
            AdvertisingSystem.OnProviderAdClosed(AdProvider.LevelPlay, AdType.Banner);
        }
        #endregion

        #region Interstitial Event Handlers
        private void OnInterstitialLoaded(LevelPlayAdInfo adInfo)
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay interstitial loaded");
            AdvertisingSystem.OnProviderAdLoaded(AdProvider.LevelPlay, AdType.Interstitial);
        }

        private void OnInterstitialLoadFailed(LevelPlayAdError error)
        {
            Debug.LogError($"[AdvertisingSystem]: LevelPlay interstitial load failed: {error}");
        }

        private void OnInterstitialDisplayed(LevelPlayAdInfo adInfo)
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay interstitial displayed");
        }

        private void OnInterstitialDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            Debug.LogError($"[AdvertisingSystem]: LevelPlay interstitial display failed: {error}");
            currentInterstitialCallback?.Invoke(false);
            currentInterstitialCallback = null;
        }

        private void OnInterstitialClicked(LevelPlayAdInfo adInfo)
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay interstitial clicked");
        }

        private void OnInterstitialClosed(LevelPlayAdInfo adInfo)
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay interstitial closed");
            AdvertisingSystem.OnProviderAdClosed(AdProvider.LevelPlay, AdType.Interstitial);
            
            currentInterstitialCallback?.Invoke(true);
            currentInterstitialCallback = null;
            
            // Reset delay time and immediately request new interstitial
            AdvertisingSystem.ResetInterstitialDelayTime();
            
            // Force request new interstitial ad immediately
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay requesting new interstitial after ad closed");
            LoadInterstitialAd();
        }
        #endregion

        #region Rewarded Video Event Handlers
        private void OnRewardedLoaded(LevelPlayAdInfo adInfo)
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay rewarded video loaded");
            AdvertisingSystem.OnProviderAdLoaded(AdProvider.LevelPlay, AdType.RewardedVideo);
        }

        private void OnRewardedLoadFailed(LevelPlayAdError error)
        {
            Debug.LogError($"[AdvertisingSystem]: LevelPlay rewarded video load failed: {error}");
        }

        private void OnRewardedDisplayed(LevelPlayAdInfo adInfo)
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay rewarded video displayed");
        }

        private void OnRewardedDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            Debug.LogError($"[AdvertisingSystem]: LevelPlay rewarded video display failed: {error}");
            currentRewardedCallback?.Invoke(false);
            currentRewardedCallback = null;
        }

        private void OnRewardedRewarded(LevelPlayAdInfo adInfo, LevelPlayReward adReward)
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log($"[AdvertisingSystem]: LevelPlay rewarded video rewarded: {adReward}");
            
            // Only invoke callback if it hasn't been called yet
            if (currentRewardedCallback != null)
            {
                currentRewardedCallback.Invoke(true);
                currentRewardedCallback = null;
            }
            
            AdvertisingSystem.ResetInterstitialDelayTime();
            AdvertisingSystem.RequestRewardBasedVideo();
        }

        private void OnRewardedClicked(LevelPlayAdInfo adInfo)
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay rewarded video clicked");
        }

        private void OnRewardedClosed(LevelPlayAdInfo adInfo)
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay rewarded video closed");
            AdvertisingSystem.OnProviderAdClosed(AdProvider.LevelPlay, AdType.RewardedVideo);
            
            // If reward hasn't been given yet, give it now (user watched the full ad)
            if (currentRewardedCallback != null)
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: LevelPlay giving reward on ad close");
                currentRewardedCallback.Invoke(true);
                currentRewardedCallback = null;
            }
            
            // Reset delay time and immediately request new rewarded video
            AdvertisingSystem.ResetInterstitialDelayTime();
            
            // Force request new rewarded video ad immediately
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: LevelPlay requesting new rewarded video after ad closed");
            LoadRewardedAd();
        }
        #endregion
    }
#endif
}