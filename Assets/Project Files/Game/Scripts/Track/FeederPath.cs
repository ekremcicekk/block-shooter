using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    /// <summary>
    /// A feeder/input path that holds BlockGroups waiting to enter the main ConveyorPath.
    /// When ConnectionPoint signals a gap, the front group slides into the main track.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(ConveyorTrackMesh))]
    public class FeederPath : MonoBehaviour
    {
        [Header("References")]
        public ConveyorPathController mainPath;
        public ConnectionPoint connectionPoint;
        public ConveyorBlock3D blockPrefab;

        [Header("Groups")]
        public List<BlockColorType> groupColors = new();
        public int lanesPerGroup = 5;
        public int rowsPerGroup = 20;

        [Header("Feeder Movement")]
        public float feedSpeed = 2f;

        private SplineContainer _splineContainer;
        private float _splineWorldLength;
        private readonly List<FeederEntry> _feederGroups = new();
        private bool _isFeeding;
        private int _colorIndex;

        private struct FeederEntry
        {
            public BlockGroup Group;
            public float T;
        }

        private void Awake()
        {
            _splineContainer = GetComponent<SplineContainer>();
        }

        private void Start()
        {
            _splineWorldLength = SplineUtility.CalculateLength(_splineContainer.Spline, transform.localToWorldMatrix);
            SpawnFeederGroups();
        }

        private void SpawnFeederGroups()
        {
            for (int i = 0; i < groupColors.Count; i++)
            {
                var group = CreateGroup(groupColors[i]);
                float groupLen = group.SplineLength;
                float t = 1f - (i * groupLen / _splineWorldLength);
                t = Mathf.Clamp01(t);

                _feederGroups.Add(new FeederEntry { Group = group, T = t });
                PlaceGroupAtT(group, t);
            }
        }

        private void Update()
        {
            if (_isFeeding || !GameManager.Instance.IsPlaying) return;

            // Passively move all feeder groups toward the main path exit (T=1)
            float deltaTPerSecond = (feedSpeed * 0.3f) / Mathf.Max(_splineWorldLength, 1f);
            float delta = deltaTPerSecond * Time.deltaTime;

            for (int i = 0; i < _feederGroups.Count; i++)
            {
                var entry = _feederGroups[i];
                if (entry.Group == null) continue;

                float newT = Mathf.Min(entry.T + delta, 1f);
                entry.T = newT;
                _feederGroups[i] = entry;
                PlaceGroupAtT(entry.Group, newT);
            }
        }

        public void StartFeeding()
        {
            if (_feederGroups.Count == 0) return;
            StartCoroutine(FeedNextGroup());
        }

        private IEnumerator FeedNextGroup()
        {
            _isFeeding = true;

            var entry = _feederGroups[0];
            BlockGroup group = entry.Group;

            if (group == null || group.IsEmpty) { _isFeeding = false; yield break; }

            // Animate group sliding to T=1 (junction exit)
            float t = entry.T;
            while (t < 1f)
            {
                float delta = (feedSpeed / _splineWorldLength) * Time.deltaTime;
                t = Mathf.Min(t + delta, 1f);
                entry.T = t;
                _feederGroups[0] = entry;
                PlaceGroupAtT(group, t);
                yield return null;
            }

            // Transfer group to main path
            _feederGroups.RemoveAt(0);
            group.transform.SetParent(null);
            mainPath.InsertGroupAt(group, connectionPoint.mainSplineT);

            connectionPoint.OnGapFilled();

            // Shift remaining groups forward
            ShiftGroupsForward();

            _isFeeding = false;
        }

        private void ShiftGroupsForward()
        {
            float groupTLen = rowsPerGroup * 0.5f / Mathf.Max(_splineWorldLength, 1f);
            for (int i = 0; i < _feederGroups.Count; i++)
            {
                var entry = _feederGroups[i];
                entry.T = Mathf.Clamp01(entry.T + groupTLen);
                _feederGroups[i] = entry;
                PlaceGroupAtT(entry.Group, entry.T);
            }
        }

        private void PlaceGroupAtT(BlockGroup group, float t)
        {
            _splineContainer.Spline.Evaluate(t, out var pos, out var tangent, out var up);
            Vector3 worldPos = transform.TransformPoint(pos);
            Vector3 worldTangent = transform.TransformDirection(tangent).normalized;
            Vector3 worldUp = transform.TransformDirection(up).normalized;
            if (worldUp == Vector3.zero) worldUp = Vector3.up;

            group.transform.position = worldPos;
            if (worldTangent != Vector3.zero)
                group.transform.rotation = Quaternion.LookRotation(worldTangent, worldUp);
        }

        private BlockGroup CreateGroup(BlockColorType color)
        {
            var go = new GameObject($"Group_{color}");
            go.transform.SetParent(transform, false);
            var group = go.AddComponent<BlockGroup>();
            group.Initialize(color, blockPrefab, lanesPerGroup, rowsPerGroup);
            return group;
        }

        public void AddGroupColor(BlockColorType color)
        {
            groupColors.Add(color);
            var group = CreateGroup(color);
            float t = _feederGroups.Count > 0
                ? Mathf.Max(0, _feederGroups[^1].T - group.SplineLength / _splineWorldLength)
                : 0.8f;
            _feederGroups.Add(new FeederEntry { Group = group, T = t });
        }
    }
}
