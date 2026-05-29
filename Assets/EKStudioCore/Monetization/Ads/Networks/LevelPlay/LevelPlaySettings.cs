using UnityEngine;

namespace EKStudio.Monetization.Networks
{
    [System.Serializable]
    public class LevelPlaySettings
    {
        [Header("Android Settings")]
        [SerializeField] string androidAppKey = "your_android_app_key";
        [SerializeField] string androidBannerID = "DefaultBanner";
        [SerializeField] string androidInterstitialID = "DefaultInterstitial";
        [SerializeField] string androidRewardedVideoID = "DefaultRewardedVideo";
        
        [Header("iOS Settings")]
        [SerializeField] string iOSAppKey = "your_ios_app_key";
        [SerializeField] string iOSBannerID = "DefaultBanner";
        [SerializeField] string iOSInterstitialID = "DefaultInterstitial";
        [SerializeField] string iOSRewardedVideoID = "DefaultRewardedVideo";
        
        [Header("Banner Settings")]
        [SerializeField] BannerPosition bannerPosition = BannerPosition.Bottom;
        [SerializeField] BannerPlacementType bannerType = BannerPlacementType.Banner;

        public string AndroidAppKey => androidAppKey;
        public string IOSAppKey => iOSAppKey;
        public string AndroidBannerID => androidBannerID;
        public string IOSBannerID => iOSBannerID;
        public string AndroidInterstitialID => androidInterstitialID;
        public string IOSInterstitialID => iOSInterstitialID;
        public string AndroidRewardedVideoID => androidRewardedVideoID;
        public string IOSRewardedVideoID => iOSRewardedVideoID;
        public BannerPosition BannerPosition => bannerPosition;
        public BannerPlacementType BannerType => bannerType;

        public enum BannerPlacementType
        {
            Banner = 0,
            Large = 1,
            Rectangle = 2,
            Smart = 3
        }
    }
}
