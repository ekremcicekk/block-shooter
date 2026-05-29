using UnityEngine;

namespace EKStudio.Monetization
{
    public abstract class AdNetworkProvider
    {
        protected AdProvider providerType;
        public AdProvider ProviderType => providerType;

        protected AdConfiguration AdConfiguration;
        protected bool isInitialised = false;

        public AdNetworkProvider(AdProvider providerType)
        {
            this.providerType = providerType;
        }

        public bool IsInitialised() => isInitialised;

        protected void OnProviderInitialised()
        {
            isInitialised = true;
            AdvertisingSystem.OnProviderInitialised(providerType);
            
            if (AdConfiguration.SystemLogs)
                Debug.Log($"[AdvertisingSystem]: {providerType} is initialized!");
        }

        // Abstract methods that each provider must implement
        public abstract void Initialise(AdConfiguration AdConfiguration);
        public abstract void DisplayBanner();
        public abstract void ConcealBanner();
        public abstract void RemoveBanner();
        public abstract void LoadInterstitialAd();
        public abstract void DisplayInterstitialAd(InterstitialAdCallback callback);
        public abstract bool IsInterstitialReady();
        public abstract void LoadRewardedAd();
        public abstract void DisplayRewardedAd(RewardedAdCallback callback);
        public abstract bool IsRewardedAdReady();

        // Callback delegates
        public delegate void RewardedAdCallback(bool hasReward);
        public delegate void InterstitialAdCallback(bool isDisplayed);
    }
}
