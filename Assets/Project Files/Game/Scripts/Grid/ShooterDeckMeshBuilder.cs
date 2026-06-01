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
    /// Empty cell at col=0          → connects to left  wing (no wall at x=cx[0]).
    /// Empty cell at col=gridCols-1 → connects to right wing (no wall at x=cx[gridCols]).
    /// Empty cell at row=0          → connects to back  wing (no wall at z=cz[0]).
    /// Front row (row=gridRows-1)   → always open, no wall at z=cz[gridRows].
    ///
    /// Bevel (bevelSize B):
    ///   All three outer edges (left, right, back) get a 45° chamfer strip
    ///   between the flat top surface and the vertical outer wall.
    ///   Back-left and back-right corners get a diagonal triangle cap.
    ///
    /// Submesh 0 = top surface  (deckTopMaterial)
    /// Submesh 1 = side walls   (deckWallMaterial)
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
        [Tooltip("Inset from cell boundary adjacent to a filled cell (gap around shooter blocks)")]
        public float cellPadding   = 0.05f;
        [Tooltip("Chamfer size on outer platform edges (left/right/back). 0 = sharp corners.")]
        public float bevelSize     = 0.03f;

        private MeshFilter _mf;
        private void Awake() => _mf = GetComponent<MeshFilter>();

        /// <summary>
        /// Build and assign the mesh.
        /// isEmpty[col, row] = true  → empty cell, platform tile generated.
        /// isEmpty[col, row] = false → filled cell, no tile.
        /// </summary>
        public void BuildMesh(bool[,] isEmpty)
        {
            if (_mf == null) _mf = GetComponent<MeshFilter>();

            float gHW = gridCols * cellSize * 0.5f;
            float gHD = gridRows * cellSize * 0.5f;
            float W   = sideWingWidth;
            float D   = backDepth;
            float yT  = 0f;
            float yB  = -tileHeight;
            float P   = cellPadding;
            float B   = Mathf.Clamp(bevelSize, 0f, Mathf.Min(tileHeight, Mathf.Min(W, D)) * 0.9f);
            float yM  = yT - B; // top of vertical outer walls / bottom of chamfer strips

            // Grid corner positions
            var cx = new float[gridCols + 1];
            var cz = new float[gridRows + 1];
            for (int i = 0; i <= gridCols; i++) cx[i] = -gHW + i * cellSize;
            for (int j = 0; j <= gridRows; j++) cz[j] = -gHD + j * cellSize;

            // Key Z/X positions
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

                // Padded tile bounds: inset only on edges adjacent to filled cells.
                // Wing-connected boundary edges are NOT inset — they merge seamlessly.
                float tx0 = cx[c];
                float tx1 = cx[c+1];
                float tz0 = cz[r];
                float tz1 = cz[r+1];

                bool filledL = c > 0            && !E(c-1, r);
                bool filledR = c < gridCols - 1 && !E(c+1, r);
                bool filledB = r > 0            && !E(c, r-1);
                bool filledF = r < gridRows - 1 && !E(c, r+1);

                if (filledL) tx0 += P;
                if (filledR) tx1 -= P;
                if (filledB) tz0 += P;
                if (filledF) tz1 -= P;

                AddTop(verts, uvs, trisTop, tx0, tx1, tz0, tz1, yT);

                // Left wall: skip c=0 (connects to left wing)
                if (filledL)
                    AddWallX(verts, uvs, trisWall, cx[c], cz[r], cz[r+1], yT, yB, false);
                // Right wall: skip c=gridCols-1 (connects to right wing)
                if (filledR)
                    AddWallX(verts, uvs, trisWall, cx[c+1], cz[r], cz[r+1], yT, yB, true);
                // Back wall (-Z): skip r=0 (connects to back wing)
                if (filledB)
                    AddWallZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[r], yT, yB, false);
                // Front wall (+Z): skip r=gridRows-1 (front is always open)
                if (filledF)
                    AddWallZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[r+1], yT, yB, true);
            }

            // ── 2. Wing top surfaces (shrunk by B on outer beveled edges) ────
            // Left wing: x from (xL+B)..cx[0], z from cz[0]..zFront
            AddTop(verts, uvs, trisTop, xL+B, cx[0],        cz[0], zFront, yT);
            // Right wing: x from cx[gridCols]..(xR-B), z from cz[0]..zFront
            AddTop(verts, uvs, trisTop, cx[gridCols], xR-B, cz[0], zFront, yT);
            // Back wing: full width between bevels, depth between bevel and grid
            AddTop(verts, uvs, trisTop, xL+B, xR-B, zExtBack+B, cz[0], yT);

            // ── 3. Outer walls (top starts at yM = yT-B due to bevel) ────────
            AddWallX(verts, uvs, trisWall, xL, zExtBack, zFront, yM, yB, false); // Left  outer (-X)
            AddWallX(verts, uvs, trisWall, xR, zExtBack, zFront, yM, yB, true);  // Right outer (+X)
            AddWallZ(verts, uvs, trisWall, xL, xR, zExtBack,     yM, yB, false); // Back  outer (-Z)
            // Front face: intentionally omitted — always open.

            // ── 4. Bevel chamfer strips (outer edges) ────────────────────────
            // Left chamfer: along x=xL, from (xL+B, yT) sloping to (xL, yM)
            AddBevelEdgeX(verts, uvs, trisWall, xL, zExtBack+B, zFront, yT, B, false);
            // Right chamfer: along x=xR, from (xR-B, yT) sloping to (xR, yM)
            AddBevelEdgeX(verts, uvs, trisWall, xR, zExtBack+B, zFront, yT, B, true);
            // Back chamfer: along z=zExtBack, from (x, yT, zExtBack+B) sloping to (x, yM, zExtBack)
            AddBevelEdgeZ(verts, uvs, trisWall, xL+B, xR-B, zExtBack, yT, B, false);

            // ── 5. Bevel corner triangles (back-left and back-right) ─────────
            AddBevelCornerTri(verts, uvs, trisWall, xL+B, yT, B, zExtBack, false);
            AddBevelCornerTri(verts, uvs, trisWall, xR-B, yT, B, zExtBack, true);

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
            // Inner BACK (z=cz[0]): filled r=0 cells face back wing; normal -Z
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

        // Bevel chamfer strip along an X-aligned outer wall (wall at fixed x, spanning z0..z1).
        // normalRight=true  → right-facing wall (+X normal): chamfer slopes from (x-B, yT) to (x, yM).
        // normalRight=false → left-facing wall  (-X normal): chamfer slopes from (x+B, yT) to (x, yM).
        static void AddBevelEdgeX(List<Vector3> v, List<Vector2> u, List<int> t,
            float x, float z0, float z1, float yT, float B, bool normalRight)
        {
            float xInner = normalRight ? x - B : x + B;
            float yMid   = yT - B;
            if (normalRight)
                AddWall(v, u, t, x,yMid,z0, xInner,yT,z0, xInner,yT,z1, x,yMid,z1);
            else
                AddWall(v, u, t, x,yMid,z1, xInner,yT,z1, xInner,yT,z0, x,yMid,z0);
        }

        // Bevel chamfer strip along a Z-aligned outer wall (wall at fixed z, spanning x0..x1).
        // normalBack=false → back-facing wall (-Z normal): chamfer slopes from (x, yT, z+B) to (x, yM, z).
        // normalBack=true  → front-facing wall (+Z normal): chamfer slopes from (x, yT, z-B) to (x, yM, z).
        static void AddBevelEdgeZ(List<Vector3> v, List<Vector2> u, List<int> t,
            float x0, float x1, float z, float yT, float B, bool normalBack)
        {
            float zInner = normalBack ? z - B : z + B;
            float yMid   = yT - B;
            if (normalBack)
                AddWall(v, u, t, x1,yMid,z, x1,yT,zInner, x0,yT,zInner, x0,yMid,z);
            else
                AddWall(v, u, t, x0,yMid,z, x0,yT,zInner, x1,yT,zInner, x1,yMid,z);
        }

        // Diagonal corner triangle where two outer bevel chamfer strips meet at a back corner.
        // xInner = x position of the inner top corner (xL+B for left, xR-B for right).
        // rightCorner=true  → back-right corner (normal faces +X,-Z).
        // rightCorner=false → back-left  corner (normal faces -X,-Z).
        static void AddBevelCornerTri(List<Vector3> v, List<Vector2> u, List<int> t,
            float xInner, float yT, float B, float zBack, bool rightCorner)
        {
            float xOuter = rightCorner ? xInner + B : xInner - B;
            float yMid   = yT - B;
            int b = v.Count;
            v.Add(new Vector3(xInner, yT,   zBack+B)); // P0 — top inner point
            v.Add(new Vector3(xInner, yMid, zBack));   // P1 — bottom on back edge
            v.Add(new Vector3(xOuter, yMid, zBack+B)); // P2 — bottom on side edge
            u.Add(new Vector2(0,1)); u.Add(new Vector2(0,0)); u.Add(new Vector2(1,0));
            // back-left:  P0→P1→P2 gives outward -X,-Z normal
            // back-right: P0→P2→P1 gives outward +X,-Z normal
            if (rightCorner)
            { t.Add(b); t.Add(b+2); t.Add(b+1); }
            else
            { t.Add(b); t.Add(b+1); t.Add(b+2); }
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
