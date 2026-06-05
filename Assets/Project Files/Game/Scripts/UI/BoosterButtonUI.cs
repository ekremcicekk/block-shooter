using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace BlockShooter
{
    public class BoosterButtonUI : MonoBehaviour
    {
        [Header("Booster Type")]
        public BoosterType boosterType;

        [Header("UI Elements")]
        public Button button;
        public Image iconImage;
        public TextMeshProUGUI countText;
        public GameObject lockOverlay;
        public GameObject unlockBadge;

        private void OnEnable()
        {
            GameManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            button?.onClick.AddListener(OnClick);
            Refresh();
        }

        private void HandleStateChanged(GameState state)
        {
            Refresh();
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            bool unlocked = BoosterManager.Instance != null && BoosterManager.Instance.IsBoosterUnlocked(boosterType);
            int count = SaveManager.GetBoosterCount(boosterType);

            if (lockOverlay != null) lockOverlay.SetActive(!unlocked);
            if (unlockBadge != null) unlockBadge.SetActive(false);
            if (countText != null) countText.text = count > 0 ? count.ToString() : "0";

            bool canUse = unlocked && count > 0 && GameManager.Instance.IsPlaying;
            if (canUse && boosterType == BoosterType.SuperShooter)
            {
                canUse = SlotSystem.Instance != null && SlotSystem.Instance.GetSlottedBlocks().Count > 0;
            }
            if (canUse && boosterType == BoosterType.MoveShooter)
            {
                canUse = SlotSystem.Instance != null && SlotSystem.Instance.HasEmptySlot && 
                         ShooterGrid.Instance != null && ShooterGrid.Instance.HasLockedBlocks();
            }

            if (button != null) button.interactable = canUse;
        }

        private void OnClick()
        {
            bool success = BoosterManager.Instance != null && BoosterManager.Instance.ActivateBooster(boosterType);
            if (success)
            {
                transform.DOPunchScale(Vector3.one * 0.2f, 0.25f, 3, 0.5f);
                Refresh();
            }
        }
    }
}
