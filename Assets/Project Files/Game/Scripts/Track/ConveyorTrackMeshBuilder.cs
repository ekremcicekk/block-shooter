using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    /// <summary>
    /// Closed-profile spline sweep — equivalent to Blender curve bevel extrusion.
    ///
    /// Architecture:
    ///   1. Build a closed 2D polygon (profile ring).
    ///   2. Sample the spline → array of (position, right, up) frames.
    ///   3. Project each profile vertex into every frame → 3D ring.
    ///   4. Connect ring[s] to ring[s+1] with quads for every profile edge.
    ///   5. Each profile edge is its own isolated vertex strip → automatic hard edges
    ///      between strips (chamfer bevels are crisp, belt top is smooth).
    ///
    /// Profile cross-section (Y = up, X = right, viewed from front):
    ///
    ///   P2────P3         P6────P7
    ///  ╱        ╲       ╱        ╲    ← chamfered top-outer corners
    /// P1          P4───P5          P8 ← wall tops / belt surface at Y=0
    /// |                             |
    /// P0                            P9
    /// |  P11──────────────────P10   |  ← underside
    /// └──────────────────────────────┘
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ConveyorTrackMeshBuilder : MonoBehaviour
    {
        // ── Profile Parameters ────────────────────────────────────────────────
        [Header("Cross-Section Profile")]
        [Tooltip("Half-width of the flat belt surface (inner groove width = 2 × this)")]
        public float beltHalfWidth  = 0.5f;
        [Tooltip("How far the outer walls rise ABOVE the belt surface")]
        public float wallAboveBelt  = 0.12f;
        [Tooltip("How far the outer walls hang DOWN below the belt surface")]
        public float railHeight     = 1.0f;
        [Tooltip("Thickness of each outer wall")]
        public float railWidth      = 0.05f;
        [Tooltip("Chamfer size on the top-OUTER corner of each wall (clamped to railWidth)")]
        public float bevelSize      = 0.04f;

        // ── Sweep Parameters ──────────────────────────────────────────────────
        [Header("Sweep Quality")]
        [Range(60, 800)]
        [Tooltip("Number of cross-section rings along the spline")]
        public int resolution = 180;

        [Header("UV Tiling")]
        [Tooltip("How many times the texture tiles along the spline length")]
        public float vTiling = 8f;

        // ── Private ───────────────────────────────────────────────────────────
        private SplineContainer _spline;
        private MeshFilter      _meshFilter;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _spline     = GetComponent<SplineContainer>();
            _meshFilter = GetComponent<MeshFilter>();
        }

        private void Start() => BuildMesh();

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public void BuildMesh()
        {
            if (_spline     == null) _spline     = GetComponent<SplineContainer>();
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            _meshFilter.mesh = Sweep();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Profile definition
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the closed 2D polygon. Vertices are listed counter-clockwise
        /// when viewed from the sweep direction (+Z local), so outward normals
        /// are generated automatically with the (A, C, B) winding below.
        ///
        /// 12-point profile (Y=up, X=right):
        ///
        ///   P2────P3         P6────P7
        ///  ╱        ╲       ╱        ╲
        /// P1          P4───P5          P8
        /// |    belt groove (Y=0)        |
        /// P0                            P9
        /// |  P11──────────────────P10   |
        /// └──────────────────────────────┘
        ///
        /// Outer walls stand wallAboveBelt above belt surface (Y=0).
        /// Top-outer corner of each wall is chamfered by bevelSize.
        /// Belt surface sits at Y=0, recessed inside the walls.
        /// </summary>
        private Vector2[] BuildProfile()
        {
            float hw = beltHalfWidth;
            float rw = railWidth;
            float rh = railHeight;
            float wa = wallAboveBelt;
            float bv = Mathf.Clamp(bevelSize, 0f, Mathf.Min(rw, wa));

            // P0:  left  outer wall bottom
            // P1:  left  outer wall top, bevel start
            // P2:  left  bevel end (chamfered top-outer corner)
            // P3:  left  wall inner top (horizontal wall-top face right edge)
            // P4:  left  belt edge (inner step down to belt surface)
            // P5:  right belt edge
            // P6:  right wall inner top
            // P7:  right bevel end
            // P8:  right outer wall top, bevel start
            // P9:  right outer wall bottom
            // P10: underside right
            // P11: underside left
            return new Vector2[]
            {
                new Vector2(-hw - rw,        -rh),    // P0
                new Vector2(-hw - rw,         wa - bv), // P1
                new Vector2(-hw - rw + bv,    wa),    // P2
                new Vector2(-hw,              wa),    // P3
                new Vector2(-hw,              0f),    // P4
                new Vector2( hw,              0f),    // P5
                new Vector2( hw,              wa),    // P6
                new Vector2( hw + rw - bv,    wa),    // P7
                new Vector2( hw + rw,         wa - bv), // P8
                new Vector2( hw + rw,        -rh),    // P9
                new Vector2( hw,             -rh),    // P10
                new Vector2(-hw,             -rh),    // P11
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Core sweep algorithm
        // ─────────────────────────────────────────────────────────────────────

        private Mesh Sweep()
        {
            Vector2[] profile = BuildProfile();
            int pCount  = profile.Length;    // 8 vertices in closed polygon
            int sCount  = resolution + 1;    // rings along spline (last = first for closed)

            // ── Step 1: sample spline frames ──────────────────────────────────
            var wPos   = new Vector3[sCount];
            var wRight = new Vector3[sCount];
            var wUp    = new Vector3[sCount];
            SampleFrames(sCount, wPos, wRight, wUp);

            // ── Step 2: compute UV coordinates ───────────────────────────────
            // U  — normalized perimeter around the profile (0 at P0, 1 back at P0)
            float[] perimU  = ComputeProfilePerimU(profile);   // length = pCount + 1

            // V  — arc-length along spline, tiled by vTiling
            float[] splineV = ComputeSplineV(wPos, sCount);    // length = sCount

            // ── Step 3: allocate buffers ──────────────────────────────────────
            // Each of the pCount edges gets its own isolated vertex strip.
            // This produces hard normals at every profile vertex (chamfer bevels,
            // wall-to-floor transitions, etc.) — no unintended smoothing across corners.
            //
            // Per edge strip:  2 verts/ring × sCount rings  →  2·sCount verts
            // Total:           pCount × 2 × sCount  verts
            //                  pCount × resolution × 6  indices
            int totalVerts = pCount * 2 * sCount;
            int totalTris  = pCount * resolution * 6;

            var verts = new Vector3[totalVerts];
            var uvs   = new Vector2[totalVerts];
            var tris  = new int[totalTris];

            // ── Step 4: fill vertices + UVs, build triangles ──────────────────
            int ti = 0;

            for (int e = 0; e < pCount; e++)
            {
                Vector2 pa = profile[e];
                Vector2 pb = profile[(e + 1) % pCount];

                float uA = perimU[e];
                float uB = perimU[e + 1];   // perimU has pCount+1 entries; last = 1.0

                int stripBase = e * 2 * sCount;

                // Write vertices for every ring in this edge's strip
                for (int s = 0; s < sCount; s++)
                {
                    int vi = stripBase + s * 2;

                    verts[vi    ] = ToWorld(pa, s, wPos, wRight, wUp);
                    verts[vi + 1] = ToWorld(pb, s, wPos, wRight, wUp);

                    float v = splineV[s];
                    uvs[vi    ] = new Vector2(uA, v);
                    uvs[vi + 1] = new Vector2(uB, v);
                }

                // Build quads connecting ring[s] → ring[s+1]
                for (int s = 0; s < resolution; s++)
                {
                    int b  = stripBase + s * 2;
                    int v0 = b;         // ring s,   profile[e]
                    int v1 = b + 1;     // ring s,   profile[e+1]
                    int v2 = b + 2;     // ring s+1, profile[e]
                    int v3 = b + 3;     // ring s+1, profile[e+1]

                    // Winding: (A, C, B) + (B, C, D)
                    // For a CCW profile, this produces outward-facing normals.
                    tris[ti++] = v0; tris[ti++] = v2; tris[ti++] = v1;
                    tris[ti++] = v1; tris[ti++] = v2; tris[ti++] = v3;
                }
            }

            // ── Step 5: assemble mesh ─────────────────────────────────────────
            var mesh = new Mesh
            {
                name        = "ConveyorTrack_Swept",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            };
            mesh.vertices  = verts;
            mesh.uv        = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Projects a 2D profile point into the 3D spline frame at sample s.</summary>
        private static Vector3 ToWorld(Vector2 p, int s,
            Vector3[] wPos, Vector3[] wRight, Vector3[] wUp)
            => wPos[s] + wRight[s] * p.x + wUp[s] * p.y;

        /// <summary>Samples position + right + up for every ring along the spline.</summary>
        private void SampleFrames(int sCount, Vector3[] wPos, Vector3[] wRight, Vector3[] wUp)
        {
            for (int s = 0; s < sCount; s++)
            {
                // t=0 at ring 0, t=1 at ring[resolution] (wraps for closed splines)
                float t = (float)s / resolution;

                _spline.Spline.Evaluate(t, out var pos, out var tan, out var up);

                Vector3 fwd = transform.TransformDirection((Vector3)tan).normalized;
                Vector3 upW = transform.TransformDirection((Vector3)up);
                if (upW.sqrMagnitude < 0.001f) upW = transform.up;
                upW = upW.normalized;

                wPos[s]   = transform.TransformPoint(pos);
                wRight[s] = Vector3.Cross(upW, fwd).normalized;
                wUp[s]    = upW;
            }
        }

        /// <summary>
        /// Normalised perimeter U for each profile vertex + the closing edge.
        /// Returns array of length pCount + 1 where result[0]=0, result[pCount]=1.
        /// </summary>
        private static float[] ComputeProfilePerimU(Vector2[] profile)
        {
            int n   = profile.Length;
            var acc = new float[n + 1];
            acc[0]  = 0f;
            for (int i = 1; i < n; i++)
                acc[i] = acc[i - 1] + Vector2.Distance(profile[i], profile[i - 1]);
            acc[n] = acc[n - 1] + Vector2.Distance(profile[n - 1], profile[0]);

            float total = acc[n];
            if (total > 0f)
                for (int i = 0; i <= n; i++) acc[i] /= total;

            return acc;
        }

        /// <summary>
        /// Arc-length V coordinate for each ring, tiled by vTiling.
        /// Returns array of length sCount.
        /// </summary>
        private float[] ComputeSplineV(Vector3[] wPos, int sCount)
        {
            var   dist  = new float[sCount];
            float total = 0f;

            for (int s = 1; s < sCount; s++)
            {
                total   += Vector3.Distance(wPos[s], wPos[s - 1]);
                dist[s]  = total;
            }

            if (total > 0f)
                for (int s = 0; s < sCount; s++)
                    dist[s] = (dist[s] / total) * vTiling;

            return dist;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Editor
        // ─────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        [UnityEditor.CustomEditor(typeof(ConveyorTrackMeshBuilder))]
        public class Editor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();
                GUILayout.Space(8);
                if (GUILayout.Button("Rebuild Mesh", GUILayout.Height(36)))
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
