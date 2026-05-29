using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Reflection;

namespace EKStudio.IAP
{
    /// <summary>
    /// IAP Purchase Button Component
    /// Drop this on any button, select a product, and it's ready to use!
    /// NO CODE NEEDED - just configure in Inspector
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class IAPPurchaseButton : MonoBehaviour
    {
        [Header("Product Selection")]
        [Tooltip("Select IAP product from your IAPSettings asset")]
        public IAPSettings iapSettings;
        
        [Tooltip("Enter Product ID (must match IAPSettings product)\nExample: com.yourcompany.yourapp.coins100")]
        public string productId;
        
        [Header("UI References (Optional)")]
        [Tooltip("Text to display product price (will auto-update from store)")]
        public Text priceText;
        
        [Tooltip("TextMeshPro text for product price (alternative to Text)")]
        public TextMeshProUGUI priceTextTMP;
        
        [Tooltip("Text to display product name")]
        public Text nameText;
        
        [Tooltip("TextMeshPro text for product name (alternative to Text)")]
        public TextMeshProUGUI nameTextTMP;
        
        [Tooltip("Text to display product description")]
        public Text descriptionText;
        
        [Tooltip("TextMeshPro text for product description (alternative to Text)")]
        public TextMeshProUGUI descriptionTextTMP;

        private Button button;
        private IAPProduct product;
        private float timerRemaining = 0f;
        private bool isTimerActive = false;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnPurchaseButtonClick);
            
            // Find product in settings
            if (iapSettings != null && !string.IsNullOrEmpty(productId))
            {
                product = iapSettings.FindProduct(productId);
                if (product == null)
                {
                    Debug.LogWarning($"[IAPPurchaseButton] Product '{productId}' not found in IAPSettings!");
                }
            }
            
            // Subscribe to purchase success event
            SubscribeToPurchaseEvents();
        }
        
        /// <summary>
        /// Subscribe to IAPManager purchase events
        /// </summary>
        private void SubscribeToPurchaseEvents()
        {
            try
            {
                var iapManagerType = Type.GetType("EKStudio.IAP.IAPManager, Assembly-CSharp");
                if (iapManagerType == null) return;

                var onPurchaseSuccessEvent = iapManagerType.GetEvent("OnPurchaseSuccess", BindingFlags.Public | BindingFlags.Static);
                if (onPurchaseSuccessEvent != null)
                {
                    Action<string> successHandler = OnPurchaseSuccessHandler;
                    onPurchaseSuccessEvent.AddEventHandler(null, successHandler);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[IAPPurchaseButton] Failed to subscribe to purchase events: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle purchase success event
        /// </summary>
        private void OnPurchaseSuccessHandler(string purchasedProductId)
        {
            // Only process if it's our product
            if (purchasedProductId == productId)
            {
                Debug.Log($"[IAPPurchaseButton] ✅ Purchase successful: {product.productName}");
                // Note: Rewards are processed by IAPRewardHandler automatically
                // No need to process here - prevents duplicate reward distribution
            }
        }

        private void Start()
        {
            UpdateProductInfo();
            LoadTimerState(); // Load saved timer state if TimedFree product
        }

        /// <summary>
        /// Update UI with product info
        /// </summary>
        private void UpdateProductInfo()
        {
            if (product == null) return;

            // Update name
            if (nameText != null)
                nameText.text = product.productName;
            if (nameTextTMP != null)
                nameTextTMP.text = product.productName;
            
            // Update description
            if (descriptionText != null)
                descriptionText.text = product.description;
            if (descriptionTextTMP != null)
                descriptionTextTMP.text = product.description;
            
            // Update price from store (dynamic, localized)
            string storePrice = GetStorePriceForProduct(productId);
            string displayPrice = string.IsNullOrEmpty(storePrice) ? product.price : storePrice;
            
            if (priceText != null)
                priceText.text = displayPrice;
            if (priceTextTMP != null)
                priceTextTMP.text = displayPrice;
        }

        /// <summary>
        /// Update timer display for TimedFree products
        /// </summary>
        private void Update()
        {
            if (isTimerActive && product?.purchaseMethod == IAPPurchaseMethod.TimedFree)
            {
                timerRemaining -= Time.deltaTime;
                
                if (timerRemaining <= 0)
                {
                    // Timer finished, button is FREE again
                    isTimerActive = false;
                    timerRemaining = 0;
                    PlayerPrefs.DeleteKey($"TimedFreeTimer_{productId}");
                    PlayerPrefs.Save();
                    UpdateButtonUI();
                }
                else
                {
                    // Update button text with countdown
                    UpdateTimerDisplay();
                }
            }
        }

        /// <summary>
        /// Load timer state from PlayerPrefs (if product was previously claimed)
        /// </summary>
        private void LoadTimerState()
        {
            if (product == null || product.purchaseMethod != IAPPurchaseMethod.TimedFree)
                return;

            string timerKey = $"TimedFreeTimer_{productId}";
            if (PlayerPrefs.HasKey(timerKey))
            {
                long savedTime = long.Parse(PlayerPrefs.GetString(timerKey, "0"));
                long currentTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long elapsed = currentTime - savedTime;
                
                timerRemaining = product.timedFreeCooldown - elapsed;
                
                if (timerRemaining > 0)
                {
                    isTimerActive = true;
                    button.interactable = false;
                }
                else
                {
                    // Timer expired, reset
                    PlayerPrefs.DeleteKey(timerKey);
                    PlayerPrefs.Save();
                    isTimerActive = false;
                    timerRemaining = 0;
                    button.interactable = true;
                }
            }
            else
            {
                // No timer set, button is FREE
                isTimerActive = false;
                button.interactable = true;
            }
            
            UpdateButtonUI();
        }

        /// <summary>
        /// Update timer display text
        /// </summary>
        private void UpdateTimerDisplay()
        {
            int minutes = (int)timerRemaining / 60;
            int seconds = (int)timerRemaining % 60;
            string timerText = $"{minutes:00}:{seconds:00}";
            
            if (priceText != null)
                priceText.text = timerText;
            if (priceTextTMP != null)
                priceTextTMP.text = timerText;
        }

        /// <summary>
        /// Update button UI based on timer state
        /// </summary>
        private void UpdateButtonUI()
        {
            if (isTimerActive)
            {
                button.interactable = false;
                UpdateTimerDisplay();
            }
            else
            {
                button.interactable = true;
                if (priceText != null)
                    priceText.text = "FREE";
                if (priceTextTMP != null)
                    priceTextTMP.text = "FREE";
            }
        }

        /// <summary>
        /// Called when button is clicked
        /// </summary>
        public void OnPurchaseButtonClick()
        {
            if (product == null)
            {
                Debug.LogError($"[IAPPurchaseButton] Cannot purchase: Product '{productId}' not configured!");
                return;
            }

            Debug.Log($"[IAPPurchaseButton] 🛒 Initiating purchase: {product.productName} ({productId})");
            
            // Handle TimedFree products
            if (product.purchaseMethod == IAPPurchaseMethod.TimedFree)
            {
                HandleTimedFreePurchase();
                return;
            }
            
            // Handle other purchase types
            HandleStandardPurchase();
        }

        /// <summary>
        /// Handle TimedFree product purchase
        /// </summary>
        private void HandleTimedFreePurchase()
        {
            // Process rewards through IAPRewardHandler (triggers events for game-specific handling)
            Debug.Log($"[IAPPurchaseButton] 🎁 Free reward claimed: {product.productName}");
            IAPRewardHandler.ProcessRewards(product);
            
            // Start cooldown timer
            long currentTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            PlayerPrefs.SetString($"TimedFreeTimer_{productId}", currentTime.ToString());
            PlayerPrefs.Save();
            
            timerRemaining = product.timedFreeCooldown;
            isTimerActive = true;
            UpdateButtonUI();
            
            Debug.Log($"[IAPPurchaseButton] ⏱ Timer started: {product.timedFreeCooldown}s cooldown");
        }

        /// <summary>
        /// Handle standard purchase (RealMoney or RewardedVideo)
        /// </summary>
        private void HandleStandardPurchase()
        {
            try
            {
                var iapManagerType = Type.GetType("EKStudio.IAP.IAPManager, Assembly-CSharp");
                if (iapManagerType == null)
                {
                    Debug.LogError("[IAPPurchaseButton] IAPManager not found!");
                    return;
                }

                var instanceProp = iapManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                var iapManager = instanceProp?.GetValue(null);
                
                if (iapManager == null)
                {
                    Debug.LogError("[IAPPurchaseButton] ❌ IAPManager not initialized! Make sure Loading.cs calls InitializeIAP()");
                    Debug.LogError("[IAPPurchaseButton] Check: Is Loading scene loaded? Is iapSettings assigned in Loading inspector?");
                    return;
                }
                
                Debug.Log("[IAPPurchaseButton] ✅ IAPManager found, calling PurchaseProduct...");

                // Call IAPManager.PurchaseProduct(productId)
                var purchaseMethod = iapManagerType.GetMethod("PurchaseProduct", new[] { typeof(string) });
                if (purchaseMethod != null)
                {
                    purchaseMethod.Invoke(iapManager, new object[] { productId });
                }
                else
                {
                    Debug.LogError("[IAPPurchaseButton] PurchaseProduct method not found!");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAPPurchaseButton] Purchase error: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// Give reward to player (for TimedFree products)
        /// </summary>
        private void GiveReward()
        {
            if (product == null) return;

            // Add coins
            if (product.coinReward > 0)
            {
                int currentCoins = PlayerPrefs.GetInt("TotalCoin", 0);
                int newCoins = currentCoins + product.coinReward;
                PlayerPrefs.SetInt("TotalCoin", newCoins);
                Debug.Log($"[IAPPurchaseButton] 🪙 +{product.coinReward} coins (Total: {newCoins})");
            }

            // Add gems
            if (product.gemReward > 0)
            {
                int currentGems = PlayerPrefs.GetInt("TotalGem", 0);
                int newGems = currentGems + product.gemReward;
                PlayerPrefs.SetInt("TotalGem", newGems);
                Debug.Log($"[IAPPurchaseButton] 💎 +{product.gemReward} gems (Total: {newGems})");
            }

            PlayerPrefs.Save();
        }

        #region Helper Methods

        private string GetStorePriceForProduct(string prodId)
        {
            try
            {
                var iapManagerType = Type.GetType("EKStudio.IAP.IAPManager, Assembly-CSharp");
                var instanceProp = iapManagerType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                var iapManager = instanceProp?.GetValue(null);
                
                if (iapManager != null)
                {
                    var getPriceMethod = iapManagerType.GetMethod("GetProductPrice");
                    return (string)getPriceMethod?.Invoke(iapManager, new object[] { prodId });
                }
            }
            catch { }
            
            return null;
        }

        #endregion
    }
}
