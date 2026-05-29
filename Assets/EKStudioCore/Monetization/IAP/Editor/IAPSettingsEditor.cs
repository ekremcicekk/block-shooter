using UnityEditor;
using UnityEngine;

namespace EKStudio.IAP
{
    /// <summary>
    /// Custom Editor for IAPSettings providing user-friendly interface
    /// for managing In-App Purchase products
    /// </summary>
    [CustomEditor(typeof(IAPSettings))]
    public class IAPSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty enableIAP;
        private SerializedProperty enableNoAds;
        private SerializedProperty noAdsProductId;
        private SerializedProperty noAdsPrice;
        private SerializedProperty customProducts;
        private SerializedProperty googlePlayPublicKey;
        private SerializedProperty appleSharedSecret;
        private SerializedProperty usePlatformSpecificIds;
        private SerializedProperty debugMode;
        
        private bool showNoAdsSettings = true;
        private bool showCustomProducts = true;
        private bool showStoreConfiguration = false;
        
        // Foldout states for each product (index -> isExpanded)
        private System.Collections.Generic.Dictionary<int, bool> productFoldoutStates = new System.Collections.Generic.Dictionary<int, bool>();
        
        private void OnEnable()
        {
            enableIAP = serializedObject.FindProperty("enableIAP");
            enableNoAds = serializedObject.FindProperty("enableNoAds");
            noAdsProductId = serializedObject.FindProperty("noAdsProductId");
            noAdsPrice = serializedObject.FindProperty("noAdsPrice");
            customProducts = serializedObject.FindProperty("customProducts");
            googlePlayPublicKey = serializedObject.FindProperty("googlePlayPublicKey");
            appleSharedSecret = serializedObject.FindProperty("appleSharedSecret");
            usePlatformSpecificIds = serializedObject.FindProperty("usePlatformSpecificIds");
            debugMode = serializedObject.FindProperty("debugMode");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            IAPSettings settings = (IAPSettings)target;
            
            // Header
            DrawHeader();
            
            GUILayout.Space(10);
            
            // IAP System Toggle
            DrawIAPSystemToggle();
            
            if (!enableIAP.boolValue)
            {
                EditorGUILayout.HelpBox("IAP System is disabled. Enable it to configure products.", MessageType.Warning);
                serializedObject.ApplyModifiedProperties();
                return;
            }
            
            GUILayout.Space(10);
            
            // No Ads Configuration
            DrawNoAdsSection();
            
            GUILayout.Space(10);
            
            // Custom Products Section
            DrawCustomProductsSection();
            
            GUILayout.Space(10);
            
            // Store Configuration
            DrawStoreConfiguration();
            
            GUILayout.Space(10);
            
            // Quick Actions
            DrawQuickActions(settings);
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private new void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.3f, 0.7f, 0.3f) }
            };
            
            EditorGUILayout.LabelField("💰 IAP SETTINGS", headerStyle);
            EditorGUILayout.LabelField("Configure In-App Purchases for your game", EditorStyles.centeredGreyMiniLabel);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawIAPSystemToggle()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("IAP System", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(enableIAP, new GUIContent("Enable IAP System", 
                "Master toggle for entire IAP system"));
            
            if (EditorGUI.EndChangeCheck() && enableIAP.boolValue)
            {
                Debug.Log("[IAPSettings] IAP System enabled");
            }
            
            EditorGUILayout.PropertyField(debugMode, new GUIContent("Debug Mode", 
                "Enable detailed logs for IAP operations"));
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawNoAdsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            showNoAdsSettings = EditorGUILayout.Foldout(showNoAdsSettings, 
                "🚫 Remove Ads Product", true, EditorStyles.foldoutHeader);
            
            if (showNoAdsSettings)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(enableNoAds, new GUIContent("Enable No Ads Product", 
                    "Shows No Ads button in game UI"));
                
                if (enableNoAds.boolValue)
                {
                    EditorGUILayout.Space(5);
                    
                    EditorGUILayout.PropertyField(noAdsProductId, new GUIContent("Product ID", 
                        "Unique ID matching Store product (both iOS & Android)"));
                    
                    // Validate Product ID
                    if (string.IsNullOrEmpty(noAdsProductId.stringValue))
                    {
                        EditorGUILayout.HelpBox("⚠ Product ID is required!", MessageType.Error);
                    }
                    else if (!noAdsProductId.stringValue.Contains("."))
                    {
                        EditorGUILayout.HelpBox("Product ID should follow format: com.company.app.product", MessageType.Warning);
                    }
                    
                    EditorGUILayout.PropertyField(noAdsPrice, new GUIContent("Display Price", 
                        "Price shown to players (e.g., $2.99)"));
                    
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox(
                        "💡 Remove Ads is Non-Consumable (one-time purchase, permanent)\n" +
                        "• Removes banner and interstitial ads\n" +
                        "• Keeps rewarded videos (player rewards)\n" +
                        "• Purchase persists across devices", 
                        MessageType.Info);
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawCustomProductsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            showCustomProducts = EditorGUILayout.Foldout(showCustomProducts, 
                "🛒 Custom IAP Products (" + customProducts.arraySize + ")", true, EditorStyles.foldoutHeader);
            
            if (showCustomProducts)
            {
                EditorGUI.indentLevel++;
                
                if (customProducts.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("No custom products added yet.\nClick 'Add New Product' to create one.", MessageType.Info);
                }
                
                // Draw each product
                for (int i = 0; i < customProducts.arraySize; i++)
                {
                    DrawProductCard(i);
                }
                
                EditorGUILayout.Space(5);
                
                // Add new product button
                if (GUILayout.Button("➕ Add New Product", GUILayout.Height(30)))
                {
                    customProducts.InsertArrayElementAtIndex(customProducts.arraySize);
                    var newProduct = customProducts.GetArrayElementAtIndex(customProducts.arraySize - 1);
                    
                    // Initialize new product
                    newProduct.FindPropertyRelative("productId").stringValue = "com.yourcompany.yourapp.newproduct";
                    newProduct.FindPropertyRelative("productName").stringValue = "New Product";
                    newProduct.FindPropertyRelative("productType").enumValueIndex = (int)IAPProductType.Consumable;
                    newProduct.FindPropertyRelative("rewardType").enumValueIndex = (int)IAPRewardType.Coin;
                    newProduct.FindPropertyRelative("purchaseMethod").enumValueIndex = (int)IAPPurchaseMethod.RealMoney;
                    newProduct.FindPropertyRelative("price").stringValue = "$0.99";
                    newProduct.FindPropertyRelative("description").stringValue = "Product description";
                    newProduct.FindPropertyRelative("coinReward").intValue = 0;
                    newProduct.FindPropertyRelative("gemReward").intValue = 0;
                    newProduct.FindPropertyRelative("timedFreeCooldown").intValue = 3600;
                    newProduct.FindPropertyRelative("includesNoAds").boolValue = false;
                    newProduct.FindPropertyRelative("customValue").intValue = 0;
                    newProduct.FindPropertyRelative("customData").stringValue = "";
                    newProduct.FindPropertyRelative("isNoAdsProduct").boolValue = false;
                    
                    Debug.Log("[IAPSettings] New product added");
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawProductCard(int index)
        {
            var product = customProducts.GetArrayElementAtIndex(index);
            
            var productId = product.FindPropertyRelative("productId");
            var productName = product.FindPropertyRelative("productName");
            
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical("box");
            
            // Initialize foldout state if not exists
            if (!productFoldoutStates.ContainsKey(index))
            {
                productFoldoutStates[index] = false; // Closed by default
            }
            
            // Product Header with Foldout
            EditorGUILayout.BeginHorizontal();
            
            // Foldout arrow
            productFoldoutStates[index] = EditorGUILayout.Foldout(
                productFoldoutStates[index], 
                $"Product {index + 1}: {productName.stringValue}", 
                true, 
                EditorStyles.foldoutHeader
            );
            
            GUILayout.FlexibleSpace();
            
            // Delete button
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("✖", GUILayout.Width(25), GUILayout.Height(20)))
            {
                if (EditorUtility.DisplayDialog("Delete Product", 
                    $"Are you sure you want to delete '{productName.stringValue}'?", "Delete", "Cancel"))
                {
                    customProducts.DeleteArrayElementAtIndex(index);
                    productFoldoutStates.Remove(index);
                    return;
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            // Show quick info when collapsed
            if (!productFoldoutStates[index])
            {
                EditorGUI.indentLevel++;
                
                GUIStyle miniStyle = new GUIStyle(EditorStyles.miniLabel);
                miniStyle.normal.textColor = Color.grey;
                
                EditorGUILayout.BeginHorizontal();
                
                // Product Type badge
                var collapsedProductType = product.FindPropertyRelative("productType");
                string typeIcon = GetProductTypeBadge((IAPProductType)collapsedProductType.enumValueIndex);
                EditorGUILayout.LabelField(typeIcon, miniStyle, GUILayout.Width(120));
                
                // Reward Type badge
                var collapsedRewardType = product.FindPropertyRelative("rewardType");
                string rewardIcon = GetRewardTypeBadge((IAPRewardType)collapsedRewardType.enumValueIndex);
                EditorGUILayout.LabelField(rewardIcon, miniStyle, GUILayout.Width(100));
                
                // Purchase Method badge
                var collapsedPurchaseMethod = product.FindPropertyRelative("purchaseMethod");
                var collapsedPrice = product.FindPropertyRelative("price");
                string methodIcon = GetPurchaseMethodBadge((IAPPurchaseMethod)collapsedPurchaseMethod.enumValueIndex, collapsedPrice.stringValue);
                EditorGUILayout.LabelField(methodIcon, miniStyle, GUILayout.Width(100));
                
                // No Ads badge
                var collapsedIncludesNoAds = product.FindPropertyRelative("includesNoAds");
                if (collapsedIncludesNoAds.boolValue)
                {
                    GUIStyle noAdStyle = new GUIStyle(EditorStyles.miniLabel);
                    noAdStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                    EditorGUILayout.LabelField("🚫 +No Ads", noAdStyle, GUILayout.Width(80));
                }
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUI.indentLevel--;
                
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Get all properties
            var androidProductId = product.FindPropertyRelative("androidProductId");
            var iosProductId = product.FindPropertyRelative("iosProductId");
            var productType = product.FindPropertyRelative("productType");
            var rewardType = product.FindPropertyRelative("rewardType");
            var purchaseMethod = product.FindPropertyRelative("purchaseMethod");
            var price = product.FindPropertyRelative("price");
            var description = product.FindPropertyRelative("description");
            var coinReward = product.FindPropertyRelative("coinReward");
            var gemReward = product.FindPropertyRelative("gemReward");
            var boosterRewards = product.FindPropertyRelative("boosterRewards");
            var timedFreeCooldown = product.FindPropertyRelative("timedFreeCooldown");
            var includesNoAds = product.FindPropertyRelative("includesNoAds");
            var customValue = product.FindPropertyRelative("customValue");
            var customData = product.FindPropertyRelative("customData");
            
            EditorGUILayout.Space(3);
            
            // Product ID
            EditorGUILayout.PropertyField(productId, new GUIContent("Product ID (Default)"));
            if (string.IsNullOrEmpty(productId.stringValue) || !productId.stringValue.Contains("."))
            {
                EditorGUILayout.HelpBox("Invalid Product ID format!", MessageType.Error);
            }
            
            // Platform-specific IDs (conditional)
            if (usePlatformSpecificIds.boolValue)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("Platform-Specific IDs (Optional)", EditorStyles.miniBoldLabel);
                
                EditorGUILayout.PropertyField(androidProductId, new GUIContent("🤖 Android ID", 
                    "Leave empty to use default Product ID"));
                EditorGUILayout.PropertyField(iosProductId, new GUIContent("🍎 iOS ID", 
                    "Leave empty to use default Product ID"));
                
                EditorGUILayout.HelpBox("If empty, default Product ID will be used for that platform", MessageType.Info);
            }
            
            EditorGUILayout.Space(3);
            
            // Product Name
            EditorGUILayout.PropertyField(productName, new GUIContent("Display Name"));
            
            // Product Type
            EditorGUILayout.PropertyField(productType, new GUIContent("Product Type"));
            
            // Type info
            IAPProductType type = (IAPProductType)productType.enumValueIndex;
            string typeInfo = GetProductTypeInfo(type);
            if (!string.IsNullOrEmpty(typeInfo))
            {
                EditorGUILayout.HelpBox(typeInfo, MessageType.Info);
            }
            
            EditorGUILayout.Space(3);
            
            // Reward Type
            EditorGUILayout.PropertyField(rewardType, new GUIContent("Reward Type", 
                "What does this product reward?"));
            
            // Purchase Method
            EditorGUILayout.PropertyField(purchaseMethod, new GUIContent("Purchase Method", 
                "How can this product be obtained?"));
            
            IAPPurchaseMethod method = (IAPPurchaseMethod)purchaseMethod.enumValueIndex;
            
            // Price (only for RealMoney and Multiple)
            if (method == IAPPurchaseMethod.RealMoney || method == IAPPurchaseMethod.Multiple)
            {
                EditorGUILayout.PropertyField(price, new GUIContent("Display Price"));
            }
            else if (method == IAPPurchaseMethod.TimedFree)
            {
                EditorGUILayout.PropertyField(timedFreeCooldown, new GUIContent("Cooldown (seconds)", 
                    "Time to wait before next free claim (default: 3600 = 1 hour)"));
                
                int hours = timedFreeCooldown.intValue / 3600;
                int minutes = (timedFreeCooldown.intValue % 3600) / 60;
                EditorGUILayout.HelpBox($"Cooldown: {hours}h {minutes}m", MessageType.Info);
            }
            else if (method == IAPPurchaseMethod.RewardedVideo)
            {
                EditorGUILayout.HelpBox("Player will watch a rewarded video to get this product", MessageType.Info);
            }
            
            // Description
            EditorGUILayout.PropertyField(description, new GUIContent("Description"));
            
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Reward Configuration", EditorStyles.miniBoldLabel);
            
            IAPRewardType reward = (IAPRewardType)rewardType.enumValueIndex;
            
            // Coin Reward
            if (reward == IAPRewardType.Coin || reward == IAPRewardType.Mixed)
            {
                EditorGUILayout.PropertyField(coinReward, new GUIContent("Coins to Give", 
                    "Coins awarded on purchase (0 = none)"));
            }
            
            // Gem Reward
            if (reward == IAPRewardType.Gem || reward == IAPRewardType.Mixed)
            {
                EditorGUILayout.PropertyField(gemReward, new GUIContent("Gems to Give", 
                    "Gems awarded on purchase (0 = none)"));
            }
            
            // Booster Rewards
            if (reward == IAPRewardType.Booster || reward == IAPRewardType.Mixed)
            {
                EditorGUILayout.PropertyField(boosterRewards, new GUIContent("Booster Rewards"), true);
                
                EditorGUILayout.HelpBox("Booster types: hammer, vacuum, freezetimer, all", MessageType.Info);
            }
            
            // Custom Data (for all types)
            if (reward == IAPRewardType.Custom || reward == IAPRewardType.Special)
            {
                EditorGUILayout.PropertyField(customValue, new GUIContent("Custom Value", 
                    "Use for any integer value"));
                
                EditorGUILayout.PropertyField(customData, new GUIContent("Custom Data", 
                    "Use for any string data"));
            }
            
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Special Features", EditorStyles.miniBoldLabel);
            
            // Includes No Ads checkbox
            EditorGUILayout.PropertyField(includesNoAds, new GUIContent("Include No Ads Removal", 
                "If checked, this product will also remove all ads permanently"));
            
            if (includesNoAds.boolValue)
            {
                EditorGUILayout.HelpBox("✅ This product will remove all banner and interstitial ads permanently", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private string GetProductTypeInfo(IAPProductType type)
        {
            switch (type)
            {
                case IAPProductType.NonConsumable:
                    return "✓ One-time purchase\n✓ Never expires\n✓ Restores on new device\nExample: Unlock Level Pack, Premium Version";
                case IAPProductType.Consumable:
                    return "✓ Can be purchased repeatedly\n✗ Does not restore\n✗ Cannot be refunded\nExample: Coin Pack, Extra Lives";
                case IAPProductType.Subscription:
                    return "✓ Recurring payment (monthly/yearly)\n✓ Auto-renews\n✓ Can be cancelled\nExample: VIP Membership, Ad-Free Subscription";
                default:
                    return "";
            }
        }
        
        private string GetProductTypeBadge(IAPProductType type)
        {
            switch (type)
            {
                case IAPProductType.NonConsumable:
                    return "📦 Non-Consumable";
                case IAPProductType.Consumable:
                    return "🔄 Consumable";
                case IAPProductType.Subscription:
                    return "⏰ Subscription";
                default:
                    return "❓ Unknown";
            }
        }
        
        private string GetRewardTypeBadge(IAPRewardType type)
        {
            switch (type)
            {
                case IAPRewardType.Coin:
                    return "💰 Coins";
                case IAPRewardType.Booster:
                    return "🔨 Boosters";
                case IAPRewardType.Gem:
                    return "💎 Gems";
                case IAPRewardType.Mixed:
                    return "🎁 Mixed";
                case IAPRewardType.Special:
                    return "⭐ Special";
                case IAPRewardType.Custom:
                    return "🎨 Custom";
                default:
                    return "❓ Unknown";
            }
        }
        
        private string GetPurchaseMethodBadge(IAPPurchaseMethod method, string price)
        {
            switch (method)
            {
                case IAPPurchaseMethod.RealMoney:
                    return $"💵 {price}";
                case IAPPurchaseMethod.TimedFree:
                    return "⏰ Timed Free";
                case IAPPurchaseMethod.RewardedVideo:
                    return "📺 Rewarded AD";
                case IAPPurchaseMethod.Multiple:
                    return $"🔀 {price}";
                default:
                    return "❓ Unknown";
            }
        }
        
        private void DrawStoreConfiguration()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            showStoreConfiguration = EditorGUILayout.Foldout(showStoreConfiguration, 
                "⚙ Store Configuration", true, EditorStyles.foldoutHeader);
            
            if (showStoreConfiguration)
            {
                EditorGUI.indentLevel++;
                
                // Android (Google Play) Configuration
                EditorGUILayout.LabelField("🤖 Android (Google Play)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(googlePlayPublicKey, new GUIContent("Public Key", 
                    "RSA public key from Google Play Console (required for receipt validation)"));
                
                if (string.IsNullOrEmpty(googlePlayPublicKey.stringValue))
                {
                    EditorGUILayout.HelpBox("⚠ Required for production Android builds!\n" +
                        "Optional for testing/development.", MessageType.Warning);
                }
                
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(
                    "📝 How to get Google Play Public Key:\n" +
                    "1. Go to Google Play Console\n" +
                    "2. Select your app\n" +
                    "3. Monetization → Monetization setup\n" +
                    "4. Copy 'Base64-encoded RSA public key'\n" +
                    "5. Paste here",
                    MessageType.Info);
                
                EditorGUILayout.Space(10);
                
                // iOS (App Store) Configuration
                EditorGUILayout.LabelField("🍎 iOS (App Store)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(appleSharedSecret, new GUIContent("Shared Secret", 
                    "Shared secret from App Store Connect (required for auto-renewable subscriptions)"));
                
                if (string.IsNullOrEmpty(appleSharedSecret.stringValue))
                {
                    EditorGUILayout.HelpBox("ℹ Optional for Non-Consumable and Consumable products.\n" +
                        "REQUIRED for auto-renewable Subscriptions.", MessageType.Info);
                }
                
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(
                    "📝 How to get Apple Shared Secret:\n" +
                    "1. Go to App Store Connect\n" +
                    "2. My Apps → Select your app\n" +
                    "3. App Information\n" +
                    "4. Scroll to 'App-Specific Shared Secret'\n" +
                    "5. Generate or copy existing secret",
                    MessageType.Info);
                
                EditorGUILayout.Space(10);
                
                // Platform-Specific Product IDs
                EditorGUILayout.LabelField("🌐 Platform-Specific Product IDs", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(usePlatformSpecificIds, new GUIContent("Enable Platform-Specific IDs", 
                    "Use different product IDs for Android and iOS"));
                
                if (usePlatformSpecificIds.boolValue)
                {
                    EditorGUILayout.HelpBox(
                        "✅ Each product can have separate Android and iOS product IDs.\n" +
                        "Example:\n" +
                        "• Android: com.company.app.coins100.android\n" +
                        "• iOS: com.company.app.coins100.ios",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "ℹ Products will use the same Product ID for all platforms.\n" +
                        "Example: com.company.app.coins100",
                        MessageType.Info);
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawQuickActions(IAPSettings settings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("📋 Copy All Product IDs", GUILayout.Height(25)))
            {
                CopyAllProductIds(settings);
            }
            
            if (GUILayout.Button("📝 Export Product List", GUILayout.Height(25)))
            {
                ExportProductList(settings);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void CopyAllProductIds(IAPSettings settings)
        {
            var products = settings.GetAllProducts();
            if (products.Count == 0)
            {
                EditorUtility.DisplayDialog("No Products", "No products configured yet.", "OK");
                return;
            }
            
            string ids = "";
            foreach (var product in products)
            {
                ids += product.productId + "\n";
            }
            
            EditorGUIUtility.systemCopyBuffer = ids;
            Debug.Log($"[IAPSettings] Copied {products.Count} product IDs to clipboard");
            EditorUtility.DisplayDialog("Copied", $"Copied {products.Count} product IDs to clipboard!", "OK");
        }
        
        private void ExportProductList(IAPSettings settings)
        {
            var products = settings.GetAllProducts();
            if (products.Count == 0)
            {
                EditorUtility.DisplayDialog("No Products", "No products configured yet.", "OK");
                return;
            }
            
            string export = "=== IAP PRODUCT LIST ===\n\n";
            foreach (var product in products)
            {
                export += $"Product: {product.productName}\n";
                export += $"ID: {product.productId}\n";
                export += $"Type: {product.GetProductTypeDisplay()}\n";
                export += $"Price: {product.price}\n";
                export += $"Description: {product.description}\n";
                if (product.coinReward > 0)
                    export += $"Coins: {product.coinReward}\n";
                export += "\n";
            }
            
            EditorGUIUtility.systemCopyBuffer = export;
            Debug.Log($"[IAPSettings] Exported {products.Count} products to clipboard");
            EditorUtility.DisplayDialog("Exported", $"Exported {products.Count} products to clipboard!", "OK");
        }
    }
}

