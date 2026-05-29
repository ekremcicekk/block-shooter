using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    /// <summary>
    /// Marks where a FeederPath joins the main ConveyorPath.
    /// Placed on the main track spline at the merge knot index.
    /// Automatically orients and places the junction mesh.
    /// </summary>
    public class ConnectionPoint : MonoBehaviour
    {
        [Header("Connection Config")]
        [Tooltip("T parameter on the MAIN spline (0-1) where feeder enters")]
        [Range(0f, 1f)] public float mainSplineT = 0.5f;

        [Header("References")]
        public FeederPath feederPath;
        public GameObject junctionMeshPrefab;
        public Transform junctionInstance;

        [Header("State")]
        public bool IsEmpty { get; private set; }

        private ConveyorPathController _mainPath;

        public void Initialize(ConveyorPathController mainPath)
        {
            _mainPath = mainPath;
            PlaceJunctionMesh();
        }

        private void PlaceJunctionMesh()
        {
            if (junctionMeshPrefab == null) return;

            var splineContainer = _mainPath.GetComponent<SplineContainer>();
            if (splineContainer == null) return;

            splineContainer.Spline.Evaluate(mainSplineT, out var pos, out var tangent, out var up);

            if (junctionInstance == null)
            {
                var go = Instantiate(junctionMeshPrefab, _mainPath.transform);
                junctionInstance = go.transform;
            }

            junctionInstance.position = (Vector3)pos + _mainPath.transform.position;

            // Rotate junction to align with track tangent + feeder direction
            Vector3 trackDir = ((Vector3)tangent).normalized;
            if (trackDir != Vector3.zero)
                junctionInstance.rotation = Quaternion.LookRotation(trackDir, Vector3.up);
        }

        /// <summary>Called by ConveyorPathController when the empty gap reaches this connection point.</summary>
        public void OnGapArrived()
        {
            IsEmpty = true;
            feederPath?.StartFeeding();
        }

        public void OnGapFilled()
        {
            IsEmpty = false;
        }
    }
}
