using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    public class BoosterManager : MonoBehaviour
    {
        public static BoosterManager Instance { get; private set; }

        [Header("Boosters")]
        public BombBooster bombBooster;
        public RainbowBooster rainbowBooster;
        public FreezeBooster freezeBooster;

        [Header("Reward on first unlock")]
        public int initialBoosterCount = 2;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            CheckUnlocks();
        }

        private void CheckUnlocks()
        {
            int currentLevel = SaveManager.CurrentLevel;
            GameConfig cfg = GameManager.Instance.config;

            CheckGiveInitial(BoosterType.Bomb, cfg.bombBoosterUnlockLevel, currentLevel);
            CheckGiveInitial(BoosterType.Rainbow, cfg.rainbowBoosterUnlockLevel, currentLevel);
            CheckGiveInitial(BoosterType.Freeze, cfg.freezeBoosterUnlockLevel, currentLevel);
        }

        private void CheckGiveInitial(BoosterType type, int unlockLevel, int currentLevel)
        {
            string seenKey = $"BoosterSeen_{type}";
            if (currentLevel >= unlockLevel && PlayerPrefs.GetInt(seenKey, 0) == 0)
            {
                SaveManager.AddBooster(type, initialBoosterCount);
                PlayerPrefs.SetInt(seenKey, 1);
                PlayerPrefs.Save();
                BoosterUnlockUI.Instance?.ShowUnlock(type);
            }
        }

        public bool ActivateBooster(BoosterType type)
        {
            return type switch
            {
                BoosterType.Bomb => bombBooster != null && bombBooster.TryActivate(),
                BoosterType.Rainbow => rainbowBooster != null && rainbowBooster.TryActivate(),
                BoosterType.Freeze => freezeBooster != null && freezeBooster.TryActivate(),
                _ => false
            };
        }

        public bool IsBoosterUnlocked(BoosterType type)
        {
            int unlockLevel = type switch
            {
                BoosterType.Bomb => GameManager.Instance.config.bombBoosterUnlockLevel,
                BoosterType.Rainbow => GameManager.Instance.config.rainbowBoosterUnlockLevel,
                BoosterType.Freeze => GameManager.Instance.config.freezeBoosterUnlockLevel,
                _ => 999
            };
            return SaveManager.CurrentLevel >= unlockLevel;
        }

        // Debug/test helper
        [ContextMenu("Give All Boosters")]
        private void GiveAllBoosters()
        {
            SaveManager.AddBooster(BoosterType.Bomb, 3);
            SaveManager.AddBooster(BoosterType.Rainbow, 3);
            SaveManager.AddBooster(BoosterType.Freeze, 3);
        }
    }
}
