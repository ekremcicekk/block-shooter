using UnityEngine;
using System;

namespace EKStudio.Monetization
{
    /// <summary>
    /// IDFA (Identifier for Advertisers) Manager for iOS devices
    /// Handles IDFA consent and tracking authorization
    /// </summary>
    public static class IDFAManager
    {
        // Events for IDFA status changes
        public static event Action<bool> OnIDFAStatusChanged;

        /// <summary>
        /// Request IDFA authorization from user
        /// On iOS, system automatically shows dialog, no separate UI needed
        /// </summary>
        public static void RequestIDFAAuthorization()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // On iOS, App Tracking Transparency system automatically shows dialog
            RequestIDFAAuthorizationNative();
#else
            // For non-iOS platforms or editor, simulate consent
            OnIDFAStatusChanged?.Invoke(true);
#endif
        }

        /// <summary>
        /// Check if IDFA tracking is authorized
        /// </summary>
        /// <returns>True if IDFA tracking is authorized</returns>
        public static bool IsIDFAAuthorized()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return IsIDFAAuthorizedNative();
#else
            // For non-iOS platforms, return true if consent was given
            return GDPRManager.HasIDFAConsent();
#endif
        }

        /// <summary>
        /// Get IDFA identifier if authorized
        /// </summary>
        /// <returns>IDFA string or empty if not authorized</returns>
        public static string GetIDFA()
        {
            if (!IsIDFAAuthorized())
            {
                Debug.LogWarning("[IDFAManager] IDFA not authorized");
                return string.Empty;
            }

#if UNITY_IOS && !UNITY_EDITOR
            return GetIDFANative();
#else
            // For non-iOS platforms, return a mock IDFA
            return "MOCK_IDFA_" + SystemInfo.deviceUniqueIdentifier;
#endif
        }

        /// <summary>
        /// Check if IDFA is available on this device
        /// </summary>
        /// <returns>True if IDFA is available</returns>
        public static bool IsIDFAAvailable()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return IsIDFAAvailableNative();
#else
            // IDFA is only available on iOS
            return false;
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        /// <summary>
        /// Native iOS method to request IDFA authorization
        /// </summary>
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void RequestIDFAAuthorizationNative();

        /// <summary>
        /// Native iOS method to check IDFA authorization status
        /// </summary>
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern bool IsIDFAAuthorizedNative();

        /// <summary>
        /// Native iOS method to get IDFA identifier
        /// </summary>
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string GetIDFANative();

        /// <summary>
        /// Native iOS method to check IDFA availability
        /// </summary>
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern bool IsIDFAAvailableNative();
#endif

        /// <summary>
        /// Initialize IDFA system
        /// </summary>
        public static void Initialize()
        {
            // Silent initialization
        }
    }
}
