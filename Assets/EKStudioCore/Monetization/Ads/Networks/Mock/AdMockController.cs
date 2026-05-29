#pragma warning disable 0649

using UnityEngine;

namespace EKStudio.Monetization.Networks
{
    public class AdMockController : MonoBehaviour
    {
        [SerializeField] GameObject bannerObject;
        [SerializeField] GameObject interstitialObject;
        [SerializeField] GameObject rewardedVideoObject;

        private RectTransform bannerRectTransform;
        private AdNetworkProvider.InterstitialAdCallback interstitialCallback;
        private AdNetworkProvider.RewardedAdCallback RewardedAdCallback;

        private void Awake()
        {
            if (bannerObject != null)
            {
                bannerRectTransform = (RectTransform)bannerObject.transform;
                bannerObject.SetActive(true);
            }
            DontDestroyOnLoad(gameObject);
        }

        public void Initialise(AdConfiguration settings)
        {
            if (bannerRectTransform != null && settings.MockAdSettings != null)
            {
                switch (settings.MockAdSettings.bannerPosition)
                {
                    case BannerPosition.Bottom:
                        bannerRectTransform.pivot = new Vector2(0.5f, 0.0f);
                        bannerRectTransform.anchorMin = new Vector2(0.0f, 0.0f);
                        bannerRectTransform.anchorMax = new Vector2(1.0f, 0.0f);
                        bannerRectTransform.anchoredPosition = Vector2.zero;
                        break;
                    case BannerPosition.Top:
                        bannerRectTransform.pivot = new Vector2(0.5f, 1.0f);
                        bannerRectTransform.anchorMin = new Vector2(0.0f, 1.0f);
                        bannerRectTransform.anchorMax = new Vector2(1.0f, 1.0f);
                        bannerRectTransform.anchoredPosition = Vector2.zero;
                        break;
                }
            }
        }

        public void DisplayBanner()
        {
            if (bannerObject != null)
                bannerObject.SetActive(true);
        }

        public void ConcealBanner()
        {
            if (bannerObject != null)
                bannerObject.SetActive(false);
        }

        public void DisplayInterstitialAd()
        {
            if (interstitialObject != null)
                interstitialObject.SetActive(true);
        }

        public void CloseInterstitial()
        {
            if (interstitialObject != null)
                interstitialObject.SetActive(false);
            AdvertisingSystem.OnProviderAdClosed(AdProvider.Mock, AdType.Interstitial);
        }

        public void DisplayRewardedAd()
        {
            if (rewardedVideoObject != null)
                rewardedVideoObject.SetActive(true);
        }

        public void CloseRewardedVideo()
        {
            if (rewardedVideoObject != null)
                rewardedVideoObject.SetActive(false);
            AdvertisingSystem.OnProviderAdClosed(AdProvider.Mock, AdType.RewardedVideo);
        }

        #region Buttons
        public void CloseInterstitialButton()
        {
            AdvertisingSystem.ExecuteInterstitialCallback(true);
            CloseInterstitial();
        }

        public void CloseRewardedVideoButton()
        {
            if (RewardedAdCallback != null)
            {
                RewardedAdCallback.Invoke(false);
                RewardedAdCallback = null;
            }
            CloseRewardedVideo();
        }

        public void GetRewardButton()
        {
            Debug.Log($"[AdMockController] GetRewardButton clicked - callback: {RewardedAdCallback != null}");
            if (RewardedAdCallback != null)
            {
                RewardedAdCallback.Invoke(true);
                RewardedAdCallback = null;
            }
            CloseRewardedVideo();
        }
        #endregion

        #region Callback Management
        public void SetInterstitialCallback(AdNetworkProvider.InterstitialAdCallback callback)
        {
            interstitialCallback = callback;
        }
        
        public void SetRewardedVideoCallback(AdNetworkProvider.RewardedAdCallback callback)
        {
            RewardedAdCallback = callback;
        }

        public void OnInterstitialClosed()
        {
            if (interstitialCallback != null)
            {
                interstitialCallback.Invoke(true);
                interstitialCallback = null;
            }
            CloseInterstitial();
        }
        #endregion
    }
}
