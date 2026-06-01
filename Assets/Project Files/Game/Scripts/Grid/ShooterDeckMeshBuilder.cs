using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Generates a merged deck mesh for the shooter grid.
    ///
    /// isEmpty[col, row] = true  → empty cell → raised tile (platform visible here).
    /// isEmpty[col, row] = false → filled cell → no tile (shooter block is the visual).
    ///
    /// The mesh consists of:
    ///   • Tiles for every empty cell inside the grid.
    ///   • A left-wing strip extending sideWingWidth to the left of the grid.
    ///   • A right-wing strip extending sideWingWidth to the right of the grid.
    ///   • A back-wing strip extending backDepth behind the grid (full width incl. side wings).
    ///   • The FRONT edge is always open — no wall, no forward extension.
    ///
    /// Wing-to-cell connection rule:
    ///   Empty cell at col=0          → left wing connects seamlessly (no inner wall).
    ///   Empty cell at col=gridCols-1 → right wing connects seamlessly.
    ///   Empty cell at row=gridRows-1 → back wing connects seamlessly.
    ///   Filled cell at those boundaries → inner boundary wall separates wing from cell gap.
    ///
    /// Submesh 0 = top surface (deckTopMaterial)
    /// Submesh 1 = side walls  (deckWallMaterial)
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ShooterDeckMeshBuilder : MonoBehaviour
    {
        public int   gridCols      = 4;
        public int   gridRows      = 2;
        public float cellSize      = 1.2f;
        [Tooltip("How far the platform extends to the left and right of the grid")]
        public float sideWingWidth = 2f;
        [Tooltip("How far the platform extends behind the grid")]
        public float backDepth     = 2f;
        [Tooltip("Height the walls drop below Y=0")]
        public float tileHeight    = 0.15f;

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

            // Grid corner positions
            var cx = new float[gridCols + 1];
            var cz = new float[gridRows + 1];
            for (int i = 0; i <= gridCols; i++) cx[i] = -gHW + i * cellSize;
            for (int j = 0; j <= gridRows; j++) cz[j] = -gHD + j * cellSize;

            // Derived edge positions
            float xL     = cx[0]          - W;    // left outer edge
            float xR     = cx[gridCols]   + W;    // right outer edge
            float zFront = cz[0];                  // front edge (always open)
            float zBack  = cz[gridRows]   + D;    // back outer edge

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

                // Top tile
                AddTop(verts, uvs, trisTop, cx[c], cx[c+1], cz[r], cz[r+1], yT);

                // Inner walls — only between this empty cell and an adjacent FILLED cell.
                // Left: not leftmost col (leftmost connects to left wing)
                if (c > 0 && !E(c-1, r))
                    AddWallX(verts, uvs, trisWall, cx[c], cz[r], cz[r+1], yT, yB, false);
                // Right: not rightmost col (rightmost connects to right wing)
                if (c < gridCols - 1 && !E(c+1, r))
                    AddWallX(verts, uvs, trisWall, cx[c+1], cz[r], cz[r+1], yT, yB, true);
                // Front: front row is always open (no wall at z=cz[0])
                if (r > 0 && !E(c, r-1))
                    AddWallZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[r], yT, yB, false);
                // Back: not backmost row (backmost connects to back wing)
                if (r < gridRows - 1 && !E(c, r+1))
                    AddWallZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[r+1], yT, yB, true);
            }

            // ── 2. Wing top surfaces ─────────────────────────────────────────
            // Left wing: spans full grid depth (zFront → cz[gridRows])
            AddTop(verts, uvs, trisTop, xL, cx[0], zFront, cz[gridRows], yT);
            // Right wing
            AddTop(verts, uvs, trisTop, cx[gridCols], xR, zFront, cz[gridRows], yT);
            // Back wing: full width including side-wing columns
            AddTop(verts, uvs, trisTop, xL, xR, cz[gridRows], zBack, yT);

            // ── 3. Outer walls ───────────────────────────────────────────────
            // Left outer (normal −X, visible from outside/left)
            AddWallX(verts, uvs, trisWall, xL, zFront, zBack, yT, yB, false);
            // Right outer (normal +X)
            AddWallX(verts, uvs, trisWall, xR, zFront, zBack, yT, yB, true);
            // Back outer (normal +Z)
            AddWallZ(verts, uvs, trisWall, xL, xR, zBack, yT, yB, true);
            // Front: intentionally omitted — always open.

            // ── 4. Inner wing boundary walls ────────────────────────────────
            // Left inner (x=cx[0]): wall where c=0 is FILLED; normal +X (faces grid interior)
            for (int r = 0; r < gridRows; r++)
            {
                if (E(0, r)) continue; // empty → connects to wing, no wall
                AddWallX(verts, uvs, trisWall, cx[0], cz[r], cz[r+1], yT, yB, true);
            }
            // Right inner (x=cx[gridCols]): wall where c=gridCols-1 is FILLED; normal -X
            for (int r = 0; r < gridRows; r++)
            {
                if (E(gridCols - 1, r)) continue;
                AddWallX(verts, uvs, trisWall, cx[gridCols], cz[r], cz[r+1], yT, yB, false);
            }
            // Back inner (z=cz[gridRows]): wall where r=gridRows-1 is FILLED; normal +Z
            for (int c = 0; c < gridCols; c++)
            {
                if (E(c, gridRows - 1)) continue;
                AddWallZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[gridRows], yT, yB, true);
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

        // Horizontal quad at y, normal +Y.
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

        // Vertical wall at fixed X, spanning Z range.
        // normalRight=true  → normal +X (wall visible from the +X/right side)
        // normalRight=false → normal -X (wall visible from the -X/left side)
        static void AddWallX(List<Vector3> v, List<Vector2> u, List<int> t,
            float x, float z0, float z1, float yT, float yB, bool normalRight)
        {
            if (normalRight)
                AddWall(v, u, t, x,yB,z0,  x,yT,z0,  x,yT,z1,  x,yB,z1);  // normal +X
            else
                AddWall(v, u, t, x,yB,z1,  x,yT,z1,  x,yT,z0,  x,yB,z0);  // normal -X
        }

        // Vertical wall at fixed Z, spanning X range.
        // normalBack=true  → normal +Z (wall visible from behind, +Z side)
        // normalBack=false → normal -Z (wall visible from front, -Z side)
        static void AddWallZ(List<Vector3> v, List<Vector2> u, List<int> t,
            float x0, float x1, float z, float yT, float yB, bool normalBack)
        {
            if (normalBack)
                AddWall(v, u, t, x1,yB,z,  x1,yT,z,  x0,yT,z,  x0,yB,z);  // normal +Z
            else
                AddWall(v, u, t, x0,yB,z,  x0,yT,z,  x1,yT,z,  x1,yB,z);  // normal -Z
        }

        // Raw quad: BL, TL, TR, BR (CCW winding = normal faces viewer).
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
