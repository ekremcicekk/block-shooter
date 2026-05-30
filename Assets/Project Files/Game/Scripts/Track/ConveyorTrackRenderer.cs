using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    /// <summary>
    /// Tiles a segment prefab along a Spline to form the conveyor track visual.
    /// Segment prefab must face the +Z axis and be centered at origin.
    /// Auto-calculates segment count from spline length — no manual input needed.
    /// Also manages directional arrows that move at block speed.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public class ConveyorTrackRenderer : MonoBehaviour
    {
        [Header("Track Mesh")]
        [Tooltip("A short straight track segment prefab (faces +Z, centered at origin)")]
        public GameObject segmentPrefab;

        [Header("Direction Arrows")]
        [Tooltip("Arrow sprite/mesh prefab that moves along the track")]
        public GameObject arrowPrefab;
        [Tooltip("World-unit distance between consecutive arrows")]
        public float arrowSpacing = 2.0f;


        private SplineContainer _splineContainer;
        private float _splineWorldLength;
        private readonly List<GameObject> _segments = new();
        private readonly List<ArrowMarker> _arrows = new();

        private struct ArrowMarker
        {
            public Transform Transform;
            public float T;
            public Quaternion PrefabLocalRot; // applied on top of spline tangent
        }

        private void Awake()
        {
            _splineContainer = GetComponent<SplineContainer>();
        }

        private void Start()
        {
            _splineWorldLength = SplineUtility.CalculateLength(
                _splineContainer.Spline, transform.localToWorldMatrix);

            BuildTrack();
            SpawnArrows();
        }

        // ── Track Tiling ──────────────────────────────────────────────────────

        public void BuildTrack()
        {
            ClearSegments();
            if (segmentPrefab == null) return;

            float segLen = GetSegmentLength();
            if (segLen <= 0f) return;

            int count = Mathf.CeilToInt(_splineWorldLength / segLen);

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                var seg = Instantiate(segmentPrefab, transform);
                PlaceObjectOnSpline(seg.transform, t);
                _segments.Add(seg);
            }
        }

        private float GetSegmentLength()
        {
            // Try to measure the prefab's Z-axis extent from its renderers
            var renderers = segmentPrefab.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 1f;

            var combined = renderers[0].bounds;
            foreach (var r in renderers) combined.Encapsulate(r.bounds);
            float len = combined.size.z;
            return len > 0.001f ? len : 1f;
        }

        private void ClearSegments()
        {
            foreach (var s in _segments)
                if (s != null) Destroy(s);
            _segments.Clear();
        }

        // ── Arrow Movement ────────────────────────────────────────────────────

        private void SpawnArrows()
        {
            foreach (var a in _arrows)
                if (a.Transform != null) Destroy(a.Transform.gameObject);
            _arrows.Clear();

            if (arrowPrefab == null || arrowSpacing <= 0f || _splineWorldLength <= 0f) return;

            // Derive count from spacing so arrows are evenly distributed at the desired interval
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

        private void Update()
        {
            if (!GameManager.Instance.IsPlaying) return;

            float speed = ConveyorPathController.Instance != null ? ConveyorPathController.Instance.speed : 1.5f;
            float deltaTPerSec = speed / Mathf.Max(_splineWorldLength, 0.1f);
            float delta = deltaTPerSec * Time.deltaTime;

            for (int i = 0; i < _arrows.Count; i++)
            {
                var a = _arrows[i];
                if (a.Transform == null) continue;

                a.T = (a.T + delta) % 1f;
                _arrows[i] = a;
                PlaceArrow(a.Transform, a.T, a.PrefabLocalRot);
            }
        }

        // ── Spline Placement ──────────────────────────────────────────────────

        // Used for track segments — no rotation offset needed
        private void PlaceObjectOnSpline(Transform obj, float t)
        {
            _splineContainer.Spline.Evaluate(t, out var pos, out var tangent, out var up);

            obj.position = transform.TransformPoint(pos);

            Vector3 fwd = transform.TransformDirection((Vector3)tangent).normalized;
            Vector3 upDir = transform.TransformDirection((Vector3)up).normalized;
            if (upDir == Vector3.zero) upDir = Vector3.up;
            if (fwd != Vector3.zero)
                obj.rotation = Quaternion.LookRotation(fwd, upDir);
        }

        // Used for arrows — preserves the prefab's local rotation on top of spline orientation
        private void PlaceArrow(Transform obj, float t, Quaternion prefabLocalRot)
        {
            _splineContainer.Spline.Evaluate(t, out var pos, out var tangent, out var up);

            obj.position = transform.TransformPoint(pos);

            Vector3 fwd = transform.TransformDirection((Vector3)tangent).normalized;
            Vector3 upDir = transform.TransformDirection((Vector3)up).normalized;
            if (upDir == Vector3.zero) upDir = Vector3.up;
            if (fwd != Vector3.zero)
                obj.rotation = Quaternion.LookRotation(fwd, upDir) * prefabLocalRot;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void RebuildInEditor()
        {
            _splineContainer = GetComponent<SplineContainer>();
            _splineWorldLength = SplineUtility.CalculateLength(
                _splineContainer.Spline, transform.localToWorldMatrix);
            BuildTrack();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_splineContainer == null) return;
            Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.4f);
            int steps = 64;
            Vector3 prev = transform.TransformPoint(_splineContainer.Spline.EvaluatePosition(0f));
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 next = transform.TransformPoint(_splineContainer.Spline.EvaluatePosition(t));
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
#endif
    }
}
