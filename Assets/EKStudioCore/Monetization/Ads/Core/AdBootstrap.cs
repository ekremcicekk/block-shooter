using UnityEngine;

namespace EKStudio.Monetization
{
    public static class AdsBootstrapper
    {
        // Auto-create and initialize AdvertisingSystem at app start if not present in scene
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureAdvertisingSystem()
        {
            // Don't create if an init module already exists
            var existing = Object.FindFirstObjectByType<AdvertisingInitializer>();
            if (existing != null)
            {
                return;
            }

            // Load settings from Resources (required by package consumers)
            var settings = Resources.Load<AdConfiguration>("AdConfiguration");
            if (settings == null)
            {
                // No settings available; skip auto-initialization
                return;
            }

            // Create an init module and initialize AdvertisingSystem with load-on-start enabled
            var go = new GameObject("AdvertisingInitializer(Auto)");
            Object.DontDestroyOnLoad(go);
            var initModule = go.AddComponent<AdvertisingInitializer>();
            initModule.Settings = settings;
            initModule.LoadAdOnStart = true;
        }
    }
}


