using UnityEngine;
using DG.Tweening;

namespace BlockShooter
{
    /// <summary>
    /// Root UI controller attached to the BoosterArea.
    /// Manages high-level coordination and gameplay interactions of the booster slot components.
    /// </summary>
    public class BoosterAreaUI : MonoBehaviour
    {
        public static BoosterAreaUI Instance { get; private set; }

        [Header("Booster Slot Components")]
        [Tooltip("The BoosterSlotUI component for the Extra Slot booster")]
        public BoosterSlotUI extraSlotSlot;

        [Tooltip("The BoosterSlotUI component for the Move Shooter booster")]
        public BoosterSlotUI moveShooterSlot;

        [Tooltip("The BoosterSlotUI component for the Super Shooter booster")]
        public BoosterSlotUI superShooterSlot;
        
        private bool _wasAnyBuyPanelActive;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Bind high-level gameplay listeners to slot buttons
            SetupBoosterListeners(extraSlotSlot);
            SetupBoosterListeners(moveShooterSlot);
            SetupBoosterListeners(superShooterSlot);

            // Hide buy panels by default at start of the level
            if (extraSlotSlot != null && extraSlotSlot.buyPanel != null) extraSlotSlot.buyPanel.SetActive(false);
            if (moveShooterSlot != null && moveShooterSlot.buyPanel != null) moveShooterSlot.buyPanel.SetActive(false);
            if (superShooterSlot != null && superShooterSlot.buyPanel != null) superShooterSlot.buyPanel.SetActive(false);

            RefreshUI();
        }

        private void OnEnable()
        {
            GameManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            RefreshUI();
        }

        private void Update()
        {
            bool isAnyActive = IsAnyBuyPanelActive();
            if (isAnyActive && !_wasAnyBuyPanelActive)
            {
                _wasAnyBuyPanelActive = true;
                PauseGame(GetActiveBuyPanel());
            }
            else if (!isAnyActive && _wasAnyBuyPanelActive)
            {
                _wasAnyBuyPanelActive = false;
                ResumeGame();
            }

            if (GameManager.Instance != null && (GameManager.Instance.IsPlaying || isAnyActive))
            {
                RefreshUI();
            }
        }

        private void SetupBoosterListeners(BoosterSlotUI slot)
        {
            if (slot == null) return;

            if (slot.mainButton != null)
            {
                slot.mainButton.onClick.AddListener(() => OnBoosterMainClicked(slot));
            }

            if (slot.buyWithCoinsButton != null)
            {
                slot.buyWithCoinsButton.onClick.AddListener(() => OnBuyWithCoinsClicked(slot));
            }
        }

        public void RefreshUI()
        {
            RefreshBoosterState(extraSlotSlot);
            RefreshBoosterState(moveShooterSlot);
            RefreshBoosterState(superShooterSlot);
        }

        private void RefreshBoosterState(BoosterSlotUI slot)
        {
            if (slot == null) return;

            BoosterType type = slot.boosterType;
            bool unlocked = BoosterManager.Instance != null && BoosterManager.Instance.IsBoosterUnlocked(type);
            bool usable = IsBoosterUsable(type);
            int count = SaveManager.GetBoosterCount(type);

            int unlockLevel = 0;
            int buyCost = 0;

            if (GameManager.Instance != null && GameManager.Instance.config != null)
            {
                unlockLevel = type switch
                {
                    BoosterType.ExtraSlot => GameManager.Instance.config.extraSlotUnlockLevel,
                    BoosterType.SuperShooter => GameManager.Instance.config.superShooterUnlockLevel,
                    BoosterType.MoveShooter => GameManager.Instance.config.moveShooterUnlockLevel,
                    _ => 0
                };

                buyCost = type switch
                {
                    BoosterType.ExtraSlot => GameManager.Instance.config.extraSlotBuyCost,
                    BoosterType.SuperShooter => GameManager.Instance.config.superShooterBuyCost,
                    BoosterType.MoveShooter => GameManager.Instance.config.moveShooterBuyCost,
                    _ => 0
                };
            }

            slot.Refresh(unlocked, usable, count, unlockLevel, buyCost);
        }

        private bool IsBoosterUsable(BoosterType type)
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return false;

            return type switch
            {
                BoosterType.ExtraSlot => SlotSystem.Instance != null && SlotSystem.Instance.MaxSlots < 5,
                BoosterType.MoveShooter => SlotSystem.Instance != null && SlotSystem.Instance.HasEmptySlot &&
                                           ShooterGrid.Instance != null && ShooterGrid.Instance.HasLockedBlocks(),
                BoosterType.SuperShooter => SlotSystem.Instance != null && HasNonShootingSlottedBlock(),
                _ => false
            };
        }

        private bool HasNonShootingSlottedBlock()
        {
            if (SlotSystem.Instance == null) return false;
            foreach (var b in SlotSystem.Instance.GetSlottedBlocks())
            {
                if (b != null && !b.IsShooting)
                    return true;
            }
            return false;
        }

        private void OnBoosterMainClicked(BoosterSlotUI slot)
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying || slot == null) return;

            BoosterType type = slot.boosterType;
            int count = SaveManager.GetBoosterCount(type);
            if (count > 0)
            {
                // Activate/use booster directly
                bool success = BoosterManager.Instance != null && BoosterManager.Instance.ActivateBooster(type);
                if (success)
                {
                    RefreshUI();
                }
            }
            else
            {
                // Open buy panel
                slot.buyPanel?.SetActive(true);
            }
        }

        private void OnBuyWithCoinsClicked(BoosterSlotUI slot)
        {
            if (GameManager.Instance == null || GameManager.Instance.config == null || slot == null) return;

            BoosterType type = slot.boosterType;
            int cost = type switch
            {
                BoosterType.ExtraSlot => GameManager.Instance.config.extraSlotBuyCost,
                BoosterType.SuperShooter => GameManager.Instance.config.superShooterBuyCost,
                BoosterType.MoveShooter => GameManager.Instance.config.moveShooterBuyCost,
                _ => 0
            };

            if (SaveManager.Coins >= cost)
            {
                SaveManager.Coins -= cost;
                SaveManager.AddBooster(type, 1);

                // Update Coins HUD UI with animation
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.UpdateCoinUI(true);
                }

                // Close buy panel
                slot.buyPanel?.SetActive(false);
                RefreshUI();
            }
            else
            {
                // Punch scale coin text on HUD to show insufficient money
                if (UIManager.Instance != null && UIManager.Instance.coinText != null)
                {
                    UIManager.Instance.coinText.transform.DOPunchPosition(Vector3.right * 10f, 0.25f, 5, 0.5f).SetUpdate(true);
                }
            }
        }

        private bool IsAnyBuyPanelActive()
        {
            if (extraSlotSlot != null && extraSlotSlot.buyPanel != null && extraSlotSlot.buyPanel.activeSelf) return true;
            if (moveShooterSlot != null && moveShooterSlot.buyPanel != null && moveShooterSlot.buyPanel.activeSelf) return true;
            if (superShooterSlot != null && superShooterSlot.buyPanel != null && superShooterSlot.buyPanel.activeSelf) return true;
            return false;
        }

        private GameObject GetActiveBuyPanel()
        {
            if (extraSlotSlot != null && extraSlotSlot.buyPanel != null && extraSlotSlot.buyPanel.activeSelf) return extraSlotSlot.buyPanel;
            if (moveShooterSlot != null && moveShooterSlot.buyPanel != null && moveShooterSlot.buyPanel.activeSelf) return moveShooterSlot.buyPanel;
            if (superShooterSlot != null && superShooterSlot.buyPanel != null && superShooterSlot.buyPanel.activeSelf) return superShooterSlot.buyPanel;
            return null;
        }

        private void PauseGame(GameObject activePanel)
        {
            if (activePanel != null)
            {
                var anims = activePanel.GetComponentsInChildren<Animator>(true);
                foreach (var anim in anims)
                {
                    anim.updateMode = AnimatorUpdateMode.UnscaledTime;
                }
            }
            Time.timeScale = 0f;
            GameManager.Instance?.SetState(GameState.Paused);
        }

        private void ResumeGame()
        {
            Time.timeScale = 1f;
            GameManager.Instance?.SetState(GameState.Playing);
        }
    }
}
