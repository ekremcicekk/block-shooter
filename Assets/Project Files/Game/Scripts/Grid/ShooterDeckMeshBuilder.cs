using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Generates one merged deck mesh for the shooter grid.
    ///
    /// Rules:
    ///   Empty cell  → raises a tile flush with Y=0.
    ///   Filled cell → no tile (shooter block is the visual).
    ///   Boundary edge of empty region → extends outward by wingWidth,
    ///   then a vertical wall drops down by tileHeight.
    ///
    /// Submesh 0 = top/deck surface, Submesh 1 = side walls.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ShooterDeckMeshBuilder : MonoBehaviour
    {
        public int   gridCols   = 4;
        public int   gridRows   = 2;
        public float cellSize   = 1.2f;
        [Tooltip("Outer extension width beyond the empty-cell boundary")]
        public float wingWidth  = 0.5f;
        [Tooltip("Height the wall drops below Y=0")]
        public float tileHeight = 0.2f;

        private MeshFilter _mf;
        private void Awake() => _mf = GetComponent<MeshFilter>();

        /// <summary>
        /// Build and assign the mesh.
        /// isEmpty[col, row] = true  → empty cell, tile will be generated.
        /// isEmpty[col, row] = false → filled cell, no tile.
        /// </summary>
        public void BuildMesh(bool[,] isEmpty)
        {
            if (_mf == null) _mf = GetComponent<MeshFilter>();

            float gHW = gridCols * cellSize * 0.5f;
            float gHD = gridRows * cellSize * 0.5f;
            float W   = wingWidth;
            float yT  = 0f;
            float yB  = -tileHeight;

            // Corner vertex positions for a (cols+1) x (rows+1) grid
            var cx = new float[gridCols + 1];
            var cz = new float[gridRows + 1];
            for (int i = 0; i <= gridCols; i++) cx[i] = -gHW + i * cellSize;
            for (int j = 0; j <= gridRows; j++) cz[j] = -gHD + j * cellSize;

            bool E(int c, int r) =>
                c >= 0 && c < gridCols && r >= 0 && r < gridRows && isEmpty[c, r];

            var verts    = new List<Vector3>();
            var uvs      = new List<Vector2>();
            var trisTop  = new List<int>();
            var trisWall = new List<int>();

            for (int r = 0; r < gridRows; r++)
            for (int c = 0; c < gridCols; c++)
            {
                if (!E(c, r)) continue;

                bool lb = !E(c-1, r);   // boundary on left
                bool rb = !E(c+1, r);   // boundary on right
                bool fb = !E(c, r-1);   // boundary on front (−Z)
                bool bb = !E(c, r+1);   // boundary on back  (+Z)

                // ── Top surface ─────────────────────────────────────────────
                // Main cell quad
                Top(verts, uvs, trisTop, cx[c], cx[c+1], cz[r], cz[r+1], yT);

                // Edge extensions
                if (lb) Top(verts, uvs, trisTop, cx[c]-W,    cx[c],      cz[r],    cz[r+1],   yT);
                if (rb) Top(verts, uvs, trisTop, cx[c+1],    cx[c+1]+W,  cz[r],    cz[r+1],   yT);
                if (fb) Top(verts, uvs, trisTop, cx[c],      cx[c+1],    cz[r]-W,  cz[r],     yT);
                if (bb) Top(verts, uvs, trisTop, cx[c],      cx[c+1],    cz[r+1],  cz[r+1]+W, yT);

                // Corner fills (where two extensions meet)
                if (lb && fb) Top(verts, uvs, trisTop, cx[c]-W,   cx[c],     cz[r]-W,  cz[r],     yT);
                if (rb && fb) Top(verts, uvs, trisTop, cx[c+1],   cx[c+1]+W, cz[r]-W,  cz[r],     yT);
                if (lb && bb) Top(verts, uvs, trisTop, cx[c]-W,   cx[c],     cz[r+1],  cz[r+1]+W, yT);
                if (rb && bb) Top(verts, uvs, trisTop, cx[c+1],   cx[c+1]+W, cz[r+1],  cz[r+1]+W, yT);

                // ── Walls (outer edge of extensions, including corners) ───────
                // Left wall  (normal −X) — Z extended by corners if present
                if (lb)
                {
                    float z0 = fb ? cz[r]-W : cz[r];
                    float z1 = bb ? cz[r+1]+W : cz[r+1];
                    Wall(verts, uvs, trisWall,
                        cx[c]-W,yB,z1,  cx[c]-W,yT,z1,
                        cx[c]-W,yT,z0,  cx[c]-W,yB,z0);
                }
                // Right wall (normal +X)
                if (rb)
                {
                    float z0 = fb ? cz[r]-W : cz[r];
                    float z1 = bb ? cz[r+1]+W : cz[r+1];
                    Wall(verts, uvs, trisWall,
                        cx[c+1]+W,yB,z0,  cx[c+1]+W,yT,z0,
                        cx[c+1]+W,yT,z1,  cx[c+1]+W,yB,z1);
                }
                // Front wall (normal −Z)
                if (fb)
                {
                    float x0 = lb ? cx[c]-W : cx[c];
                    float x1 = rb ? cx[c+1]+W : cx[c+1];
                    Wall(verts, uvs, trisWall,
                        x0,yB,cz[r]-W,  x0,yT,cz[r]-W,
                        x1,yT,cz[r]-W,  x1,yB,cz[r]-W);
                }
                // Back wall  (normal +Z)
                if (bb)
                {
                    float x0 = lb ? cx[c]-W : cx[c];
                    float x1 = rb ? cx[c+1]+W : cx[c+1];
                    Wall(verts, uvs, trisWall,
                        x1,yB,cz[r+1]+W,  x1,yT,cz[r+1]+W,
                        x0,yT,cz[r+1]+W,  x0,yB,cz[r+1]+W);
                }
            }

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

        // ── Geometry helpers ──────────────────────────────────────────────────

        // Horizontal quad, normal +Y.
        static void Top(List<Vector3> v, List<Vector2> u, List<int> t,
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

        // Vertical quad. Pass corners: BL, TL, TR, BR (viewed from OUTSIDE).
        // Normal is outward by construction.
        static void Wall(List<Vector3> v, List<Vector2> u, List<int> t,
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
