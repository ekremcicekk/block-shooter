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

        [Header("Booster Data")]
        public BoosterData extraSlotData;
        public BoosterData freePickData;
        public BoosterData colorBlastData;
        public BoosterData moveShooterData;

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
            BoosterData data = type switch
            {
                BoosterType.ExtraSlot  => extraSlotData,
                BoosterType.FreePick   => freePickData,
                BoosterType.ColorBlast => colorBlastData,
                BoosterType.MoveShooter => moveShooterData,
                _ => null
            };

            if (data == null) return;

            if (boosterIcon != null && data.icon != null) boosterIcon.sprite = data.icon;
            if (boosterNameText != null) boosterNameText.text = $"{data.boosterName} Unlocked!";

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
