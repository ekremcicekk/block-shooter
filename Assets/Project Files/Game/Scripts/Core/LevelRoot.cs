using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    public enum GridCellType { Empty, ShooterBlock, Door, MysteryShooter, FreezeShooter }

    [Serializable]
    public class LevelGridCell
    {
        public int            col, row;
        public GridCellType   type      = GridCellType.ShooterBlock;
        public BlockColorType color     = BlockColorType.Red;
        public int            shotCount = -1;
        public int            doorCount = 3;
        public int            freezeCount = 3;
    }

    [Serializable]
    public class LevelConveyorGroup
    {
        public BlockColorType color     = BlockColorType.Red;
        public int            rowCount  = 20;
        public int            laneCount = 5;
    }

    [Serializable]
    public class BranchPathData
    {
        public string branchName = "Branch_0";
        public float mergeT = 0.5f;
        public bool connectFromLeft = false;

        public List<Vector3> splineKnots = new();
        public List<Vector3> splineTangentsIn = new();
        public List<Vector3> splineTangentsOut = new();
        public List<int> splineTangentModes = new();

        public List<LevelConveyorGroup> groups = new();
    }

    public class LevelRoot : MonoBehaviour
    {
        [Header("Sub-Systems")]
        public ConveyorController conveyorController;
        public ShooterGrid        shooterGrid;
        public SlotSystem         slotSystem;
        public FireRange          fireRange;

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
        [HideInInspector] public List<BranchPathData>     branches   = new();

        public void Initialize()
        {
            if (conveyorController != null) conveyorController.Initialize();
            if (shooterGrid        != null) shooterGrid.Initialize();
            if (slotSystem         != null) slotSystem.Initialize();
        }
    }
}
