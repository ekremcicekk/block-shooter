using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BlockShooter
{
    /// <summary>
    /// Component attached directly to each individual booster slot (e.g. Booster_Slot)
    /// to hold localized, type-safe references and prevent fragile string-based hierarchy searches.
    /// </summary>
    public class BoosterSlotUI : MonoBehaviour
    {
        [Header("Booster Type Configuration")]
        [Tooltip("The type of booster this slot represents")]
        public BoosterType boosterType;

        [Header("Internal UI References")]
        public Button mainButton;
        
        [Header("State Groups")]
        public GameObject lockGroup;
        public TextMeshProUGUI lockLvlText;
        public GameObject activeGroup;
        public GameObject deactiveGroup;

        [Header("Count & Plus Icon")]
        public GameObject countGroup;
        public TextMeshProUGUI countText;
        public GameObject plusBuyGroup;

        [Header("Buy Panel References")]
        public GameObject buyPanel;
        public Button buyWithCoinsButton;
        public TextMeshProUGUI buyPriceText;

        /// <summary>
        /// Updates the visual state of this individual slot based on calculated gameplay conditions.
        /// </summary>
        public void Refresh(bool unlocked, bool usable, int count, int unlockLevel, int buyCost)
        {
            // 1. Lock State
            if (lockGroup != null)
            {
                lockGroup.SetActive(!unlocked);
            }
            if (lockLvlText != null)
            {
                lockLvlText.text = $"Level {unlockLevel}";
            }

            // 2. Active / Deactive States
            if (activeGroup != null)
            {
                activeGroup.SetActive(unlocked && usable);
            }
            if (deactiveGroup != null)
            {
                deactiveGroup.SetActive(unlocked && !usable);
            }

            // 3. Count / PlusBuy States
            if (countGroup != null)
            {
                countGroup.SetActive(unlocked && count > 0);
            }
            if (countText != null)
            {
                countText.text = count.ToString();
            }
            if (plusBuyGroup != null)
            {
                plusBuyGroup.SetActive(unlocked && count == 0);
            }

            // 4. Main Button Interactability
            if (mainButton != null)
            {
                mainButton.interactable = unlocked && usable;
            }

            // 5. Buy Panel Cost display
            if (buyPriceText != null)
            {
                buyPriceText.text = buyCost.ToString();
            }
        }
    }
}
