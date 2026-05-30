using UnityEngine;

namespace BlockShooter
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "BlockShooter/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Block Settings")]
        public int defaultShotCount = 100;
        public float fireRate = 0.15f;
        public float projectileSpeed = 12f;

        [Header("Grid Settings")]
        public float gridCellSize = 1.2f;

        [Header("Colors")]
        public Color redColor = new Color(0.9f, 0.2f, 0.2f);
        public Color blueColor = new Color(0.2f, 0.5f, 0.9f);
        public Color greenColor = new Color(0.2f, 0.8f, 0.3f);
        public Color yellowColor = new Color(1f, 0.85f, 0.1f);
        public Color purpleColor = new Color(0.6f, 0.2f, 0.9f);
        public Color orangeColor = new Color(1f, 0.55f, 0.1f);

        [Header("Booster Unlock Levels (fallback when BoosterData SO is not assigned)")]
        public int extraSlotUnlockLevel  = 1;
        public int freePickUnlockLevel   = 3;
        public int colorBlastUnlockLevel = 5;

        [Header("Scoring")]
        public int scorePerBlock = 10;
        public int scoreComboMultiplier = 2;

        public Color GetColor(BlockColorType colorType)
        {
            return colorType switch
            {
                BlockColorType.Red => redColor,
                BlockColorType.Blue => blueColor,
                BlockColorType.Green => greenColor,
                BlockColorType.Yellow => yellowColor,
                BlockColorType.Purple => purpleColor,
                BlockColorType.Orange => orangeColor,
                _ => Color.white
            };
        }
    }
}
