using UnityEngine;
using UnityEngine.UI;
using EKStudio.Monetization;

namespace EKStudio.Monetization.Networks
{
    /// <summary>
    /// Helper class for creating Mock Ads Canvas at runtime
    /// </summary>
    public static class MockAdsCanvasCreator
    {
        public static GameObject CreateMockAdsCanvas()
        {
            // Create main Canvas
            GameObject canvasObj = new GameObject("MockAdsCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // Create Banner Ad
            GameObject bannerObj = CreateBannerAd(canvasObj.transform);
            
            // Create Interstitial Ad
            GameObject interstitialObj = CreateInterstitialAd(canvasObj.transform);
            
            // Create Rewarded Video Ad
            GameObject rewardedVideoObj = CreateRewardedVideoAd(canvasObj.transform);
            
            // Add AdMockController
            AdMockController controller = canvasObj.AddComponent<AdMockController>();
            
            // Assign references (using reflection)
            var bannerField = typeof(AdMockController).GetField("bannerObject", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var interstitialField = typeof(AdMockController).GetField("interstitialObject", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rewardedVideoField = typeof(AdMockController).GetField("rewardedVideoObject", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            bannerField?.SetValue(controller, bannerObj);
            interstitialField?.SetValue(controller, interstitialObj);
            rewardedVideoField?.SetValue(controller, rewardedVideoObj);
            
            return canvasObj;
        }
        
        private static GameObject CreateBannerAd(Transform parent)
        {
            GameObject bannerObj = new GameObject("BannerAd");
            bannerObj.transform.SetParent(parent, false);
            
            RectTransform rectTransform = bannerObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 0.1f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            
            Image image = bannerObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            // Add Banner text
            GameObject textObj = new GameObject("BannerText");
            textObj.transform.SetParent(bannerObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            Text text = textObj.AddComponent<Text>();
            text.text = "MOCK BANNER AD";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            
            bannerObj.SetActive(false);
            return bannerObj;
        }
        
        private static GameObject CreateInterstitialAd(Transform parent)
        {
            GameObject interstitialObj = new GameObject("InterstitialAd");
            interstitialObj.transform.SetParent(parent, false);
            
            RectTransform rectTransform = interstitialObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            Image image = interstitialObj.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            
            // Add Interstitial text
            GameObject textObj = new GameObject("InterstitialText");
            textObj.transform.SetParent(interstitialObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.2f, 0.4f);
            textRect.anchorMax = new Vector2(0.8f, 0.6f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            Text text = textObj.AddComponent<Text>();
            text.text = "MOCK INTERSTITIAL AD\n\nClick to close";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 32;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            
            // Add Close button
            GameObject closeBtn = CreateButton(interstitialObj.transform, "Close", 
                new Vector2(0.8f, 0.8f), new Vector2(0.95f, 0.95f));
            
            Button button = closeBtn.GetComponent<Button>();
            button.onClick.AddListener(() => {
                // Find AdMockController and call OnInterstitialClosed
                AdMockController controller = interstitialObj.GetComponentInParent<AdMockController>();
                if (controller != null)
                {
                    controller.OnInterstitialClosed();
                }
                else
                {
                    // Fallback
                    AdvertisingSystem.ExecuteInterstitialCallback(true);
                    interstitialObj.SetActive(false);
                }
            });
            
            interstitialObj.SetActive(false);
            return interstitialObj;
        }
        
        private static GameObject CreateRewardedVideoAd(Transform parent)
        {
            GameObject rewardedVideoObj = new GameObject("RewardedVideoAd");
            rewardedVideoObj.transform.SetParent(parent, false);
            
            RectTransform rectTransform = rewardedVideoObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            Image image = rewardedVideoObj.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            
            // Add Rewarded video text
            GameObject textObj = new GameObject("RewardedVideoText");
            textObj.transform.SetParent(rewardedVideoObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.2f, 0.4f);
            textRect.anchorMax = new Vector2(0.8f, 0.6f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            Text text = textObj.AddComponent<Text>();
            text.text = "MOCK REWARDED VIDEO AD\n\nWatch to get reward!";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 32;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            
            // Add Get Reward button
            GameObject rewardBtn = CreateButton(rewardedVideoObj.transform, "Get Reward", 
                new Vector2(0.3f, 0.2f), new Vector2(0.5f, 0.35f));
            
            Button rewardButton = rewardBtn.GetComponent<Button>();
            rewardButton.onClick.AddListener(() => {
                AdvertisingSystem.ExecuteRewardVideoCallback(true);
                rewardedVideoObj.SetActive(false);
            });
            
            // Add Close button
            GameObject closeBtn = CreateButton(rewardedVideoObj.transform, "Close", 
                new Vector2(0.5f, 0.2f), new Vector2(0.7f, 0.35f));
            
            Button closeButton = closeBtn.GetComponent<Button>();
            closeButton.onClick.AddListener(() => {
                AdvertisingSystem.ExecuteRewardVideoCallback(false);
                rewardedVideoObj.SetActive(false);
            });
            
            rewardedVideoObj.SetActive(false);
            return rewardedVideoObj;
        }
        
        private static GameObject CreateButton(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject buttonObj = new GameObject(text + "Button");
            buttonObj.transform.SetParent(parent, false);
            
            RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            
            Button button = buttonObj.AddComponent<Button>();
            
            // Add Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            Text buttonText = textObj.AddComponent<Text>();
            buttonText.text = text;
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.fontSize = 20;
            buttonText.color = Color.white;
            buttonText.alignment = TextAnchor.MiddleCenter;
            
            return buttonObj;
        }
    }
}
