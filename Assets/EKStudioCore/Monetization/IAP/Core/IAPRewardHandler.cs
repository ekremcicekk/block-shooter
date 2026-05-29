using UnityEngine;
using System;

namespace EKStudio.IAP
{
    /// <summary>
    /// IAP Reward Handler - Processes rewards from IAP purchases
    /// This system is GENERIC and can be used in any game template
    /// 
    /// Extend this class or use callbacks to handle game-specific rewards
    /// </summary>
    public static class IAPRewardHandler
    {
        // Events for custom reward processing
        public static event Action<int> OnCoinsAwarded;
        public static event Action<string, int> OnBoosterAwarded;  // (boosterType, amount)
        public static event Action<string> OnItemUnlocked;          // (itemId)
        public static event Action<int> OnGemsAwarded;
        public static event Action<string, int> OnCustomReward;     // (rewardType, amount)
        
        /// <summary>
        /// Process all rewards from a product
        /// </summary>
        public static void ProcessRewards(IAPProduct product)
        {
            if (product == null)
            {
                Debug.LogError("[IAPRewardHandler] Product is null!");
                return;
            }
            
            Debug.Log($"[IAPRewardHandler] Processing rewards for: {product.productName}");
            
            // Process coin reward
            if (product.coinReward > 0)
            {
                AwardCoins(product.coinReward);
            }
            
            // Process booster rewards ONLY if rewardType is Booster or Mixed
            if ((product.rewardType == IAPRewardType.Booster || product.rewardType == IAPRewardType.Mixed)
                && product.boosterRewards != null && product.boosterRewards.Length > 0)
            {
                foreach (var boosterReward in product.boosterRewards)
                {
                    AwardBooster(boosterReward.boosterType, boosterReward.amount);
                }
            }
            
            // Process gem reward
            if (product.gemReward > 0)
            {
                AwardGems(product.gemReward);
            }
            
            // Process custom rewards
            if (!string.IsNullOrEmpty(product.customData))
            {
                ProcessCustomReward(product.customData, product.customValue);
            }
            
            // Process No Ads (both old flag and new includesNoAds)
            if (product.isNoAdsProduct || product.includesNoAds)
            {
                DisableAds();
            }
        }
        
        /// <summary>
        /// Award coins to player (MUST be handled via OnCoinsAwarded event)
        /// </summary>
        private static void AwardCoins(int amount)
        {
            Debug.Log($"[IAPRewardHandler] 💰 Awarding {amount} coins");
            
            // Invoke event for custom handling
            OnCoinsAwarded?.Invoke(amount);
            
            // Fallback: Save to PlayerPrefs if no handler registered
            if (OnCoinsAwarded == null || OnCoinsAwarded.GetInvocationList().Length == 0)
            {
                int currentCoins = PlayerPrefs.GetInt("IAP_TotalCoins", 0);
                currentCoins += amount;
                PlayerPrefs.SetInt("IAP_TotalCoins", currentCoins);
                PlayerPrefs.Save();
                
                Debug.LogWarning($"[IAPRewardHandler] ⚠ No coin handler registered! Saved to PlayerPrefs (IAP_TotalCoins={currentCoins})");
                Debug.LogWarning("[IAPRewardHandler] → Subscribe to OnCoinsAwarded event to handle coins in your game");
            }
        }
        
        /// <summary>
        /// Award booster to player (MUST be handled via OnBoosterAwarded event)
        /// </summary>
        private static void AwardBooster(string boosterType, int amount)
        {
            Debug.Log($"[IAPRewardHandler] 🔨 Awarding {amount}x {boosterType}");
            
            // Invoke event for custom handling
            OnBoosterAwarded?.Invoke(boosterType, amount);
            
            // Fallback: Save to PlayerPrefs if no handler registered
            if (OnBoosterAwarded == null || OnBoosterAwarded.GetInvocationList().Length == 0)
            {
                string key = $"IAP_Booster_{boosterType}";
                int currentCount = PlayerPrefs.GetInt(key, 0);
                currentCount += amount;
                PlayerPrefs.SetInt(key, currentCount);
                PlayerPrefs.Save();
                
                Debug.LogWarning($"[IAPRewardHandler] ⚠ No booster handler registered! Saved to PlayerPrefs ({key}={currentCount})");
                Debug.LogWarning("[IAPRewardHandler] → Subscribe to OnBoosterAwarded event to handle boosters in your game");
            }
        }
        
        /// <summary>
        /// Award gems to player (game-specific implementation via callback)
        /// </summary>
        private static void AwardGems(int amount)
        {
            Debug.Log($"[IAPRewardHandler] 💎 Awarding {amount} gems");
            
            // Invoke event for custom handling
            OnGemsAwarded?.Invoke(amount);
            
            // Default implementation - use PlayerPrefs
            if (OnGemsAwarded == null || OnGemsAwarded.GetInvocationList().Length == 0)
            {
                int currentGems = PlayerPrefs.GetInt("TotalGems", 0);
                currentGems += amount;
                PlayerPrefs.SetInt("TotalGems", currentGems);
                PlayerPrefs.Save();
                
                Debug.Log($"[IAPRewardHandler] ✅ Gems awarded successfully (Default implementation)");
            }
        }
        
        /// <summary>
        /// Process custom reward data (game-specific implementation via callback)
        /// </summary>
        private static void ProcessCustomReward(string rewardType, int rewardValue)
        {
            Debug.Log($"[IAPRewardHandler] 🎁 Processing custom reward: {rewardType} = {rewardValue}");
            
            // Invoke event for custom handling
            OnCustomReward?.Invoke(rewardType, rewardValue);
            
            // Examples of custom reward handling:
            // if (rewardType == "character_unlock")
            // {
            //     UnlockCharacter(rewardValue);
            // }
            // else if (rewardType == "level_unlock")
            // {
            //     UnlockLevel(rewardValue);
            // }
        }
        
        /// <summary>
        /// Disable ads (integrates with EKStudio.Monetization.AdvertisingSystem)
        /// </summary>
        private static void DisableAds()
        {
            Debug.Log("[IAPRewardHandler] 🚫 Disabling ads");
            
            try
            {
                Monetization.AdvertisingSystem.DisableAds();
                Debug.Log("[IAPRewardHandler] ✅ Ads disabled successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[IAPRewardHandler] ⚠ Failed to disable ads: {e.Message}");
            }
        }
        
        /// <summary>
        /// Clear all event subscriptions (call when changing scenes)
        /// </summary>
        public static void ClearCallbacks()
        {
            OnCoinsAwarded = null;
            OnBoosterAwarded = null;
            OnItemUnlocked = null;
            OnGemsAwarded = null;
            OnCustomReward = null;
        }
    }
}
