using UnityEngine;
using UnityEngine.UI;

namespace EKStudio.IAP
{
    /// <summary>
    /// Hides or disables a UI element if "No Ads" has been purchased.
    /// - Checks state on enable
    /// - Updates automatically on purchase/restore events
    /// Attach this to your "Remove Ads" button GameObject (or set a custom target).
    /// </summary>
    public class HideIfNoAdsPurchased : MonoBehaviour
    {
        [Tooltip("Optional target to hide/disable. If null, uses this GameObject.")]
        public GameObject target;

        [Tooltip("If true, disables Selectable instead of hiding GameObject.")]
        public bool disableInsteadOfHide = false;

        private Selectable selectable;

        private void Awake()
        {
            if (target == null) target = gameObject;
            if (disableInsteadOfHide)
            {
                selectable = target.GetComponent<Selectable>();
            }
        }

        private void OnEnable()
        {
            Refresh();
            IAPManager.OnPurchaseSuccess += OnPurchaseSuccess;
            IAPManager.OnRestoreSuccess += OnRestoreSuccess;
        }

        private void OnDisable()
        {
            IAPManager.OnPurchaseSuccess -= OnPurchaseSuccess;
            IAPManager.OnRestoreSuccess -= OnRestoreSuccess;
        }

        private void OnPurchaseSuccess(string productId)
        {
            Refresh();
        }

        private void OnRestoreSuccess()
        {
            Refresh();
        }

        public void Refresh()
        {
            bool noAdsPurchased = false;

            if (IAPManager.Instance != null)
            {
                noAdsPurchased = IAPManager.Instance.HasPurchasedNoAds();
            }

            if (disableInsteadOfHide)
            {
                if (selectable == null) selectable = target.GetComponent<Selectable>();
                if (selectable != null)
                {
                    selectable.interactable = !noAdsPurchased;
                }
            }
            else
            {
                if (target != null)
                {
                    target.SetActive(!noAdsPurchased);
                }
            }
        }
    }
}
