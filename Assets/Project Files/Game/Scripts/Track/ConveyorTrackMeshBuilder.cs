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
            int edgeCount = pCount - 1;              // 11 edges — excludes closing underside floor (P11→P0)
            int sCount    = resolution + 1;

            var wPos   = new Vector3[sCount];
            var wRight = new Vector3[sCount];
            var wUp    = new Vector3[sCount];
            SampleFrames(sCount, wPos, wRight, wUp);

            float[] perimU  = ComputeProfilePerimU(profile);
            float[] splineV = ComputeSplineV(wPos, sCount);

            const int beltEdge  = 5;
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

            // ── Gap zone ──────────────────────────────────────────────────────
            // T=0 (s=0) = FireRange position. Removes gapHalf triangles on each side → symmetric.
            int gapHalf    = Mathf.Max(1, Mathf.RoundToInt(openZoneHalfT * resolution));
            int s_capFirst = gapHalf;
            int s_capB     = resolution - gapHalf;

            // Find LevelRoot
            var root = GetComponentInParent<LevelRoot>();
            if (root == null) root = FindObjectOfType<LevelRoot>();

            bool isMainTrack = GetComponent<ConveyorController>() != null;

            // ── Triangles ─────────────────────────────────────────────────────
            for (int e = 0; e < edgeCount; e++)
            {
                int  stripBase = e * 2 * sCount;
                bool isBelt    = (e == beltEdge);

                for (int s = 0; s < resolution; s++)
                {
                    int b = stripBase + s * 2;

                    // If it's a branch track, skip the end part that intersects with the main conveyor
                    if (!isMainTrack)
                    {
                        float distToEnd = Vector3.Distance(wPos[s], wPos[resolution]);
                        if (distToEnd < (beltHalfWidth + railWidth + 0.05f))
                        {
                            continue;
                        }
                    }

                    if (isBelt)
                    {
                        trisBelt.Add(b);   trisBelt.Add(b+2); trisBelt.Add(b+1);
                        trisBelt.Add(b+1); trisBelt.Add(b+2); trisBelt.Add(b+3);
                    }
                    else
                    {
                        if (openZoneEnabled && e >= 6 && e <= 10)
                        {
                            if (s < gapHalf || s >= resolution - gapHalf) continue;
                        }

                        // Branch path connection gaps
                        if (isMainTrack)
                        {
                            float tSample = (float)s / resolution;
                            bool skipLeft, skipRight;
                            if (IsInBranchGap(tSample, root, out skipLeft, out skipRight))
                            {
                                if (skipLeft && e >= 0 && e <= 4) continue;
                                if (skipRight && e >= 6 && e <= 10) continue;
                            }
                        }

                        trisWall.Add(b);   trisWall.Add(b+2); trisWall.Add(b+1);
                        trisWall.Add(b+1); trisWall.Add(b+2); trisWall.Add(b+3);
                    }
                }
            }

            // ── Gap outer face strip ──────────────────────────────────────────
            // The entire right wall (e=6..10) is removed in the gap zone.
            // Add a dedicated strip at x=outerX, from y=beltY to y=floorY,
            // facing the player. This replaces e=10 but starts exactly at belt level.
            if (openZoneEnabled)
            {
                float innerX = profile[6].x;    // hw  — inner face of right wall (P6)
                float beltY  = profile[6].y;    // belt level
                float floorY = profile[11].y;   // bottom level

                int gapStripBase = verts.Count;
                for (int s = 0; s <= resolution; s++)
                {
                    verts.Add(ToWorld(new Vector2(innerX, beltY),  s, wPos, wRight, wUp));
                    verts.Add(ToWorld(new Vector2(innerX, floorY), s, wPos, wRight, wUp));
                    uvs.Add(Vector2.zero); uvs.Add(Vector2.zero);
                }
                for (int s = 0; s < resolution; s++)
                {
                    if (s >= gapHalf && s < resolution - gapHalf) continue;

                    int b = gapStripBase + s * 2;
                    trisWall.Add(b);   trisWall.Add(b+2); trisWall.Add(b+1);
                    trisWall.Add(b+1); trisWall.Add(b+2); trisWall.Add(b+3);
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

        // End-cap at sample s covering the full right-wall cross-section:
        // P6 (inner base) → P7 → P8 → P9 → P10 → P11 (outer base) → virtual inner bottom → back.
        // Fan-triangulated from P6, double-sided.
        private static void AddWallCap(Vector2[] profile, Vector3[] wPos, Vector3[] wRight, Vector3[] wUp,
            List<Vector3> verts, List<Vector2> uvs, List<int> trisWall, int s)
        {
            var poly = new Vector2[]
            {
                profile[6],                                      // P6  inner belt-level
                profile[7],                                      // P7  inner wall top
                profile[8],                                      // P8  chamfer
                profile[9],                                      // P9  chamfer
                profile[10],                                     // P10 outer wall top
                profile[11],                                     // P11 outer bottom
                new Vector2(profile[6].x, profile[11].y),       // virtual inner bottom
            };

            int baseIdx = verts.Count;
            foreach (var p in poly)
            {
                verts.Add(ToWorld(p, s, wPos, wRight, wUp));
                uvs.Add(Vector2.zero);
            }

            for (int i = 1; i < poly.Length - 1; i++)
            {
                int a = baseIdx, b = baseIdx + i, c = baseIdx + i + 1;
                trisWall.Add(a); trisWall.Add(b); trisWall.Add(c);
                trisWall.Add(a); trisWall.Add(c); trisWall.Add(b); // double-sided
            }
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

        private bool IsInBranchGap(float t, LevelRoot root, out bool skipLeft, out bool skipRight)
        {
            skipLeft = false;
            skipRight = false;
            if (root == null || root.branches == null) return false;

            float splineLength = SplineUtility.CalculateLength(_spline.Spline, transform.localToWorldMatrix);
            float branchHalfWidthWorld = beltHalfWidth + railWidth;
            float gapHalfT = splineLength > 0f ? (branchHalfWidthWorld / splineLength) : 0.025f;

            foreach (var branch in root.branches)
            {
                float center = branch.mergeT;
                float min = center - gapHalfT;
                float max = center + gapHalfT;

                bool inGap = false;
                if (min <= max)
                {
                    inGap = (t >= min && t <= max);
                }
                else
                {
                    inGap = (t >= min || t <= max);
                }

                if (inGap)
                {
                    bool fromLeft = true;
                    if (branch.splineKnots != null && branch.splineKnots.Count >= 2)
                    {
                        _spline.Spline.Evaluate(center, out _, out var mainTanLocal, out var mainUpLocal);
                        // Transform from spline-local space to world space
                        Vector3 mainTan = transform.TransformDirection((Vector3)mainTanLocal).normalized;
                        Vector3 mainUp = transform.TransformDirection((Vector3)mainUpLocal).normalized;
                        if (mainUp == Vector3.zero) mainUp = Vector3.up;
                        Vector3 mainRight = Vector3.Cross(mainUp, mainTan).normalized;

                        // Branch knots are in world space
                        Vector3 branchLast = branch.splineKnots[branch.splineKnots.Count - 1];
                        Vector3 branchSecondLast = branch.splineKnots[branch.splineKnots.Count - 2];
                        Vector3 toBranch = (branchSecondLast - branchLast).normalized;
                        float dot = Vector3.Dot(toBranch, mainRight);
                        fromLeft = (dot < 0f);
                    }
                    else
                    {
                        fromLeft = branch.connectFromLeft;
                    }

                    // Only skip the OUTER wall — the wall that faces the approaching branch
                    if (fromLeft) skipLeft = true;
                    else skipRight = true;
                }
            }

            return skipLeft || skipRight;
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
