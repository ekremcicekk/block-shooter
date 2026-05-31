using System.Collections.Generic;
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
        [Range(20, 800)]
        [Tooltip("Number of cross-section rings along the spline")]
        public int resolution = 60;

        [Header("UV Tiling")]
        [Tooltip("How many times the texture tiles along the spline length")]
        public float vTiling = 8f;

        [Header("Open Zone (FireRange)")]
        [Tooltip("Skip wall triangles near the spline seam (T=0) so the track is open at the shooting area")]
        public bool  openZoneEnabled = true;
        [Tooltip("Fraction of spline (0–1) kept open on each side of T=0. Start at 0.01 and increase until gap looks right.")]
        [Range(0f, 0.25f)]
        public float openZoneHalfT   = 0.015f;

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
            int pCount    = profile.Length;          // 12 profile vertices
            int edgeCount = pCount;                  // 12 edges — includes closing P11→P0 (floor)
            int sCount    = resolution + 1;

            var wPos   = new Vector3[sCount];
            var wRight = new Vector3[sCount];
            var wUp    = new Vector3[sCount];
            SampleFrames(sCount, wPos, wRight, wUp);

            float[] perimU  = ComputeProfilePerimU(profile);
            float[] splineV = ComputeSplineV(wPos, sCount);

            const int beltEdge  = 5;
            int       floorEdge = pCount - 1;        // edge 11: P11→P0, closes the bottom
            int       sweepVCount = edgeCount * 2 * sCount;

            var verts    = new List<Vector3>(sweepVCount + 64);
            var uvs      = new List<Vector2>(sweepVCount + 64);
            var trisWall = new List<int>((edgeCount - 1) * resolution * 6 + 64);
            var trisBelt = new List<int>(resolution * 6);

            // ── Sweep vertices ────────────────────────────────────────────────
            for (int e = 0; e < edgeCount; e++)
            {
                Vector2 pa = profile[e];
                Vector2 pb = profile[(e + 1) % pCount]; // closing edge wraps to P0

                float uA = perimU[e], uB = perimU[(e + 1) % pCount];

                for (int s = 0; s < sCount; s++)
                {
                    verts.Add(ToWorld(pa, s, wPos, wRight, wUp));
                    verts.Add(ToWorld(pb, s, wPos, wRight, wUp));
                    float v = splineV[s];
                    uvs.Add(new Vector2(uA, v));
                    uvs.Add(new Vector2(uB, v));
                }
            }

            // ── Gap centre detection ──────────────────────────────────────────
            // T=0 on the spline is always placed at the FireRange by the level editor.
            // s=0 corresponds to T=0, so we centre the gap there directly.
            int gapCentre  = 0;
            int gapHalf    = Mathf.Max(1, Mathf.RoundToInt(openZoneHalfT * resolution));
            int s_capFirst = gapHalf % resolution;
            int s_capB     = (resolution - gapHalf) % resolution;

            // ── Triangles ─────────────────────────────────────────────────────
            for (int e = 0; e < edgeCount; e++)
            {
                int  stripBase = e * 2 * sCount;
                bool isBelt    = (e == beltEdge);
                bool isFloor   = (e == floorEdge);

                for (int s = 0; s < resolution; s++)
                {
                    int b = stripBase + s * 2;

                    if (isBelt)
                    {
                        trisBelt.Add(b);   trisBelt.Add(b+2); trisBelt.Add(b+1);
                        trisBelt.Add(b+1); trisBelt.Add(b+2); trisBelt.Add(b+3);
                    }
                    else if (isFloor)
                    {
                        trisWall.Add(b);   trisWall.Add(b+2); trisWall.Add(b+1);
                        trisWall.Add(b+1); trisWall.Add(b+2); trisWall.Add(b+3);
                    }
                    else
                    {
                        // e=6..9  : player-facing right wall (upper section) → skip inside gap.
                        // e=10    : right outer wall LOWER (P10→P11) → kept even in gap,
                        //           so the bottom edge of the opening has a visible face.
                        // e=0..4  : back left wall → always rendered, never cut.
                        if (openZoneEnabled && e >= 6 && e <= 9)
                        {
                            int dist = Mathf.Abs(s - gapCentre);
                            if (dist > resolution / 2) dist = resolution - dist;
                            if (dist < gapHalf) continue;
                        }

                        trisWall.Add(b);   trisWall.Add(b+2); trisWall.Add(b+1);
                        trisWall.Add(b+1); trisWall.Add(b+2); trisWall.Add(b+3);
                    }
                }
            }

            // ── End caps at gap edges ─────────────────────────────────────────
            if (openZoneEnabled)
            {
                AddWallCap(profile, wPos, wRight, wUp, verts, uvs, trisWall, s_capFirst);
                AddWallCap(profile, wPos, wRight, wUp, verts, uvs, trisWall, s_capB);
            }

            // ── Assemble mesh ─────────────────────────────────────────────────
            var mesh = new Mesh
            {
                name        = "ConveyorTrack_Swept",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trisWall, 0);
            mesh.SetTriangles(trisBelt, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Flat quad cap at sample s for the gap opening.
        // Inner top  = P6  (belt-right level, x=hw, y=0)     — "inner edge" side
        // Outer top  = P10 (x=hw+rw, y=wa-bv)               — snaps flush to e=10 start
        // Outer bot  = P11 (x=hw+rw, y=-rh)
        // Inner bot  = virtual (x=hw, y=-rh)
        // This cap stays at or just below belt level and closes from the inner edge.
        private static void AddWallCap(Vector2[] profile, Vector3[] wPos, Vector3[] wRight, Vector3[] wUp,
            List<Vector3> verts, List<Vector2> uvs, List<int> trisWall, int s)
        {
            var a = ToWorld(profile[6],                                       s, wPos, wRight, wUp); // P6  inner top
            var b = ToWorld(profile[10],                                      s, wPos, wRight, wUp); // P10 outer top
            var c = ToWorld(profile[11],                                      s, wPos, wRight, wUp); // P11 outer bot
            var d = ToWorld(new Vector2(profile[6].x, profile[11].y),        s, wPos, wRight, wUp); // virtual inner bot

            int bi = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(Vector2.zero); uvs.Add(Vector2.zero);
            uvs.Add(Vector2.zero); uvs.Add(Vector2.zero);

            trisWall.Add(bi);   trisWall.Add(bi+1); trisWall.Add(bi+2);
            trisWall.Add(bi);   trisWall.Add(bi+2); trisWall.Add(bi+3);
            trisWall.Add(bi);   trisWall.Add(bi+2); trisWall.Add(bi+1); // double-sided
            trisWall.Add(bi);   trisWall.Add(bi+3); trisWall.Add(bi+2);
        }

        // Fan-triangulated flat polygon at sample s, double-sided, for profile[pStart..pEnd].
        private static void AddCapPolygon(Vector2[] profile, Vector3[] wPos, Vector3[] wRight, Vector3[] wUp,
            List<Vector3> verts, List<Vector2> uvs, List<int> trisWall,
            int s, int pStart, int pEnd)
        {
            int count   = pEnd - pStart + 1;
            int baseIdx = verts.Count;

            for (int i = pStart; i <= pEnd; i++)
            {
                verts.Add(ToWorld(profile[i], s, wPos, wRight, wUp));
                uvs.Add(new Vector2(0.5f, 0.5f));
            }

            for (int i = 1; i < count - 1; i++)
            {
                int a = baseIdx, b = baseIdx + i, c = baseIdx + i + 1;
                trisWall.Add(a); trisWall.Add(b); trisWall.Add(c);
                trisWall.Add(a); trisWall.Add(c); trisWall.Add(b); // double-sided
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Projects a 2D profile point into the 3D spline frame at sample s.</summary>
        private static Vector3 ToWorld(Vector2 p, int s,
            Vector3[] wPos, Vector3[] wRight, Vector3[] wUp)
            => wPos[s] + wRight[s] * p.x + wUp[s] * p.y;

        /// <summary>
        /// Samples position + right + up for every ring along the spline.
        /// All values are in LOCAL space — mesh vertices are stored in local coordinates.
        /// Do NOT convert to world space here; that would double-apply the transform offset.
        /// </summary>
        private void SampleFrames(int sCount, Vector3[] wPos, Vector3[] wRight, Vector3[] wUp)
        {
            for (int s = 0; s < sCount; s++)
            {
                float t = (float)s / resolution;
                _spline.Spline.Evaluate(t, out var pos, out var tan, out var up);

                // Spline.Evaluate returns local-space values — use them directly for mesh verts
                Vector3 fwd = ((Vector3)tan).normalized;
                Vector3 upL = ((Vector3)up).normalized;
                if (upL.sqrMagnitude < 0.001f) upL = Vector3.up;

                wPos[s]   = (Vector3)pos;
                wRight[s] = Vector3.Cross(upL, fwd).normalized;
                wUp[s]    = upL;
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
