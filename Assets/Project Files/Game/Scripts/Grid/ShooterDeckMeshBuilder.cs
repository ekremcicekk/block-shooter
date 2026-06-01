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

            // Grid corner positions
            var cx = new float[gridCols + 1];
            var cz = new float[gridRows + 1];
            for (int i = 0; i <= gridCols; i++) cx[i] = -gHW + i * cellSize;
            for (int j = 0; j <= gridRows; j++) cz[j] = -gHD + j * cellSize;

            // Key Z positions
            float zExtBack = cz[0] - D;        // back outer edge  (away from conveyor)
            float zFront   = cz[gridRows];      // front face       (toward conveyor, OPEN)
            float xL       = cx[0]          - W;
            float xR       = cx[gridCols]   + W;

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

                // Compute padded tile bounds: inset only on edges adjacent to filled cells.
                // Wing-connected edges (boundary edges) are NOT inset — they merge seamlessly.
                float tx0 = cx[c];
                float tx1 = cx[c+1];
                float tz0 = cz[r];
                float tz1 = cz[r+1];

                bool filledL = c > 0             && !E(c-1, r);   // filled neighbor to left
                bool filledR = c < gridCols - 1  && !E(c+1, r);   // filled neighbor to right
                bool filledB = r > 0             && !E(c, r-1);   // filled neighbor behind
                bool filledF = r < gridRows - 1  && !E(c, r+1);   // filled neighbor in front

                if (filledL) tx0 += P;
                if (filledR) tx1 -= P;
                if (filledB) tz0 += P;
                if (filledF) tz1 -= P;

                AddTop(verts, uvs, trisTop, tx0, tx1, tz0, tz1, yT);

                // Walls start at padded tile edge, span to grid corner (wall covers full cell boundary).
                // Left wall: skip c=0 (connects to left wing)
                if (filledL)
                    AddWallX(verts, uvs, trisWall, cx[c], cz[r], cz[r+1], yT, yB, false);
                // Right wall: skip c=gridCols-1 (connects to right wing)
                if (filledR)
                    AddWallX(verts, uvs, trisWall, cx[c+1], cz[r], cz[r+1], yT, yB, true);
                // Back wall (-Z direction): skip r=0 (connects to back wing)
                if (filledB)
                    AddWallZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[r], yT, yB, false);
                // Front wall (+Z direction): skip r=gridRows-1 (front is always open)
                if (filledF)
                    AddWallZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[r+1], yT, yB, true);
            }

            // ── 2. Wing top surfaces ─────────────────────────────────────────
            // Left wing: alongside the full grid depth (no overlap with back wing)
            AddTop(verts, uvs, trisTop, xL, cx[0],        cz[0], zFront, yT);
            // Right wing
            AddTop(verts, uvs, trisTop, cx[gridCols], xR, cz[0], zFront, yT);
            // Back wing: full width (xL..xR), extends in -Z from grid back face
            AddTop(verts, uvs, trisTop, xL, xR, zExtBack, cz[0], yT);

            // ── 3. Outer walls ───────────────────────────────────────────────
            AddWallX(verts, uvs, trisWall, xL, zExtBack, zFront, yT, yB, false); // Left  outer (−X)
            AddWallX(verts, uvs, trisWall, xR, zExtBack, zFront, yT, yB, true);  // Right outer (+X)
            AddWallZ(verts, uvs, trisWall, xL, xR, zExtBack,     yT, yB, false); // Back  outer (−Z)
            // Front face: intentionally omitted — always open.

            // ── 4. Inner wing boundary walls ────────────────────────────────
            // Inner LEFT (x=cx[0]): per row, filled c=0 cells; normal +X
            for (int r = 0; r < gridRows; r++)
            {
                if (E(0, r)) continue;
                AddWallX(verts, uvs, trisWall, cx[0], cz[r], cz[r+1], yT, yB, true);
            }
            // Inner RIGHT (x=cx[gridCols]): per row, filled c=gridCols-1 cells; normal -X
            for (int r = 0; r < gridRows; r++)
            {
                if (E(gridCols - 1, r)) continue;
                AddWallX(verts, uvs, trisWall, cx[gridCols], cz[r], cz[r+1], yT, yB, false);
            }
            // Inner BACK (z=cz[0]): per col, filled r=0 cells; normal -Z (faces back extension)
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
