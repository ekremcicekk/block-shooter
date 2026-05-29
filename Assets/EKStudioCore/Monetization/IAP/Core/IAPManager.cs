using UnityEngine;
using System;
using System.Collections.Generic;

#if UNITY_PURCHASING
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using UnityEngine.Purchasing.Security;
#endif

namespace EKStudio.IAP
{
    /// <summary>
    /// IAP Manager - Handles all In-App Purchase operations
    /// Works with Unity IAP Package (com.unity.purchasing)
    /// 
    /// Auto-initializes via IAPBootstrapper.
    /// Can also be manually initialized from Loading scene for custom setup.
    /// </summary>
    public class IAPManager : MonoBehaviour
#if UNITY_PURCHASING
        , IStoreListener
#endif
    {
        private static IAPManager _instance;
        public static IAPManager Instance => _instance;
        
        [Header("Settings")]
        [Tooltip("IAP Settings reference\nAuto-loaded from Resources if not assigned")]
        public IAPSettings iapSettings;
        
        // IAP Status
        private bool isInitialized = false;
        private Dictionary<string, bool> purchasedProducts = new Dictionary<string, bool>();
        
#if UNITY_PURCHASING
        // Unity IAP
        private IStoreController storeController;
        private IExtensionProvider extensionProvider;
#endif
        
        // Events
        public static event Action<string> OnPurchaseSuccess;
        public static event Action<string> OnPurchaseFailed;
        public static event Action OnRestoreSuccess;
        public static event Action OnRestoreFailed;
        
        private void Awake()
        {
            // Singleton check
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        private void Start()
        {
            // Validate settings (should be assigned by Loading scene)
            if (iapSettings == null)
            {
                Debug.LogError("[IAPManager] ❌ IAPSettings not assigned!\n" +
                              "SOLUTION: Assign IAPSettings in Loading scene Inspector.\n" +
                              "Location: Project Files/Data/IAPSettings.asset\n" +
                              "IAP System will be disabled.");
                return;
            }
            
            if (!iapSettings.enableIAP)
            {
                Debug.Log("[IAPManager] IAP System is disabled in settings");
                return;
            }
            
            // Initialize IAP
            InitializeIAP();
        }
        
        /// <summary>
        /// Initialize IAP System with Unity Purchasing
        /// </summary>
        private void InitializeIAP()
        {
            #if UNITY_PURCHASING
            if (iapSettings.debugMode)
                Debug.Log("[IAPManager] 🔄 Initializing Unity IAP...");
            
            // Check if already initializing/initialized
            if (isInitialized || storeController != null)
            {
                if (iapSettings.debugMode)
                    Debug.Log("[IAPManager] ✓ IAP already initialized");
                return;
            }
            
            // Create ConfigurationBuilder
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            
            // Add all products from settings
            var products = iapSettings.GetAllProducts();
            if (products.Count == 0)
            {
                Debug.LogWarning("[IAPManager] ⚠ No products configured in IAPSettings!");
                return;
            }
            
            foreach (var product in products)
            {
                // Get platform-specific product ID
                string productId = product.GetPlatformProductId(iapSettings.usePlatformSpecificIds);
                
                // Convert product type
                ProductType unityProductType = ConvertProductType(product.productType);
                
                // Add product to builder
                builder.AddProduct(productId, unityProductType);
                
                if (iapSettings.debugMode)
                    Debug.Log($"[IAPManager] ➕ Added product: {product.productName} ({productId}) - {unityProductType}");
            }
            
            // Start initialization
            UnityPurchasing.Initialize(this, builder);
            
            if (iapSettings.debugMode)
                Debug.Log($"[IAPManager] ✓ IAP initialization started with {products.Count} products");
            
            #else
            // Mock initialization for template without Unity IAP package
            Debug.LogWarning("[IAPManager] ⚠ Unity IAP Package not installed. Using MOCK mode.");
            Debug.LogWarning("[IAPManager] Install 'In App Purchasing' package from Package Manager to enable real IAP.");
            
            // Load purchased state from PlayerPrefs (for testing)
            LoadPurchaseStates();
            
            isInitialized = true;
            Debug.Log($"[IAPManager] 🧪 MOCK IAP initialized with {iapSettings.GetAllProducts().Count} products");
            #endif
        }
        
#if UNITY_PURCHASING
        /// <summary>
        /// Convert IAP product type to Unity product type
        /// </summary>
        private ProductType ConvertProductType(IAPProductType productType)
        {
            switch (productType)
            {
                case IAPProductType.Consumable:
                    return ProductType.Consumable;
                case IAPProductType.NonConsumable:
                    return ProductType.NonConsumable;
                case IAPProductType.Subscription:
                    return ProductType.Subscription;
                default:
                    return ProductType.Consumable;
            }
        }
#endif
        
        /// <summary>
        /// Purchase a product by ID (supports RealMoney, TimedFree, and RewardedVideo)
        /// </summary>
        public void PurchaseProduct(string productId)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[IAPManager] ⚠ IAP not initialized yet!");
                OnPurchaseFailed?.Invoke(productId);
                return;
            }
            
            if (!iapSettings.IsValidProductId(productId))
            {
                Debug.LogError($"[IAPManager] ❌ Invalid product ID: {productId}");
                OnPurchaseFailed?.Invoke(productId);
                return;
            }
            
            var product = iapSettings.FindProduct(productId);
            if (product == null)
            {
                Debug.LogError($"[IAPManager] ❌ Product not found: {productId}");
                OnPurchaseFailed?.Invoke(productId);
                return;
            }
            
            // Handle different purchase methods
            if (product.purchaseMethod == IAPPurchaseMethod.TimedFree)
            {
                PurchaseTimedFreeProduct(productId, product);
                return;
            }
            else if (product.purchaseMethod == IAPPurchaseMethod.RewardedVideo)
            {
                PurchaseWithRewardedVideo(productId, product);
                return;
            }
            // For RealMoney and Multiple, continue with normal IAP flow
            
            #if UNITY_PURCHASING
            if (storeController == null)
            {
                Debug.LogError("[IAPManager] ❌ Store controller not initialized!");
                OnPurchaseFailed?.Invoke(productId);
                return;
            }
            
            string platformProductId = product.GetPlatformProductId(iapSettings.usePlatformSpecificIds);
            
            // Get Unity product
            Product unityProduct = storeController.products.WithID(platformProductId);
            if (unityProduct == null || !unityProduct.availableToPurchase)
            {
                Debug.LogError($"[IAPManager] ❌ Product not available for purchase: {platformProductId}");
                OnPurchaseFailed?.Invoke(productId);
                return;
            }
            
            if (iapSettings.debugMode)
                Debug.Log($"[IAPManager] 🛒 Initiating purchase: {product.productName} ({platformProductId})");
            
            // Initiate purchase
            storeController.InitiatePurchase(unityProduct);
            
            #else
            // Mock purchase for template
            if (iapSettings.debugMode)
                Debug.Log($"[IAPManager] 🧪 MOCK Purchase: {productId}");
            MockPurchase(productId);
            #endif
        }
        
        /// <summary>
        /// Restore previous purchases (iOS requirement)
        /// </summary>
        public void RestorePurchases()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[IAPManager] ⚠ IAP not initialized yet!");
                OnRestoreFailed?.Invoke();
                return;
            }
            
            #if UNITY_PURCHASING
            #if UNITY_IOS || UNITY_TVOS || UNITY_STANDALONE_OSX
            if (extensionProvider == null)
            {
                Debug.LogError("[IAPManager] ❌ Extension provider not initialized!");
                OnRestoreFailed?.Invoke();
                return;
            }
            
            if (iapSettings.debugMode)
                Debug.Log("[IAPManager] 🔄 Restoring purchases...");
            
            // Get Apple extension
            var appleExtensions = extensionProvider.GetExtension<IAppleExtensions>();
            
            // Restore transactions
            appleExtensions.RestoreTransactions((result) =>
            {
                if (result)
                {
                    if (iapSettings.debugMode)
                        Debug.Log("[IAPManager] ✅ Restore completed successfully");
                    OnRestoreSuccess?.Invoke();
                }
                else
                {
                    Debug.LogWarning("[IAPManager] ⚠ Restore failed or no purchases to restore");
                    OnRestoreFailed?.Invoke();
                }
            });
            #else
            Debug.LogWarning("[IAPManager] ⚠ Restore purchases only available on iOS/tvOS/macOS");
            OnRestoreFailed?.Invoke();
            #endif
            #else
            // Mock restore
            if (iapSettings.debugMode)
                Debug.Log("[IAPManager] 🧪 MOCK Restore Purchases");
            MockRestore();
            #endif
        }
        
        /// <summary>
        /// Check if a product has been purchased
        /// </summary>
        public bool IsProductPurchased(string productId)
        {
            return purchasedProducts.ContainsKey(productId) && purchasedProducts[productId];
        }
        
        /// <summary>
        /// Check if No Ads has been purchased
        /// </summary>
        public bool HasPurchasedNoAds()
        {
            if (!iapSettings || !iapSettings.enableNoAds)
                return false;
                
            return IsProductPurchased(iapSettings.noAdsProductId);
        }
        
        /// <summary>
        /// Purchase a timed free product (claim free item with cooldown)
        /// </summary>
        private void PurchaseTimedFreeProduct(string productId, IAPProduct product)
        {
            // Check if available
            if (!IAPTimedFreeManager.IsAvailable(productId))
            {
                int remainingSeconds = IAPTimedFreeManager.GetRemainingCooldown(productId);
                string timeRemaining = IAPTimedFreeManager.GetRemainingCooldownFormatted(productId);
                
                Debug.LogWarning($"[IAPManager] ⏰ Timed free product not available yet!");
                Debug.LogWarning($"[IAPManager] Wait {timeRemaining} ({remainingSeconds}s)");
                OnPurchaseFailed?.Invoke(productId);
                return;
            }
            
            // Claim the product
            IAPTimedFreeManager.ClaimProduct(productId, product.timedFreeCooldown);
            
            // Process rewards
            ProcessPurchaseSuccess(productId, product);
            
            Debug.Log($"[IAPManager] ✅ Timed free product claimed: {product.productName}");
        }
        
        /// <summary>
        /// Purchase with rewarded video
        /// </summary>
        // Prevent double reward for the same rewarded video purchase
        private bool rewardedVideoRewardGiven = false;
        private void PurchaseWithRewardedVideo(string productId, IAPProduct product)
        {
            if (!Monetization.AdvertisingSystem.IsRewardBasedVideoLoaded())
            {
                Debug.LogWarning("[IAPManager] ⚠ Rewarded video not loaded!");
                OnPurchaseFailed?.Invoke(productId);
                return;
            }

            rewardedVideoRewardGiven = false;
            Monetization.AdvertisingSystem.ShowRewardBasedVideo((bool success) =>
            {
                if (success)
                {
                    if (!rewardedVideoRewardGiven)
                    {
                        rewardedVideoRewardGiven = true;
                        ProcessPurchaseSuccess(productId, product);
                        Debug.Log($"[IAPManager] ✅ Rewarded video product claimed: {product.productName}");
                    }
                }
                else
                {
                    Debug.LogWarning("[IAPManager] ⚠ Rewarded video cancelled or failed");
                    OnPurchaseFailed?.Invoke(productId);
                }
                Monetization.AdvertisingSystem.RequestRewardBasedVideo();
            });
        }
        
        /// <summary>
        /// Check if a timed free product is available
        /// </summary>
        public bool IsTimedFreeAvailable(string productId)
        {
            return IAPTimedFreeManager.IsAvailable(productId);
        }
        
        /// <summary>
        /// Get remaining cooldown for a timed free product
        /// </summary>
        public string GetTimedFreeCooldown(string productId)
        {
            return IAPTimedFreeManager.GetRemainingCooldownFormatted(productId);
        }
        
        /// <summary>
        /// Get product price from store (returns display price if store not available)
        /// </summary>
        public string GetProductPrice(string productId)
        {
            var product = iapSettings?.FindProduct(productId);
            if (product == null)
                return "$?.??";
            
            #if UNITY_PURCHASING
            if (storeController != null && isInitialized)
            {
                string platformProductId = product.GetPlatformProductId(iapSettings.usePlatformSpecificIds);
                Product unityProduct = storeController.products.WithID(platformProductId);
                
                if (unityProduct != null && unityProduct.metadata != null)
                {
                    // Return localized price from store
                    return unityProduct.metadata.localizedPriceString;
                }
            }
            #endif
            
            // Fallback to display price from settings
            return product.price;
        }
        
        // ========================================
        // MOCK IMPLEMENTATION (For template testing without Unity IAP)
        // ========================================
        
        #if !UNITY_PURCHASING
        /// <summary>
        /// Mock purchase for testing without Unity IAP package
        /// </summary>
        private void MockPurchase(string productId)
        {
            var product = iapSettings.FindProduct(productId);
            if (product == null)
            {
                Debug.LogError($"[IAPManager] MOCK Purchase failed: Product not found");
                OnPurchaseFailed?.Invoke(productId);
                return;
            }
            
            // Simulate purchase dialog
            string message = $"MOCK IAP Purchase\n\n" +
                            $"Product: {product.productName}\n" +
                            $"Price: {product.price}\n" +
                            $"Type: {product.GetProductTypeDisplay()}\n\n" +
                            $"This is a MOCK purchase (Unity IAP not installed).\n" +
                            $"In real build, payment dialog would appear here.";
            
            #if UNITY_EDITOR
            if (UnityEditor.EditorUtility.DisplayDialog("Mock IAP Purchase", message, "Purchase", "Cancel"))
            {
                ProcessPurchaseSuccess(productId, product);
            }
            else
            {
                Debug.Log($"[IAPManager] MOCK Purchase cancelled by user");
                OnPurchaseFailed?.Invoke(productId);
            }
            #else
            // In build, auto-success for testing
            ProcessPurchaseSuccess(productId, product);
            #endif
        }
        
        private void MockRestore()
        {
            #if UNITY_EDITOR
            if (UnityEditor.EditorUtility.DisplayDialog("Mock Restore Purchases", 
                "Restore all non-consumable purchases?\n\nThis is a MOCK operation.", "Restore", "Cancel"))
            {
                Debug.Log("[IAPManager] MOCK Restore completed");
                OnRestoreSuccess?.Invoke();
            }
            else
            {
                OnRestoreFailed?.Invoke();
            }
            #else
            OnRestoreSuccess?.Invoke();
            #endif
        }
        #endif
        
        /// <summary>
        /// Process successful purchase
        /// </summary>
        private void ProcessPurchaseSuccess(string productId, IAPProduct product)
        {
            Debug.Log($"[IAPManager] ✅ Purchase SUCCESS: {product.productName} ({productId})");
            
            // Mark as purchased
            if (product.productType == IAPProductType.NonConsumable)
            {
                purchasedProducts[productId] = true;
                PlayerPrefs.SetInt($"IAP_{productId}", 1);
                PlayerPrefs.Save();
            }
            
            // Process purchase rewards
            ProcessPurchaseRewards(product);
            
            // Invoke success event
            OnPurchaseSuccess?.Invoke(productId);
        }
        
        /// <summary>
        /// Process purchase rewards - Uses IAPRewardHandler for generic reward processing
        /// </summary>
        private void ProcessPurchaseRewards(IAPProduct product)
        {
            // Use IAPRewardHandler to process all rewards
            // This is a GENERIC system that can be used in any game template
            IAPRewardHandler.ProcessRewards(product);
        }
        
        /// <summary>
        /// Load purchase states from PlayerPrefs
        /// </summary>
        private void LoadPurchaseStates()
        {
            var products = iapSettings.GetAllProducts();
            foreach (var product in products)
            {
                if (product.productType == IAPProductType.NonConsumable)
                {
                    bool isPurchased = PlayerPrefs.GetInt($"IAP_{product.productId}", 0) == 1;
                    purchasedProducts[product.productId] = isPurchased;
                    
                    if (isPurchased)
                    {
                        Debug.Log($"[IAPManager] Loaded purchase state: {product.productName} (Purchased)");
                        
                        // Re-apply No Ads if it was purchased
                        if (product.isNoAdsProduct)
                        {
                            Monetization.AdvertisingSystem.DisableAds();
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Get all configured products
        /// </summary>
        public List<IAPProduct> GetAllProducts()
        {
            return iapSettings?.GetAllProducts() ?? new List<IAPProduct>();
        }
        
        /// <summary>
        /// Check if IAP system is ready
        /// </summary>
        public bool IsInitialized()
        {
            return isInitialized;
        }
        
        // ========================================
        // IStoreListener Callbacks (Unity IAP)
        // ========================================
        
#if UNITY_PURCHASING
        /// <summary>
        /// Called when Unity IAP is initialized successfully
        /// </summary>
        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            storeController = controller;
            extensionProvider = extensions;
            isInitialized = true;
            
            if (iapSettings.debugMode)
            {
                Debug.Log("[IAPManager] ✅ IAP Initialized Successfully!");
                Debug.Log($"[IAPManager] Available products: {controller.products.all.Length}");
                
                // Log all products with their prices
                foreach (Product product in controller.products.all)
                {
                    if (product.availableToPurchase)
                    {
                        Debug.Log($"[IAPManager] ✓ {product.metadata.localizedTitle} - {product.metadata.localizedPriceString}");
                    }
                }
            }
            
            // Load previous purchases from receipts
            LoadPurchasesFromReceipts();
        }
        
        /// <summary>
        /// Called when Unity IAP initialization fails
        /// </summary>
        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.LogError($"[IAPManager] ❌ IAP Initialization FAILED: {error}");
            
            string errorMessage = error switch
            {
                InitializationFailureReason.PurchasingUnavailable => 
                    "Purchasing unavailable (Check billing service)",
                InitializationFailureReason.NoProductsAvailable => 
                    "No products available (Check product IDs in store)",
                InitializationFailureReason.AppNotKnown => 
                    "App not known (Check bundle ID and store setup)",
                _ => error.ToString()
            };
            
            Debug.LogError($"[IAPManager] Error details: {errorMessage}");
            isInitialized = false;
        }
        
        /// <summary>
        /// Called when Unity IAP initialization fails (Unity 2020.3+ signature)
        /// </summary>
        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogError($"[IAPManager] ❌ IAP Initialization FAILED: {error}");
            Debug.LogError($"[IAPManager] Error message: {message}");
            isInitialized = false;
        }
        
        /// <summary>
        /// Called when a purchase completes
        /// </summary>
        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs purchaseEvent)
        {
            string purchasedProductId = purchaseEvent.purchasedProduct.definition.id;
            
            if (iapSettings.debugMode)
                Debug.Log($"[IAPManager] 🛒 Processing purchase: {purchasedProductId}");
            
            // Find our product definition
            var product = FindProductByPlatformId(purchasedProductId);
            if (product == null)
            {
                Debug.LogError($"[IAPManager] ❌ Purchased product not found in settings: {purchasedProductId}");
                return PurchaseProcessingResult.Complete;
            }
            
            // Validate receipt (if enabled)
            bool isValid = ValidateReceipt(purchaseEvent.purchasedProduct.receipt);
            if (!isValid)
            {
                Debug.LogError($"[IAPManager] ❌ Receipt validation FAILED for: {product.productName}");
                OnPurchaseFailed?.Invoke(product.productId);
                return PurchaseProcessingResult.Complete;
            }
            
            // Process successful purchase
            ProcessPurchaseSuccess(product.productId, product);
            
            return PurchaseProcessingResult.Complete;
        }
        
        /// <summary>
        /// Called when a purchase fails
        /// </summary>
        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            Debug.LogWarning($"[IAPManager] ⚠ Purchase FAILED: {product.definition.id}");
            Debug.LogWarning($"[IAPManager] Reason: {failureReason}");
            
            string errorMessage = failureReason switch
            {
                PurchaseFailureReason.PurchasingUnavailable => "Purchasing unavailable",
                PurchaseFailureReason.ExistingPurchasePending => "Existing purchase pending",
                PurchaseFailureReason.ProductUnavailable => "Product unavailable",
                PurchaseFailureReason.SignatureInvalid => "Signature invalid",
                PurchaseFailureReason.UserCancelled => "User cancelled",
                PurchaseFailureReason.PaymentDeclined => "Payment declined",
                PurchaseFailureReason.DuplicateTransaction => "Duplicate transaction",
                PurchaseFailureReason.Unknown => "Unknown error",
                _ => failureReason.ToString()
            };
            
            Debug.LogWarning($"[IAPManager] Details: {errorMessage}");
            
            // Find our product definition
            var ourProduct = FindProductByPlatformId(product.definition.id);
            string productId = ourProduct?.productId ?? product.definition.id;
            
            OnPurchaseFailed?.Invoke(productId);
        }
        
        /// <summary>
        /// Find product by platform-specific ID
        /// </summary>
        private IAPProduct FindProductByPlatformId(string platformProductId)
        {
            var products = iapSettings.GetAllProducts();
            foreach (var product in products)
            {
                string ourPlatformId = product.GetPlatformProductId(iapSettings.usePlatformSpecificIds);
                if (ourPlatformId == platformProductId)
                    return product;
            }
            return null;
        }
        
        /// <summary>
        /// Validate receipt using platform-specific validation
        /// </summary>
        private bool ValidateReceipt(string receipt)
        {
            // Always validate in production builds
            #if !UNITY_EDITOR
            
            try
            {
                var validator = new CrossPlatformValidator(
                    GooglePlayTangle.Data(),
                    AppleTangle.Data(),
                    Application.identifier
                );
                
                var result = validator.Validate(receipt);
                
                if (iapSettings.debugMode)
                    Debug.Log($"[IAPManager] ✅ Receipt validation SUCCESS ({result.Length} items)");
                
                return true;
            }
            catch (IAPSecurityException ex)
            {
                Debug.LogError($"[IAPManager] ❌ Receipt validation FAILED: {ex.Message}");
                return false;
            }
            
            #else
            // Skip validation in editor
            if (iapSettings.debugMode)
                Debug.Log("[IAPManager] ℹ Receipt validation skipped (Editor mode)");
            return true;
            #endif
        }
        
        /// <summary>
        /// Load purchases from receipts (for Non-Consumable products)
        /// </summary>
        private void LoadPurchasesFromReceipts()
        {
            if (storeController == null) return;
            
            foreach (Product product in storeController.products.all)
            {
                if (product.hasReceipt)
                {
                    var ourProduct = FindProductByPlatformId(product.definition.id);
                    if (ourProduct != null && ourProduct.productType == IAPProductType.NonConsumable)
                    {
                        purchasedProducts[ourProduct.productId] = true;
                        
                        if (iapSettings.debugMode)
                            Debug.Log($"[IAPManager] ✓ Loaded receipt: {ourProduct.productName}");
                        
                        // Re-apply No Ads if purchased
                        if (ourProduct.isNoAdsProduct)
                        {
                            Monetization.AdvertisingSystem.DisableAds();
                            if (iapSettings.debugMode)
                                Debug.Log("[IAPManager] 🚫 No Ads re-applied from receipt");
                        }
                    }
                }
            }
        }
#endif
    }
}


