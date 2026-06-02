using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ShooterDeckMeshBuilder : MonoBehaviour
    {
        public int   gridCols      = 4;
        public int   gridRows      = 2;
        public float cellSize      = 1.2f;
        public float sideWingWidth = 2f;
        public float backDepth     = 2f;
        public float tileHeight    = 0.15f;
        [Tooltip("Bevel width — how far the arc cuts into the top surface and wall.")]
        public float bevelSize     = 0.05f;
        [Tooltip("1 = flat chamfer. 3-6 = smooth rounded arc.")]
        public int   bevelSegments = 4;

        private MeshFilter _mf;
        private void Awake() => _mf = GetComponent<MeshFilter>();

        public void BuildMesh(bool[,] isEmpty)
        {
            if (_mf == null) _mf = GetComponent<MeshFilter>();

            float gHW = gridCols * cellSize * 0.5f;
            float gHD = gridRows * cellSize * 0.5f;
            float yT  = 0f;
            float yB  = -tileHeight;
            float B   = Mathf.Clamp(bevelSize, 0f,
                            Mathf.Min(tileHeight, Mathf.Min(sideWingWidth, backDepth)) * 0.9f);
            int   S   = Mathf.Max(1, bevelSegments);

            var cx = new float[gridCols + 1];
            var cz = new float[gridRows + 1];
            for (int i = 0; i <= gridCols; i++) cx[i] = -gHW + i * cellSize;
            for (int j = 0; j <= gridRows; j++) cz[j] = -gHD + j * cellSize;

            float zBack  = cz[0] - backDepth;
            float zFront = cz[gridRows];
            float xL     = cx[0]         - sideWingWidth;
            float xR     = cx[gridCols]  + sideWingWidth;

            bool E(int c, int r) =>
                c >= 0 && c < gridCols && r >= 0 && r < gridRows && isEmpty[c, r];

            var verts    = new List<Vector3>();
            var uvs      = new List<Vector2>();
            var trisTop  = new List<int>();
            var trisWall = new List<int>();

            // ── 1. Empty cell tiles + inter-cell walls (NO bevel — unchanged) ─
            for (int r = 0; r < gridRows; r++)
            for (int c = 0; c < gridCols; c++)
            {
                if (!E(c, r)) continue;
                AddTop(verts, uvs, trisTop, cx[c], cx[c+1], cz[r], cz[r+1], yT);

                if (c > 0 && !E(c-1, r))
                    AddWallX(verts, uvs, trisWall, cx[c], cz[r], cz[r+1], yT, yB, false);
                if (c < gridCols-1 && !E(c+1, r))
                    AddWallX(verts, uvs, trisWall, cx[c+1], cz[r], cz[r+1], yT, yB, true);
                if (r > 0 && !E(c, r-1))
                    AddWallZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[r], yT, yB, false);
                if (r < gridRows-1 && !E(c, r+1))
                    AddWallZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[r+1], yT, yB, true);
            }

            // ── 2. Wing tops (edges adjacent to beveled walls are inset by B) ─
            AddTop(verts, uvs, trisTop, xL+B, cx[0],        cz[0], zFront,   yT); // left wing
            AddTop(verts, uvs, trisTop, cx[gridCols], xR-B, cz[0], zFront,   yT); // right wing
            AddTop(verts, uvs, trisTop, xL+B, xR-B,  zBack+B, cz[0],        yT); // back wing

            // ── 3. Outer walls — shortened by B at top (Y only, XZ unchanged) ─
            AddWallX(verts, uvs, trisWall, xL, zBack,  zFront, yT-B, yB, false);
            AddWallX(verts, uvs, trisWall, xR, zBack,  zFront, yT-B, yB, true);
            AddWallZ(verts, uvs, trisWall, xL, xR,     zBack,  yT-B, yB, false);

            // ── 4. Outer bevel arcs (replace the original sharp top edge) ────
            AddBevelArcX(verts, uvs, trisWall, xL, zBack+B, zFront,  yT, B, S, false);
            AddBevelArcX(verts, uvs, trisWall, xR, zBack+B, zFront,  yT, B, S, true);
            AddBevelArcZ(verts, uvs, trisWall, xL+B, xR-B,  zBack,   yT, B, S, false);

            // ── 5. Outer corner fills (back-left and back-right) ─────────────
            AddBevelCorner(verts, uvs, trisWall, xL+B, yT, B, zBack, false);
            AddBevelCorner(verts, uvs, trisWall, xR-B, yT, B, zBack, true);

            // ── 6. Inner boundary walls + bevel arcs ─────────────────────────
            for (int r = 0; r < gridRows; r++)
            {
                // Inner left (x=cx[0], faces +X into wing)
                if (!E(0, r))
                {
                    AddWallX(verts, uvs, trisWall, cx[0], cz[r], cz[r+1], yT-B, yB, true);
                    AddBevelArcX(verts, uvs, trisWall, cx[0], cz[r], cz[r+1], yT, B, S, true);
                }
                // Inner right (x=cx[gridCols], faces -X into wing)
                if (!E(gridCols-1, r))
                {
                    AddWallX(verts, uvs, trisWall, cx[gridCols], cz[r], cz[r+1], yT-B, yB, false);
                    AddBevelArcX(verts, uvs, trisWall, cx[gridCols], cz[r], cz[r+1], yT, B, S, false);
                }
            }
            for (int c = 0; c < gridCols; c++)
            {
                // Inner back (z=cz[0], faces -Z into back wing)
                if (!E(c, 0))
                {
                    AddWallZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[0], yT-B, yB, false);
                    AddBevelArcZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[0], yT, B, S, false);
                }
            }

            // ── Build mesh ────────────────────────────────────────────────────
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

        static void AddWallX(List<Vector3> v, List<Vector2> u, List<int> t,
            float x, float z0, float z1, float yTop, float yBot, bool normalRight)
        {
            if (normalRight)
                AddQuad(v, u, t, x,yBot,z0, x,yTop,z0, x,yTop,z1, x,yBot,z1);
            else
                AddQuad(v, u, t, x,yBot,z1, x,yTop,z1, x,yTop,z0, x,yBot,z0);
        }

        static void AddWallZ(List<Vector3> v, List<Vector2> u, List<int> t,
            float x0, float x1, float z, float yTop, float yBot, bool normalFwd)
        {
            if (normalFwd)
                AddQuad(v, u, t, x1,yBot,z, x1,yTop,z, x0,yTop,z, x0,yBot,z);
            else
                AddQuad(v, u, t, x0,yBot,z, x0,yTop,z, x1,yTop,z, x1,yBot,z);
        }

        /// <summary>
        /// Bevel arc on an X-aligned wall (fixed x, runs z0..z1).
        /// Generates S quad strips replacing the original sharp top edge.
        /// Arc goes from (x, yT-B, z) inward to (xInner, yT, z).
        /// normalRight=false → left wall (-X normal, xInner = x+B)
        /// normalRight=true  → right wall (+X normal, xInner = x-B)
        /// </summary>
        static void AddBevelArcX(List<Vector3> v, List<Vector2> u, List<int> t,
            float x, float z0, float z1, float yT, float B, int S, bool normalRight)
        {
            float xInner = normalRight ? x - B : x + B;
            // Precompute arc rings (px=x coord, py=y coord) for k=0..S
            var ring = new (float px, float py)[S + 1];
            for (int k = 0; k <= S; k++)
            {
                float a = k / (float)S * Mathf.PI * 0.5f;
                // Arc center at (xInner, yT-B). Starts at (x, yT-B), ends at (xInner, yT).
                float px = xInner + (x - xInner) * Mathf.Cos(a); // x → xInner as k→S
                float py = yT - B + B * Mathf.Sin(a);            // yT-B → yT as k→S
                ring[k]  = (px, py);
            }
            // ring[0] = (x, yT-B) — top of shortened wall
            // ring[S] = (xInner, yT) — inset top surface edge

            for (int k = 0; k < S; k++)
            {
                var (ax, ay) = ring[k];
                var (bx, by) = ring[k+1];
                if (normalRight)
                    AddQuad(v, u, t, ax,ay,z0, bx,by,z0, bx,by,z1, ax,ay,z1);
                else
                    AddQuad(v, u, t, ax,ay,z1, bx,by,z1, bx,by,z0, ax,ay,z0);
            }
        }

        /// <summary>
        /// Bevel arc on a Z-aligned wall (fixed z, runs x0..x1).
        /// normalFwd=false → back wall (-Z normal, zInner = z+B)
        /// normalFwd=true  → front wall (+Z normal, zInner = z-B)
        /// </summary>
        static void AddBevelArcZ(List<Vector3> v, List<Vector2> u, List<int> t,
            float x0, float x1, float z, float yT, float B, int S, bool normalFwd)
        {
            float zInner = normalFwd ? z - B : z + B;
            var ring = new (float pz, float py)[S + 1];
            for (int k = 0; k <= S; k++)
            {
                float a  = k / (float)S * Mathf.PI * 0.5f;
                float pz = zInner + (z - zInner) * Mathf.Cos(a);
                float py = yT - B + B * Mathf.Sin(a);
                ring[k]  = (pz, py);
            }

            for (int k = 0; k < S; k++)
            {
                var (az, ay) = ring[k];
                var (bz, by) = ring[k+1];
                if (normalFwd)
                    AddQuad(v, u, t, x1,ay,az, x1,by,bz, x0,by,bz, x0,ay,az);
                else
                    AddQuad(v, u, t, x0,ay,az, x0,by,bz, x1,by,bz, x1,ay,az);
            }
        }

        /// <summary>
        /// Corner fill where the back bevel arc meets the left/right bevel arc.
        /// Fills the gap at (xOuter, zBack) with two triangles:
        ///   A = (xInner, yT,   zBack+B) — top inner corner
        ///   S = (xOuter, yT-B, zBack+B) — left/right arc bottom at z=zBack+B
        ///   C = (xOuter, yT-B, zBack)   — actual wall corner (junction at yT-B)
        ///   K = (xInner, yT-B, zBack)   — back arc bottom at x=xInner
        /// rightCorner=false → back-left, rightCorner=true → back-right
        /// </summary>
        static void AddBevelCorner(List<Vector3> v, List<Vector2> u, List<int> t,
            float xInner, float yT, float B, float zBack, bool rightCorner)
        {
            float xOuter = rightCorner ? xInner + B : xInner - B;
            float yM     = yT - B;

            int b = v.Count;
            v.Add(new Vector3(xInner, yT, zBack+B)); // 0 A
            v.Add(new Vector3(xOuter, yM, zBack+B)); // 1 S
            v.Add(new Vector3(xOuter, yM, zBack));   // 2 C
            v.Add(new Vector3(xInner, yM, zBack));   // 3 K
            u.Add(new Vector2(0.5f,1f)); u.Add(new Vector2(0,0));
            u.Add(new Vector2(0,0));     u.Add(new Vector2(1,0));

            if (rightCorner)
            {
                t.Add(b); t.Add(b+1); t.Add(b+3); // A S K
                t.Add(b+1); t.Add(b+2); t.Add(b+3); // S C K
            }
            else
            {
                t.Add(b); t.Add(b+3); t.Add(b+1); // A K S
                t.Add(b+1); t.Add(b+3); t.Add(b+2); // S K C
            }
        }

        // BL, TL, TR, BR — outward CCW winding.
        static void AddQuad(List<Vector3> v, List<Vector2> u, List<int> t,
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
