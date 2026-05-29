using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    /// <summary>
    /// Sweeps a 2D cross-section profile along the spline to produce a smooth track mesh.
    /// The profile matches a U-channel: flat belt surface + two side rails hanging down.
    ///
    ///  Cross-section (viewed from front, Y = up, X = right):
    ///
    ///   P0──P1        P6──P7
    ///   |    \        /    |
    ///   |     P2────P5     |     <- belt bottom
    ///   |     P3────P4     |     <- belt top surface  (facing up)
    ///  wall            wall
    ///
    /// Adjust Belt Half Width, Rail Height, Rail Width, Belt Thickness in the Inspector.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ConveyorTrackMeshBuilder : MonoBehaviour
    {
        [Header("Cross-Section Profile")]
        [Tooltip("Half-width of the flat belt surface")]
        public float beltHalfWidth  = 1.2f;
        [Tooltip("Thickness of the belt plate")]
        public float beltThickness  = 0.05f;
        [Tooltip("Height of the side rails (how far they hang down)")]
        public float railHeight     = 0.28f;
        [Tooltip("Width / thickness of each side rail")]
        public float railWidth      = 0.10f;

        [Header("Quality")]
        [Range(60, 800)] public int resolution = 240;

        // ── Runtime ───────────────────────────────────────────────────────────
        private SplineContainer _spline;
        private MeshFilter      _meshFilter;

        private void Awake()
        {
            _spline     = GetComponent<SplineContainer>();
            _meshFilter = GetComponent<MeshFilter>();
        }

        private void Start() => BuildMesh();

        // ── Profile Definition ────────────────────────────────────────────────

        // Returns the 2D cross-section profile.
        // X = track-right direction, Y = track-up direction.
        // Belt top surface sits at Y = 0 → beltThickness.
        // Rails hang from Y = 0 downward.
        private Vector2[] BuildProfile()
        {
            float hw = beltHalfWidth;
            float rw = railWidth;
            float rh = railHeight;
            float bt = beltThickness;

            //  P0 ─ P1          P6 ─ P7
            //  |     P2 ──── P5     |
            //        P3 ──── P4         <- belt top (Y = bt)
            //
            //  Left outer wall : P0(bottom) → P1(top)
            //  Left inner wall : P1 → P2(top, inner)
            //  Belt bottom edge: P2 → P3 (left belt side)
            //  Belt top surface: P3 → P4
            //  Belt bottom edge: P4 → P5 (right belt side)
            //  Right inner wall: P5 → P6
            //  Right outer wall: P6(top) → P7(bottom)

            return new Vector2[]
            {
                new(-hw - rw, -rh),   // P0  left outer wall bottom
                new(-hw - rw,  0f),   // P1  left outer wall top
                new(-hw,       0f),   // P2  left rail–belt junction (bottom)
                new(-hw,       bt),   // P3  left belt top edge
                new( hw,       bt),   // P4  right belt top edge  ← belt surface
                new( hw,       0f),   // P5  right rail–belt junction (bottom)
                new( hw + rw,  0f),   // P6  right outer wall top
                new( hw + rw, -rh),   // P7  right outer wall bottom
            };
        }

        // ── Mesh Generation ───────────────────────────────────────────────────

        public void BuildMesh()
        {
            if (_spline     == null) _spline     = GetComponent<SplineContainer>();
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();

            _meshFilter.mesh = GenerateMesh();
        }

        private Mesh GenerateMesh()
        {
            Vector2[] profile = BuildProfile();
            int       pCount  = profile.Length;          // 8
            int       sCount  = resolution + 1;          // spline samples

            // Pre-compute world-space frame at every spline sample
            var wPos   = new Vector3[sCount];
            var wRight = new Vector3[sCount];
            var wUp    = new Vector3[sCount];

            for (int s = 0; s < sCount; s++)
            {
                float t = (float)s / resolution;
                _spline.Spline.Evaluate(t, out var pos, out var tan, out var up);

                Vector3 fwd = transform.TransformDirection((Vector3)tan).normalized;
                Vector3 upW = transform.TransformDirection((Vector3)up);
                if (upW.sqrMagnitude < 0.001f) upW = Vector3.up;
                upW = upW.normalized;

                wPos[s]   = transform.TransformPoint(pos);
                wRight[s] = Vector3.Cross(upW, fwd).normalized;
                wUp[s]    = upW;
            }

            // Each profile edge (pCount-1 edges) × (resolution quads)
            int edgeCount = pCount - 1;
            int quadCount = edgeCount * resolution;
            int vertCount = quadCount * 4;
            int triCount  = quadCount * 6;

            var verts = new Vector3[vertCount];
            var norms = new Vector3[vertCount];
            var uvs   = new Vector2[vertCount];
            var tris  = new int[triCount];

            int vi = 0, ti = 0;

            for (int e = 0; e < edgeCount; e++)
            {
                Vector2 pa = profile[e];
                Vector2 pb = profile[e + 1];

                // UV: u spans the edge (0→1), v tiles along the path
                float uA = (float)e       / edgeCount;
                float uB = (float)(e + 1) / edgeCount;

                for (int s = 0; s < resolution; s++)
                {
                    Vector3 v0 = ProfileToWorld(pa, s,     wPos, wRight, wUp);
                    Vector3 v1 = ProfileToWorld(pb, s,     wPos, wRight, wUp);
                    Vector3 v2 = ProfileToWorld(pa, s + 1, wPos, wRight, wUp);
                    Vector3 v3 = ProfileToWorld(pb, s + 1, wPos, wRight, wUp);

                    float vCoord0 = (float)s       / resolution * 8f;
                    float vCoord1 = (float)(s + 1) / resolution * 8f;

                    verts[vi]     = v0; uvs[vi]     = new Vector2(uA, vCoord0); vi++;
                    verts[vi]     = v1; uvs[vi]     = new Vector2(uB, vCoord0); vi++;
                    verts[vi]     = v2; uvs[vi]     = new Vector2(uA, vCoord1); vi++;
                    verts[vi]     = v3; uvs[vi]     = new Vector2(uB, vCoord1); vi++;

                    int b = vi - 4;
                    tris[ti++] = b;     tris[ti++] = b + 2; tris[ti++] = b + 1;
                    tris[ti++] = b + 1; tris[ti++] = b + 2; tris[ti++] = b + 3;
                }
            }

            var mesh = new Mesh { name = "ConveyorTrack_Swept" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices    = verts;
            mesh.uv          = uvs;
            mesh.triangles   = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 ProfileToWorld(Vector2 p, int sampleIdx,
            Vector3[] wPos, Vector3[] wRight, Vector3[] wUp)
        {
            return wPos[sampleIdx]
                 + wRight[sampleIdx] * p.x
                 + wUp[sampleIdx]    * p.y;
        }

        // ── Editor ────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        [UnityEditor.CustomEditor(typeof(ConveyorTrackMeshBuilder))]
        public class Editor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();
                UnityEditor.EditorGUILayout.Space(6);
                if (GUILayout.Button("Rebuild Mesh", GUILayout.Height(34)))
                {
                    var builder = (ConveyorTrackMeshBuilder)target;
                    builder._spline     = builder.GetComponent<SplineContainer>();
                    builder._meshFilter = builder.GetComponent<MeshFilter>();
                    builder.BuildMesh();
                    UnityEditor.EditorUtility.SetDirty(builder.gameObject);
                }
            }
        }
#endif
    }
}
