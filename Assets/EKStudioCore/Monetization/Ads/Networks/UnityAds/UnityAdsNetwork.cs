#pragma warning disable 0414

using UnityEngine;

#if MODULE_UNITYADS
using UnityEngine.Advertisements;
#endif

namespace EKStudio.Monetization.Networks
{
#if MODULE_UNITYADS
    public class UnityAdsNetwork : AdNetworkProvider
    {
        private const int INIT_CHECK_MAX_ATTEMPT_AMOUNT = 5;

        private static string placementBannerID;
        private static string placementInterstitialID;
        private static string placementRewardedVideoID;
        private static string appId;

        private bool interstitialIsLoaded;
        private bool rewardVideoIsLoaded;
        private int initializationAttemptCount = 0;
        private UnityAdvertismentListener unityAdvertisment;

        public UnityAdsNetwork(AdProvider moduleType) : base(moduleType) { }

        public override void Initialise(AdConfiguration AdConfiguration)
        {
            this.AdConfiguration = AdConfiguration;

            if (isInitialised || Advertisement.isInitialized)
            {
                Debug.LogError("[AdvertisingSystem]: Unity Ads is already initialized!");
                return;
            }

            if (!Advertisement.isSupported)
            {
                Debug.LogError("[AdvertisingSystem]: Unity Ads isn't supported!");
                return;
            }

            placementBannerID = GetBannerID();
            placementInterstitialID = GetInterstitialID();
            placementRewardedVideoID = GetRewardedVideoID();
            appId = GetUnityAdsAppID();

            // Create dedicated GameObject for UnityAdvertismentListener (FIXED: was using random MonoBehaviour)
            GameObject listenerObject = new GameObject("UnityAdsListener");
            Object.DontDestroyOnLoad(listenerObject);
            unityAdvertisment = listenerObject.AddComponent<UnityAdvertismentListener>();
            unityAdvertisment.Init(AdConfiguration, this);

            Advertisement.Initialize(appId, AdConfiguration.TestMode, unityAdvertisment);
            Advertisement.Banner.SetPosition((UnityEngine.Advertisements.BannerPosition)AdConfiguration.UnityAdsSettings.BannerPosition);

            if (AdConfiguration.SystemLogs)
            {
                Debug.Log("[AdvertisingSystem]: Unity Ads initialized: " + Advertisement.isInitialized);
                Debug.Log("[AdvertisingSystem]: Unity Ads is supported: " + Advertisement.isSupported);
                Debug.Log("[AdvertisingSystem]: Unity Ads test mode enabled: " + Advertisement.debugMode);
                Debug.Log("[AdvertisingSystem]: Unity Ads version: " + Advertisement.version);
            }

            OnProviderInitialised();
        }

        public override void RemoveBanner()
        {
            Advertisement.Banner.Hide(true);
        }

        public override void ConcealBanner()
        {
            Advertisement.Banner.Hide(false);
        }

        public override void LoadInterstitialAd()
        {
            // Reset loaded state to ensure fresh ad loading
            interstitialIsLoaded = false;
            Advertisement.Load(placementInterstitialID, unityAdvertisment);
        }

        public override void LoadRewardedAd()
        {
            // Reset loaded state to ensure fresh ad loading
            rewardVideoIsLoaded = false;
            Advertisement.Load(placementRewardedVideoID, unityAdvertisment);
        }

        public override void DisplayBanner()
        {
            Advertisement.Banner.Show(placementBannerID);
        }

        public override void DisplayInterstitialAd(InterstitialAdCallback callback)
        {
            if (interstitialIsLoaded)
            {
                Advertisement.Show(placementInterstitialID, unityAdvertisment);
            }
            else
            {
                callback?.Invoke(false);
            }
        }

        public override void DisplayRewardedAd(RewardedAdCallback callback)
        {
            if (rewardVideoIsLoaded)
            {
                Advertisement.Show(placementRewardedVideoID, unityAdvertisment);
            }
            else
            {
                callback?.Invoke(false);
            }
        }

        public override bool IsInterstitialReady()
        {
            return interstitialIsLoaded;
        }

        public override bool IsRewardedAdReady()
        {
            return rewardVideoIsLoaded;
        }

        public string GetUnityAdsAppID()
        {
#if UNITY_ANDROID
            return AdConfiguration.UnityAdsSettings.AndroidAppID;
#elif UNITY_IOS
            return AdConfiguration.UnityAdsSettings.IOSAppID;
#else
            return string.Empty;
#endif
        }

        public string GetBannerID()
        {
#if UNITY_ANDROID
            return AdConfiguration.UnityAdsSettings.AndroidBannerID;
#elif UNITY_IOS
            return AdConfiguration.UnityAdsSettings.IOSBannerID;
#else
            return string.Empty;
#endif
        }

        public string GetInterstitialID()
        {
#if UNITY_ANDROID
            return AdConfiguration.UnityAdsSettings.AndroidInterstitialID;
#elif UNITY_IOS
            return AdConfiguration.UnityAdsSettings.IOSInterstitialID;
#else
            return string.Empty;
#endif
        }

        public string GetRewardedVideoID()
        {
#if UNITY_ANDROID
            return AdConfiguration.UnityAdsSettings.AndroidRewardedVideoID;
#elif UNITY_IOS
            return AdConfiguration.UnityAdsSettings.IOSRewardedVideoID;
#else
            return string.Empty;
#endif
        }

        private class UnityAdvertismentListener : MonoBehaviour, IUnityAdsLoadListener, IUnityAdsShowListener, IUnityAdsInitializationListener
        {
            private UnityAdsNetwork adsHandler;
            private AdConfiguration AdConfiguration;

            public void Init(AdConfiguration AdConfiguration, UnityAdsNetwork adsHandler)
            {
                this.AdConfiguration = AdConfiguration;
                this.adsHandler = adsHandler;
            }

            public void OnInitializationComplete()
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: Unity Ads is initialized!");
            }

            public void OnInitializationFailed(UnityAdsInitializationError error, string message)
            {
                adsHandler.initializationAttemptCount++;

                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: OnInitializationFailed event error:" + error.ToString() + "    message: " + message);

                if (adsHandler.initializationAttemptCount <= INIT_CHECK_MAX_ATTEMPT_AMOUNT)
                {
                    Advertisement.Initialize(appId, AdConfiguration.TestMode, this);
                }
                else
                {
                    if (AdConfiguration.SystemLogs)
                        Debug.Log("[AdvertisingSystem]: OnInitializationFailed in every attempt");
                }
            }

            public void OnUnityAdsAdLoaded(string placementId)
            {
                if (placementId.Equals(placementBannerID))
                {
                    Advertisement.Banner.Show(placementBannerID);
                    if (AdConfiguration.SystemLogs)
                        Debug.Log("[AdvertisingSystem]: OnUnityAdsAdLoaded - banner loaded");
                }
                else if (placementId.Equals(placementInterstitialID))
                {
                    adsHandler.interstitialIsLoaded = true;
                    if (AdConfiguration.SystemLogs)
                        Debug.Log("[AdvertisingSystem]: OnUnityAdsAdLoaded - interstitial loaded");
                }
                else if (placementId.Equals(placementRewardedVideoID))
                {
                    adsHandler.rewardVideoIsLoaded = true;
                    if (AdConfiguration.SystemLogs)
                        Debug.Log("[AdvertisingSystem]: OnUnityAdsAdLoaded - rewardVideo loaded");
                }
            }

            public void OnUnityAdsDidError(string message)
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: OnUnityAdsDidError - " + message);
            }

            public void OnUnityAdsDidFinish(string placementId, ShowResult showResult)
            {
                if (placementId == placementInterstitialID)
                {
                    AdvertisingSystem.OnProviderAdClosed(AdProvider.UnityAds, AdType.Interstitial);
                    AdvertisingSystem.ExecuteInterstitialCallback(showResult == ShowResult.Finished);
                    AdvertisingSystem.ResetInterstitialDelayTime();
                }
                else if (placementId == placementRewardedVideoID)
                {
                    bool state = showResult == ShowResult.Finished;
                    AdvertisingSystem.ExecuteRewardVideoCallback(state);
                    AdvertisingSystem.OnProviderAdClosed(AdProvider.UnityAds, AdType.RewardedVideo);
                    AdvertisingSystem.ResetInterstitialDelayTime();
                }

                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: OnUnityAdsDidFinish - " + placementId + ". Result - " + showResult);
            }

            public void OnUnityAdsDidStart(string placementId)
            {
                if (placementId == placementInterstitialID)
                {
                    AdvertisingSystem.OnProviderAdDisplayed(AdProvider.UnityAds, AdType.Interstitial);
                }
                else if (placementId == placementRewardedVideoID)
                {
                    AdvertisingSystem.OnProviderAdDisplayed(AdProvider.UnityAds, AdType.RewardedVideo);
                }

                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: OnUnityAdsDidStart - " + placementId);
            }

            public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
            {
                if (placementId == placementInterstitialID)
                {
                    adsHandler.interstitialIsLoaded = false;
                }
                else if (placementId == placementRewardedVideoID)
                {
                    adsHandler.rewardVideoIsLoaded = false;
                }

                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: OnUnityAdsFailedToLoad - " + placementId + ". Error - " + error + " .Message: " + message);
            }

            public void OnUnityAdsReady(string placementId)
            {
                if (placementId == placementBannerID)
                {
                    AdvertisingSystem.OnProviderAdLoaded(AdProvider.UnityAds, AdType.Banner);
                }
                else if (placementId == placementInterstitialID)
                {
                    AdvertisingSystem.OnProviderAdLoaded(AdProvider.UnityAds, AdType.Interstitial);
                }
                else if (placementId == placementRewardedVideoID)
                {
                    AdvertisingSystem.OnProviderAdLoaded(AdProvider.UnityAds, AdType.RewardedVideo);
                }

                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: OnUnityAdsReady - " + placementId);
            }

            public void OnUnityAdsShowClick(string placementId)
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: OnUnityAdsShowClick - " + placementId);
            }

            public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState showCompletionState)
            {
                if (placementId == placementInterstitialID)
                {
                    adsHandler.interstitialIsLoaded = false;
                    AdvertisingSystem.OnProviderAdClosed(AdProvider.UnityAds, AdType.Interstitial);
                    AdvertisingSystem.ExecuteInterstitialCallback(showCompletionState == UnityAdsShowCompletionState.COMPLETED);
                    AdvertisingSystem.ResetInterstitialDelayTime();
                    AdvertisingSystem.LoadInterstitialAd();
                }
                else if (placementId == placementRewardedVideoID)
                {
                    adsHandler.rewardVideoIsLoaded = false;
                    bool state = showCompletionState == UnityAdsShowCompletionState.COMPLETED;
                    AdvertisingSystem.ExecuteRewardVideoCallback(state);
                    AdvertisingSystem.OnProviderAdClosed(AdProvider.UnityAds, AdType.RewardedVideo);
                    AdvertisingSystem.ResetInterstitialDelayTime();
                    AdvertisingSystem.RequestRewardBasedVideo();
                }

                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: OnUnityAdsShowComplete - " + placementId + ". Result - " + showCompletionState);
            }

            public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: OnUnityAdsShowFailure - " + placementId + " - " + message);

                if (error == UnityAdsShowError.NOT_READY)
                {
                    if (placementId == placementInterstitialID)
                    {
                        AdvertisingSystem.LoadInterstitialAd();
                    }
                    else if (placementId == placementRewardedVideoID)
                    {
                        AdvertisingSystem.RequestRewardBasedVideo();
                    }
                }
            }

            public void OnUnityAdsShowStart(string placementId)
            {
                if (placementId == placementInterstitialID)
                {
                    AdvertisingSystem.OnProviderAdLoaded(AdProvider.UnityAds, AdType.Interstitial);
                }
                else if (placementId == placementRewardedVideoID)
                {
                    AdvertisingSystem.OnProviderAdLoaded(AdProvider.UnityAds, AdType.RewardedVideo);
                }

                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: OnUnityAdsShowStart - " + placementId);
            }
        }
    }
#endif
}
