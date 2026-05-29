using UnityEngine;
using EKStudio.Monetization.Networks;

namespace EKStudio.Monetization
{
    [CreateAssetMenu(fileName = "Ads Settings", menuName = "EKStudio/Monetization/Ad Configuration")]
    public class AdConfiguration : ScriptableObject
    {
        [Header("Provider Selection")]
        [SerializeField] AdProvider bannerType = AdProvider.Mock;
        [SerializeField] AdProvider interstitialType = AdProvider.Mock;
        [SerializeField] AdProvider rewardedVideoType = AdProvider.Mock;

        [Header("Provider Containers")]
        [SerializeField] LevelPlaySettings levelPlaySettings;
        [SerializeField] AdMobSettings adMobSettings;
        [SerializeField] UnityAdsSettings unityAdsSettings;
        [SerializeField] MockAdSettings mockAdSettings;

        [Header("Settings")]
        [SerializeField] bool testMode = false;
        [SerializeField] bool systemLogs = false;
        [SerializeField] float interstitialFirstStartDelay = 40f;
        [SerializeField] float interstitialStartDelay = 40f;
        [SerializeField] float interstitialShowingDelay = 30f;
        [SerializeField] bool autoShowInterstitial = false;

        [Header("Privacy & GDPR Configuration")]
        [SerializeField] bool enableGDPR = false;
        [SerializeField] GDPRCanvas gdprCanvasPrefab = null;
        [SerializeField] string privacyPolicyUrl = "";
        [SerializeField] string termsOfUseUrl = "";
        [SerializeField] bool enableIDFA = false;
        [SerializeField] bool showGDPROnFirstLaunch = true;

        // Properties
        public AdProvider BannerType => bannerType;
        public AdProvider InterstitialType => interstitialType;
        public AdProvider RewardedVideoType => rewardedVideoType;
        public LevelPlaySettings LevelPlaySettings => levelPlaySettings;
        public AdMobSettings AdMobSettings => adMobSettings;
        public UnityAdsSettings UnityAdsSettings => unityAdsSettings;
        public MockAdSettings MockAdSettings => mockAdSettings;
        public bool TestMode => testMode;
        public bool SystemLogs => systemLogs;
        public float InterstitialFirstStartDelay => interstitialFirstStartDelay;
        public float InterstitialStartDelay => interstitialStartDelay;
        public float InterstitialShowingDelay => interstitialShowingDelay;
        public bool AutoShowInterstitial => autoShowInterstitial;

        // Privacy & GDPR Properties
        public bool EnableGDPR => enableGDPR;
        public GDPRCanvas GDPRCanvasPrefab => gdprCanvasPrefab;
        public string PrivacyPolicyUrl => privacyPolicyUrl;
        public string TermsOfUseUrl => termsOfUseUrl;
        public bool EnableIDFA => enableIDFA;
        public bool ShowGDPROnFirstLaunch => showGDPROnFirstLaunch;

        public bool IsMockEnabled()
        {
            return bannerType == AdProvider.Mock || 
                   interstitialType == AdProvider.Mock || 
                   rewardedVideoType == AdProvider.Mock;
        }
    }
}
