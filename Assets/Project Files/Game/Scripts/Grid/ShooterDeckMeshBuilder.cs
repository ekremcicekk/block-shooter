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
        public float bevelSize     = 0.03f;

        private MeshFilter _mf;
        private void Awake() => _mf = GetComponent<MeshFilter>();

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

            // ── 4. Bevel chamfer strips (outer edges only) ───────────────────
            // Left  chamfer: along x=xL, slopes from (xL+B, yT) down to (xL, yM)
            AddBevelEdgeX(verts, uvs, trisWall, xL, zExtBack+B, zFront,   yT, B, false);
            // Right chamfer: along x=xR, slopes from (xR-B, yT) down to (xR, yM)
            AddBevelEdgeX(verts, uvs, trisWall, xR, zExtBack+B, zFront,   yT, B, true);
            // Back  chamfer: along z=zExtBack, slopes from (x, yT, zExtBack+B) down to (x, yM, zExtBack)
            AddBevelEdgeZ(verts, uvs, trisWall, xL+B, xR-B,     zExtBack, yT, B, false);

            // ── 5. Bevel corner caps (back-left and back-right) ──────────────
            // Each cap is a quad (2 tris) closing the gap between two meeting chamfer strips.
            //   A = top inner point
            //   S = bottom on the side (left/right) chamfer end
            //   C = actual wall corner (where left wall meets back wall at yM)  ← was missing before
            //   K = bottom on the back chamfer end
            AddBevelCornerCap(verts, uvs, trisWall, xL+B, yT, B, zExtBack, false); // back-left
            AddBevelCornerCap(verts, uvs, trisWall, xR-B, yT, B, zExtBack, true);  // back-right

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

        /// <summary>
        /// 45° chamfer strip along the top of an X-aligned outer wall (wall at fixed x, spanning z0..z1).
        /// normalRight=true  (right +X wall): top edge at (x-B, yT), bottom at (x, yM).
        /// normalRight=false (left  -X wall): top edge at (x+B, yT), bottom at (x, yM).
        /// </summary>
        static void AddBevelEdgeX(List<Vector3> v, List<Vector2> u, List<int> t,
            float x, float z0, float z1, float yT, float B, bool normalRight)
        {
            float xInner = normalRight ? x - B : x + B;
            float yM     = yT - B;
            if (normalRight)
                AddWall(v, u, t, x,yM,z0, xInner,yT,z0, xInner,yT,z1, x,yM,z1);
            else
                AddWall(v, u, t, x,yM,z1, xInner,yT,z1, xInner,yT,z0, x,yM,z0);
        }

        /// <summary>
        /// 45° chamfer strip along the top of a Z-aligned outer wall (wall at fixed z, spanning x0..x1).
        /// normalBack=false (-Z back wall): top edge at (x, yT, z+B), bottom at (x, yM, z).
        /// normalBack=true  (+Z front wall): top edge at (x, yT, z-B), bottom at (x, yM, z).
        /// </summary>
        static void AddBevelEdgeZ(List<Vector3> v, List<Vector2> u, List<int> t,
            float x0, float x1, float z, float yT, float B, bool normalBack)
        {
            float zInner = normalBack ? z - B : z + B;
            float yM     = yT - B;
            if (normalBack)
                AddWall(v, u, t, x1,yM,z, x1,yT,zInner, x0,yT,zInner, x0,yM,z);
            else
                AddWall(v, u, t, x0,yM,z, x0,yT,zInner, x1,yT,zInner, x1,yM,z);
        }

        /// <summary>
        /// Corner cap where two outer chamfer strips meet at a back corner.
        /// Fills the gap with a quad (2 triangles) so the mesh is watertight.
        ///
        /// Four vertices of the cap:
        ///   A = (xInner, yT, zBack+B)  — top inner shared point
        ///   S = (xOuter, yM, zBack+B)  — side chamfer lower end
        ///   C = (xOuter, yM, zBack)    — actual wall corner (where left/right wall meets back wall)
        ///   K = (xInner, yM, zBack)    — back chamfer lower end
        ///
        /// rightCorner=false → back-left  (xOuter = xInner-B, outward normal toward -X,-Z)
        /// rightCorner=true  → back-right (xOuter = xInner+B, outward normal toward +X,-Z)
        /// </summary>
        static void AddBevelCornerCap(List<Vector3> v, List<Vector2> u, List<int> t,
            float xInner, float yT, float B, float zBack, bool rightCorner)
        {
            float xOuter = rightCorner ? xInner + B : xInner - B;
            float yM     = yT - B;

            // Vertices: [0]=A, [1]=S, [2]=C, [3]=K
            int b = v.Count;
            v.Add(new Vector3(xInner, yT, zBack+B)); // A — top inner
            v.Add(new Vector3(xOuter, yM, zBack+B)); // S — side chamfer lower end
            v.Add(new Vector3(xOuter, yM, zBack));   // C — actual wall corner
            v.Add(new Vector3(xInner, yM, zBack));   // K — back chamfer lower end
            u.Add(new Vector2(0.5f,1f)); u.Add(new Vector2(0,0));
            u.Add(new Vector2(0,0));     u.Add(new Vector2(1,0));

            if (rightCorner)
            {
                // back-right: diagonal A→S→K, horizontal S→C→K (outward +X,-Z)
                t.Add(b); t.Add(b+1); t.Add(b+3); // A S K
                t.Add(b+1); t.Add(b+2); t.Add(b+3); // S C K
            }
            else
            {
                // back-left: diagonal A→K→S, horizontal S→K→C (outward -X,-Z)
                t.Add(b); t.Add(b+3); t.Add(b+1); // A K S
                t.Add(b+1); t.Add(b+3); t.Add(b+2); // S K C
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
