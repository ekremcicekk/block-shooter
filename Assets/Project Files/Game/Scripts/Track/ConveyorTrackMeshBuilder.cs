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
        /// Returns the OPEN 2D polygon (no bottom edge).
        /// 12 vertices: left wall bottom → up the wall → belt surface → down the wall → right wall bottom.
        /// The closing edge (P11 → P0) is intentionally omitted in Sweep() so the
        /// underside of the track has zero triangles — it's never visible from above.
        ///
        ///    P2────────P3              P9────────P10
        ///   ╱            ╲           ╱              ╲
        ///  P1              P4───────P8               P11  ← wall tops / belt at Y=0
        ///  |     (WALL)    P5───────P7    (WALL)     |
        ///  P0                 (BELT)                 P11
        ///  open ↕                                    open ↕   ← no floor triangles
        ///
        /// Submesh 0 = Wall  (all edges except P5→P6)
        /// Submesh 1 = Belt  (edge P5→P6, flat top where blocks travel)
        ///
        /// bevelSize clamped to min(railWidth/2, wallAboveBelt).
        /// </summary>
        private Vector2[] BuildProfile()
        {
            float hw = beltHalfWidth;
            float rw = railWidth;
            float rh = railHeight;
            float wa = wallAboveBelt;
            float bv = Mathf.Clamp(bevelSize, 0f, Mathf.Min(rw * 0.5f, wa));

            // P0:  left outer-bottom
            // P1:  left outer-top corner — vertical side
            // P2:  left outer-top corner — horizontal side
            // P3:  left inner-top corner — horizontal side
            // P4:  left inner-top corner — vertical side
            // P5:  belt left edge                         ← BELT edge starts here
            // P6:  belt right edge                        ← BELT edge ends here
            // P7:  right inner-top corner — vertical side
            // P8:  right inner-top corner — horizontal side
            // P9:  right outer-top corner — horizontal side
            // P10: right outer-top corner — vertical side
            // P11: right outer-bottom
            // (no P12/P13 underside — mesh is open at the bottom)
            return new Vector2[]
            {
                new Vector2(-hw - rw,          -rh),       // P0
                new Vector2(-hw - rw,           wa - bv),  // P1
                new Vector2(-hw - rw + bv,      wa),       // P2
                new Vector2(-hw - bv,           wa),       // P3
                new Vector2(-hw,                wa - bv),  // P4
                new Vector2(-hw,                0f),       // P5  ← belt left
                new Vector2( hw,                0f),       // P6  ← belt right
                new Vector2( hw,                wa - bv),  // P7
                new Vector2( hw + bv,           wa),       // P8
                new Vector2( hw + rw - bv,      wa),       // P9
                new Vector2( hw + rw,           wa - bv),  // P10
                new Vector2( hw + rw,          -rh),       // P11
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Core sweep algorithm
        // ─────────────────────────────────────────────────────────────────────

        private Mesh Sweep()
        {
            Vector2[] profile = BuildProfile();
            int pCount    = profile.Length;   // 12 vertices (open profile)
            int edgeCount = pCount - 1;       // 11 edges — skip closing edge → no bottom face
            int sCount    = resolution + 1;

            // ── Step 1: sample spline frames ──────────────────────────────────
            var wPos   = new Vector3[sCount];
            var wRight = new Vector3[sCount];
            var wUp    = new Vector3[sCount];
            SampleFrames(sCount, wPos, wRight, wUp);

            // ── Step 2: compute UV coordinates ───────────────────────────────
            float[] perimU  = ComputeProfilePerimU(profile);
            float[] splineV = ComputeSplineV(wPos, sCount);

            // ── Step 3: allocate buffers ──────────────────────────────────────
            // Per edge strip: 2 verts/ring × sCount rings
            // Two submeshes: submesh 0 = wall faces, submesh 1 = belt face (P5→P6)
            const int beltEdge     = 5;   // index of the P5→P6 belt edge in the profile
            int totalVerts         = edgeCount * 2 * sCount;
            int beltTriCount       = resolution * 6;
            int wallTriCount       = (edgeCount - 1) * resolution * 6;

            var verts    = new Vector3[totalVerts];
            var uvs      = new Vector2[totalVerts];
            var trisWall = new int[wallTriCount];
            var trisBelt = new int[beltTriCount];
            int tiWall = 0, tiBelt = 0;

            // ── Step 4: fill vertices + UVs, build triangles ──────────────────
            for (int e = 0; e < edgeCount; e++)
            {
                Vector2 pa = profile[e];
                Vector2 pb = profile[e + 1];   // no modulo — open profile

                float uA = perimU[e];
                float uB = perimU[e + 1];

                int stripBase = e * 2 * sCount;
                bool isBelt   = (e == beltEdge);

                for (int s = 0; s < sCount; s++)
                {
                    int vi = stripBase + s * 2;
                    verts[vi    ] = ToWorld(pa, s, wPos, wRight, wUp);
                    verts[vi + 1] = ToWorld(pb, s, wPos, wRight, wUp);
                    float v = splineV[s];
                    uvs[vi    ] = new Vector2(uA, v);
                    uvs[vi + 1] = new Vector2(uB, v);
                }

                for (int s = 0; s < resolution; s++)
                {
                    int b  = stripBase + s * 2;
                    // Winding (A,C,B)+(B,C,D) → outward normals for CCW profile
                    if (isBelt)
                    {
                        trisBelt[tiBelt++] = b;     trisBelt[tiBelt++] = b+2; trisBelt[tiBelt++] = b+1;
                        trisBelt[tiBelt++] = b+1;   trisBelt[tiBelt++] = b+2; trisBelt[tiBelt++] = b+3;
                    }
                    else
                    {
                        trisWall[tiWall++] = b;     trisWall[tiWall++] = b+2; trisWall[tiWall++] = b+1;
                        trisWall[tiWall++] = b+1;   trisWall[tiWall++] = b+2; trisWall[tiWall++] = b+3;
                    }
                }
            }

            // ── Step 5: assemble mesh ─────────────────────────────────────────
            var mesh = new Mesh
            {
                name        = "ConveyorTrack_Swept",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            };
            mesh.vertices     = verts;
            mesh.uv           = uvs;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trisWall, 0);   // Material slot 0 — walls
            mesh.SetTriangles(trisBelt, 1);   // Material slot 1 — belt surface
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
