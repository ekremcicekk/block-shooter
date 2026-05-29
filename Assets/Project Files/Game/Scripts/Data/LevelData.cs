using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BlockShooter
{
    [CreateAssetMenu(fileName = "Level_00", menuName = "BlockShooter/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Header("Level Info")]
        public int levelIndex;
        public string levelName;
        public LevelDifficulty difficulty = LevelDifficulty.Normal;

        [Header("Conveyor Track")]
        [Tooltip("Prefab containing SplineContainer + ConveyorTrackRenderer + ConveyorPathController")]
        public GameObject trackPrefab;
        public float conveyorSpeedMultiplier = 1f;
        public List<ConveyorRowData> conveyorRows = new();

        [Header("Shooter Grid Config")]
        public List<GridCellData> gridCells = new();

        [Header("Level Goal")]
        public LevelGoalType goalType = LevelGoalType.ClearAllBlocks;
        public int goalAmount = 0;

        [Header("Available Colors")]
        public List<BlockColorType> availableColors = new() { BlockColorType.Red, BlockColorType.Blue, BlockColorType.Green };

#if UNITY_EDITOR
        [ContextMenu("Fill Grid — T-Shape (default)")]
        private void FillTShape()
        {
            gridCells = GridLayouts.TShape(availableColors);
            EditorUtility.SetDirty(this);
        }
#endif
    }

    [Serializable]
    public class ConveyorRowData
    {
        public List<BlockColorType> columns = new();
    }

    [Serializable]
    public class GridCellData
    {
        public int column;
        public int row;
        public GridCellType cellType = GridCellType.ShooterBlock;
        public BlockColorType color = BlockColorType.Red;
        public int customShotCount = -1; // -1 = use default
        public int doorBlockCount = 3;   // for door cells
    }

    /// <summary>
    /// Helper: builds a T-shaped grid layout used as the default Level 1 layout.
    ///
    /// Coordinate system (ShooterGrid.GetWorldPosition):
    ///   col  → X axis   (0 = left, 4 = right, col2 = centre X=0)
    ///   row  → Z axis   (0 = back/bottom-screen, higher = closer to slots)
    ///
    /// T-shape (5 cols wide, 3 rows deep):
    ///   row 2  cols 0-4   ← FRONT ROW (slota en yakın, tıklanabilir)
    ///   row 1  cols 1-3   ← MIDDLE
    ///   row 0  col  2     ← BACK (en derinne)
    ///
    /// Colors cycle through availableColors left-to-right, front-to-back.
    /// </summary>
    public static class GridLayouts
    {
        public static List<GridCellData> TShape(List<BlockColorType> colors, int shotCount = -1)
        {
            var cells = new List<GridCellData>();
            var layout = new (int col, int row)[]
            {
                // row 2 — front (5 wide)
                (0,2),(1,2),(2,2),(3,2),(4,2),
                // row 1 — middle (3 wide, centred)
                (1,1),(2,1),(3,1),
                // row 0 — back (1 wide, centre)
                (2,0),
            };

            for (int i = 0; i < layout.Length; i++)
            {
                cells.Add(new GridCellData
                {
                    column         = layout[i].col,
                    row            = layout[i].row,
                    cellType       = GridCellType.ShooterBlock,
                    color          = colors != null && colors.Count > 0
                                     ? colors[i % colors.Count]
                                     : BlockColorType.Red,
                    customShotCount = shotCount,
                });
            }
            return cells;
        }
    }

    public enum GridCellType
    {
        Empty,
        ShooterBlock,
        Door
    }

    public enum LevelDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    public enum LevelGoalType
    {
        ClearAllBlocks,
        ClearCount,
        SurviveTime
    }
}
