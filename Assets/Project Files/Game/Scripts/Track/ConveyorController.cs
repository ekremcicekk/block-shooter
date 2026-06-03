using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    /// <summary>
    /// Moves pre-placed BlockGroup children along a SplineContainer loop.
    /// Replaces the old ConveyorPathController. Block GameObjects are created
    /// by the Level Editor tool — this script only animates them at runtime.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public class ConveyorController : MonoBehaviour
    {
        public static ConveyorController Instance { get; private set; }

        [Header("Movement")]
        public float speed = 1.5f;
        public bool  loop  = true;

        [Header("Direction Arrows")]
        [Tooltip("Arrow prefab that moves along the track")]
        public GameObject arrowPrefab;
        [Tooltip("World-unit distance between consecutive arrows")]
        public float      arrowSpacing = 2.0f;

        public bool  IsFrozen         { get => _isFrozen; set => _isFrozen = value; }
        public float SplineWorldLength => _splineWorldLength;
        public SplineContainer SplineContainer => _splineContainer;

        private SplineContainer _splineContainer;
        private float _splineWorldLength;
        private bool  _isFrozen;

        private readonly List<GroupEntry> _groups = new();
        private readonly List<ArrowMarker> _arrows = new();

        private struct GroupEntry
        {
            public BlockGroup Group;
            public float HeadT;
            public float TailT;
        }

        private struct ArrowMarker
        {
            public Transform Transform;
            public float T;
            public Quaternion PrefabLocalRot; // applied on top of spline tangent
        }

        private void Awake()
        {
            if (!Application.isPlaying) return;
            if (Instance != null && Instance != this) Destroy(Instance.gameObject);
            Instance = this;
            _splineContainer = GetComponent<SplineContainer>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Called by LevelRoot.Initialize(). Scans BlockGroup children and starts movement.
        /// </summary>
        public void Initialize(float speedMultiplier = 1f)
        {
            if (_splineContainer == null) _splineContainer = GetComponent<SplineContainer>();
            speed *= speedMultiplier;
            _splineWorldLength = SplineUtility.CalculateLength(
                _splineContainer.Spline, transform.localToWorldMatrix);

            var blockGroups = GetComponentsInChildren<BlockGroup>(true);
            float currentT = 0f;
            foreach (var group in blockGroups)
            {
                // Skip block groups that belong to branch paths
                if (group.GetComponentInParent<BranchPath>() != null) continue;

                group.Initialize();
                AddGroup(group, currentT);
                currentT += WorldLengthToT(group.SplineLength);
                if (currentT >= 1f) currentT -= 1f;
            }

            // Initialize all branch paths in the scene
            var branchPaths = FindObjectsOfType<BranchPath>();
            foreach (var bp in branchPaths)
            {
                bp.Initialize();
            }

            SpawnArrows();
        }

        private void SpawnArrows()
        {
            foreach (var a in _arrows)
                if (a.Transform != null) Destroy(a.Transform.gameObject);
            _arrows.Clear();

            if (arrowPrefab == null || arrowSpacing <= 0f || _splineWorldLength <= 0f) return;

            int count = Mathf.Max(1, Mathf.RoundToInt(_splineWorldLength / arrowSpacing));
            Quaternion prefabRot = arrowPrefab.transform.localRotation;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                var go = Instantiate(arrowPrefab, transform);
                var marker = new ArrowMarker { Transform = go.transform, T = t, PrefabLocalRot = prefabRot };
                PlaceArrow(go.transform, t, prefabRot);
                _arrows.Add(marker);
            }
        }

        private void PlaceArrow(Transform obj, float t, Quaternion prefabLocalRot)
        {
            if (_splineContainer == null) return;
            _splineContainer.Spline.Evaluate(t, out var pos, out var tangent, out var up);

            pos.y = 0f;

            obj.position = transform.TransformPoint(pos);

            Vector3 fwd = transform.TransformDirection((Vector3)tangent).normalized;
            Vector3 upDir = transform.TransformDirection((Vector3)up).normalized;
            if (upDir == Vector3.zero) upDir = Vector3.up;
            if (fwd != Vector3.zero)
                obj.rotation = Quaternion.LookRotation(fwd, upDir) * prefabLocalRot;
        }

        private void Update()
        {
            if (_isFrozen || !GameManager.Instance.IsPlaying) return;
            if (_splineWorldLength <= 0f) return;

            float delta = (speed / _splineWorldLength) * Time.deltaTime;

            for (int i = 0; i < _groups.Count; i++)
            {
                var entry = _groups[i];
                if (entry.Group == null) continue;

                entry.HeadT = (entry.HeadT + delta) % 1f;
                entry.TailT = (entry.TailT + delta) % 1f;
                _groups[i]  = entry;

                PlaceGroupAtT(entry.Group, entry.HeadT);
            }

            for (int i = 0; i < _arrows.Count; i++)
            {
                var a = _arrows[i];
                if (a.Transform == null) continue;

                a.T = (a.T + delta) % 1f;
                _arrows[i] = a;
                PlaceArrow(a.Transform, a.T, a.PrefabLocalRot);
            }
        }

        public void AddGroup(BlockGroup group, float startT = 0f)
        {
            float groupTLength = WorldLengthToT(group.SplineLength);
            _groups.Add(new GroupEntry
            {
                Group = group,
                HeadT = startT,
                TailT = (startT + groupTLength) % 1f
            });
            group.transform.SetParent(transform, false);
            PlaceGroupAtT(group, startT);
            group.OnGroupCleared += HandleGroupCleared;
        }

        public void InsertGroupAt(BlockGroup group, float t) => AddGroup(group, t);

        public void ForceUpdateGroupPosition(BlockGroup group)
        {
            foreach (var entry in _groups)
            {
                if (entry.Group == group)
                {
                    PlaceGroupAtT(group, entry.HeadT);
                    break;
                }
            }
        }

        public bool IsGapAt(float t) => IsTrackEmptyAt(t);

        public void RegisterExternalBlock(ConveyorBlock3D block, float connectionT)
        {
            block.transform.SetParent(transform, true);
        }

        public void DestroyBlocksInFireRange()
        {
            if (FireRange.Instance == null) return;
            var bounds = FireRange.Instance.GetBounds();
            foreach (var entry in _groups)
            {
                if (entry.Group == null || entry.Group.IsEmpty) continue;
                entry.Group.DestroyBlocksInBounds(bounds);
            }
        }

        public List<ConveyorBlock3D> GetOrderedBlocks(BlockColorType colorType, bool anyColor = false)
        {
            var result = new List<ConveyorBlock3D>();
            foreach (var entry in _groups)
            {
                if (entry.Group == null || entry.Group.IsEmpty) continue;
                if (!anyColor && entry.Group.colorType != colorType) continue;

                for (int row = 0; row < entry.Group.RowCount; row++)
                    for (int lane = 0; lane < entry.Group.LaneCount; lane++)
                    {
                        var block = entry.Group.GetBlock(row, lane);
                        if (block != null && !block.IsDestroyed && block.gameObject.activeSelf)
                            result.Add(block);
                    }
            }
            return result;
        }

        private void PlaceGroupAtT(BlockGroup group, float headT)
        {
            if (_splineWorldLength <= 0f) return;

            float groupTLength = group.SplineLength / _splineWorldLength;

            for (int row = 0; row < group.RowCount; row++)
            {
                // Row_0 = leading edge (highest T offset → enters fire range first).
                // Row_N-1 = trailing edge (T = headT → enters last).
                float rowT = (headT + (float)(group.RowCount - 1 - row) / group.RowCount * groupTLength) % 1f;
                _splineContainer.Spline.Evaluate(rowT, out var pos, out var tangent, out var up);

                Vector3 worldPos = transform.TransformPoint(pos);
                Vector3 fwd     = transform.TransformDirection((Vector3)tangent).normalized;
                Vector3 upDir   = transform.TransformDirection((Vector3)up).normalized;
                if (upDir == Vector3.zero) upDir = Vector3.up;
                Vector3 right   = Vector3.Cross(upDir, fwd).normalized;
                Quaternion rot  = fwd != Vector3.zero ? Quaternion.LookRotation(fwd, upDir) : Quaternion.identity;

                for (int lane = 0; lane < group.LaneCount; lane++)
                {
                    var block = group.GetBlock(row, lane);
                    if (block == null || !block.gameObject.activeSelf) continue;
                    float xOff = (lane - (group.LaneCount - 1) * 0.5f) * group.LaneSpacing;
                    Vector3 targetPos = worldPos + right * xOff;
                    Quaternion targetRot = rot;
                    block.transform.position = targetPos + block.transitionOffset;
                    block.transform.rotation = targetRot * block.transitionRotOffset;
                }
            }

            _splineContainer.Spline.Evaluate(headT, out var hPos, out _, out _);
            group.transform.position = transform.TransformPoint(hPos);
        }

        private bool IsTrackEmptyAt(float t)
        {
            foreach (var entry in _groups)
            {
                if (entry.Group == null) continue;
                float head = entry.HeadT, tail = entry.TailT;
                if (head <= tail)
                {
                    if (t >= head && t <= tail) return false;
                }
                else
                {
                    if (t >= head || t <= tail) return false;
                }
            }
            return true;
        }

        private void HandleGroupCleared(BlockGroup group)
        {
            group.OnGroupCleared -= HandleGroupCleared;
            _groups.RemoveAll(e => e.Group == group);
            if (group != null && group.gameObject != null)
            {
                Destroy(group.gameObject);
            }
        }

        public void RemoveGroup(BlockGroup group)
        {
            if (group == null) return;
            group.OnGroupCleared -= HandleGroupCleared;
            _groups.RemoveAll(e => e.Group == group);
            Destroy(group.gameObject);
        }

        private float WorldLengthToT(float worldLen)
        {
            return _splineWorldLength > 0f ? worldLen / _splineWorldLength : 0f;
        }

        public bool IsRangeEmpty(float startT, float endT)
        {
            startT = (startT % 1f + 1f) % 1f;
            endT = (endT % 1f + 1f) % 1f;

            foreach (var entry in _groups)
            {
                if (entry.Group == null || !entry.Group.gameObject.activeInHierarchy) continue;

                float head = entry.HeadT;
                float tail = entry.TailT;

                if (Overlays(startT, endT, head, tail))
                {
                    return false;
                }
            }
            return true;
        }

        public bool IsRangeEmptyForLane(float startT, float endT, int laneIndex)
        {
            startT = (startT % 1f + 1f) % 1f;
            endT = (endT % 1f + 1f) % 1f;

            foreach (var entry in _groups)
            {
                var group = entry.Group;
                if (group == null || !group.gameObject.activeInHierarchy) continue;

                float head = entry.HeadT;
                float tail = entry.TailT;

                // Treat empty groups (newly created merged groups) as occupying all lanes
                if (group.IsEmpty)
                {
                    if (Overlays(startT, endT, head, tail))
                    {
                        return false;
                    }
                    continue;
                }

                float groupTLength = group.SplineLength / _splineWorldLength;

                for (int row = 0; row < group.RowCount; row++)
                {
                    var block = group.GetBlock(row, laneIndex);
                    if (block == null || !block.gameObject.activeSelf || block.IsDestroyed) continue;

                    // Calculate the exact T of this row along the spline
                    float rowT = (head + (float)(group.RowCount - 1 - row) / group.RowCount * groupTLength) % 1f;

                    if (IsTInRange(rowT, startT, endT))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool IsTInRange(float t, float start, float end)
        {
            if (start <= end)
            {
                return t >= start && t <= end;
            }
            else
            {
                return t >= start || t <= end;
            }
        }

        private bool Overlays(float s1, float e1, float s2, float e2)
        {
            if (s1 <= e1)
            {
                if (s2 <= e2)
                {
                    return s1 <= e2 && e1 >= s2;
                }
                else
                {
                    return s1 <= e2 || e1 >= s2;
                }
            }
            else
            {
                if (s2 <= e2)
                {
                    return s2 <= e1 || e2 >= s1;
                }
                else
                {
                    return true;
                }
            }
        }
    }
}
