using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// EK Studio - Generic Welcome Window
// Reusable onboarding panel for game templates. It auto-opens on first load
// if any required dependency is missing and provides actions to resolve them.
// UI and comments are fully in English for reuse across projects.

namespace EKStudio.Onboarding
{
    internal class WelcomeWindow : EditorWindow
    {
        // Update this label per template branding when needed.
        private const string TemplateDisplayName = "Color Block Jam Game Template";

        // Per-project key so we only auto-open once when requirements are missing.
        private const string PrefsKeyPrefix = "EKStudio_WelcomeShown_";
        private static string ProjectKey => PrefsKeyPrefix + Application.dataPath.GetHashCode();

        // Requirement definition for extensibility.
        private class Requirement
        {
            public string Id;                   // unique key
            public string Title;                // short display title
            public string Description;          // user-facing description
            public Func<bool> CheckInstalled;   // returns true if present
            public string AssetUrl;             // optional asset store page
            public bool ShowPackageManager;     // whether to show a button to open Package Manager
        }

        private static List<Requirement> _requirements;
		// Cached requirement states to avoid expensive checks every OnGUI repaint
		private struct RequirementState { public bool installed; public string error; }
		private static Dictionary<string, RequirementState> _requirementStates;
		private static double _lastRequirementsRefreshTime;
		private const double RequirementsRefreshCooldownSec = 2.0; // throttle auto refreshes

		private static void EnsureRequirements()
        {
            if (_requirements != null) return;

            _requirements = new List<Requirement>
            {
                // Simple Toon shader package used by materials
                new Requirement
                {
                    Id = "simple-toon",
                    Title = "Simple Toon Shader",
                    Description = "3D block materials use Simple Toon shaders for visual appeal. Required for proper block appearance.",
                    CheckInstalled = IsSimpleToonInstalled,
                    AssetUrl = "https://assetstore.unity.com/packages/vfx/shaders/simple-toon-185038",
                    ShowPackageManager = true
                },
                // NaughtyAttributes for enhanced inspector
                new Requirement
                {
                    Id = "naughty-attributes",
                    Title = "NaughtyAttributes",
                    Description = "Enhanced Unity Inspector with attributes for better development experience. Used in scripts.",
                    CheckInstalled = IsNaughtyAttributesInstalled,
                    AssetUrl = "https://github.com/dbrizov/NaughtyAttributes",
                    ShowPackageManager = false
                },
                // DOTween for animations
                new Requirement
                {
                    Id = "dotween",
                    Title = "DOTween (HOTween v2)",
                    Description = "Professional tweening library for smooth block animations, UI transitions, and effects.",
                    CheckInstalled = IsDOTweenInstalled,
                    AssetUrl = "https://github.com/Demigiant/dotween",
                    ShowPackageManager = false
                },
                // LeanTouch for touch input
                new Requirement
                {
                    Id = "lean-touch",
                    Title = "Lean Touch",
                    Description = "Touch and drag input system for mobile and desktop. Essential for block dragging mechanic.",
                    CheckInstalled = IsLeanTouchInstalled,
                    AssetUrl = "https://assetstore.unity.com/packages/tools/input-management/lean-touch-30111",
                    ShowPackageManager = false
                },
                // QuickOutline for block selection
                new Requirement
                {
                    Id = "quick-outline",
                    Title = "Quick Outline",
                    Description = "Visual outline effect for selected blocks. Provides player feedback during interactions.",
                    CheckInstalled = IsQuickOutlineInstalled,
                    AssetUrl = "https://github.com/chrisnolet/QuickOutline",
                    ShowPackageManager = false
                },
                // UIEffect for UI enhancements
                new Requirement
                {
                    Id = "uieffect",
                    Title = "UIEffect-upm",
                    Description = "UI effects package for enhanced menu visuals, buttons, and particle effects.",
                    CheckInstalled = IsUIEffectInstalled,
                    AssetUrl = "https://github.com/mob-sakai/UIEffect",
                    ShowPackageManager = false
                },
                // Vibration for mobile feedback
                new Requirement
                {
                    Id = "vibration",
                    Title = "Vibration",
                    Description = "Mobile haptic feedback system for match success, combos, and level completion.",
                    CheckInstalled = IsVibrationInstalled,
                    AssetUrl = "https://github.com/Elringus/Vibration",
                    ShowPackageManager = false
                }
            };
        }

		private static void EnsureRequirementStates()
		{
			if (_requirementStates == null)
				_requirementStates = new Dictionary<string, RequirementState>();
		}

		// Refresh requirement states; force=true bypasses throttle
		private static void RefreshRequirementStates(bool force)
		{
			EnsureRequirements();
			EnsureRequirementStates();
			if (!force)
			{
				if (EditorApplication.timeSinceStartup - _lastRequirementsRefreshTime < RequirementsRefreshCooldownSec)
					return;
			}

			_lastRequirementsRefreshTime = EditorApplication.timeSinceStartup;
			foreach (var req in _requirements)
			{
				var state = new RequirementState { installed = false, error = null };
				try { state.installed = req.CheckInstalled(); }
				catch (Exception ex) { state.installed = false; state.error = ex.Message; }
				_requirementStates[req.Id] = state;
			}
		}

        [MenuItem("Tools/Welcome Guide", priority = 0)]
        public static void OpenFromMenu()
        {
            EnsureRequirements();
            ShowWindow(true);
        }

        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            // Delay the check to ensure Unity is fully loaded
            EditorApplication.delayCall += () =>
            {
                EnsureRequirements();

                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                // If any requirement is missing, always open the window on editor load
                // Do NOT mark as shown until all requirements are satisfied
                if (AnyRequirementMissing())
                {
                    EditorApplication.delayCall += () => { ShowWindow(false); };
                }
            };
        }

        private Vector2 _scroll;

        private void OnGUI()
        {
            EnsureRequirements();

            var allGood = !AnyRequirementMissing();

            // Modern background
            DrawModernBackground();

            // Header with modern styling
            GUILayout.Space(16);
            DrawModernHeader();
            GUILayout.Space(12);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;

                DrawModernSummary(allGood);
                GUILayout.Space(10);

                DrawModernRequirementsList();
                GUILayout.Space(12);
            }

            GUILayout.FlexibleSpace();
            DrawModernFooter(allGood);
            GUILayout.Space(8);
        }

        // Modern background with subtle gradient effect
        private void DrawModernBackground()
        {
            var rect = new Rect(0, 0, position.width, position.height);
            var bgColor = new Color(0.13f, 0.14f, 0.16f, 1f); // dark mode background
            EditorGUI.DrawRect(rect, bgColor);
        }

        private static void DrawModernHeader()
        {
            // Modern header with better styling
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(20);
                // Modern icon with gradient background
                var iconRect = GUILayoutUtility.GetRect(56, 56);
                
                // Gradient background effect
                var bgColor1 = new Color(0.25f, 0.35f, 0.8f, 1f);
                var bgColor2 = new Color(0.15f, 0.25f, 0.6f, 1f);
                
                // Draw gradient-like effect with multiple rectangles
                for (int i = 0; i < 4; i++)
                {
                    var gradientRect = new Rect(iconRect.x + i, iconRect.y + i, iconRect.width - i * 2, iconRect.height - i * 2);
                    var gradientColor = Color.Lerp(bgColor1, bgColor2, i * 0.25f);
                    EditorGUI.DrawRect(gradientRect, gradientColor);
                }
                
                // Try multiple icon options for better quality
                var icon = EditorGUIUtility.FindTexture("d_Settings") ?? 
                          EditorGUIUtility.FindTexture("d_UnityEditor.GameView") ??
                          EditorGUIUtility.FindTexture("d_UnityEditor.ConsoleWindow") ??
                          EditorGUIUtility.FindTexture("d_UnityEditor.InspectorWindow");
                          
                if (icon != null)
                {
                    var iconInnerRect = new Rect(iconRect.x + 12, iconRect.y + 12, 32, 32);
                    GUI.DrawTexture(iconInnerRect, icon, ScaleMode.ScaleToFit);
                }
                else
                {
                    // Fallback: draw a custom icon
                    var customIconRect = new Rect(iconRect.x + 16, iconRect.y + 16, 24, 24);
                    EditorGUI.DrawRect(customIconRect, new Color(0.9f, 0.9f, 0.95f, 1f));
                    
                    // Draw a simple gear-like shape
                    var centerX = customIconRect.x + customIconRect.width / 2;
                    var centerY = customIconRect.y + customIconRect.height / 2;
                    
                    // Draw circles for gear effect
                    var innerCircle = new Rect(centerX - 6, centerY - 6, 12, 12);
                    var outerCircle = new Rect(centerX - 8, centerY - 8, 16, 16);
                    
                    EditorGUI.DrawRect(outerCircle, new Color(0.7f, 0.7f, 0.8f, 1f));
                    EditorGUI.DrawRect(innerCircle, new Color(0.4f, 0.4f, 0.5f, 1f));
                }
                GUILayout.Space(16);
                var titleStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.92f, 0.92f, 0.95f, 1f) }
                };
                var subStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    fontSize = 14,
                    normal = { textColor = new Color(0.7f, 0.7f, 0.75f, 1f) }
                };
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(TemplateDisplayName, titleStyle);
                    GUILayout.Space(4);
                    GUILayout.Label("Welcome to the puzzle game template! Let's set up your project.", subStyle);
                    GUILayout.Space(8);
                    
                    // Welcome message with modern styling
                    var welcomeStyle = new GUIStyle(EditorStyles.label)
                    {
                        wordWrap = true,
                        fontSize = 13,
                        normal = { textColor = new Color(0.8f, 0.8f, 0.85f, 1f) }
                    };
                    
                    GUILayout.Label("Match colorful blocks in 2x2 patterns and clear the board!", welcomeStyle);
                    GUILayout.Space(2);
                    GUILayout.Label("Please support us with a review if you enjoy this template.", welcomeStyle);
                    GUILayout.Space(8);
                    
                    // Social buttons in header
                    DrawHeaderSocialButtons();
                }
                GUILayout.Space(20);
            }
        }

        private static void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // Icon
                var icon = EditorGUIUtility.FindTexture("d_UnityEditor.InspectorWindow");
                if (icon != null)
                {
                    GUILayout.Label(icon, GUILayout.Width(32), GUILayout.Height(32));
                }

                var titleStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold
                };
                var subStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(TemplateDisplayName, titleStyle);
                    GUILayout.Label("Welcome! This window helps you complete setup by installing any missing dependencies.", subStyle);
                }
            }
        }

        private void DrawModernSummary(bool allGood)
        {
            // Modern card-style summary
            var cardRect = GUILayoutUtility.GetRect(0, 0);
            cardRect.x = 20;
            cardRect.width = position.width - 40;
            cardRect.height = allGood ? 60 : 70;
            
            // Card background with shadow effect
            var cardBgColor = new Color(0.18f, 0.19f, 0.22f, 1f); // dark card
            var borderColor = allGood ? new Color(0.2f, 0.7f, 0.3f, 1f) : new Color(0.9f, 0.4f, 0.2f, 1f);
            // Shadow
            var shadowRect = new Rect(cardRect.x + 2, cardRect.y + 2, cardRect.width, cardRect.height);
            EditorGUI.DrawRect(shadowRect, new Color(0, 0, 0, 0.18f));
            // Card background
            EditorGUI.DrawRect(cardRect, cardBgColor);
            // Border
            EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.y, cardRect.width, 1), borderColor);
            EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.y + cardRect.height - 1, cardRect.width, 1), borderColor);
            GUILayout.Space(cardRect.height);
            // Content
            var contentRect = new Rect(cardRect.x + 16, cardRect.y + 12, cardRect.width - 32, cardRect.height - 24);
            var style = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                fontSize = 14,
                normal = { textColor = new Color(0.85f, 0.85f, 0.9f, 1f) }
            };
            var iconStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 18,
                normal = { textColor = borderColor }
            };
            if (allGood)
            {
                GUI.Label(new Rect(contentRect.x, contentRect.y, 24, 24), "✓", iconStyle);
                GUI.Label(new Rect(contentRect.x + 28, contentRect.y, contentRect.width - 28, 24),
                    "Perfect! All required packages are installed. Your project is ready to go!", style);
            }
            else
            {
                GUI.Label(new Rect(contentRect.x, contentRect.y, 24, 24), "⚠", iconStyle);
                GUI.Label(new Rect(contentRect.x + 28, contentRect.y, contentRect.width - 28, 24),
                    "Setup Required: Some packages are missing. Use the actions below to install them quickly.", style);
            }
        }

        private static void DrawSummary(bool allGood)
        {
            using (new EditorGUILayout.VerticalScope("HelpBox"))
            {
                var style = new GUIStyle(EditorStyles.label) { wordWrap = true };
                if (allGood)
                {
                    EditorGUILayout.LabelField("All set! Required items look good. You can start using the template right away.", style);
                }
                else
                {
                    EditorGUILayout.LabelField("Some items are missing. Use the actions below to install them.", style);
                }
            }
        }

        private void DrawModernRequirementsList()
        {
            // Keep requirement states fresh but throttled
            RefreshRequirementStates(force: false);

            // Sort requirements: missing packages first, then installed
            List<Requirement> missingList = new List<Requirement>();
            List<Requirement> installedList = new List<Requirement>();

            foreach (var req in _requirements)
            {
                bool installed = _requirementStates != null && 
                                _requirementStates.TryGetValue(req.Id, out var state) && 
                                state.installed;
                
                if (installed)
                    installedList.Add(req);
                else
                    missingList.Add(req);
            }

            // Combine lists: missing first, then installed
            List<Requirement> sortedList = new List<Requirement>();
            sortedList.AddRange(missingList);
            sortedList.AddRange(installedList);

            // Display sorted requirements
            foreach (var req in sortedList)
            {
                bool installed = _requirementStates != null && _requirementStates.TryGetValue(req.Id, out var state) && state.installed;
                string error = _requirementStates != null && _requirementStates.TryGetValue(req.Id, out state) ? state.error : null;

                // Modern card for each requirement
                var cardRect = GUILayoutUtility.GetRect(0, 0);
                cardRect.x = 20;
                cardRect.width = position.width - 40;
                cardRect.height = CalculateRequirementCardHeight(req, installed);

                // Card styling
                var cardBgColor = new Color(0.16f, 0.17f, 0.19f, 1f); // dark card
                var borderColor = installed ? new Color(0.2f, 0.7f, 0.3f, 1f) : new Color(0.5f, 0.5f, 0.5f, 1f);
                // Shadow
                var shadowRect = new Rect(cardRect.x + 1, cardRect.y + 1, cardRect.width, cardRect.height);
                EditorGUI.DrawRect(shadowRect, new Color(0, 0, 0, 0.13f));
                // Card background
                EditorGUI.DrawRect(cardRect, cardBgColor);
                // Border
                EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.y, cardRect.width, 1), borderColor);
                EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.y + cardRect.height - 1, cardRect.width, 1), borderColor);
                // Content
                var contentRect = new Rect(cardRect.x + 16, cardRect.y + 12, cardRect.width - 32, cardRect.height - 24);
                DrawRequirementContent(req, installed, error, contentRect);
                GUILayout.Space(cardRect.height + 8);
            }
        }

        private static float CalculateRequirementCardHeight(Requirement req, bool installed)
        {
            float height = 90; // Base height - increased for better spacing
            
            if (!string.IsNullOrEmpty(req.Description))
                height += 30; // More space for description
            
            if (!installed)
                height += 40; // More space for buttons
            
            if (height < 120) height = 120; // Higher minimum height
            
            return height;
        }

        private static void DrawRequirementContent(Requirement req, bool installed, string error, Rect contentRect)
        {
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                normal = { textColor = new Color(0.92f, 0.92f, 0.95f, 1f) }
            };
            var descStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                fontSize = 13,
                normal = { textColor = new Color(0.7f, 0.7f, 0.75f, 1f) }
            };

            // Title and status
            var titleRect = new Rect(contentRect.x, contentRect.y, contentRect.width - 120, 24);
            GUI.Label(titleRect, req.Title, titleStyle);

            var statusRect = new Rect(contentRect.x + contentRect.width - 110, contentRect.y, 110, 24);
            DrawModernStatusPill(statusRect, installed ? "✓ Installed" : "⚠ Missing", installed);

            // Description
            if (!string.IsNullOrEmpty(req.Description))
            {
                var descRect = new Rect(contentRect.x, contentRect.y + 30, contentRect.width - 20, 30);
                GUI.Label(descRect, req.Description, descStyle);
            }

            // Action buttons
            if (!installed)
            {
                var buttonY = contentRect.y + (string.IsNullOrEmpty(req.Description) ? 35 : 65);
                var buttonHeight = 32;
                var buttonSpacing = 10;

                float buttonX = contentRect.x;
                if (!string.IsNullOrEmpty(req.AssetUrl))
                {
                    var buttonRect = new Rect(buttonX, buttonY, 120, buttonHeight);
                    if (DrawModernButton(buttonRect, "Open Asset Store", new Color(0.2f, 0.5f, 0.8f, 1f)))
                    {
                        Application.OpenURL(req.AssetUrl);
                    }
                    buttonX += 120 + buttonSpacing;
                }

                if (req.ShowPackageManager)
                {
                    var buttonRect = new Rect(buttonX, buttonY, 140, buttonHeight);
                    if (DrawModernButton(buttonRect, "Package Manager", new Color(0.3f, 0.6f, 0.3f, 1f)))
                    {
                        OpenPackageManagerWindow();
                    }
                    buttonX += 140 + buttonSpacing;
                }

                var recheckRect = new Rect(buttonX, buttonY, 80, buttonHeight);
                if (DrawModernButton(recheckRect, "Re-check", new Color(0.6f, 0.6f, 0.6f, 1f)))
                {
                    // Force refresh cached states and repaint
                    RefreshRequirementStates(force: true);
                    EditorApplication.delayCall += () =>
                    {
                        var win = GetWindow<WelcomeWindow>();
                        win.Repaint();
                    };
                }
            }

            // Error message
            if (!string.IsNullOrEmpty(error))
            {
                var errorRect = new Rect(contentRect.x, contentRect.y + contentRect.height - 20, contentRect.width, 16);
                var errorStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    normal = { textColor = new Color(0.8f, 0.3f, 0.3f, 1f) }
                };
                GUI.Label(errorRect, "Error: " + error, errorStyle);
            }
        }

        private static bool DrawModernButton(Rect rect, string text, Color color)
        {
            // Button shadow
            var shadowRect = new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height);
            EditorGUI.DrawRect(shadowRect, new Color(0, 0, 0, 0.25f));
            // Button background
            EditorGUI.DrawRect(rect, color);
            // Button text (no hover/flicker)
            var textStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(rect, text, textStyle);
            // Handle click (no hover effect)
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        private static void DrawRequirementsList()
        {
            foreach (var req in _requirements)
            {
                bool installed = false;
                string error = null;
                try { installed = req.CheckInstalled(); }
                catch (Exception ex) { installed = false; error = ex.Message; }

                using (new EditorGUILayout.VerticalScope("Box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(req.Title, EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        DrawStatusPill(installed ? "Installed" : "Missing", installed);
                    }

                    if (!string.IsNullOrEmpty(req.Description))
                    {
                        EditorGUILayout.LabelField(req.Description, new GUIStyle(EditorStyles.label) { wordWrap = true });
                    }

                    if (!installed)
                    {
                        GUILayout.Space(4);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (!string.IsNullOrEmpty(req.AssetUrl))
                            {
                                if (GUILayout.Button("Open Asset Page", GUILayout.Height(22)))
                                {
                                    Application.OpenURL(req.AssetUrl);
                                }
                            }

                            if (req.ShowPackageManager)
                            {
                                if (GUILayout.Button("Open Package Manager", GUILayout.Height(22)))
                                {
                                    OpenPackageManagerWindow();
                                }
                            }

                            if (GUILayout.Button("Re-check", GUILayout.Height(22)))
                            {
                                // Just repaint to re-run the check function
                                EditorApplication.delayCall += () =>
                                {
                                    var win = GetWindow<WelcomeWindow>();
                                    win.Repaint();
                                };
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        EditorGUILayout.HelpBox(error, MessageType.None);
                    }
                }
            }
        }

        // Tips section removed

        private static void DrawModernStatusPill(Rect rect, string text, bool success)
        {
            // Pill background with modern styling
            var bgColor = success ? new Color(0.2f, 0.7f, 0.3f, 1f) : new Color(0.85f, 0.25f, 0.25f, 1f); // Modern red for missing
            
            // Shadow
            var shadowRect = new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height);
            EditorGUI.DrawRect(shadowRect, new Color(0, 0, 0, 0.2f));
            
            // Pill background
            EditorGUI.DrawRect(rect, bgColor);

            // Text - larger and more readable
            var textStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            GUI.Label(rect, text, textStyle);
        }

        private void DrawModernFooter(bool allGood)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(20);
                GUILayout.FlexibleSpace();
                
                // Modern close button
                var buttonRect = GUILayoutUtility.GetRect(140, 32);
                var buttonColor = allGood ? new Color(0.3f, 0.6f, 0.3f, 1f) : new Color(0.6f, 0.6f, 0.6f, 1f);
                
                if (DrawModernButton(buttonRect, allGood ? "✓ All Set - Close" : "Close for now", buttonColor))
                {
                    Close();
                }
                
                GUILayout.Space(20);
            }
        }

        private static void DrawHeaderSocialButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(0);
                
                // Discord button
                var discordRect = GUILayoutUtility.GetRect(110, 38);
                if (DrawStaticSocialButton(discordRect, "💬 Discord", new Color(0.4f, 0.5f, 0.9f, 1f)))
                {
                    Application.OpenURL("https://discord.com/invite/cFkW5cXFK9");
                }
                
                GUILayout.Space(10);
                
                // Email button
                var emailRect = GUILayoutUtility.GetRect(110, 38);
                if (DrawStaticSocialButton(emailRect, "📧 Contact", new Color(0.8f, 0.4f, 0.2f, 1f)))
                {
                    Application.OpenURL("https://bouncy-lemur-ac3.notion.site/292cc6421bf2800d896dc3062dbc13c3");
                }
                
                GUILayout.Space(10);
                
                // Documentation button
                var docRect = GUILayoutUtility.GetRect(150, 38);
                if (DrawStaticSocialButton(docRect, "📚 Documentation", new Color(0.3f, 0.7f, 0.5f, 1f)))
                {
                    Application.OpenURL("https://bouncy-lemur-ac3.notion.site/Color-Block-Jam-Game-Template-29bcc6421bf2808fa625d8163fa96f73");
                }
                
                GUILayout.Space(10);
                
                // Additional Files button
                var filesRect = GUILayoutUtility.GetRect(150, 38);
                if (DrawStaticSocialButton(filesRect, "📦 Additional Files", new Color(0.7f, 0.3f, 0.7f, 1f)))
                {
                    Application.OpenURL("https://bouncy-lemur-ac3.notion.site/Color-Block-Jam-Additional-Files-Installation-29bcc6421bf281ab8fb5f2920669cb0c");
                }
                
                GUILayout.FlexibleSpace();
            }
            
            GUILayout.Space(20);
            
            // Project Settings Bar Menu
            DrawProjectSettingsBar();
        }
        
        private static void DrawProjectSettingsBar()
        {
            // Get the current window instance
            var window = GetWindow<WelcomeWindow>();
            if (window == null) return;
            
            // Modern bar background - wider to accommodate larger buttons
            var barRect = GUILayoutUtility.GetRect(0, 0);
            barRect.x = 20;
            barRect.width = window.position.width - 40;
            barRect.height = 70;
            
            // Bar background with subtle styling
            var barBgColor = new Color(0.15f, 0.16f, 0.18f, 1f);
            var borderColor = new Color(0.3f, 0.3f, 0.35f, 1f);
            
            // Shadow
            var shadowRect = new Rect(barRect.x + 1, barRect.y + 1, barRect.width, barRect.height);
            EditorGUI.DrawRect(shadowRect, new Color(0, 0, 0, 0.1f));
            
            // Bar background
            EditorGUI.DrawRect(barRect, barBgColor);
            
            // Border
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width, 1), borderColor);
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y + barRect.height - 1, barRect.width, 1), borderColor);
            
            GUILayout.Space(barRect.height);
            
            // Content
            var contentRect = new Rect(barRect.x + 16, barRect.y + 8, barRect.width - 32, barRect.height - 16);
            
            // Title
            var titleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.9f, 0.95f, 1f) }
            };
            GUI.Label(new Rect(contentRect.x, contentRect.y, 200, 20), "⚙️ Project Settings", titleStyle);
            
            // Quick access buttons - responsive layout with larger buttons
            var buttonY = contentRect.y + 22;
            var buttonHeight = 32;
            var buttonSpacing = 8;
            
            // Calculate available width and button width - ensure buttons fit properly
            var availableWidth = contentRect.width;
            var totalButtons = 5;
            var buttonWidth = Mathf.Min(180, (availableWidth - (totalButtons - 1) * buttonSpacing) / totalButtons);
            
            float buttonX = contentRect.x;
            
            // Ads Settings button
            var adsRect = new Rect(buttonX, buttonY, buttonWidth, buttonHeight);
            if (DrawProjectSettingsButton(adsRect, "🎯 Ads Settings", new Color(0.2f, 0.6f, 0.8f, 1f)))
            {
                OpenAdsSettings();
            }
            buttonX += buttonWidth + buttonSpacing;
            
            // IAP Settings button
            var iapRect = new Rect(buttonX, buttonY, buttonWidth, buttonHeight);
            if (DrawProjectSettingsButton(iapRect, "💰 IAP Settings", new Color(0.95f, 0.7f, 0.2f, 1f)))
            {
                OpenIAPSettings();
            }
            buttonX += buttonWidth + buttonSpacing;
            
            // Audio Data button
            var audioRect = new Rect(buttonX, buttonY, buttonWidth, buttonHeight);
            if (DrawProjectSettingsButton(audioRect, "🔊 Audio Data", new Color(0.6f, 0.4f, 0.8f, 1f)))
            {
                OpenAudioData();
            }
            buttonX += buttonWidth + buttonSpacing;
            
            // Game Data button
            var gameDataRect = new Rect(buttonX, buttonY, buttonWidth, buttonHeight);
            if (DrawProjectSettingsButton(gameDataRect, "🎮 Game Data", new Color(0.8f, 0.5f, 0.2f, 1f)))
            {
                OpenGameData();
            }
            buttonX += buttonWidth + buttonSpacing;

            // Level Editor button
            var levelEditorRect = new Rect(buttonX, buttonY, buttonWidth, buttonHeight);
            if (DrawProjectSettingsButton(levelEditorRect, "🛠️ Level Editor", new Color(0.2f, 0.7f, 0.7f, 1f)))
            {
                OpenLevelEditorScene();
            }
            
            // Add extra space after Project Settings bar
            GUILayout.Space(16);
        }
        
        private static bool DrawProjectSettingsButton(Rect rect, string text, Color color)
        {
            // Button shadow
            var shadowRect = new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height);
            EditorGUI.DrawRect(shadowRect, new Color(0, 0, 0, 0.2f));
            
            // Button background
            EditorGUI.DrawRect(rect, color);
            
            // Button text - larger font for bigger buttons
            var textStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            
            GUI.Label(rect, text, textStyle);
            
            // Handle click
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }
            
            return false;
        }
        
        private static void OpenAdsSettings()
        {
            // Type ve isimle arama (her yerden bulur)
            SelectAssetByTypeAndName<UnityEngine.ScriptableObject>("Ad Configuration");
        }
        
        private static void OpenIAPSettings()
        {
            // Search for IAPSettings in common locations
            string[] possiblePaths = {
                "Assets/Resources/IAPSettings.asset",
                "Assets/Project Files/Data/IAPSettings.asset",
                "Assets/EKStudioCore/Monetization/IAP/Resources/IAPSettings.asset"
            };
            
            UnityEngine.Object asset = null;
            foreach (var path in possiblePaths)
            {
                asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset != null) break;
            }
            
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            else
            {
                Debug.LogWarning("IAPSettings asset not found! Create it via: Assets > Create > EKStudio > IAP > IAP Settings");
            }
        }
        
        private static void OpenAudioData()
        {
            // Type ve isimle arama (her yerden bulur)
            SelectAssetByTypeAndName<UnityEngine.ScriptableObject>("AudioData");
        }
        
        private static void OpenGameData()
        {
            // Load GameData asset from specific path (data file, not script)
            string assetPath = "Assets/Project Files/Data/GameData.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            else
            {
                Debug.LogWarning($"GameData data file not found at: {assetPath}");
            }
        }
        
        private static void OpenLevelEditorScene()
        {
            // Attempt to open the Level Editor scene
            const string defaultScenePath = "Assets/Project Files/Game/Scenes/LevelEditor.unity";

            // Prompt to save modified scenes before switching
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // Try default path first
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(defaultScenePath);
            if (sceneAsset != null)
            {
                EditorSceneManager.OpenScene(defaultScenePath, OpenSceneMode.Single);
                Debug.Log($"Opened Level Editor scene: {defaultScenePath}");
                return;
            }

            // Fallback: search by name anywhere in project
            try
            {
                string[] guids = AssetDatabase.FindAssets("t:Scene LevelEditor");
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith("LevelEditor.unity", StringComparison.OrdinalIgnoreCase))
                    {
                        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                        Debug.Log($"Opened Level Editor scene: {path}");
                        return;
                    }
                }
            }
            catch { /* ignore */ }

            Debug.LogWarning("LevelEditor scene not found. Expected at 'Assets/Project Files/Game/Scenes/LevelEditor.unity'.");
        }
        
        

        private static bool DrawStaticSocialButton(Rect rect, string text, Color color)
        {
            // Button shadow
            var shadowRect = new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height);
            EditorGUI.DrawRect(shadowRect, new Color(0, 0, 0, 0.15f));
            
            // Button background
            EditorGUI.DrawRect(rect, color);
            
            // Button text - larger font size
            var textStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            GUI.Label(rect, text, textStyle);

            // Handle click
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }

            return false;
        }

        private bool DrawSocialButton(Rect rect, string text, Color color)
        {
            // Button shadow
            var shadowRect = new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height);
            EditorGUI.DrawRect(shadowRect, new Color(0, 0, 0, 0.15f));
            
            // Button background
            EditorGUI.DrawRect(rect, color);
            
            // Button text - larger font size
            var textStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            GUI.Label(rect, text, textStyle);

            // Handle click
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }

            return false;
        }

        private static void DrawStatusPill(string text, bool success)
        {
            var pill = new GUIStyle("RL Footer")
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                fixedHeight = 18,
                padding = new RectOffset(8, 8, 0, 0)
            };

            var prev = GUI.backgroundColor;
            GUI.backgroundColor = success ? new Color(0.25f, 0.6f, 0.35f, 0.9f) : new Color(0.75f, 0.3f, 0.3f, 0.9f);
            GUILayout.Box(text, pill, GUILayout.Width(90));
            GUI.backgroundColor = prev;
        }

        private static bool IsSimpleToonInstalled()
        {
            // Primary heuristic: known shader names from Simple Toon
            var s1 = Shader.Find("Simple Toon/SToon Default");
            var s2 = Shader.Find("Simple Toon/SToon Outline");
            if (s1 != null || s2 != null) return true;

            // Fallback: search any shader that contains SToon in its name
            #if UNITY_EDITOR
            try
            {
                string[] guids = AssetDatabase.FindAssets("t:Shader SToon");
                if (guids != null && guids.Length > 0) return true;
            }
            catch { /* ignore */ }
            #endif
            return false;
        }

        private static bool IsNaughtyAttributesInstalled()
        {
            #if UNITY_EDITOR
            try
            {
                // Check for NaughtyAttributes namespace or specific files
                string[] guids = AssetDatabase.FindAssets("NaughtyAttributes");
                if (guids != null && guids.Length > 0) return true;

                // Check for specific NaughtyAttributes files
                guids = AssetDatabase.FindAssets("t:Script NaughtyAttributes");
                if (guids != null && guids.Length > 0) return true;
            }
            catch { /* ignore */ }
            #endif
            return false;
        }

        private static bool IsDOTweenInstalled()
        {
            #if UNITY_EDITOR
            try
            {
                // Check for DOTween namespace
                string[] guids = AssetDatabase.FindAssets("DOTween");
                if (guids != null && guids.Length > 0) return true;

                // Check for DOTweenSettings asset
                guids = AssetDatabase.FindAssets("DOTweenSettings");
                if (guids != null && guids.Length > 0) return true;

                // Check for DOTween scripts
                guids = AssetDatabase.FindAssets("t:Script DOTween");
                if (guids != null && guids.Length > 0) return true;
            }
            catch { /* ignore */ }
            #endif
            return false;
        }

        private static bool IsUIEffectInstalled()
        {
            #if UNITY_EDITOR
            try
            {
                // Check for UIEffect files
                string[] guids = AssetDatabase.FindAssets("UIEffect");
                if (guids != null && guids.Length > 0) return true;

                // Check for UIEffect scripts
                guids = AssetDatabase.FindAssets("t:Script UIEffect");
                if (guids != null && guids.Length > 0) return true;
            }
            catch { /* ignore */ }
            #endif
            return false;
        }

        private static bool IsVibrationInstalled()
        {
            #if UNITY_EDITOR
            try
            {
                // Check for Vibration files
                string[] guids = AssetDatabase.FindAssets("Vibration");
                if (guids != null && guids.Length > 0) return true;

                // Check for Vibration script specifically
                guids = AssetDatabase.FindAssets("Vibration.cs");
                if (guids != null && guids.Length > 0) return true;
            }
            catch { /* ignore */ }
            #endif
            return false;
        }

        private static bool IsLeanTouchInstalled()
        {
            #if UNITY_EDITOR
            try
            {
                // Check for LeanTouch files
                string[] guids = AssetDatabase.FindAssets("LeanTouch");
                if (guids != null && guids.Length > 0) return true;

                // Check for specific LeanTouch scripts
                guids = AssetDatabase.FindAssets("t:Script LeanTouch");
                if (guids != null && guids.Length > 0) return true;
                
                // Check for LeanFinger script (core class)
                guids = AssetDatabase.FindAssets("LeanFinger");
                if (guids != null && guids.Length > 0) return true;
            }
            catch { /* ignore */ }
            #endif
            return false;
        }

        private static bool IsQuickOutlineInstalled()
        {
            #if UNITY_EDITOR
            try
            {
                // Check for QuickOutline files
                string[] guids = AssetDatabase.FindAssets("QuickOutline");
                if (guids != null && guids.Length > 0) return true;

                // Check for Outline script specifically
                guids = AssetDatabase.FindAssets("Outline t:Script");
                if (guids != null && guids.Length > 0)
                {
                    // Verify it's the QuickOutline, not Unity's UI.Outline
                    foreach (var guid in guids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (path.Contains("QuickOutline")) return true;
                    }
                }
            }
            catch { /* ignore */ }
            #endif
            return false;
        }

        private static void OpenPackageManagerWindow()
        {
            // Best-effort open Package Manager
            EditorApplication.ExecuteMenuItem("Window/Package Manager");
            try
            {
                var pmType = Type.GetType("UnityEditor.PackageManager.UI.Window, UnityEditor.PackageManagerUIModule");
                var openMethod = pmType?.GetMethod("Open", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                openMethod?.Invoke(null, null);
            }
            catch { /* ignore */ }
        }
        // Asset'i type ve isimle bulup seçer
        private static void SelectAssetByTypeAndName<T>(string assetName) where T : UnityEngine.Object
        {
            string filter = $"t:{typeof(T).Name} {assetName}";
            string[] guids = AssetDatabase.FindAssets(filter);
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null && asset.name == assetName)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                    return;
                }
            }
            Debug.LogWarning($"{typeof(T).Name} asset named '{assetName}' not found!");
        }

        private static bool AnyRequirementMissing()
        {
            RefreshRequirementStates(force: false);
            foreach (var r in _requirements)
            {
                if (_requirementStates.TryGetValue(r.Id, out var state))
                {
                    if (!state.installed) return true;
                }
                else
                {
                    // If state missing, assume missing until computed
                    return true;
                }
            }
            return false;
        }

        private static void ShowWindow(bool focus)
        {
            var win = GetWindow<WelcomeWindow>(true, TemplateDisplayName + " — Welcome", focus);
            win.minSize = new Vector2(1000, 700);
            win.maxSize = new Vector2(1400, 1200);
            win.Show();
        }
    }
}
