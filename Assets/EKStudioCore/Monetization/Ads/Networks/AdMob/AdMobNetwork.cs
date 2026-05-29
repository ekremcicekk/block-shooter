using UnityEngine;
using System.Collections.Generic;

#if MODULE_ADMOB
using GoogleMobileAds.Api;
#endif

namespace EKStudio.Monetization.Networks
{
#if MODULE_ADMOB
    public class AdMobNetwork : AdNetworkProvider
    {
        private const int RETRY_ATTEMPT_DEFAULT_VALUE = 1;
        private int interstitialRetryAttempt = RETRY_ATTEMPT_DEFAULT_VALUE;
        private int rewardedRetryAttempt = RETRY_ATTEMPT_DEFAULT_VALUE;

        private BannerView bannerView;
        private InterstitialAd interstitial;
        private RewardedAd rewardBasedVideo;
        private InterstitialAdCallback currentInterstitialCallback;

#if UNITY_EDITOR
        private GameObject editorInterstitialCanvas;
#endif

        public AdMobNetwork(AdProvider providerType) : base(providerType) { }

        public override void Initialise(AdConfiguration AdConfiguration)
        {
            this.AdConfiguration = AdConfiguration;

            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: AdMob is trying to initialize!");

#if UNITY_IOS
            MobileAds.SetiOSAppPauseOnBackground(true);
#endif

            RequestConfiguration requestConfiguration = new RequestConfiguration()
            {
                TagForChildDirectedTreatment = TagForChildDirectedTreatment.Unspecified,
                TestDeviceIds = AdConfiguration.AdMobSettings.TestDevicesIDs
            };

            MobileAds.SetRequestConfiguration(requestConfiguration);
            MobileAds.Initialize(InitCompleteAction);
        }

        private void InitCompleteAction(InitializationStatus initStatus)
        {
            GoogleMobileAds.Common.MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                OnProviderInitialised();
            });
        }

        public override void RemoveBanner()
        {
            if (bannerView != null)
                bannerView.Destroy();
        }

        public override void ConcealBanner()
        {
            if (bannerView != null)
                bannerView.Hide();
        }

        public override void LoadInterstitialAd()
        {
            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdMobNetwork] LoadInterstitialAd called");

            // Always create new interstitial ad to ensure fresh ad loading
            if (interstitial != null)
            {
                // Clean up existing ad
                interstitial.OnAdFullScreenContentOpened -= HandleInterstitialOpened;
                interstitial.OnAdFullScreenContentClosed -= HandleInterstitialClosed;
                interstitial.OnAdClicked -= HandleInterstitialClicked;
                interstitial.Destroy();
                interstitial = null;
            }

            InterstitialAd.Load(GetInterstitialID(), GetAdRequest(), (InterstitialAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    if (AdConfiguration.SystemLogs)
                        Debug.Log("[AdvertisingSystem]: Interstitial ad failed to load an ad with error: " + error);

                    interstitialRetryAttempt++;
                    return;
                }

                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: Interstitial ad loaded with response: " + ad.GetResponseInfo());

                interstitial = ad;
                interstitialRetryAttempt = RETRY_ATTEMPT_DEFAULT_VALUE;
                
                // Pre-load only: register callbacks but do NOT show immediately.
                // Show will be called separately when DisplayInterstitialAd() is invoked.
                interstitial.OnAdFullScreenContentOpened += HandleInterstitialOpened;
                interstitial.OnAdFullScreenContentClosed += HandleInterstitialClosed;
                interstitial.OnAdClicked += HandleInterstitialClicked;
                
                AdvertisingSystem.OnProviderAdLoaded(providerType, AdType.Interstitial);
            });
        }

        public override void LoadRewardedAd()
        {
            // Always create new rewarded video ad to ensure fresh ad loading
            if (rewardBasedVideo != null)
            {
                // Clean up existing ad
                rewardBasedVideo.OnAdFullScreenContentFailed -= HandleRewardBasedVideoFailedToShow;
                rewardBasedVideo.OnAdFullScreenContentOpened -= HandleRewardBasedVideoOpened;
                rewardBasedVideo.OnAdFullScreenContentClosed -= HandleRewardBasedVideoClosed;
                rewardBasedVideo.OnAdClicked -= HandleRewardBasedVideoClicked;
                rewardBasedVideo.Destroy();
                rewardBasedVideo = null;
            }

            RewardedAd.Load(GetRewardedVideoID(), GetAdRequest(), (RewardedAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    AdvertisingSystem.ExecuteRewardVideoCallback(false);
                    if (AdConfiguration.SystemLogs)
                        Debug.Log("[AdvertisingSystem]: HandleRewardBasedVideoFailedToLoad event received with message: " + error);
                    rewardedRetryAttempt++;
                    return;
                }

                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: Rewarded ad loaded with response: " + ad.GetResponseInfo());

                rewardedRetryAttempt = RETRY_ATTEMPT_DEFAULT_VALUE;
                AdvertisingSystem.OnProviderAdLoaded(providerType, AdType.RewardedVideo);

                rewardBasedVideo = ad;
                rewardBasedVideo.OnAdFullScreenContentFailed += HandleRewardBasedVideoFailedToShow;
                rewardBasedVideo.OnAdFullScreenContentOpened += HandleRewardBasedVideoOpened;
                rewardBasedVideo.OnAdFullScreenContentClosed += HandleRewardBasedVideoClosed;
                rewardBasedVideo.OnAdClicked += HandleRewardBasedVideoClicked;
            });
        }

        private void RequestBanner()
        {
            if (bannerView != null)
            {
                bannerView.Destroy();
            }

            AdSize adSize = AdSize.Banner;
            switch (AdConfiguration.AdMobSettings.BannerType)
            {
                case AdMobSettings.BannerPlacementType.Banner:
                    adSize = AdSize.Banner;
                    break;
                case AdMobSettings.BannerPlacementType.MediumRectangle:
                    adSize = AdSize.MediumRectangle;
                    break;
                case AdMobSettings.BannerPlacementType.IABBanner:
                    adSize = AdSize.IABBanner;
                    break;
                case AdMobSettings.BannerPlacementType.Leaderboard:
                    adSize = AdSize.Leaderboard;
                    break;
            }

            AdPosition adPosition = AdPosition.Bottom;
            switch (AdConfiguration.AdMobSettings.BannerPosition)
            {
                case BannerPosition.Bottom:
                    adPosition = AdPosition.Bottom;
                    break;
                case BannerPosition.Top:
                    adPosition = AdPosition.Top;
                    break;
            }

            string bannerID = GetBannerID();
            
            bannerView = new BannerView(bannerID, adSize, adPosition);

            bannerView.OnBannerAdLoaded += HandleAdLoaded;
            bannerView.OnBannerAdLoadFailed += HandleAdFailedToLoad;
            bannerView.OnAdPaid += HandleAdPaid;
            bannerView.OnAdClicked += HandleAdClicked;
            bannerView.OnAdFullScreenContentClosed += HandleAdClosed;

            bannerView.LoadAd(GetAdRequest());
        }

        public override void DisplayBanner()
        {
            if (bannerView == null)
            {
                RequestBanner();
            }

            if (bannerView != null)
            {
                bannerView.Show();
            }
        }

        public override void DisplayInterstitialAd(InterstitialAdCallback callback)
        {
            currentInterstitialCallback = callback;
            if (interstitial != null && interstitial.CanShowAd())
            {
                interstitial.Show();
            }
            else
            {
                callback?.Invoke(false);
            }
        }


        public override void DisplayRewardedAd(RewardedAdCallback callback)
        {
            if (rewardBasedVideo != null && rewardBasedVideo.CanShowAd())
            {
                rewardBasedVideo.Show((GoogleMobileAds.Api.Reward reward) =>
                {
                    AdvertisingSystem.CallEventInMainThread(delegate
                    {
                        AdvertisingSystem.OnProviderAdDisplayed(providerType, AdType.RewardedVideo);
                        callback?.Invoke(true);
                        AdvertisingSystem.ExecuteRewardVideoCallback(true);
                        AdvertisingSystem.ResetInterstitialDelayTime();
                        AdvertisingSystem.RequestRewardBasedVideo();
                    });
                });
            }
            else
            {
                callback?.Invoke(false);
            }
        }

        public override bool IsInterstitialReady()
        {
            return interstitial != null && interstitial.CanShowAd();
        }

        public override bool IsRewardedAdReady()
        {
            return rewardBasedVideo != null && rewardBasedVideo.CanShowAd();
        }

        public AdRequest GetAdRequest()
        {
            return new AdRequest();
        }

        #region Banner Callbacks
        public void HandleAdLoaded()
        {
            AdvertisingSystem.CallEventInMainThread(delegate
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: HandleAdLoaded event received");
                AdvertisingSystem.OnProviderAdLoaded(providerType, AdType.Banner);
            });
        }

        public void HandleAdFailedToLoad(LoadAdError error)
        {
            AdvertisingSystem.CallEventInMainThread(delegate
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: HandleFailedToReceiveAd event received with message: " + error.GetMessage());
            });
        }

        private void HandleAdPaid(AdValue adValue)
        {
            AdvertisingSystem.CallEventInMainThread(delegate
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: HandleAdPaid event received");
            });
        }

        public void HandleAdClicked()
        {
            AdvertisingSystem.CallEventInMainThread(delegate
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: HandleAdClicked event received");
            });
        }

        public void HandleAdClosed()
        {
            AdvertisingSystem.CallEventInMainThread(delegate
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: HandleAdClosed event received");
                AdvertisingSystem.OnProviderAdClosed(providerType, AdType.Banner);
            });
        }
        #endregion

        #region Interstitial Callback
        public void HandleInterstitialOpened()
        {
            AdvertisingSystem.CallEventInMainThread(delegate
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: HandleInterstitialOpened event received");
                AdvertisingSystem.OnProviderAdDisplayed(providerType, AdType.Interstitial);

#if UNITY_EDITOR
                // Show Editor mock interstitial UI
                ShowEditorInterstitialMock();
#endif
            });
        }

        public void HandleInterstitialClosed()
        {
#if UNITY_EDITOR
            // Resume game in Editor (AdMob SDK pauses it automatically)
            Time.timeScale = 1f;
            
            // Clean up Editor mock if it exists
            if (editorInterstitialCanvas != null)
            {
                Object.Destroy(editorInterstitialCanvas);
                editorInterstitialCanvas = null;
            }
#endif
            
            AdvertisingSystem.CallEventInMainThread(delegate
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: HandleInterstitialClosed event received");
                AdvertisingSystem.OnProviderAdClosed(providerType, AdType.Interstitial);
                
                // Call the specific callback if available
                if (currentInterstitialCallback != null)
                {
                    currentInterstitialCallback.Invoke(true);
                    currentInterstitialCallback = null;
                }
                else
                {
                    // Fallback to general callback
                    AdvertisingSystem.ExecuteInterstitialCallback(true);
                }
                
                AdvertisingSystem.ResetInterstitialDelayTime();
                AdvertisingSystem.LoadInterstitialAd();
            });
        }

        private void HandleInterstitialClicked()
        {
            AdvertisingSystem.CallEventInMainThread(delegate
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: HandleInterstitialClicked event received");
            });
        }
        #endregion

        #region RewardedVideo Callback
        private void HandleRewardBasedVideoFailedToShow(AdError error)
        {
            AdvertisingSystem.CallEventInMainThread(delegate
            {
                AdvertisingSystem.ExecuteRewardVideoCallback(false);
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: HandleRewardBasedVideoFailedToShow event received with message: " + error);
                rewardedRetryAttempt++;
            });
        }

        public void HandleRewardBasedVideoOpened()
        {
            AdvertisingSystem.CallEventInMainThread(delegate
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: HandleRewardBasedVideoOpened event received");
            });
        }

        public void HandleRewardBasedVideoClosed()
        {
            AdvertisingSystem.CallEventInMainThread(delegate
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: HandleRewardBasedVideoClosed event received");
                AdvertisingSystem.OnProviderAdClosed(providerType, AdType.RewardedVideo);
            });
        }

        private void HandleRewardBasedVideoClicked()
        {
            AdvertisingSystem.CallEventInMainThread(delegate
            {
                if (AdConfiguration.SystemLogs)
                    Debug.Log("[AdvertisingSystem]: HandleRewardBasedVideoClicked event received");
            });
        }
        #endregion

        public string GetBannerID()
        {
            // Runtime platform check for Editor
            if (Application.isEditor)
                return "ca-app-pub-3940256099942544/6300978111"; // Google test banner ID

#if UNITY_ANDROID
            return AdConfiguration.AdMobSettings.AndroidBannerID;
#elif UNITY_IOS
            return AdConfiguration.AdMobSettings.IOSBannerID;
#else
            return "ca-app-pub-3940256099942544/6300978111"; // AdMob test banner ID
#endif
        }

        public string GetInterstitialID()
        {
            // Runtime platform check to ensure Editor always returns test ID
            if (Application.isEditor)
                return "ca-app-pub-3940256099942544/1033173712"; // Google test interstitial ID

#if UNITY_ANDROID
            return AdConfiguration.AdMobSettings.AndroidInterstitialID;
#elif UNITY_IOS
            return AdConfiguration.AdMobSettings.IOSInterstitialID;
#else
            return "unexpected_platform";
#endif
        }

        public string GetRewardedVideoID()
        {
            // Runtime platform check for Editor
            if (Application.isEditor)
                return "ca-app-pub-3940256099942544/5224354917"; // Google test rewarded ID

#if UNITY_ANDROID
            return AdConfiguration.AdMobSettings.AndroidRewardedVideoID;
#elif UNITY_IOS
            return AdConfiguration.AdMobSettings.IOSRewardedVideoID;
#else
            return "ca-app-pub-3940256099942544/5224354917"; // AdMob test rewarded video ID
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Displays a mock interstitial UI in Unity Editor for testing purposes.
        /// AdMob SDK does not render interstitial ads in Editor, so this provides a visual placeholder.
        /// </summary>
        private void ShowEditorInterstitialMock()
        {
            if (editorInterstitialCanvas != null)
            {
                Object.Destroy(editorInterstitialCanvas);
            }

            // Create fullscreen canvas
            editorInterstitialCanvas = new GameObject("EditorInterstitialMock");
            Object.DontDestroyOnLoad(editorInterstitialCanvas);

            Canvas canvas = editorInterstitialCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            UnityEngine.UI.CanvasScaler scaler = editorInterstitialCanvas.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            editorInterstitialCanvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Background panel
            GameObject bgPanel = new GameObject("Background");
            bgPanel.transform.SetParent(editorInterstitialCanvas.transform, false);
            
            UnityEngine.UI.Image bgImage = bgPanel.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            
            RectTransform bgRect = bgPanel.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // Test Ad Text
            GameObject textObj = new GameObject("TestAdText");
            textObj.transform.SetParent(bgPanel.transform, false);
            
            UnityEngine.UI.Text adText = textObj.AddComponent<UnityEngine.UI.Text>();
            adText.text = "TEST INTERSTITIAL AD\n(Editor Mock)\n\nClick to close";
            adText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            adText.fontSize = 48;
            adText.alignment = TextAnchor.MiddleCenter;
            adText.color = Color.white;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            // Close button (entire canvas is clickable)
            UnityEngine.UI.Button closeButton = bgPanel.AddComponent<UnityEngine.UI.Button>();
            closeButton.onClick.AddListener(() =>
            {
                // Resume game (AdMob SDK pauses game automatically)
                Time.timeScale = 1f;
                
                Object.Destroy(editorInterstitialCanvas);
                editorInterstitialCanvas = null;
                
                // Trigger close event manually
                HandleInterstitialClosed();
            });
        }
#endif
    }
#endif
}
