using System.Collections.Generic;
using UnityEngine;

namespace EKStudio.Monetization.Networks
{
    [System.Serializable]
    public class AdMobSettings
    {
        public static readonly string ANDROID_BANNER_TEST_ID = "ca-app-pub-3940256099942544/6300978111";
        public static readonly string IOS_BANNER_TEST_ID = "ca-app-pub-3940256099942544/2934735716";
        public static readonly string ANDROID_INTERSTITIAL_TEST_ID = "ca-app-pub-3940256099942544/1033173712";
        public static readonly string IOS_INTERSTITIAL_TEST_ID = "ca-app-pub-3940256099942544/4411468910";
        public static readonly string ANDROID_REWARDED_VIDEO_TEST_ID = "ca-app-pub-3940256099942544/5224354917";
        public static readonly string IOS_REWARDED_VIDEO_TEST_ID = "ca-app-pub-3940256099942544/1712485313";

        [Header("Android Settings")]
        [SerializeField] string androidAppId;
        [SerializeField] string androidBannerID = ANDROID_BANNER_TEST_ID;
        [SerializeField] string androidInterstitialID = ANDROID_INTERSTITIAL_TEST_ID;
        [SerializeField] string androidRewardedVideoID = ANDROID_REWARDED_VIDEO_TEST_ID;

        [Header("iOS Settings")]
        [SerializeField] string iosAppId;
        [SerializeField] string iOSBannerID = IOS_BANNER_TEST_ID;
        [SerializeField] string iOSInterstitialID = IOS_INTERSTITIAL_TEST_ID;
        [SerializeField] string iOSRewardedVideoID = IOS_REWARDED_VIDEO_TEST_ID;

        [Header("Banner Settings")]
        [SerializeField] BannerPlacementType bannerType = BannerPlacementType.Banner;
        [SerializeField] BannerPosition bannerPosition = BannerPosition.Bottom;

        [Header("Test Settings")]
        [SerializeField] List<string> testDevicesIDs;

        // Android Properties
        public string AndroidAppId => androidAppId;
        public string AndroidBannerID => androidBannerID;
        public string AndroidInterstitialID => androidInterstitialID;
        public string AndroidRewardedVideoID => androidRewardedVideoID;

        // iOS Properties
        public string IOSAppId => iosAppId;
        public string IOSBannerID => iOSBannerID;
        public string IOSInterstitialID => iOSInterstitialID;
        public string IOSRewardedVideoID => iOSRewardedVideoID;

        // Banner Properties
        public BannerPlacementType BannerType => bannerType;
        public BannerPosition BannerPosition => bannerPosition;

        // Test Properties
        public List<string> TestDevicesIDs => testDevicesIDs;

        public enum BannerPlacementType
        {
            Banner = 0,
            MediumRectangle = 1,
            IABBanner = 2,
            Leaderboard = 3,
        }
    }
}
