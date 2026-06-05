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
        public int mysteryShooterUnlockLevel = 4;

        [Header("Block Materials (Legacy - Migrated to Colors List)")]
        public Material redMaterial;
        public Material blueMaterial;
        public Material greenMaterial;
        public Material yellowMaterial;
        public Material purpleMaterial;
        public Material orangeMaterial;

        [System.Serializable]
        public class ColorDefinition
        {
            public BlockColorType colorType;
            public string displayName;
            public Color editorColor;
            public Material material;
        }

        [Header("Registry of Colors & Materials")]
        public System.Collections.Generic.List<ColorDefinition> colors = new System.Collections.Generic.List<ColorDefinition>();

        [Header("Booster Unlock Levels (fallback when BoosterData SO is not assigned)")]
        public int extraSlotUnlockLevel  = 1;
        public int freePickUnlockLevel   = 3;
        public int colorBlastUnlockLevel = 5;
        public int moveShooterUnlockLevel = 2;

        [Header("Scoring")]
        public int scorePerBlock = 10;
        public int scoreComboMultiplier = 2;

        private void OnValidate()
        {
            // Auto-populate default colors if registry is completely empty (migration)
            if (colors == null || colors.Count == 0)
            {
                colors = new System.Collections.Generic.List<ColorDefinition>
                {
                    new ColorDefinition { colorType = BlockColorType.Red,    displayName = "Red",    editorColor = new Color(0.9f, 0.2f, 0.2f),   material = redMaterial },
                    new ColorDefinition { colorType = BlockColorType.Blue,   displayName = "Blue",   editorColor = new Color(0.2f, 0.5f, 0.9f),   material = blueMaterial },
                    new ColorDefinition { colorType = BlockColorType.Green,  displayName = "Green",  editorColor = new Color(0.2f, 0.8f, 0.3f),   material = greenMaterial },
                    new ColorDefinition { colorType = BlockColorType.Yellow, displayName = "Yellow", editorColor = new Color(1.0f, 0.85f, 0.1f),  material = yellowMaterial },
                    new ColorDefinition { colorType = BlockColorType.Purple, displayName = "Purple", editorColor = new Color(0.6f, 0.2f, 0.9f),   material = purpleMaterial },
                    new ColorDefinition { colorType = BlockColorType.Orange, displayName = "Orange", editorColor = new Color(1.0f, 0.55f, 0.1f),  material = orangeMaterial }
                };
            }
        }

        public Material GetMaterial(BlockColorType colorType)
        {
            if (colors != null)
            {
                var def = colors.Find(x => x.colorType == colorType);
                if (def != null && def.material != null)
                    return def.material;
            }

            // Fallback for legacy configuration
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

        // Fallback for code that still needs a Color
        public Color GetColor(BlockColorType colorType)
        {
            if (colors != null)
            {
                var def = colors.Find(x => x.colorType == colorType);
                if (def != null)
                {
                    return def.editorColor;
                }
            }

            // Legacy fallback if not registered
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
