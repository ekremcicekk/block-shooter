using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using DG.Tweening;

namespace BlockShooter
{
    /// <summary>
    /// Feeder path that launches blocks to fill a gap on the main conveyor.
    /// No mesh junction needed — blocks physically fly from feeder to main track.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public class FlyingBlockFeeder : MonoBehaviour
    {
        [Header("Main Track Reference")]
        public ConveyorController mainPath;

        [Header("Connection")]
        [Tooltip("T position (0-1) on the MAIN spline where this feeder connects")]
        [Range(0f, 1f)] public float mainConnectionT = 0.5f;

        [Header("Block Groups (in order)")]
        public List<BlockColorType> groupColors = new();
        public int lanesPerGroup = 5;
        public int rowsPerGroup = 20;

        [Header("Prefabs")]
        public ConveyorBlock3D blockPrefab;

        [Header("Flight Settings")]
        public float flyDuration = 0.35f;
        public Ease flyEase = Ease.OutBack;
        public float flyArcHeight = 2f;

        private SplineContainer _feederSpline;
        private SplineContainer _mainSpline;
        private int _currentGroupIndex;
        private bool _isFeeding;

        private void Awake()
        {
            _feederSpline = GetComponent<SplineContainer>();
        }

        private void Start()
        {
            _mainSpline = mainPath?.GetComponent<SplineContainer>();
        }

        private void Update()
        {
            if (!GameManager.Instance.IsPlaying || _isFeeding) return;
            if (_currentGroupIndex >= groupColors.Count) return;

            // Check if there's a gap at our connection point on the main track
            if (mainPath != null && mainPath.IsGapAt(mainConnectionT))
                StartCoroutine(FeedNextGroup());
        }

        private IEnumerator FeedNextGroup()
        {
            _isFeeding = true;

            BlockColorType color = groupColors[_currentGroupIndex];
            _currentGroupIndex++;

            // Calculate target positions on main path (one per lane × row)
            var targetPositions = GetTargetPositions(color);
            var spawnPositions = GetFeederSpawnPositions();

            // Spawn blocks at feeder positions and fly them to target
            var flying = new List<Coroutine>();
            int blockIdx = 0;
            foreach (var targetPos in targetPositions)
            {
                Vector3 startPos = spawnPositions[blockIdx % spawnPositions.Count];
                int capturedIdx = blockIdx;
                float delay = blockIdx * 0.008f; // small stagger for visual appeal
                StartCoroutine(FlyBlock(color, startPos, targetPos, delay));
                blockIdx++;
            }

            // Wait for all blocks to arrive
            yield return new WaitForSeconds(flyDuration + rowsPerGroup * lanesPerGroup * 0.008f + 0.1f);

            _isFeeding = false;
        }

        private IEnumerator FlyBlock(BlockColorType color, Vector3 from, Vector3 to, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            if (blockPrefab == null) yield break;

            var block = Instantiate(blockPrefab, from, Quaternion.identity);
            block.Initialize(color, GameManager.Instance.config.GetColor(color));

            // Arc trajectory via DOTween waypoints
            Vector3 midPoint = Vector3.Lerp(from, to, 0.5f) + Vector3.up * flyArcHeight;
            var path = new[] { from, midPoint, to };

            block.transform.DOPath(path, flyDuration, PathType.CatmullRom)
                .SetEase(flyEase)
                .OnComplete(() => OnBlockLanded(block, to));
        }

        private void OnBlockLanded(ConveyorBlock3D block, Vector3 targetPos)
        {
            block.transform.position = targetPos;
            // Hand block to the main path controller
            mainPath?.RegisterExternalBlock(block, mainConnectionT);
        }

        // ── Position Helpers ──────────────────────────────────────────────────

        private List<Vector3> GetTargetPositions(BlockColorType color)
        {
            var positions = new List<Vector3>();
            if (_mainSpline == null) return positions;

            float gapStartT = mainConnectionT;
            float groupTLength = mainPath != null
                ? (rowsPerGroup * 0.5f) / Mathf.Max(mainPath.SplineWorldLength, 1f)
                : 0.1f;

            for (int row = 0; row < rowsPerGroup; row++)
            {
                float rowT = (gapStartT + (float)row / rowsPerGroup * groupTLength) % 1f;
                _mainSpline.Spline.Evaluate(rowT, out var pos, out var tangent, out var up);

                Vector3 worldPos = mainPath.transform.TransformPoint(pos);
                Vector3 fwd = mainPath.transform.TransformDirection((Vector3)tangent).normalized;
                Vector3 upDir = mainPath.transform.TransformDirection((Vector3)up).normalized;
                if (upDir == Vector3.zero) upDir = Vector3.up;
                Vector3 right = Vector3.Cross(upDir, fwd).normalized;

                float spacing = 0.5f;
                for (int lane = 0; lane < lanesPerGroup; lane++)
                {
                    float x = (lane - (lanesPerGroup - 1) * 0.5f) * spacing;
                    positions.Add(worldPos + right * x);
                }
            }
            return positions;
        }

        private List<Vector3> GetFeederSpawnPositions()
        {
            var positions = new List<Vector3>();
            // Spawn from the tip of the feeder spline (T=1, closest to main track)
            _feederSpline.Spline.Evaluate(1f, out var pos, out var tangent, out var up);
            Vector3 worldPos = transform.TransformPoint(pos);
            Vector3 fwd = transform.TransformDirection((Vector3)tangent).normalized;
            Vector3 upDir = transform.TransformDirection((Vector3)up).normalized;
            if (upDir == Vector3.zero) upDir = Vector3.up;
            Vector3 right = Vector3.Cross(upDir, fwd).normalized;

            for (int lane = 0; lane < lanesPerGroup; lane++)
            {
                float x = (lane - (lanesPerGroup - 1) * 0.5f) * 0.5f;
                positions.Add(worldPos + right * x);
            }
            return positions;
        }
    }
}
