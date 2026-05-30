using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Lightweight metadata-only ScriptableObject referenced by UI (win panel, progress tracker).
    /// All structural level data (blocks, conveyor, slots) lives in the LevelRoot prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "Level_00", menuName = "BlockShooter/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Header("Level Info")]
        public int            levelIndex;
        public string         levelName;
        public LevelDifficulty difficulty = LevelDifficulty.Normal;

        [Header("Level Goal")]
        public LevelGoalType goalType   = LevelGoalType.ClearAllBlocks;
        public int           goalAmount = 0;
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
