using UnityEngine;

namespace BlockShooter
{
    [CreateAssetMenu(fileName = "GameplaySettingsConfig", menuName = "BlockShooter/Configs/Gameplay Settings Config")]
    public class GameplaySettingsConfig : ScriptableObject
    {
        [Header("Block Settings")]
        public float fireRate = 0.15f;
        public float projectileSpeed = 12f;

        [Header("Feature Unlock Levels")]
        public int mysteryShooterUnlockLevel = 4;
        public int freezeShooterUnlockLevel = 3;
        public int doorUnlockLevel = 2;

        [Header("Booster Unlock Levels")]
        public int extraSlotUnlockLevel = 1;
        public int superShooterUnlockLevel = 5;
        public int moveShooterUnlockLevel = 2;
    }
}
