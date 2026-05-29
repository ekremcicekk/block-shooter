using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    /// <summary>
    /// Generates a 3D U-channel mesh along a Spline.
    /// Produces: flat floor + left wall + right wall (the gray conveyor rail).
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshCollider))]
    public class ConveyorTrackMesh : MonoBehaviour
    {
        [Header("Track Dimensions")]
        [Range(0.5f, 5f)] public float trackWidth = 2.5f;
        [Range(0.05f, 0.5f)] public float wallHeight = 0.28f;
        [Range(0.02f, 0.2f)] public float wallThickness = 0.08f;
        [Range(0.01f, 0.1f)] public float floorThickness = 0.06f;

        [Header("Resolution")]
        [Range(20, 300)] public int segments = 120;

        [Header("Materials")]
        public Material trackMaterial;
        public Material wallMaterial;

        [Header("Auto-rebuild")]
        public bool rebuildOnValidate = true;

        private SplineContainer _splineContainer;
        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;

        private void Awake()
        {
            _splineContainer = GetComponent<SplineContainer>();
            _meshFilter = GetComponent<MeshFilter>();
            _meshCollider = GetComponent<MeshCollider>();
        }

        private void Start() => Rebuild();

        public void Rebuild()
        {
            if (_splineContainer == null) _splineContainer = GetComponent<SplineContainer>();
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshCollider == null) _meshCollider = GetComponent<MeshCollider>();

            Mesh mesh = GenerateTrackMesh();
            _meshFilter.sharedMesh = mesh;
            if (_meshCollider != null) _meshCollider.sharedMesh = mesh;
        }

        private Mesh GenerateTrackMesh()
        {
            var spline = _splineContainer.Spline;
            float halfW = trackWidth * 0.5f;

            var verts = new List<Vector3>();
            var tris = new List<int>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();

            // Cross-section profile (local to each segment frame):
            // Floor: from -halfW to +halfW, y=0
            // Left wall: x=-halfW, y=0 to wallHeight
            // Right wall: x=+halfW, y=0 to wallHeight
            // Inner wall caps (thickness)

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                spline.Evaluate(t, out var pos, out var tangent, out var up);

                Vector3 forward = ((Vector3)tangent).normalized;
                Vector3 upDir = ((Vector3)up).normalized;
                if (upDir == Vector3.zero) upDir = Vector3.up;
                Vector3 right = Vector3.Cross(upDir, forward).normalized;

                // Recalculate up to be perpendicular to forward
                upDir = Vector3.Cross(forward, right).normalized;

                float uCoord = t * 5f; // UV tiling along track

                // Floor vertices (4 verts per segment cross-section)
                // v0: left-bottom-outer, v1: right-bottom-outer
                // v2: left-top (floor surface), v3: right-top (floor surface)
                AddFloorVerts(verts, uvs, normals, pos, right, upDir, halfW, floorThickness, uCoord);

                // Left wall
                AddWallVerts(verts, uvs, normals, pos, right, upDir, -halfW, wallHeight, wallThickness, uCoord);

                // Right wall
                AddWallVerts(verts, uvs, normals, pos, right, upDir, halfW, wallHeight, wallThickness, uCoord);
            }

            // Build triangles
            int vertsPerSegment = 12; // 4 floor + 4 left wall + 4 right wall

            for (int i = 0; i < segments; i++)
            {
                int baseA = i * vertsPerSegment;
                int baseB = (i + 1) * vertsPerSegment;

                // Floor quad (top face)
                AddQuad(tris, baseA + 2, baseA + 3, baseB + 3, baseB + 2);

                // Left wall (outer face)
                AddQuad(tris, baseA + 4, baseB + 4, baseB + 7, baseA + 7);

                // Right wall (outer face)
                AddQuad(tris, baseA + 8, baseA + 11, baseB + 11, baseB + 8);

                // Left wall top
                AddQuad(tris, baseA + 6, baseA + 7, baseB + 7, baseB + 6);

                // Right wall top
                AddQuad(tris, baseA + 10, baseB + 10, baseB + 11, baseA + 11);
            }

            var mesh = new Mesh { name = "ConveyorTrack" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void AddFloorVerts(List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals,
            Vector3 center, Vector3 right, Vector3 up, float halfW, float thickness, float uCoord)
        {
            // Bottom left, bottom right, top left, top right
            verts.Add(center - right * halfW - up * thickness);
            verts.Add(center + right * halfW - up * thickness);
            verts.Add(center - right * halfW);
            verts.Add(center + right * halfW);

            uvs.Add(new Vector2(0, uCoord));
            uvs.Add(new Vector2(1, uCoord));
            uvs.Add(new Vector2(0, uCoord));
            uvs.Add(new Vector2(1, uCoord));

            for (int i = 0; i < 4; i++) normals.Add(up);
        }

        private void AddWallVerts(List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals,
            Vector3 center, Vector3 right, Vector3 up, float sideX, float height, float thickness, float uCoord)
        {
            float sign = sideX > 0 ? 1f : -1f;
            Vector3 baseInner = center + right * sideX;
            Vector3 baseOuter = center + right * (sideX + sign * thickness);

            verts.Add(baseOuter);
            verts.Add(baseInner);
            verts.Add(baseOuter + up * height);
            verts.Add(baseInner + up * height);

            uvs.Add(new Vector2(0, uCoord));
            uvs.Add(new Vector2(1, uCoord));
            uvs.Add(new Vector2(0, uCoord));
            uvs.Add(new Vector2(1, uCoord));

            Vector3 wallNormal = (right * sign).normalized;
            for (int i = 0; i < 4; i++) normals.Add(wallNormal);
        }

        private void AddQuad(List<int> tris, int a, int b, int c, int d)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(d);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (rebuildOnValidate && Application.isPlaying)
                Rebuild();
        }
#endif
    }
}
