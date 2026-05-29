using UnityEditor;
using UnityEngine;
using EKStudio.Monetization;
using EKStudio.Monetization.Networks;
using System.Collections.Generic;

namespace EKStudio.Monetization.EditorTools
{
    /// <summary>
    /// Modern tab-based custom editor for AdConfiguration
    /// </summary>
    [CustomEditor(typeof(AdConfiguration))]
    public class AdConfigurationEditor : UnityEditor.Editor
    {
        // Serialized Properties
        private SerializedProperty bannerType;
        private SerializedProperty interstitialType;
        private SerializedProperty rewardedVideoType;
        private SerializedProperty testMode;
        private SerializedProperty systemLogs;
        private SerializedProperty interstitialFirstStartDelay;
        private SerializedProperty interstitialStartDelay;
        private SerializedProperty interstitialShowingDelay;
        private SerializedProperty autoShowInterstitial;

        // Provider Properties
        private SerializedProperty mockAdSettings;
        private SerializedProperty levelPlaySettings;
        private SerializedProperty adMobSettings;
        private SerializedProperty unityAdsSettings;

        // Privacy & GDPR Properties
        private SerializedProperty enableGDPR;
        private SerializedProperty gdprCanvasPrefab;
        private SerializedProperty privacyPolicyUrl;
        private SerializedProperty termsOfUseUrl;
        private SerializedProperty enableIDFA;
        private SerializedProperty showGDPROnFirstLaunch;

        // Tab System
        private enum EditorTab
        {
            QuickSetup,
            AdProviders,
            TimingSettings,
            PrivacyGDPR,
            AdvancedSettings
        }

        private EditorTab currentTab = EditorTab.QuickSetup;
        private static readonly string[] tabNames = { "Quick Setup", "Ad Providers", "Timing", "Privacy & GDPR", "Advanced" };
        
        // Colors
        private static Color headerColor = new Color(0.15f, 0.55f, 0.95f);
        private static Color successColor = new Color(0.2f, 0.8f, 0.3f);
        private static Color warningColor = new Color(0.95f, 0.7f, 0.2f);
        private static Color errorColor = new Color(0.9f, 0.3f, 0.3f);

        private void OnEnable()
        {
            // Find all properties
            bannerType = serializedObject.FindProperty("bannerType");
            interstitialType = serializedObject.FindProperty("interstitialType");
            rewardedVideoType = serializedObject.FindProperty("rewardedVideoType");
            testMode = serializedObject.FindProperty("testMode");
            systemLogs = serializedObject.FindProperty("systemLogs");
            interstitialFirstStartDelay = serializedObject.FindProperty("interstitialFirstStartDelay");
            interstitialStartDelay = serializedObject.FindProperty("interstitialStartDelay");
            interstitialShowingDelay = serializedObject.FindProperty("interstitialShowingDelay");
            autoShowInterstitial = serializedObject.FindProperty("autoShowInterstitial");

            mockAdSettings = serializedObject.FindProperty("mockAdSettings");
            levelPlaySettings = serializedObject.FindProperty("levelPlaySettings");
            adMobSettings = serializedObject.FindProperty("adMobSettings");
            unityAdsSettings = serializedObject.FindProperty("unityAdsSettings");

            enableGDPR = serializedObject.FindProperty("enableGDPR");
            gdprCanvasPrefab = serializedObject.FindProperty("gdprCanvasPrefab");
            privacyPolicyUrl = serializedObject.FindProperty("privacyPolicyUrl");
            termsOfUseUrl = serializedObject.FindProperty("termsOfUseUrl");
            enableIDFA = serializedObject.FindProperty("enableIDFA");
            showGDPROnFirstLaunch = serializedObject.FindProperty("showGDPROnFirstLaunch");

            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawCustomHeader();
            EditorGUILayout.Space(10);
            
            DrawTabBar();
            EditorGUILayout.Space(5);
            
            DrawCurrentTab();

            serializedObject.ApplyModifiedProperties();
        }

        #region Header
        private void DrawCustomHeader()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            // Main Title
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = headerColor }
            };
            EditorGUILayout.LabelField("EK Studio Advertisement System", titleStyle);
            
            // Version & Status
            EditorGUILayout.Space(3);
            var subtitleStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 10,
                fontStyle = FontStyle.Italic
            };
            EditorGUILayout.LabelField("v1.2 | Monetization Configuration", subtitleStyle);
            
            EditorGUILayout.EndVertical();
        }
        #endregion

        #region Tab Bar
        private void DrawTabBar()
        {
            EditorGUILayout.BeginHorizontal();
            
            for (int i = 0; i < tabNames.Length; i++)
            {
                var isSelected = (int)currentTab == i;
                var tabStyle = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
                    fontSize = 11
                };

                if (isSelected)
                {
                    GUI.backgroundColor = headerColor;
                }

                if (GUILayout.Button(tabNames[i], tabStyle, GUILayout.Height(30)))
                {
                    currentTab = (EditorTab)i;
                }

                GUI.backgroundColor = Color.white;
            }
            
            EditorGUILayout.EndHorizontal();
        }
        #endregion

        #region Tab Content
        private void DrawCurrentTab()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            switch (currentTab)
            {
                case EditorTab.QuickSetup:
                    DrawQuickSetupTab();
                    break;
                case EditorTab.AdProviders:
                    DrawAdProvidersTab();
                    break;
                case EditorTab.TimingSettings:
                    DrawTimingSettingsTab();
                    break;
                case EditorTab.PrivacyGDPR:
                    DrawPrivacyGDPRTab();
                    break;
                case EditorTab.AdvancedSettings:
                    DrawAdvancedSettingsTab();
                    break;
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawQuickSetupTab()
        {
            DrawSectionHeader("Quick Setup", "Configure your ad types and test mode");
            
            EditorGUILayout.Space(10);
            
            // Test Mode Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Development Mode", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            testMode.boolValue = EditorGUILayout.Toggle(new GUIContent("Enable Test Mode", "Show test ads during development"), testMode.boolValue);
            systemLogs.boolValue = EditorGUILayout.Toggle(new GUIContent("Enable Debug Logs", "Log ad system events to console"), systemLogs.boolValue);
            
            if (testMode.boolValue)
            {
                EditorGUILayout.HelpBox("✓ Test mode is ENABLED. You will see test ads.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("⚠ Test mode is DISABLED. Real ads will be shown.", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
            
            // Ad Type Selection
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Ad Type Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            DrawProviderDropdown("Banner Ads", bannerType, "Always visible at top/bottom of screen");
            DrawProviderDropdown("Interstitial Ads", interstitialType, "Full-screen ads between gameplay");
            DrawProviderDropdown("Rewarded Video", rewardedVideoType, "Video ads that give player rewards");
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
            
            // Quick Actions
            DrawQuickActions();
        }

        private void DrawAdProvidersTab()
        {
            DrawSectionHeader("Ad Network Providers", "Configure your ad network credentials");
            
            EditorGUILayout.Space(10);
            
            // Mock Provider
            DrawProviderSection("Mock Ads (Testing)", mockAdSettings, new Color(0.9f, 0.7f, 0.2f), 
                "Use mock ads for development and testing without real ad network integration",
                null, 
                null);
            
            // AdMob Provider
            DrawProviderSection("Google AdMob", adMobSettings, new Color(0.26f, 0.52f, 0.96f), 
                "Google's mobile advertising platform",
                "https://apps.admob.com",
                "https://developers.google.com/admob/unity/start");
            
            // Unity Ads Provider
            DrawProviderSection("Unity Ads (Legacy)", unityAdsSettings, new Color(0.13f, 0.13f, 0.13f), 
                "Unity's classic advertising solution",
                "https://operate.dashboard.unity3d.com",
                "https://docs.unity.com/ads/");
            
            // LevelPlay Provider
            DrawProviderSection("LevelPlay (ironSource)", levelPlaySettings, new Color(0.24f, 0.71f, 0.29f), 
                "Unity's advanced mediation platform",
                "https://platform.ironsrc.com/",
                "https://developers.is.com/ironsource-mobile/unity/");
        }

        private void DrawTimingSettingsTab()
        {
            DrawSectionHeader("Timing Configuration", "Control when and how often ads appear");
            
            EditorGUILayout.Space(10);
            
            // Interstitial Timing
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Interstitial Ad Timing", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            EditorGUILayout.PropertyField(interstitialFirstStartDelay, 
                new GUIContent("First Launch Delay (seconds)", "Delay before showing first interstitial ad"));
            
            EditorGUILayout.PropertyField(interstitialStartDelay, 
                new GUIContent("Subsequent Launch Delay (seconds)", "Delay on later app launches"));
            
            EditorGUILayout.PropertyField(interstitialShowingDelay, 
                new GUIContent("Between Ads Delay (seconds)", "Minimum time between interstitial ads"));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("⏱ Timing values help prevent ad fatigue and improve user experience", MessageType.Info);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            // Auto Show Settings
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Automatic Display", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            autoShowInterstitial.boolValue = EditorGUILayout.Toggle(
                new GUIContent("Auto-Show Interstitials", "Automatically display interstitials when conditions are met"), 
                autoShowInterstitial.boolValue);
            
            if (autoShowInterstitial.boolValue)
            {
                EditorGUILayout.HelpBox("✓ Interstitials will show automatically based on timing rules", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Manual control: You must call DisplayInterstitialAd() in code", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            DrawTimingPreview();
        }

        private void DrawPrivacyGDPRTab()
        {
            DrawSectionHeader("Privacy & GDPR Configuration", "Configure privacy consent and compliance settings");
            
            EditorGUILayout.Space(10);
            
            // GDPR Enable Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("GDPR Consent System", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            enableGDPR.boolValue = EditorGUILayout.Toggle(
                new GUIContent("Enable GDPR", "Show consent dialog for GDPR compliance (EU users)"), 
                enableGDPR.boolValue);
            
            if (enableGDPR.boolValue)
            {
                EditorGUILayout.Space(3);
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(gdprCanvasPrefab, 
                    new GUIContent("GDPR Canvas Prefab", "The GDPR consent dialog prefab with GDPRCanvas component"));
                
                // Validation for canvas prefab
                if (gdprCanvasPrefab.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("⚠ GDPR Canvas Prefab is required when GDPR is enabled", MessageType.Warning);
                    
                    EditorGUILayout.Space(3);
                    if (GUILayout.Button("📋 Locate GDPR Canvas Prefab", GUILayout.Height(25)))
                    {
                        // Try to find GDPR Canvas prefab in the project
                        string[] guids = AssetDatabase.FindAssets("t:Prefab GDPRCanvas");
                        if (guids.Length > 0)
                        {
                            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                            if (prefab != null && prefab.GetComponent<GDPRCanvas>() != null)
                            {
                                gdprCanvasPrefab.objectReferenceValue = prefab.GetComponent<GDPRCanvas>();
                                serializedObject.ApplyModifiedProperties();
                                Debug.Log($"[AdConfiguration] GDPR Canvas Prefab found and assigned: {path}");
                            }
                        }
                        else
                        {
                            Debug.LogWarning("[AdConfiguration] GDPR Canvas Prefab not found in project");
                        }
                    }
                }
                else
                {
                    // Check if the prefab has GDPRCanvas component
                    var prefab = gdprCanvasPrefab.objectReferenceValue as GDPRCanvas;
                    if (prefab == null)
                    {
                        EditorGUILayout.HelpBox("⚠ Assigned prefab must have GDPRCanvas component", MessageType.Error);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("✓ GDPR Canvas Prefab is assigned correctly", MessageType.Info);
                    }
                }
                
                EditorGUILayout.Space(5);
                
                showGDPROnFirstLaunch.boolValue = EditorGUILayout.Toggle(
                    new GUIContent("Show on First Launch", "Display GDPR dialog on first app launch"), 
                    showGDPROnFirstLaunch.boolValue);
                
                EditorGUI.indentLevel--;
                
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("✓ GDPR consent dialog will be shown to users", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("GDPR consent dialog is disabled", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
            
            // Privacy Links Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Privacy Policy & Terms", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            privacyPolicyUrl.stringValue = EditorGUILayout.TextField(
                new GUIContent("Privacy Policy URL", "Link to your privacy policy page"), 
                privacyPolicyUrl.stringValue);
            
            EditorGUILayout.Space(3);
            
            termsOfUseUrl.stringValue = EditorGUILayout.TextField(
                new GUIContent("Terms of Use URL", "Link to your terms of service page"), 
                termsOfUseUrl.stringValue);
            
            EditorGUILayout.Space(5);
            
            // Validation warnings
            if (enableGDPR.boolValue)
            {
                bool hasPrivacyUrl = !string.IsNullOrEmpty(privacyPolicyUrl.stringValue);
                bool hasTermsUrl = !string.IsNullOrEmpty(termsOfUseUrl.stringValue);
                
                if (!hasPrivacyUrl || !hasTermsUrl)
                {
                    EditorGUILayout.HelpBox("⚠ Privacy policy and terms URLs are recommended for GDPR compliance", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("✓ Privacy links configured", MessageType.Info);
                }
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
            
            // IDFA Section (iOS)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("IDFA Tracking (iOS Only)", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            enableIDFA.boolValue = EditorGUILayout.Toggle(
                new GUIContent("Enable IDFA", "Request IDFA tracking permission on iOS devices"), 
                enableIDFA.boolValue);
            
            EditorGUILayout.Space(5);
            
            if (enableIDFA.boolValue)
            {
                EditorGUILayout.HelpBox("✓ IDFA tracking will be requested on iOS (App Tracking Transparency)", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("IDFA tracking is disabled. Limited ad tracking on iOS.", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
            
            // GDPR Status Info
            DrawGDPRStatusInfo();
            
            // Documentation
            DrawGDPRDocumentation();
        }

        private void DrawGDPRStatusInfo()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Current GDPR Status (Runtime)", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            if (Application.isPlaying)
            {
                bool hasConsent = GDPRManager.HasGDPRConsent();
                bool hasBeenShown = GDPRManager.HasGDPRBeenShown();
                
                EditorGUILayout.LabelField("GDPR Shown:", hasBeenShown ? "✓ Yes" : "✗ No");
                EditorGUILayout.LabelField("User Consent:", hasConsent ? "✓ Granted" : "✗ Not Granted");
                
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("Reset GDPR Consent (Testing)", GUILayout.Height(25)))
                {
                    GDPRManager.ResetConsentData();
                    Debug.Log("[AdConfiguration] GDPR consent reset for testing");
                }
            }
            else
            {
                EditorGUILayout.HelpBox("GDPR status is only available during Play mode", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawGDPRDocumentation()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("GDPR Documentation", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("📘 GDPR Guidelines", GUILayout.Height(25)))
            {
                Application.OpenURL("https://gdpr.eu/what-is-gdpr/");
            }
            
            if (GUILayout.Button("🍎 Apple ATT Guide", GUILayout.Height(25)))
            {
                Application.OpenURL("https://developer.apple.com/app-store/user-privacy-and-data-use/");
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        private void DrawAdvancedSettingsTab()
        {
            DrawSectionHeader("Advanced Configuration", "Expert settings and diagnostics");
            
            EditorGUILayout.Space(10);
            
            // System Information
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("System Information", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            EditorGUILayout.LabelField("Namespace:", "EKStudio.Monetization");
            EditorGUILayout.LabelField("Core Class:", "AdvertisingSystem");
            EditorGUILayout.LabelField("Provider Location:", "EKStudio.Monetization.Networks");
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
            
            // Diagnostics
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Diagnostics & Tools", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("🔍 Run SDK Diagnostic", GUILayout.Height(30)))
            {
                EditorApplication.ExecuteMenuItem("Tools/Ad SDK Diagnostic");
            }
            
            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox("Diagnostic tool checks if ad SDKs are properly installed and configured", MessageType.Info);
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
            
            // Debug Options
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Debug Options", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            systemLogs.boolValue = EditorGUILayout.Toggle("Verbose Logging", systemLogs.boolValue);
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            DrawDocumentationLinks();
        }
        #endregion

        #region Helper Methods
        private void DrawSectionHeader(string title, string description)
        {
            var headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            
            EditorGUILayout.LabelField(title, headerStyle);
            
            if (!string.IsNullOrEmpty(description))
            {
                var descStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    wordWrap = true
                };
                EditorGUILayout.LabelField(description, descStyle);
            }
        }

        private void DrawProviderDropdown(string label, SerializedProperty property, string tooltip)
        {
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.Width(150));
            
            var prevValue = (AdProvider)property.enumValueIndex;
            EditorGUILayout.PropertyField(property, GUIContent.none);
            var newValue = (AdProvider)property.enumValueIndex;
            
            // Show status indicator
            if (newValue != AdProvider.Disable)
            {
                GUI.color = successColor;
                EditorGUILayout.LabelField("●", GUILayout.Width(15));
                GUI.color = Color.white;
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawProviderSection(string title, SerializedProperty property, Color accentColor, 
            string description, string dashboardUrl, string docsUrl)
        {
            // Header with colored accent
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            
            // Color indicator
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = accentColor;
            GUILayout.Box("", GUILayout.Width(4), GUILayout.ExpandHeight(true));
            GUI.backgroundColor = oldColor;
            
            EditorGUILayout.BeginVertical();
            
            // Foldout
            var foldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, title, true, foldoutStyle);
            
            if (property.isExpanded)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(5);
                
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(property, GUIContent.none, true);
                EditorGUI.indentLevel--;
                
                // Action Buttons
                if (!string.IsNullOrEmpty(dashboardUrl) || !string.IsNullOrEmpty(docsUrl))
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.BeginHorizontal();
                    
                    if (!string.IsNullOrEmpty(dashboardUrl))
                    {
                        if (GUILayout.Button("📊 Dashboard", GUILayout.Height(25)))
                        {
                            Application.OpenURL(dashboardUrl);
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(docsUrl))
                    {
                        if (GUILayout.Button("📖 Documentation", GUILayout.Height(25)))
                        {
                            Application.OpenURL(docsUrl);
                        }
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);
        }

        private void DrawQuickActions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("🚀 Configure Providers →", GUILayout.Height(30)))
            {
                currentTab = EditorTab.AdProviders;
            }
            
            if (GUILayout.Button("⏱ Setup Timing →", GUILayout.Height(30)))
            {
                currentTab = EditorTab.TimingSettings;
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawTimingPreview()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Timing Preview", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            float firstDelay = interstitialFirstStartDelay.floatValue;
            float normalDelay = interstitialStartDelay.floatValue;
            float betweenDelay = interstitialShowingDelay.floatValue;
            
            EditorGUILayout.LabelField($"First session: Ads start after {firstDelay}s");
            EditorGUILayout.LabelField($"Later sessions: Ads start after {normalDelay}s");
            EditorGUILayout.LabelField($"Between ads: Minimum {betweenDelay}s gap");
            
            EditorGUILayout.EndVertical();
        }

        private void DrawDocumentationLinks()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Documentation & Support", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("📚 View README", GUILayout.Height(25)))
            {
                var readmePath = System.IO.Path.Combine(Application.dataPath, "EKStudioCore/Monetization/Ads/README.md");
                if (System.IO.File.Exists(readmePath))
                {
                    Application.OpenURL("file:///" + readmePath);
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        #endregion
    }
}
