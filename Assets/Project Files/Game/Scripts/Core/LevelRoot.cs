using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    public enum GridCellType { Empty, ShooterBlock, Door }

    [Serializable]
    public class LevelGridCell
    {
        public int            col, row;
        public GridCellType   type      = GridCellType.ShooterBlock;
        public BlockColorType color     = BlockColorType.Red;
        public int            shotCount = -1;
        public int            doorCount = 3;
    }

    [Serializable]
    public class LevelConveyorGroup
    {
        public BlockColorType color     = BlockColorType.Red;
        public int            rowCount  = 20;
        public int            laneCount = 5;
    }

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

        // ── Design data (written by Level Editor) ─────────────────────────────
        [HideInInspector] public int   gridCols     = 4;
        [HideInInspector] public int   gridRows     = 2;
        [HideInInspector] public float splineWidth  = 6f;
        [HideInInspector] public float splineDepth  = 10f;
        [HideInInspector] public int   splinePreset = 0;

        [HideInInspector] public List<Vector3>           splineKnots      = new();
        [HideInInspector] public List<Vector3>           splineTangentsIn  = new();
        [HideInInspector] public List<Vector3>           splineTangentsOut = new();
        [HideInInspector] public List<int>               splineTangentModes = new(); // TangentMode enum int values
        [HideInInspector] public float openZoneHalfT = 0.08f;
        [HideInInspector] public List<LevelGridCell>     cells       = new();
        [HideInInspector] public List<LevelConveyorGroup> groups     = new();

        public void Initialize()
        {
            if (conveyorController != null) conveyorController.Initialize();
            if (shooterGrid        != null) shooterGrid.Initialize();
            if (slotSystem         != null) slotSystem.Initialize();
        }
    }
}
