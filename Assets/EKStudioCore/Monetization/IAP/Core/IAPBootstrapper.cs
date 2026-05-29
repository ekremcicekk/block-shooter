using UnityEngine;

namespace EKStudio.IAP
{
    /// <summary>
    /// IAP Initialization Check
    /// NOTE: Primary initialization happens in Loading scene via Inspector reference
    /// This bootstrapper only serves as a safety check and logs initialization status
    /// </summary>
    public static class IAPBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CheckIAPManagerStatus()
        {
            // Check if IAPManager was initialized (by Loading scene)
            var existing = Object.FindObjectOfType<IAPManager>();
            if (existing != null)
            {
                if (existing.iapSettings != null && existing.iapSettings.debugMode)
                {
                    Debug.Log("[IAPBootstrapper] ✅ IAPManager initialized successfully");
                }
                return;
            }
            
            // IAPManager not found - this is expected if IAP is not used
            // No warning needed as it's optional
        }
    }
}


