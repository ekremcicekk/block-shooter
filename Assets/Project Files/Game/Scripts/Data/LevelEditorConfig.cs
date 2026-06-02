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
        public int   defaultShots  = 100;

        [Header("Arrow Settings")]
        [Tooltip("World-unit spacing between arrows on the conveyor path")]
        public float arrowSpacing = 2f;

        [Header("Track Materials")]
        [Tooltip("Slot 0 — wall/side material (M_ConveyorSide)")]
        public Material trackSideMaterial;
        [Tooltip("Slot 1 — belt surface material (M_ConveyorIn)")]
        public Material trackBeltMaterial;

        [Header("Shooter Deck")]
        [Tooltip("Platform extension to the left and right of the grid")]
        public float sideWingWidth = 2f;
        [Tooltip("Platform extension behind the grid (away from conveyor)")]
        public float backDepth     = 2f;
        [Tooltip("Wall drop height below Y=0")]
        public float deckTileHeight = 0.15f;
        [Tooltip("Bevel width — how far the arc cuts into the top surface and wall.")]
        public float bevelSize      = 0.05f;
        [Tooltip("1 = flat chamfer. 3-6 = smooth rounded arc.")]
        public int   bevelSegments  = 4;
        [Tooltip("Slot 0 — top/deck surface material")]
        public Material deckTopMaterial;
        [Tooltip("Slot 1 — side wall material")]
        public Material deckWallMaterial;
    }
}
