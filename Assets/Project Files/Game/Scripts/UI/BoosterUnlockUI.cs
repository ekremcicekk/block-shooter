using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace BlockShooter
{
    public class BoosterUnlockUI : MonoBehaviour
    {
        public static BoosterUnlockUI Instance { get; private set; }

        [Header("UI")]
        public GameObject panel;
        public Image boosterIcon;
        public TextMeshProUGUI boosterNameText;
        public Button closeButton;

        [Header("Booster Icons")]
        public Sprite extraSlotIcon;
        public Sprite superShooterIcon;
        public Sprite moveShooterIcon;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            panel?.SetActive(false);
            closeButton?.onClick.AddListener(Hide);
        }

        public void ShowUnlock(BoosterType type)
        {
            string displayName = type switch
            {
                BoosterType.ExtraSlot => "Extra Slot",
                BoosterType.SuperShooter => "Super Shooter",
                BoosterType.MoveShooter => "Move Shooter",
                _ => type.ToString()
            };

            Sprite icon = type switch
            {
                BoosterType.ExtraSlot => extraSlotIcon,
                BoosterType.SuperShooter => superShooterIcon,
                BoosterType.MoveShooter => moveShooterIcon,
                _ => null
            };

            if (boosterIcon != null)
            {
                if (icon != null)
                {
                    boosterIcon.gameObject.SetActive(true);
                    boosterIcon.sprite = icon;
                }
                else
                {
                    boosterIcon.gameObject.SetActive(false);
                }
            }

            if (boosterNameText != null)
            {
                boosterNameText.text = $"{displayName} Unlocked!";
            }

            panel?.SetActive(true);
            panel.transform.localScale = Vector3.zero;
            panel.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void Hide()
        {
            panel.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack)
                .SetUpdate(true).OnComplete(() => panel.SetActive(false));
        }
    }
}
