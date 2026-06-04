using UnityEngine;

namespace BlockShooter
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "BlockShooter/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Level Sequence")]
        public System.Collections.Generic.List<LevelRoot> levelPrefabs = new System.Collections.Generic.List<LevelRoot>();

        [Header("Block Settings")]
        public float fireRate = 0.15f;
        public float projectileSpeed = 12f;

[Header("Block Materials")]
        public Material redMaterial;
        public Material blueMaterial;
        public Material greenMaterial;
        public Material yellowMaterial;
        public Material purpleMaterial;
        public Material orangeMaterial;

        [Header("Booster Unlock Levels (fallback when BoosterData SO is not assigned)")]
        public int extraSlotUnlockLevel  = 1;
        public int freePickUnlockLevel   = 3;
        public int colorBlastUnlockLevel = 5;

        [Header("Scoring")]
        public int scorePerBlock = 10;
        public int scoreComboMultiplier = 2;

        public Material GetMaterial(BlockColorType colorType)
        {
            return colorType switch
            {
                BlockColorType.Red    => redMaterial,
                BlockColorType.Blue   => blueMaterial,
                BlockColorType.Green  => greenMaterial,
                BlockColorType.Yellow => yellowMaterial,
                BlockColorType.Purple => purpleMaterial,
                BlockColorType.Orange => orangeMaterial,
                _ => null
            };
        }

        // Fallback for code that still needs a Color (returns material.color if assigned)
        public Color GetColor(BlockColorType colorType)
        {
            var mat = GetMaterial(colorType);
            if (mat != null) return mat.color;
            return colorType switch
            {
                BlockColorType.Red    => new Color(0.9f, 0.2f, 0.2f),
                BlockColorType.Blue   => new Color(0.2f, 0.5f, 0.9f),
                BlockColorType.Green  => new Color(0.2f, 0.8f, 0.3f),
                BlockColorType.Yellow => new Color(1f, 0.85f, 0.1f),
                BlockColorType.Purple => new Color(0.6f, 0.2f, 0.9f),
                BlockColorType.Orange => new Color(1f, 0.55f, 0.1f),
                _ => Color.white
            };
        }
    }
}
