using UnityEngine;
using System;

namespace EKStudio.IAP
{
    /// <summary>
    /// IAP Timed Free Manager - Handles timed free products with cooldown
    /// Allows products to be claimed for free once, then requires waiting period
    /// 
    /// Example: "Free Coin Pack every 1 hour"
    /// </summary>
    public static class IAPTimedFreeManager
    {
        private const string COOLDOWN_KEY_PREFIX = "IAP_TimedFree_";
        
        /// <summary>
        /// Check if a timed free product is available to claim
        /// </summary>
        public static bool IsAvailable(string productId)
        {
            if (string.IsNullOrEmpty(productId))
                return false;
            
            string cooldownKey = COOLDOWN_KEY_PREFIX + productId;
            
            // Check if product was ever claimed
            if (!PlayerPrefs.HasKey(cooldownKey))
            {
                // Never claimed before - available!
                return true;
            }
            
            // Get last claim time
            long lastClaimTicks = long.Parse(PlayerPrefs.GetString(cooldownKey, "0"));
            DateTime lastClaimTime = new DateTime(lastClaimTicks);
            
            // Check if cooldown has passed
            return DateTime.Now >= lastClaimTime;
        }
        
        /// <summary>
        /// Get remaining cooldown time in seconds
        /// Returns 0 if available
        /// </summary>
        public static int GetRemainingCooldown(string productId)
        {
            if (!PlayerPrefs.HasKey(COOLDOWN_KEY_PREFIX + productId))
                return 0;
            
            string cooldownKey = COOLDOWN_KEY_PREFIX + productId;
            long lastClaimTicks = long.Parse(PlayerPrefs.GetString(cooldownKey, "0"));
            DateTime lastClaimTime = new DateTime(lastClaimTicks);
            DateTime availableTime = lastClaimTime;
            
            if (DateTime.Now >= availableTime)
                return 0;
            
            TimeSpan remaining = availableTime - DateTime.Now;
            return (int)remaining.TotalSeconds;
        }
        
        /// <summary>
        /// Get remaining cooldown as formatted string (HH:MM:SS)
        /// </summary>
        public static string GetRemainingCooldownFormatted(string productId)
        {
            int remainingSeconds = GetRemainingCooldown(productId);
            
            if (remainingSeconds <= 0)
                return "Available!";
            
            int hours = remainingSeconds / 3600;
            int minutes = (remainingSeconds % 3600) / 60;
            int seconds = remainingSeconds % 60;
            
            if (hours > 0)
                return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
            else
                return $"{minutes:D2}:{seconds:D2}";
        }
        
        /// <summary>
        /// Claim a timed free product (starts cooldown)
        /// </summary>
        public static void ClaimProduct(string productId, int cooldownSeconds)
        {
            if (string.IsNullOrEmpty(productId))
            {
                Debug.LogError("[IAPTimedFreeManager] Product ID is null or empty!");
                return;
            }
            
            // Calculate next available time
            DateTime nextAvailableTime = DateTime.Now.AddSeconds(cooldownSeconds);
            
            // Save to PlayerPrefs
            string cooldownKey = COOLDOWN_KEY_PREFIX + productId;
            PlayerPrefs.SetString(cooldownKey, nextAvailableTime.Ticks.ToString());
            PlayerPrefs.Save();
            
            Debug.Log($"[IAPTimedFreeManager] Product claimed: {productId}");
            Debug.Log($"[IAPTimedFreeManager] Next available at: {nextAvailableTime}");
        }
        
        /// <summary>
        /// Reset cooldown for a product (for testing/debugging)
        /// </summary>
        public static void ResetCooldown(string productId)
        {
            string cooldownKey = COOLDOWN_KEY_PREFIX + productId;
            PlayerPrefs.DeleteKey(cooldownKey);
            PlayerPrefs.Save();
            
            Debug.Log($"[IAPTimedFreeManager] Cooldown reset for: {productId}");
        }
        
        /// <summary>
        /// Reset all timed free cooldowns (for testing/debugging)
        /// </summary>
        public static void ResetAllCooldowns()
        {
            // Get all IAP products
            var iapSettings = Resources.Load<IAPSettings>("IAPSettings");
            if (iapSettings != null)
            {
                var products = iapSettings.GetAllProducts();
                foreach (var product in products)
                {
                    if (product.purchaseMethod == IAPPurchaseMethod.TimedFree ||
                        product.purchaseMethod == IAPPurchaseMethod.Multiple)
                    {
                        ResetCooldown(product.productId);
                    }
                }
            }
            
            Debug.Log("[IAPTimedFreeManager] All cooldowns reset!");
        }
        
        /// <summary>
        /// Get next available time for a product
        /// </summary>
        public static DateTime GetNextAvailableTime(string productId)
        {
            if (!PlayerPrefs.HasKey(COOLDOWN_KEY_PREFIX + productId))
                return DateTime.Now; // Available now
            
            string cooldownKey = COOLDOWN_KEY_PREFIX + productId;
            long lastClaimTicks = long.Parse(PlayerPrefs.GetString(cooldownKey, "0"));
            return new DateTime(lastClaimTicks);
        }
    }
}

