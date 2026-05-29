using System;
using System.Collections.Generic;
using UnityEngine;

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
