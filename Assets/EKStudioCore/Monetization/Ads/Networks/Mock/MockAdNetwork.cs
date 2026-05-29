using UnityEngine;

namespace EKStudio.Monetization.Networks
{
    public class MockAdNetwork : AdNetworkProvider
    {
        private AdMockController mockController;
        private bool isInterstitialLoaded = false;
        private bool isRewardVideoLoaded = false;

        public MockAdNetwork(AdProvider providerType) : base(providerType) { }

        public override void Initialise(AdConfiguration AdConfiguration)
        {
            this.AdConfiguration = AdConfiguration;

            if (AdConfiguration.SystemLogs)
                Debug.Log("[AdvertisingSystem]: Module " + providerType.ToString() + " has initialized!");

            if (AdConfiguration.IsMockEnabled())
            {
                GameObject mockCanvasPrefab = AdvertisingSystem.InitModule?.MockCanvasPrefab;
                GameObject mockCanvas;
                
                if (mockCanvasPrefab != null)
                {
                    // Create from prefab
                    mockCanvas = GameObject.Instantiate(mockCanvasPrefab);
                    mockCanvas.transform.position = Vector3.zero;
                    mockCanvas.transform.localScale = Vector3.one;
                    mockCanvas.transform.rotation = Quaternion.identity;
                }
                else
                {
                    // Create at runtime
                    mockCanvas = MockAdsCanvasCreator.CreateMockAdsCanvas();
                    Debug.Log("[AdvertisingSystem]: Mock canvas created at runtime");
                }

                mockController = mockCanvas.GetComponent<AdMockController>();
                if (mockController != null)
                {
                    mockController.Initialise(AdConfiguration);
                }
                else
                {
                    Debug.LogError("[AdvertisingSystem]: Mock controller component not found!");
                }
            }

            OnProviderInitialised();
        }

        public override void DisplayBanner()
        {
            mockController?.DisplayBanner();
            AdvertisingSystem.OnProviderAdDisplayed(providerType, AdType.Banner);
        }

        public override void ConcealBanner()
        {
            mockController?.ConcealBanner();
            AdvertisingSystem.OnProviderAdClosed(providerType, AdType.Banner);
        }

        public override void RemoveBanner()
        {
            mockController?.ConcealBanner();
            AdvertisingSystem.OnProviderAdClosed(providerType, AdType.Banner);
        }

        public override void LoadInterstitialAd()
        {
            isInterstitialLoaded = true;
            AdvertisingSystem.OnProviderAdLoaded(providerType, AdType.Interstitial);
        }

        public override bool IsInterstitialReady()
        {
            return isInterstitialLoaded;
        }

        public override void DisplayInterstitialAd(InterstitialAdCallback callback)
        {
            mockController?.DisplayInterstitialAd();
            AdvertisingSystem.OnProviderAdDisplayed(providerType, AdType.Interstitial);
            

            // Store callback for when user closes the ad
            if (mockController != null)
            {
                mockController.SetInterstitialCallback(callback);
            }
        }

        public override void LoadRewardedAd()
        {
            isRewardVideoLoaded = true;
            AdvertisingSystem.OnProviderAdLoaded(providerType, AdType.RewardedVideo);
        }

        public override bool IsRewardedAdReady()
        {
            return isRewardVideoLoaded;
        }

        public override void DisplayRewardedAd(RewardedAdCallback callback)
        {
            mockController?.DisplayRewardedAd();
            AdvertisingSystem.OnProviderAdDisplayed(providerType, AdType.RewardedVideo);
            
            // Store callback for when user clicks Get Reward or Close button
            if (mockController != null)
            {
                mockController.SetRewardedVideoCallback(callback);
            }
        }
    }
}
