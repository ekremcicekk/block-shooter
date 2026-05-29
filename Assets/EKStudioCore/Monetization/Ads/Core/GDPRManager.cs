using UnityEngine;
using System;

namespace EKStudio.Monetization
{
    /// <summary>
    /// GDPR (General Data Protection Regulation) Manager for handling user privacy consent
    /// Manages IDFA (Identifier for Advertisers) consent and GDPR compliance
    /// </summary>
    public static class GDPRManager
    {
        // PlayerPrefs keys for storing consent status
        private const string GDPR_CONSENT_KEY = "GDPR_CONSENT";
        private const string IDFA_CONSENT_KEY = "IDFA_CONSENT";
        private const string GDPR_SHOWN_KEY = "GDPR_SHOWN";

        // Events for consent status changes
        public static event Action<bool> OnGDPRConsentChanged;
        public static event Action<bool> OnIDFAConsentChanged;

        /// <summary>
        /// Check if GDPR consent has been given
        /// </summary>
        public static bool HasGDPRConsent()
        {
            return PlayerPrefs.GetInt(GDPR_CONSENT_KEY, 0) == 1;
        }

        /// <summary>
        /// Check if IDFA consent has been given
        /// </summary>
        public static bool HasIDFAConsent()
        {
            return PlayerPrefs.GetInt(IDFA_CONSENT_KEY, 0) == 1;
        }

        /// <summary>
        /// Check if GDPR dialog has been shown before
        /// </summary>
        public static bool HasGDPRBeenShown()
        {
            return PlayerPrefs.GetInt(GDPR_SHOWN_KEY, 0) == 1;
        }

        /// <summary>
        /// Set GDPR consent status
        /// </summary>
        /// <param name="consent">True if user consents, false otherwise</param>
        public static void SetGDPRConsent(bool consent)
        {
            PlayerPrefs.SetInt(GDPR_CONSENT_KEY, consent ? 1 : 0);
            PlayerPrefs.Save();
            OnGDPRConsentChanged?.Invoke(consent);
        }

        /// <summary>
        /// Set IDFA consent status
        /// </summary>
        /// <param name="consent">True if user consents, false otherwise</param>
        public static void SetIDFAConsent(bool consent)
        {
            PlayerPrefs.SetInt(IDFA_CONSENT_KEY, consent ? 1 : 0);
            PlayerPrefs.Save();
            OnIDFAConsentChanged?.Invoke(consent);
        }

        /// <summary>
        /// Mark GDPR dialog as shown
        /// </summary>
        public static void MarkGDPRAsShown()
        {
            PlayerPrefs.SetInt(GDPR_SHOWN_KEY, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Reset all GDPR and IDFA consent data
        /// </summary>
        public static void ResetConsentData()
        {
            PlayerPrefs.DeleteKey(GDPR_CONSENT_KEY);
            PlayerPrefs.DeleteKey(IDFA_CONSENT_KEY);
            PlayerPrefs.DeleteKey(GDPR_SHOWN_KEY);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Check if GDPR dialog should be shown
        /// </summary>
        /// <param name="adConfiguration">Current ad configuration settings</param>
        /// <returns>True if GDPR dialog should be shown</returns>
        public static bool ShouldShowGDPRDialog(AdConfiguration adConfiguration)
        {
            if (!adConfiguration.EnableGDPR)
                return false;

            if (!adConfiguration.ShowGDPROnFirstLaunch)
                return false;

            return !HasGDPRBeenShown();
        }

        /// <summary>
        /// Get current consent status for debugging
        /// </summary>
        /// <returns>String representation of current consent status</returns>
        public static string GetConsentStatus()
        {
            return $"GDPR: {HasGDPRConsent()}, IDFA: {HasIDFAConsent()}, Shown: {HasGDPRBeenShown()}";
        }
    }
}
