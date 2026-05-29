using System.Collections.Generic;
using UnityEngine;

namespace EKStudio.IAP
{
    /// <summary>
    /// IAP (In-App Purchase) Settings ScriptableObject
    /// Configure your IAP products here for easy management
    /// </summary>
    [CreateAssetMenu(fileName = "IAPSettings", menuName = "EKStudio/IAP Settings", order = 1)]
    public class IAPSettings : ScriptableObject
    {
        [Header("IAP System Configuration")]
        [Tooltip("Enable/Disable entire IAP system")]
        public bool enableIAP = true;
        
        [Header("No Ads Feature")]
        [Tooltip("Enable No Ads IAP product (shows No Ads button in game)")]
        public bool enableNoAds = true;
        
        [Tooltip("Product ID for Remove Ads (must match Store product ID)\nExample: com.yourcompany.yourapp.removeads")]
        public string noAdsProductId = "com.yourcompany.yourapp.removeads";
        
        [Tooltip("Display price for No Ads (e.g., $2.99)")]
        public string noAdsPrice = "$2.99";
        
        [Header("Custom IAP Products")]
        [Tooltip("Add your custom IAP products here")]
        public List<IAPProduct> customProducts = new List<IAPProduct>();
        
        [Header("Store Configuration")]
        [Tooltip("Google Play Public Key (RSA public key from Google Play Console)\nUsed for receipt validation on Android\nLeave empty for testing, but REQUIRED for production")]
        [TextArea(3, 5)]
        public string googlePlayPublicKey = "";
        
        [Tooltip("Apple App Store Shared Secret (from App Store Connect)\nRequired for auto-renewable subscriptions\nOptional for other product types")]
        public string appleSharedSecret = "";
        
        [Header("Platform-Specific Product IDs")]
        [Tooltip("Enable if your Android and iOS products have different IDs\nExample: com.company.app.product_android vs com.company.app.product_ios")]
        public bool usePlatformSpecificIds = false;
        
        [Tooltip("Enable debug logs for IAP operations")]
        public bool debugMode = true;

        /// <summary>
        /// Get all active products (including No Ads if enabled)
        /// </summary>
        public List<IAPProduct> GetAllProducts()
        {
            List<IAPProduct> allProducts = new List<IAPProduct>();
            
            // Add No Ads product if enabled
            if (enableNoAds)
            {
                allProducts.Add(new IAPProduct
                {
                    productId = noAdsProductId,
                    productName = "Remove Ads",
                    productType = IAPProductType.NonConsumable,
                    price = noAdsPrice,
                    description = "Remove all banner and interstitial ads permanently",
                    isNoAdsProduct = true
                });
            }
            
            // Add custom products
            if (customProducts != null)
            {
                allProducts.AddRange(customProducts);
            }
            
            return allProducts;
        }
        
        /// <summary>
        /// Find product by ID
        /// </summary>
        public IAPProduct FindProduct(string productId)
        {
            var products = GetAllProducts();
            return products.Find(p => p.productId == productId);
        }
        
        /// <summary>
        /// Check if a product ID is valid
        /// </summary>
        public bool IsValidProductId(string productId)
        {
            if (string.IsNullOrEmpty(productId))
                return false;
                
            return FindProduct(productId) != null;
        }
    }

    /// <summary>
    /// IAP Product Type
    /// </summary>
    public enum IAPProductType
    {
        [Tooltip("One-time purchase, never expires (e.g., Remove Ads, Unlock Level Pack)")]
        NonConsumable,
        
        [Tooltip("Can be purchased multiple times (e.g., Coin Packs, Extra Lives)")]
        Consumable,
        
        [Tooltip("Recurring subscription (e.g., VIP Membership, Monthly Premium)")]
        Subscription
    }
    
    /// <summary>
    /// IAP Reward Type - What does this product give?
    /// </summary>
    public enum IAPRewardType
    {
        [Tooltip("Only gives coins")]
        Coin,
        
        [Tooltip("Gives booster items (Hammer, Vacuum, FreezeTimer, etc.)")]
        Booster,
        
        [Tooltip("Gives gems (premium currency)")]
        Gem,
        
        [Tooltip("Mixed rewards (coins + boosters + gems)")]
        Mixed,
        
        [Tooltip("Special item (No Ads, Character Unlock, etc.)")]
        Special,
        
        [Tooltip("Custom reward (define in customData)")]
        Custom
    }
    
    /// <summary>
    /// IAP Purchase Method - How can this product be obtained?
    /// </summary>
    public enum IAPPurchaseMethod
    {
        [Tooltip("Buy with real money via App Store/Google Play")]
        RealMoney,
        
        [Tooltip("Can be claimed free once, then timer-based (e.g., 1 hour cooldown)")]
        TimedFree,
        
        [Tooltip("Watch rewarded video to get this product")]
        RewardedVideo,
        
        [Tooltip("Multiple methods available (Real Money + Rewarded Video)")]
        Multiple
    }

    /// <summary>
    /// Booster Reward Data
    /// </summary>
    [System.Serializable]
    public class BoosterReward
    {
        [Tooltip("Booster type (hammer, vacuum, freezetimer, all)")]
        public string boosterType = "hammer";
        
        [Tooltip("Amount to give")]
        public int amount = 5;
    }
    
    /// <summary>
    /// IAP Product Data - Enhanced for Store System
    /// </summary>
    [System.Serializable]
    public class IAPProduct
    {
        [Header("Product Identification")]
        [Tooltip("Unique product ID (must match Store product ID)\nFormat: com.company.app.productname\nUsed for both platforms if platform-specific IDs are disabled")]
        public string productId = "com.yourcompany.yourapp.product";
        
        [Tooltip("Display name shown to players")]
        public string productName = "Product Name";
        
        [Header("Platform-Specific IDs (Optional)")]
        [Tooltip("Android-specific product ID (Google Play)\nOnly used if 'Use Platform Specific IDs' is enabled in settings")]
        public string androidProductId = "";
        
        [Tooltip("iOS-specific product ID (App Store)\nOnly used if 'Use Platform Specific IDs' is enabled in settings")]
        public string iosProductId = "";
        
        [Header("Product Configuration")]
        [Tooltip("Type of IAP product")]
        public IAPProductType productType = IAPProductType.Consumable;
        
        [Tooltip("What does this product reward?")]
        public IAPRewardType rewardType = IAPRewardType.Coin;
        
        [Tooltip("How can this product be obtained?")]
        public IAPPurchaseMethod purchaseMethod = IAPPurchaseMethod.RealMoney;
        
        [Tooltip("Display price (e.g., $0.99, $4.99, $9.99)")]
        public string price = "$0.99";
        
        [Tooltip("Product description")]
        [TextArea(2, 4)]
        public string description = "Product description";
        
        [Header("Reward Configuration")]
        [Tooltip("Coins to give on purchase (0 if not applicable)")]
        public int coinReward = 0;
        
        [Tooltip("Gems to give on purchase (0 if not applicable)")]
        public int gemReward = 0;
        
        [Tooltip("Booster rewards (for Booster or Mixed reward type)")]
        public BoosterReward[] boosterRewards = new BoosterReward[0];
        
        [Header("Timed Free Configuration")]
        [Tooltip("Cooldown time in seconds for TimedFree products (default: 3600 = 1 hour)")]
        public int timedFreeCooldown = 3600; // 1 hour
        
        [Header("Special Features")]
        [Tooltip("Include No Ads removal in this product")]
        public bool includesNoAds = false;
        
        [Header("Custom Data")]
        [Tooltip("Custom integer value (use for any purpose)")]
        public int customValue = 0;
        
        [Tooltip("Custom string data (use for any purpose)")]
        public string customData = "";
        
        // Internal flags (for backwards compatibility with No Ads product)
        [HideInInspector]
        public bool isNoAdsProduct = false;
        
        /// <summary>
        /// Get the appropriate product ID for current platform
        /// </summary>
        public string GetPlatformProductId(bool usePlatformSpecific)
        {
            if (!usePlatformSpecific)
                return productId;
            
#if UNITY_ANDROID
            return string.IsNullOrEmpty(androidProductId) ? productId : androidProductId;
#elif UNITY_IOS
            return string.IsNullOrEmpty(iosProductId) ? productId : iosProductId;
#else
            return productId;
#endif
        }
        
        /// <summary>
        /// Get product type display name
        /// </summary>
        public string GetProductTypeDisplay()
        {
            switch (productType)
            {
                case IAPProductType.NonConsumable:
                    return "Non-Consumable (One-time)";
                case IAPProductType.Consumable:
                    return "Consumable (Repeatable)";
                case IAPProductType.Subscription:
                    return "Subscription (Recurring)";
                default:
                    return "Unknown";
            }
        }
    }
}


