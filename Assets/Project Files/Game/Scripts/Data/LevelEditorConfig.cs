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
        public GameObject conveyorBlockPrefab;
        public GameObject slotIndicatorPrefab;
        public GameObject arrowPrefab;
        public GameObject fireRangePrefab;
        public GameObject groundPrefab;

        [Header("Conveyor Defaults")]
        public float conveyorSpeed    = 1.5f;
        public int   laneCount        = 5;
        public float laneSpacing      = 0.22f;
        public float rowSpacing       = 0.22f;
        public int   rowsPerGroup     = 20;
        [Tooltip("Half-width of the flat belt surface (inner groove width = 2 × this)")]
        public float beltHalfWidth    = 0.45f;
        [Tooltip("How far the outer walls rise ABOVE the belt surface")]
        public float wallAboveBelt    = 0.3f;
        [Tooltip("How far the outer walls hang DOWN below the belt surface")]
        public float railHeight       = 1.0f;
        [Tooltip("Thickness of each outer wall")]
        public float railWidth        = 0.1f;
        [Tooltip("Chamfer size on the top-OUTER corner of each wall")]
        public float trackBevelSize   = 0.02f;
        [Tooltip("Number of cross-section rings along the spline")]
        public int   trackResolution  = 60;

        [Header("Slot Defaults")]
        public int slotCount = 4;
        public float slotSpacing = 1.2f;

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
        [Tooltip("Corner rounding radius in XZ (top-down view). 0 = sharp corners.")]
        public float bevelSize      = 0.3f;
        [Tooltip("1 = chamfer. 4-8 = smooth rounded corners.")]
        public int   bevelSegments  = 6;
        [Tooltip("Slot 0 — top/deck surface material")]
        public Material deckTopMaterial;
        [Tooltip("Slot 1 — side wall material")]
        public Material deckWallMaterial;
    }
}
