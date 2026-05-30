using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Editor-wide configuration. Assign once — shared across all levels.
    /// Create via: BlockShooter / Create / Level Editor Config
    /// </summary>
    [CreateAssetMenu(fileName = "LevelEditorConfig", menuName = "BlockShooter/Level Editor Config")]
    public class LevelEditorConfig : ScriptableObject
    {
        [Header("Save Path")]
        [Tooltip("Folder where generated level prefabs are saved (must start with Assets/)")]
        public string levelSavePath = "Assets/Project Files/Data/Levels/";

        [Header("Runtime Prefabs")]
        public GameObject shooterBlockPrefab;
        public GameObject wallElementPrefab;
        public GameObject conveyorBlockPrefab;
        public GameObject slotIndicatorPrefab;
        public GameObject trackSegmentPrefab;
        public GameObject arrowPrefab;
        public GameObject fireRangePrefab;
        public GameObject groundPrefab;

        [Header("Conveyor Defaults")]
        public float conveyorSpeed    = 1.5f;
        public int   laneCount        = 5;
        public float laneSpacing      = 0.22f;
        public float rowSpacing       = 0.22f;
        public int   rowsPerGroup     = 20;

        [Header("Slot Defaults")]
        public int slotCount = 4;

        [Header("Grid Defaults")]
        public float gridCellSize  = 1.2f;
        public int   defaultShots  = 3;
    }
}
