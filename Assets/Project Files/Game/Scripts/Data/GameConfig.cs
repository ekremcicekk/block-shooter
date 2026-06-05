using UnityEngine;
using System.Collections.Generic;

namespace BlockShooter
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "BlockShooter/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Modular Configuration Assets")]
        [Tooltip("Configuration for level prefabs sequence")]
        public LevelSequenceConfig levelSequence;

        [Tooltip("Configuration for general gameplay settings")]
        public GameplaySettingsConfig gameplaySettings;

        [Tooltip("Configuration for block colors and materials registry")]
        public ColorRegistryConfig colorRegistry;

        [Tooltip("Configuration for scoring metrics")]
        public ScoringConfig scoring;

        // ── Backward Compatible Pass-Through Properties & Methods ──────────────────

        public List<LevelRoot> levelPrefabs
        {
            get => levelSequence != null ? levelSequence.levelPrefabs : null;
            set { if (levelSequence != null) levelSequence.levelPrefabs = value; }
        }

        public float fireRate
        {
            get => gameplaySettings != null ? gameplaySettings.fireRate : 0.15f;
            set { if (gameplaySettings != null) gameplaySettings.fireRate = value; }
        }

        public float projectileSpeed
        {
            get => gameplaySettings != null ? gameplaySettings.projectileSpeed : 12f;
            set { if (gameplaySettings != null) gameplaySettings.projectileSpeed = value; }
        }

        public int mysteryShooterUnlockLevel
        {
            get => gameplaySettings != null ? gameplaySettings.mysteryShooterUnlockLevel : 4;
            set { if (gameplaySettings != null) gameplaySettings.mysteryShooterUnlockLevel = value; }
        }

        public int freezeShooterUnlockLevel
        {
            get => gameplaySettings != null ? gameplaySettings.freezeShooterUnlockLevel : 3;
            set { if (gameplaySettings != null) gameplaySettings.freezeShooterUnlockLevel = value; }
        }

        public int doorUnlockLevel
        {
            get => gameplaySettings != null ? gameplaySettings.doorUnlockLevel : 2;
            set { if (gameplaySettings != null) gameplaySettings.doorUnlockLevel = value; }
        }

        public int extraSlotUnlockLevel
        {
            get => gameplaySettings != null ? gameplaySettings.extraSlotUnlockLevel : 1;
            set { if (gameplaySettings != null) gameplaySettings.extraSlotUnlockLevel = value; }
        }

        public int superShooterUnlockLevel
        {
            get => gameplaySettings != null ? gameplaySettings.superShooterUnlockLevel : 5;
            set { if (gameplaySettings != null) gameplaySettings.superShooterUnlockLevel = value; }
        }

        public int moveShooterUnlockLevel
        {
            get => gameplaySettings != null ? gameplaySettings.moveShooterUnlockLevel : 2;
            set { if (gameplaySettings != null) gameplaySettings.moveShooterUnlockLevel = value; }
        }

        public int scorePerBlock
        {
            get => scoring != null ? scoring.scorePerBlock : 10;
            set { if (scoring != null) scoring.scorePerBlock = value; }
        }

        public int scoreComboMultiplier
        {
            get => scoring != null ? scoring.scoreComboMultiplier : 2;
            set { if (scoring != null) scoring.scoreComboMultiplier = value; }
        }

        public List<ColorRegistryConfig.ColorDefinition> colors => colorRegistry != null ? colorRegistry.colors : null;

        public Material GetMaterial(BlockColorType colorType)
        {
            return colorRegistry != null ? colorRegistry.GetMaterial(colorType) : null;
        }

        public Color GetColor(BlockColorType colorType)
        {
            return colorRegistry != null ? colorRegistry.GetColor(colorType) : Color.white;
        }
    }
}
