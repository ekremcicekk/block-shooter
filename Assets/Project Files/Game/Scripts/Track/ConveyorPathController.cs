using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    /// <summary>
    /// Manages BlockGroups moving along a SplineContainer.
    /// Handles: group movement, gap detection, connection point triggering.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public class ConveyorPathController : MonoBehaviour
    {
        [Header("Block Group Settings")]
        public ConveyorBlock3D blockPrefab;
        public int lanesPerGroup = 5;
        public int rowsPerGroup = 20;

        [Header("Movement")]
        public float speed = 1.5f;
        public bool loop = true;

        [Header("Connection Points")]
        public List<ConnectionPoint> connectionPoints = new();

        private SplineContainer _splineContainer;
        private float _splineWorldLength;
        private bool _isFrozen;

        // Active groups on this path, sorted by their T position
        private readonly List<GroupEntry> _groups = new();

        private struct GroupEntry
        {
            public BlockGroup Group;
            public float HeadT;   // T of the leading edge (front of group)
            public float TailT;   // T of the trailing edge (back of group)
        }

        public bool IsFrozen { get => _isFrozen; set => _isFrozen = value; }

        private void Awake()
        {
            _splineContainer = GetComponent<SplineContainer>();
        }

        private void Start()
        {
            _splineWorldLength = SplineUtility.CalculateLength(_splineContainer.Spline, transform.localToWorldMatrix);

            foreach (var cp in connectionPoints)
                cp.Initialize(this);
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

        private void Update()
        {
            if (_isFrozen || !GameManager.Instance.IsPlaying) return;

            float deltaTPerSecond = speed / _splineWorldLength;
            float delta = deltaTPerSecond * Time.deltaTime;

            for (int i = 0; i < _groups.Count; i++)
            {
                var entry = _groups[i];
                if (entry.Group == null || entry.Group.IsEmpty) continue;

                entry.HeadT = (entry.HeadT + delta) % 1f;
                entry.TailT = (entry.TailT + delta) % 1f;
                _groups[i] = entry;

                PlaceGroupAtT(entry.Group, entry.HeadT);
                CheckConnectionPoints(entry);
            }
        }

        private void PlaceGroupAtT(BlockGroup group, float headT)
        {
            _splineContainer.Spline.Evaluate(headT, out var pos, out var tangent, out var up);

            Vector3 worldPos = transform.TransformPoint(pos);
            Vector3 worldTangent = transform.TransformDirection(tangent).normalized;
            Vector3 worldUp = transform.TransformDirection(up).normalized;
            if (worldUp == Vector3.zero) worldUp = Vector3.up;

            group.transform.position = worldPos;
            if (worldTangent != Vector3.zero)
                group.transform.rotation = Quaternion.LookRotation(worldTangent, worldUp);
        }

        private void CheckConnectionPoints(GroupEntry entry)
        {
            foreach (var cp in connectionPoints)
            {
                // Check if the gap (empty space) of another group is passing this connection point
                // Simplified: check if there's empty track at this T
                bool isEmpty = IsTrackEmptyAt(cp.mainSplineT);
                if (isEmpty && !cp.IsEmpty)
                    cp.OnGapArrived();
                else if (!isEmpty && cp.IsEmpty)
                    cp.OnGapFilled();
            }
        }

        private bool IsTrackEmptyAt(float t)
        {
            foreach (var entry in _groups)
            {
                if (entry.Group == null || entry.Group.IsEmpty) continue;
                float head = entry.HeadT;
                float tail = entry.TailT;

                // Handle wrap-around
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
            // Group remains as empty slot, connection points will detect the gap
        }

        /// <summary>Insert a feeder group at the given T position on this path.</summary>
        public void InsertGroupAt(BlockGroup group, float t)
        {
            AddGroup(group, t);
        }

        /// <summary>Returns true if the track has no active block group covering position t.</summary>
        public bool IsGapAt(float t)
        {
            return IsTrackEmptyAt(t);
        }

        /// <summary>Called by FlyingBlockFeeder when a block lands on the track.</summary>
        public void RegisterExternalBlock(ConveyorBlock3D block, float connectionT)
        {
            // The block is now physically on the track; the BlockGroup system
            // will pick it up on next spawn cycle. For now it just sits and
            // moves with the track — we parent it and drive it manually.
            block.transform.SetParent(transform, true);
        }

        private float WorldLengthToT(float worldLen)
        {
            if (_splineWorldLength <= 0) return 0f;
            return worldLen / _splineWorldLength;
        }

        public SplineContainer SplineContainer => _splineContainer;
        public float SplineWorldLength => _splineWorldLength;
    }
}
