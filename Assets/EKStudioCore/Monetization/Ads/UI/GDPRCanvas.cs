using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EKStudio;

namespace EKStudio.Monetization
{
    /// <summary>
    /// GDPR Canvas UI Controller for handling privacy consent dialogs
    /// </summary>
    public class GDPRCanvas : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject gdprPanel;
        [SerializeField] private Button acceptAllButton;
        [SerializeField] private Button privacyPolicyButton;
        [SerializeField] private Button termsOfUseButton;

        // Callback when GDPR dialog is closed
        public static event System.Action OnGDPRDialogClosed;

        private AdConfiguration adConfiguration;
        private bool isInitialized = false;

        /// <summary>
        /// Initialize GDPR Canvas with settings
        /// </summary>
        /// <param name="adConfiguration">Ad configuration containing GDPR settings and privacy URLs</param>
        public void Initialize(AdConfiguration adConfiguration)
        {
            this.adConfiguration = adConfiguration;
            isInitialized = true;

            SetupUI();
            ShowGDPRDialog();
        }

        /// <summary>
        /// Setup UI elements and event listeners
        /// </summary>
        private void SetupUI()
        {
            if (!isInitialized) return;

            // Setup button listeners
            if (acceptAllButton != null)
                acceptAllButton.onClick.AddListener(OnAcceptAllClicked);
            
            if (privacyPolicyButton != null)
                privacyPolicyButton.onClick.AddListener(OnPrivacyPolicyClicked);
            
            if (termsOfUseButton != null)
                termsOfUseButton.onClick.AddListener(OnTermsOfUseClicked);
        }


        /// <summary>
        /// Show GDPR consent dialog
        /// </summary>
        private void ShowGDPRDialog()
        {
            if (gdprPanel != null)
                gdprPanel.SetActive(true);
        }

        /// <summary>
        /// Handle accept all button click
        /// </summary>
        private void OnAcceptAllClicked()
        {
            GDPRManager.SetGDPRConsent(true);
            GDPRManager.MarkGDPRAsShown();
            
            // On iOS, system automatically shows dialog for IDFA
            if (adConfiguration.EnableIDFA)
            {
                IDFAManager.RequestIDFAAuthorization();
            }
            
            CloseGDPRCanvas();
        }

        /// <summary>
        /// Handle privacy policy button click
        /// </summary>
        private void OnPrivacyPolicyClicked()
        {
            if (adConfiguration != null && !string.IsNullOrEmpty(adConfiguration.PrivacyPolicyUrl))
            {
                Application.OpenURL(adConfiguration.PrivacyPolicyUrl);
            }
            else
            {
                Debug.LogWarning("[GDPRCanvas] Privacy policy URL not set");
            }
        }

        /// <summary>
        /// Handle terms of use button click
        /// </summary>
        private void OnTermsOfUseClicked()
        {
            if (adConfiguration != null && !string.IsNullOrEmpty(adConfiguration.TermsOfUseUrl))
            {
                Application.OpenURL(adConfiguration.TermsOfUseUrl);
            }
            else
            {
                Debug.LogWarning("[GDPRCanvas] Terms of use URL not set");
            }
        }


        /// <summary>
        /// Close GDPR canvas
        /// </summary>
        private void CloseGDPRCanvas()
        {
            gameObject.SetActive(false);
            
            // Trigger callback to notify that GDPR dialog is closed
            OnGDPRDialogClosed?.Invoke();
        }

    }
}
