using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    // ── Design-time data classes (serialized inside the prefab) ───────────────

    public enum GridCellType { Empty, ShooterBlock, Door }

    [Serializable]
    public class LevelGridCell
    {
        public int          col;
        public int          row;
        public GridCellType type      = GridCellType.ShooterBlock;
        public BlockColorType color   = BlockColorType.Red;
        public int          shotCount = -1; // -1 = use editor config default
        public int          doorCount = 3;
    }

    [Serializable]
    public class LevelConveyorGroup
    {
        public BlockColorType color    = BlockColorType.Red;
        public int            rowCount = 20;
        public int            laneCount = 5;
    }

    // ── LevelRoot ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Root component of a fully self-contained level prefab.
    /// Holds runtime sub-system references and the design data written by the Level Editor.
    /// </summary>
    public class LevelRoot : MonoBehaviour
    {
        [Header("Sub-Systems")]
        public ConveyorController conveyorController;
        public ShooterGrid        shooterGrid;
        public SlotSystem         slotSystem;
        public FireRange          fireRange;

        [Header("Level Info")]
        public int           levelIndex = 1;
        public string        levelName  = "Level 1";
        public LevelGoalType goalType   = LevelGoalType.ClearAllBlocks;
        public int           goalAmount = 0;

        // ── Design data (written by Level Editor, used to rebuild hierarchy) ──
        [HideInInspector] public int gridCols = 4;
        [HideInInspector] public int gridRows = 2;
        [HideInInspector] public List<LevelGridCell>      cells  = new();
        [HideInInspector] public List<LevelConveyorGroup> groups = new();

        public void Initialize()
        {
            if (conveyorController != null) conveyorController.Initialize();
            if (shooterGrid        != null) shooterGrid.Initialize();
            if (slotSystem         != null) slotSystem.Initialize();
        }
    }
}
