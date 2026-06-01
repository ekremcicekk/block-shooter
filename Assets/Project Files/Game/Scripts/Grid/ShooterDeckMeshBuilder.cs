using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Generates a merged deck mesh for the shooter grid.
    ///
    /// Orientation (matches scene layout):
    ///   +Z = toward conveyor  = FRONT  → always open, no wall, no extension.
    ///   -Z = away from conveyor = BACK  → platform extends here by backDepth.
    ///   r=0          = back row  (most negative Z inside grid)
    ///   r=gridRows-1 = front row (most positive Z inside grid, closest to conveyor)
    ///
    /// isEmpty[col, row] = true  → empty cell → raised tile.
    /// isEmpty[col, row] = false → filled cell (shooter block) → no tile.
    ///
    /// Wings:
    ///   Left  wing : extends sideWingWidth to the -X side for the full grid depth.
    ///   Right wing : extends sideWingWidth to the +X side for the full grid depth.
    ///   Back  wing : full width, extends backDepth in the -Z direction.
    ///
    /// Bevel (bevelSize B):
    ///   Three outer edges (left/right/back) get a 45° chamfer strip.
    ///   Two back corners (back-left, back-right) each get a quad cap (2 triangles)
    ///   that closes the gap between the two meeting chamfer strips.
    ///
    /// Submesh 0 = top surface  (deckTopMaterial)
    /// Submesh 1 = side walls + chamfer  (deckWallMaterial)
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ShooterDeckMeshBuilder : MonoBehaviour
    {
        public int   gridCols      = 4;
        public int   gridRows      = 2;
        public float cellSize      = 1.2f;
        [Tooltip("Platform extension to the left and right of the grid")]
        public float sideWingWidth = 2f;
        [Tooltip("Platform extension behind the grid (away from conveyor, -Z direction)")]
        public float backDepth     = 2f;
        [Tooltip("Height the walls drop below Y=0")]
        public float tileHeight    = 0.15f;
        [Tooltip("45-degree chamfer size on outer platform edges (left/right/back). 0 = sharp corners.")]
        public float bevelSize     = 0.05f;
        [Tooltip("Number of edge loops in the bevel. 1 = flat chamfer, >1 = rounded arc.")]
        public int   bevelSegments = 4;

        private MeshFilter _mf;

        // Outer and inset contours in XZ (local space). Populated by BuildMesh, used by Gizmos.
        private Vector2[] _outerContour;
        private Vector2[] _insetContour;

        private void Awake() => _mf = GetComponent<MeshFilter>();

        // Returns the 4-point outer boundary polygon in XZ (clockwise from front-left).
        Vector2[] ExtractOuterContour(float xL, float xR, float zExtBack, float zFront)
        {
            return new Vector2[]
            {
                new Vector2(xL, zFront),    // 0: front-left  (open side)
                new Vector2(xL, zExtBack),  // 1: back-left
                new Vector2(xR, zExtBack),  // 2: back-right
                new Vector2(xR, zFront),    // 3: front-right (open side)
            };
        }

        // Returns a single inset contour offset inward by B.
        // Left/right edges move inward in X; back edge moves inward in Z.
        // Front edge is open — its Z is not inset.
        Vector2[] InsetContour(Vector2[] outer, float B)
        {
            // outer[0]=front-left, [1]=back-left, [2]=back-right, [3]=front-right
            return new Vector2[]
            {
                new Vector2(outer[0].x + B, outer[0].y),        // front-left  inset: x+B, z unchanged
                new Vector2(outer[1].x + B, outer[1].y + B),    // back-left   inset: x+B, z+B
                new Vector2(outer[2].x - B, outer[2].y + B),    // back-right  inset: x-B, z+B
                new Vector2(outer[3].x - B, outer[3].y),        // front-right inset: x-B, z unchanged
            };
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            float y = transform.position.y + 0.02f;
            DrawContourGizmo(_outerContour, y, Color.green,  Color.cyan,    0.06f);
            DrawContourGizmo(_insetContour, y, Color.magenta, Color.yellow, 0.04f);

            // Connect matching vertices between outer and inset with white lines
            if (_outerContour != null && _insetContour != null &&
                _outerContour.Length == _insetContour.Length)
            {
                UnityEngine.Gizmos.color = new Color(1,1,1,0.4f);
                for (int i = 0; i < _outerContour.Length; i++)
                {
                    Vector3 o = transform.TransformPoint(new Vector3(_outerContour[i].x, y, _outerContour[i].y));
                    Vector3 n = transform.TransformPoint(new Vector3(_insetContour[i].x, y, _insetContour[i].y));
                    UnityEngine.Gizmos.DrawLine(o, n);
                }
            }
        }

        void DrawContourGizmo(Vector2[] contour, float y, Color edgeColor, Color vertexColor, float sphereRadius)
        {
            if (contour == null || contour.Length < 2) return;
            for (int i = 0; i < contour.Length; i++)
            {
                Vector2 a = contour[i];
                Vector2 b = contour[(i + 1) % contour.Length];
                Vector3 pA = transform.TransformPoint(new Vector3(a.x, y, a.y));
                Vector3 pB = transform.TransformPoint(new Vector3(b.x, y, b.y));

                bool isFrontEdge = (i == contour.Length - 1);
                UnityEngine.Gizmos.color = isFrontEdge ? Color.yellow : edgeColor;
                UnityEngine.Gizmos.DrawLine(pA, pB);

                UnityEngine.Gizmos.color = vertexColor;
                UnityEngine.Gizmos.DrawSphere(pA, sphereRadius);
            }
        }
#endif

        public void BuildMesh(bool[,] isEmpty)
        {
            if (_mf == null) _mf = GetComponent<MeshFilter>();

            float gHW = gridCols * cellSize * 0.5f;
            float gHD = gridRows * cellSize * 0.5f;
            float W   = sideWingWidth;
            float D   = backDepth;
            float yT  = 0f;
            float yB  = -tileHeight;
            // Clamp bevel so it never exceeds wall height or wing dimensions.
            float B   = Mathf.Clamp(bevelSize, 0f, Mathf.Min(tileHeight, Mathf.Min(W, D)) * 0.9f);
            float yM  = yT - B; // top of vertical outer walls / bottom of chamfer strips

            var cx = new float[gridCols + 1];
            var cz = new float[gridRows + 1];
            for (int i = 0; i <= gridCols; i++) cx[i] = -gHW + i * cellSize;
            for (int j = 0; j <= gridRows; j++) cz[j] = -gHD + j * cellSize;

            float zExtBack = cz[0] - D;      // back outer edge (away from conveyor, -Z)
            float zFront   = cz[gridRows];   // front face (toward conveyor +Z, OPEN)
            float xL       = cx[0]         - W;
            float xR       = cx[gridCols]  + W;

            // Extract outer + inset contours and log both
            _outerContour = ExtractOuterContour(xL, xR, zExtBack, zFront);
            _insetContour = InsetContour(_outerContour, B);

            var sb = new System.Text.StringBuilder("ShooterDeck contours:\n");
            sb.AppendLine("  Outer:");
            for (int i = 0; i < _outerContour.Length; i++)
                sb.AppendLine($"    [{i}] ({_outerContour[i].x:F3}, {_outerContour[i].y:F3})");
            sb.AppendLine($"  Inset (B={B:F3}):");
            for (int i = 0; i < _insetContour.Length; i++)
                sb.AppendLine($"    [{i}] ({_insetContour[i].x:F3}, {_insetContour[i].y:F3})");
            Debug.Log(sb.ToString());

            bool E(int c, int r) =>
                c >= 0 && c < gridCols && r >= 0 && r < gridRows && isEmpty[c, r];

            var verts    = new List<Vector3>();
            var uvs      = new List<Vector2>();
            var trisTop  = new List<int>();
            var trisWall = new List<int>();

            // ── 1. Empty cell tiles + inner grid walls ───────────────────────
            for (int r = 0; r < gridRows; r++)
            for (int c = 0; c < gridCols; c++)
            {
                if (!E(c, r)) continue;

                AddTop(verts, uvs, trisTop, cx[c], cx[c+1], cz[r], cz[r+1], yT);

                // Left wall: skip c=0 (connects to left wing)
                if (c > 0 && !E(c-1, r))
                    AddWallX(verts, uvs, trisWall, cx[c], cz[r], cz[r+1], yT, yB, false);
                // Right wall: skip c=gridCols-1 (connects to right wing)
                if (c < gridCols - 1 && !E(c+1, r))
                    AddWallX(verts, uvs, trisWall, cx[c+1], cz[r], cz[r+1], yT, yB, true);
                // Back wall (-Z): skip r=0 (connects to back wing)
                if (r > 0 && !E(c, r-1))
                    AddWallZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[r], yT, yB, false);
                // Front wall (+Z): skip r=gridRows-1 (front is always open)
                if (r < gridRows - 1 && !E(c, r+1))
                    AddWallZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[r+1], yT, yB, true);
            }

            // ── 2. Wing top surfaces (shrunk by B on outer beveled edges) ────
            // Left wing: x from (xL+B) to cx[0], z from cz[0] to zFront
            AddTop(verts, uvs, trisTop, xL+B, cx[0],        cz[0], zFront,    yT);
            // Right wing: x from cx[gridCols] to (xR-B), z from cz[0] to zFront
            AddTop(verts, uvs, trisTop, cx[gridCols], xR-B, cz[0], zFront,    yT);
            // Back wing: full beveled width and depth
            AddTop(verts, uvs, trisTop, xL+B, xR-B,         zExtBack+B, cz[0], yT);

            // ── 3. Outer walls (top starts at yM = yT-B due to bevel) ────────
            AddWallX(verts, uvs, trisWall, xL, zExtBack, zFront,   yM, yB, false); // Left  outer (-X)
            AddWallX(verts, uvs, trisWall, xR, zExtBack, zFront,   yM, yB, true);  // Right outer (+X)
            AddWallZ(verts, uvs, trisWall, xL, xR,       zExtBack, yM, yB, false); // Back  outer (-Z)

            // ── 4. Bevel faces — arc rings from outer contour (at yM) to inset contour (at yT) ──
            int segs = Mathf.Max(1, bevelSegments);
            BuildBevelFaces(verts, uvs, trisWall, _outerContour, _insetContour, yT, yM, segs);

            // ── 6. Inner wing boundary walls ─────────────────────────────────
            // Inner LEFT (x=cx[0]): filled c=0 cells face the left wing; normal +X
            for (int r = 0; r < gridRows; r++)
            {
                if (E(0, r)) continue;
                AddWallX(verts, uvs, trisWall, cx[0], cz[r], cz[r+1], yT, yB, true);
            }
            // Inner RIGHT (x=cx[gridCols]): filled c=gridCols-1 cells; normal -X
            for (int r = 0; r < gridRows; r++)
            {
                if (E(gridCols - 1, r)) continue;
                AddWallX(verts, uvs, trisWall, cx[gridCols], cz[r], cz[r+1], yT, yB, false);
            }
            // Inner BACK (z=cz[0]): filled r=0 cells face the back wing; normal -Z
            for (int c = 0; c < gridCols; c++)
            {
                if (E(c, 0)) continue;
                AddWallZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[0], yT, yB, false);
            }

            // ── Build mesh ───────────────────────────────────────────────────
            var mesh = new Mesh { name = "ShooterDeck" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trisTop,  0);
            mesh.SetTriangles(trisWall, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _mf.sharedMesh = mesh;
        }

        // ── Geometry helpers ─────────────────────────────────────────────────

        static void AddTop(List<Vector3> v, List<Vector2> u, List<int> t,
            float x0, float x1, float z0, float z1, float y)
        {
            int b = v.Count;
            v.Add(new Vector3(x0,y,z0)); v.Add(new Vector3(x1,y,z0));
            v.Add(new Vector3(x1,y,z1)); v.Add(new Vector3(x0,y,z1));
            u.Add(new Vector2(x0,z0)); u.Add(new Vector2(x1,z0));
            u.Add(new Vector2(x1,z1)); u.Add(new Vector2(x0,z1));
            t.Add(b); t.Add(b+2); t.Add(b+1);
            t.Add(b); t.Add(b+3); t.Add(b+2);
        }

        // Wall at fixed X. normalRight=true → +X normal; false → -X normal.
        static void AddWallX(List<Vector3> v, List<Vector2> u, List<int> t,
            float x, float z0, float z1, float yT, float yB, bool normalRight)
        {
            if (normalRight)
                AddWall(v, u, t, x,yB,z0, x,yT,z0, x,yT,z1, x,yB,z1);
            else
                AddWall(v, u, t, x,yB,z1, x,yT,z1, x,yT,z0, x,yB,z0);
        }

        // Wall at fixed Z. normalBack=true → +Z normal; false → -Z normal.
        static void AddWallZ(List<Vector3> v, List<Vector2> u, List<int> t,
            float x0, float x1, float z, float yT, float yB, bool normalBack)
        {
            if (normalBack)
                AddWall(v, u, t, x1,yB,z, x1,yT,z, x0,yT,z, x0,yB,z);
            else
                AddWall(v, u, t, x0,yB,z, x0,yT,z, x1,yT,z, x1,yB,z);
        }

        // Builds bevel faces using arc-interpolated rings between outer (at yM) and inset (at yT).
        // segments=1 → single flat chamfer quad per edge.
        // segments>1 → multiple quads following a quarter-circle arc per edge.
        // No separate corner caps needed: adjacent edge quads share corner vertices automatically.
        static void BuildBevelFaces(List<Vector3> v, List<Vector2> u, List<int> t,
            Vector2[] outer, Vector2[] inset, float yT, float yM, int segments)
        {
            int n = outer.Length;
            float B = yT - yM; // bevel height (positive)

            // Build rings[k][i]: XZ position of vertex i at ring k.
            // Ring 0 = outer at yM, ring segments = inset at yT.
            // XZ follows cos arc, Y follows sin arc (quarter-circle).
            var ringXZ = new Vector2[segments + 1][];
            var ringY  = new float  [segments + 1];

            for (int k = 0; k <= segments; k++)
            {
                float theta = (k / (float)segments) * Mathf.PI * 0.5f;
                ringY[k]  = yM + B * Mathf.Sin(theta);
                float cosT = Mathf.Cos(theta);

                ringXZ[k] = new Vector2[n];
                for (int i = 0; i < n; i++)
                {
                    Vector2 diff = outer[i] - inset[i];
                    float   dist = diff.magnitude;
                    Vector2 dir  = dist > 0.0001f ? diff / dist : Vector2.zero;
                    ringXZ[k][i] = inset[i] + dir * dist * cosT;
                }
            }

            // Emit quads between consecutive rings for each non-front edge.
            for (int i = 0; i < n; i++)
            {
                int  next        = (i + 1) % n;
                bool isFrontEdge = (i == n - 1);
                if (isFrontEdge) continue;

                for (int k = 0; k < segments; k++)
                {
                    var oA = new Vector3(ringXZ[k]  [i].x,    ringY[k],   ringXZ[k]  [i].y);
                    var oB = new Vector3(ringXZ[k]  [next].x, ringY[k],   ringXZ[k]  [next].y);
                    var iA = new Vector3(ringXZ[k+1][i].x,    ringY[k+1], ringXZ[k+1][i].y);
                    var iB = new Vector3(ringXZ[k+1][next].x, ringY[k+1], ringXZ[k+1][next].y);

                    int b = v.Count;
                    v.Add(oA); v.Add(iA); v.Add(iB); v.Add(oB);
                    u.Add(new Vector2(0, (float)k/segments));
                    u.Add(new Vector2(0, (float)(k+1)/segments));
                    u.Add(new Vector2(1, (float)(k+1)/segments));
                    u.Add(new Vector2(1, (float)k/segments));
                    t.Add(b); t.Add(b+1); t.Add(b+2);
                    t.Add(b); t.Add(b+2); t.Add(b+3);
                }
            }
        }

        // Raw quad: BL, TL, TR, BR (outward-facing CCW winding).
        static void AddWall(List<Vector3> v, List<Vector2> u, List<int> t,
            float blx,float bly,float blz,
            float tlx,float tly,float tlz,
            float trx,float try_,float trz,
            float brx,float bry,float brz)
        {
            int b = v.Count;
            v.Add(new Vector3(blx,bly,blz)); v.Add(new Vector3(tlx,tly,tlz));
            v.Add(new Vector3(trx,try_,trz)); v.Add(new Vector3(brx,bry,brz));
            u.Add(new Vector2(0,0)); u.Add(new Vector2(0,1));
            u.Add(new Vector2(1,1)); u.Add(new Vector2(1,0));
            t.Add(b); t.Add(b+1); t.Add(b+2);
            t.Add(b); t.Add(b+2); t.Add(b+3);
        }

#if UNITY_EDITOR
        [UnityEditor.CustomEditor(typeof(ShooterDeckMeshBuilder))]
        public class Editor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();
                GUILayout.Space(8);
                if (GUILayout.Button("Rebuild (all empty)", GUILayout.Height(32)))
                {
                    var b = (ShooterDeckMeshBuilder)target;
                    b._mf = b.GetComponent<MeshFilter>();
                    var empty = new bool[b.gridCols, b.gridRows];
                    for (int c = 0; c < b.gridCols; c++)
                    for (int r = 0; r < b.gridRows; r++)
                        empty[c, r] = true;
                    b.BuildMesh(empty);
                    UnityEditor.EditorUtility.SetDirty(b.gameObject);
                }
            }
        }
#endif
    }
}
