using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Root component of a fully self-contained level prefab.
    /// Holds references to all sub-systems and drives their initialization.
    /// </summary>
    public class LevelRoot : MonoBehaviour
    {
        [Header("Sub-Systems")]
        public ConveyorController conveyorController;
        public ShooterGrid        shooterGrid;
        public SlotSystem         slotSystem;
        public FireRange          fireRange;

        [Header("Level Info")]
        public int            levelIndex            = 1;
        public string         levelName             = "Level 1";
        public LevelDifficulty difficulty           = LevelDifficulty.Normal;
        public LevelGoalType  goalType              = LevelGoalType.ClearAllBlocks;
        public int            goalAmount            = 0;
        public float          conveyorSpeedMultiplier = 1f;

        public void Initialize()
        {
            if (conveyorController != null)
                conveyorController.Initialize(conveyorSpeedMultiplier);
            if (shooterGrid != null)
                shooterGrid.Initialize();
            if (slotSystem != null)
                slotSystem.Initialize();
        }
    }
}
