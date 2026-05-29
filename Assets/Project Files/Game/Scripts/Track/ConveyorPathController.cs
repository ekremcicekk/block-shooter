using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    [RequireComponent(typeof(SplineContainer))]
    public class ConveyorPathController : MonoBehaviour
    {
        [Header("Block Group Settings")]
        public ConveyorBlock3D blockPrefab;
        public int lanesPerGroup = 5;
        public int rowsPerGroup = 20;

        [Header("Initial Groups (spawned on Start)")]
        [Tooltip("One entry per color group — spawned evenly around the spline at startup")]
        public BlockColorType[] groupColors = { BlockColorType.Red, BlockColorType.Blue };

        [Header("Movement")]
        public float speed = 1.5f;
        public bool loop = true;

        private SplineContainer _splineContainer;
        private float _splineWorldLength;
        private bool _isFrozen;

        private readonly List<GroupEntry> _groups = new();

        private struct GroupEntry
        {
            public BlockGroup Group;
            public float HeadT;
            public float TailT;
        }

        public bool IsFrozen { get => _isFrozen; set => _isFrozen = value; }
        public float SplineWorldLength => _splineWorldLength;
        public SplineContainer SplineContainer => _splineContainer;

        private void Awake()
        {
            _splineContainer = GetComponent<SplineContainer>();
        }

        private void Start()
        {
            _splineWorldLength = SplineUtility.CalculateLength(
                _splineContainer.Spline, transform.localToWorldMatrix);

            SpawnInitialGroups();
        }

        private void SpawnInitialGroups()
        {
            if (blockPrefab == null || groupColors == null || groupColors.Length == 0) return;

            for (int i = 0; i < groupColors.Length; i++)
            {
                float startT = (float)i / groupColors.Length;

                var groupGo = new GameObject($"BlockGroup_{groupColors[i]}");
                var group = groupGo.AddComponent<BlockGroup>();
                group.Initialize(groupColors[i], blockPrefab, lanesPerGroup, rowsPerGroup);

                AddGroup(group, startT);
            }
        }

        private void Update()
        {
            if (_isFrozen || !GameManager.Instance.IsPlaying) return;
            if (_splineWorldLength <= 0f) return;

            float delta = (speed / _splineWorldLength) * Time.deltaTime;

            for (int i = 0; i < _groups.Count; i++)
            {
                var entry = _groups[i];
                if (entry.Group == null || entry.Group.IsEmpty) continue;

                entry.HeadT = (entry.HeadT + delta) % 1f;
                entry.TailT = (entry.TailT + delta) % 1f;
                _groups[i] = entry;

                PlaceGroupAtT(entry.Group, entry.HeadT);
            }
        }

        public void AddGroup(BlockGroup group, float startT = 0f)
        {
            float groupTLength = WorldLengthToT(group.SplineLength);
            _groups.Add(new GroupEntry
            {
                Group = group,
                HeadT = startT,
                TailT = startT + groupTLength
            });
            group.transform.SetParent(transform, false);
            PlaceGroupAtT(group, startT);
            group.OnGroupCleared += HandleGroupCleared;
        }

        public void InsertGroupAt(BlockGroup group, float t) => AddGroup(group, t);

        public bool IsGapAt(float t) => IsTrackEmptyAt(t);

        public void RegisterExternalBlock(ConveyorBlock3D block, float connectionT)
        {
            block.transform.SetParent(transform, true);
        }

        /// <summary>
        /// Destroys all conveyor blocks currently inside the FireRange bounds (used by BombBooster).
        /// </summary>
        public void DestroyBlocksInFireRange()
        {
            if (FireRange.Instance == null) return;
            var fr = FireRange.Instance;
            foreach (var entry in _groups)
            {
                if (entry.Group == null || entry.Group.IsEmpty) continue;
                entry.Group.DestroyBlocksInBounds(fr.GetBounds());
            }
        }

        private void PlaceGroupAtT(BlockGroup group, float headT)
        {
            if (_splineWorldLength <= 0f) return;

            float groupTLength = group.SplineLength / _splineWorldLength;

            for (int row = 0; row < group.rowCount; row++)
            {
                float rowT = (headT + (float)row / group.rowCount * groupTLength) % 1f;
                _splineContainer.Spline.Evaluate(rowT, out var pos, out var tangent, out var up);

                Vector3 worldPos = transform.TransformPoint(pos);
                Vector3 fwd     = transform.TransformDirection((Vector3)tangent).normalized;
                Vector3 upDir   = transform.TransformDirection((Vector3)up).normalized;
                if (upDir == Vector3.zero) upDir = Vector3.up;
                Vector3 right   = Vector3.Cross(upDir, fwd).normalized;
                Quaternion rot  = fwd != Vector3.zero ? Quaternion.LookRotation(fwd, upDir) : Quaternion.identity;

                for (int lane = 0; lane < group.laneCount; lane++)
                {
                    var block = group.GetBlock(row, lane);
                    if (block == null || !block.gameObject.activeSelf) continue;
                    float xOff = (lane - (group.laneCount - 1) * 0.5f) * group.laneSpacing;
                    block.transform.SetPositionAndRotation(worldPos + right * xOff, rot);
                    block.transform.localScale = Vector3.one * group.blockSize;
                }
            }

            // Keep group transform at head for reference/debugging
            _splineContainer.Spline.Evaluate(headT, out var hPos, out _, out _);
            group.transform.position = transform.TransformPoint(hPos);
        }

        private bool IsTrackEmptyAt(float t)
        {
            foreach (var entry in _groups)
            {
                if (entry.Group == null || entry.Group.IsEmpty) continue;
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
        }

        private float WorldLengthToT(float worldLen)
        {
            return _splineWorldLength > 0f ? worldLen / _splineWorldLength : 0f;
        }
    }
}
