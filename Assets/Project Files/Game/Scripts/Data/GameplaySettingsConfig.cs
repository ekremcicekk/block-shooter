using UnityEngine;

namespace BlockShooter
{
    [System.Serializable]
    public class BoosterSettings
    {
        public int unlockLevel;
        public int buyCost;
    }

    [CreateAssetMenu(fileName = "GameplaySettingsConfig", menuName = "BlockShooter/Configs/Gameplay Settings Config")]
    public class GameplaySettingsConfig : ScriptableObject
    {
        [Header("Block Settings")]
        public float fireRate = 0.15f;
        public float projectileSpeed = 12f;
        public float conveyorSpeed = 1.5f;

        [Header("Economy Settings")]
        public int startGameCoins = 100;
        public int playOnCost = 100;
        public int winRewardCoins = 50;

        [Header("Feature Unlock Levels")]
        public int mysteryShooterUnlockLevel = 4;
        public int freezeShooterUnlockLevel = 3;
        public int doorUnlockLevel = 2;

        [Header("Booster Settings")]
        public BoosterSettings extraSlotSettings = new BoosterSettings { unlockLevel = 1, buyCost = 150 };
        public BoosterSettings superShooterSettings = new BoosterSettings { unlockLevel = 5, buyCost = 250 };
        public BoosterSettings moveShooterSettings = new BoosterSettings { unlockLevel = 2, buyCost = 200 };
    }
}
