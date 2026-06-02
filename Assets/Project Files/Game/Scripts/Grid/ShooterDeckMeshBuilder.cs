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
        [Tooltip("Corner rounding radius in XZ (top-down view). 0 = sharp corners.")]
        public float bevelSize     = 0.3f;
        [Tooltip("1 = chamfer. 4-8 = smooth rounded corners.")]
        public int   bevelSegments = 6;

        private MeshFilter _mf;
        private void Awake() => _mf = GetComponent<MeshFilter>();

        public void BuildMesh(bool[,] isEmpty)
        {
            if (_mf == null) _mf = GetComponent<MeshFilter>();

            float gHW = gridCols * cellSize * 0.5f;
            float gHD = gridRows * cellSize * 0.5f;
            float yT  = 0f;
            float yB  = -tileHeight;
            float R   = Mathf.Clamp(bevelSize, 0f, Mathf.Min(sideWingWidth, backDepth) * 0.9f);
            int   S   = Mathf.Max(1, bevelSegments);

            var cx = new float[gridCols + 1];
            var cz = new float[gridRows + 1];
            for (int i = 0; i <= gridCols; i++) cx[i] = -gHW + i * cellSize;
            for (int j = 0; j <= gridRows; j++) cz[j] = -gHD + j * cellSize;

            float zBack  = cz[0] - backDepth;
            float zFront = cz[gridRows];
            float xL     = cx[0]        - sideWingWidth;
            float xR     = cx[gridCols] + sideWingWidth;

            bool E(int c, int r) =>
                c >= 0 && c < gridCols && r >= 0 && r < gridRows && isEmpty[c, r];

            var verts    = new List<Vector3>();
            var uvs      = new List<Vector2>();
            var trisTop  = new List<int>();
            var trisWall = new List<int>();

            // ── 1. Empty cell tiles + inter-cell walls (UNCHANGED) ─────────
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

            // ── 2. Wing tops (rounded corners in XZ) ───────────────────────
            // Left wing: 3 rects + 2 corner fans
            AddTop(verts, uvs, trisTop, xL,   cx[0], zBack+R, zFront-R, yT); // full-width centre strip
            AddTop(verts, uvs, trisTop, xL+R, cx[0], zBack,   zBack+R,  yT); // back inner strip
            AddTop(verts, uvs, trisTop, xL+R, cx[0], zFront-R,zFront,   yT); // front inner strip
            AddCornerFan(verts, uvs, trisTop, xL+R, zBack+R,  R, S, 180f, yT); // back-left
            AddCornerFan(verts, uvs, trisTop, xL+R, zFront-R, R, S,  90f, yT); // front-left

            // Right wing: 3 rects + 2 corner fans
            AddTop(verts, uvs, trisTop, cx[gridCols], xR-R, zBack+R, zFront-R, yT);
            AddTop(verts, uvs, trisTop, cx[gridCols], xR-R, zBack,   zBack+R,  yT);
            AddTop(verts, uvs, trisTop, cx[gridCols], xR-R, zFront-R,zFront,   yT);
            AddCornerFan(verts, uvs, trisTop, xR-R, zBack+R,  R, S, 270f, yT); // back-right
            AddCornerFan(verts, uvs, trisTop, xR-R, zFront-R, R, S,   0f, yT); // front-right

            // Back wing: grid-width strip (no corners, handled above)
            AddTop(verts, uvs, trisTop, cx[0], cx[gridCols], zBack, cz[0], yT);

            // ── 3. Outer walls: straight sections + curved corners ──────────
            AddWallX(verts, uvs, trisWall, xL, zBack+R, zFront-R, yT, yB, false); // left straight
            AddWallX(verts, uvs, trisWall, xR, zBack+R, zFront-R, yT, yB, true);  // right straight
            AddWallZ(verts, uvs, trisWall, xL+R, xR-R, zBack, yT, yB, false);     // back straight

            AddCurvedWall(verts, uvs, trisWall, xL+R, zBack+R,  R, S, 180f, yT, yB); // back-left
            AddCurvedWall(verts, uvs, trisWall, xR-R, zBack+R,  R, S, 270f, yT, yB); // back-right
            AddCurvedWall(verts, uvs, trisWall, xL+R, zFront-R, R, S,  90f, yT, yB); // front-left
            AddCurvedWall(verts, uvs, trisWall, xR-R, zFront-R, R, S,   0f, yT, yB); // front-right

            // ── 4. Inner boundary walls (UNCHANGED) ─────────────────────────
            for (int r = 0; r < gridRows; r++)
            {
                if (!E(0, r))
                    AddWallX(verts, uvs, trisWall, cx[0], cz[r], cz[r+1], yT, yB, true);
                if (!E(gridCols-1, r))
                    AddWallX(verts, uvs, trisWall, cx[gridCols], cz[r], cz[r+1], yT, yB, false);
            }
            for (int c = 0; c < gridCols; c++)
            {
                if (!E(c, 0))
                    AddWallZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[0], yT, yB, false);
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
        /// Triangle fan filling a rounded corner on the top surface.
        /// Center (cx, cz) is the inset corner point. Arc sweeps 90° CCW from startDeg.
        /// Winding gives +Y normal (visible from above).
        /// </summary>
        static void AddCornerFan(List<Vector3> v, List<Vector2> u, List<int> t,
            float cx, float cz, float R, int S, float startDeg, float yT)
        {
            int ci = v.Count;
            v.Add(new Vector3(cx, yT, cz));
            u.Add(new Vector2(cx, cz));

            for (int k = 0; k <= S; k++)
            {
                float a  = (startDeg + k * 90f / S) * Mathf.Deg2Rad;
                float px = cx + R * Mathf.Cos(a);
                float pz = cz + R * Mathf.Sin(a);
                v.Add(new Vector3(px, yT, pz));
                u.Add(new Vector2(px, pz));
            }

            for (int k = 0; k < S; k++)
            {
                t.Add(ci);
                t.Add(ci + 1 + k);
                t.Add(ci + 2 + k);
            }
        }

        /// <summary>
        /// Curved vertical wall following a 90° arc in XZ, extruded from yT to yB.
        /// Center (cx, cz), arc sweeps CCW from startDeg. Outward normals computed by RecalculateNormals.
        /// </summary>
        static void AddCurvedWall(List<Vector3> v, List<Vector2> u, List<int> t,
            float cx, float cz, float R, int S, float startDeg, float yT, float yB)
        {
            var arc = new Vector2[S + 1];
            for (int k = 0; k <= S; k++)
            {
                float a = (startDeg + k * 90f / S) * Mathf.Deg2Rad;
                arc[k] = new Vector2(cx + R * Mathf.Cos(a), cz + R * Mathf.Sin(a));
            }

            for (int k = 0; k < S; k++)
            {
                AddQuad(v, u, t,
                    arc[k].x,   yB, arc[k].y,
                    arc[k].x,   yT, arc[k].y,
                    arc[k+1].x, yT, arc[k+1].y,
                    arc[k+1].x, yB, arc[k+1].y);
            }
        }

        static void AddQuad(List<Vector3> v, List<Vector2> u, List<int> t,
            float blx, float bly, float blz,
            float tlx, float tly, float tlz,
            float trx, float try_, float trz,
            float brx, float bry, float brz)
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
