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
            float yT  = tileHeight;
            float yB  = 0f;
            float R   = Mathf.Clamp(bevelSize, 0f, Mathf.Min(cellSize * 0.5f, Mathf.Min(sideWingWidth, backDepth) * 0.9f));
            int   S   = Mathf.Max(1, bevelSegments);

            var cx = new float[gridCols + 1];
            var cz = new float[gridRows + 1];
            for (int i = 0; i <= gridCols; i++) cx[i] = -gHW + i * cellSize;
            for (int j = 0; j <= gridRows; j++) cz[j] = -gHD + j * cellSize;

            float zBack  = cz[0] - backDepth;
            float zFront = cz[gridRows];
            float xL     = cx[0]        - sideWingWidth;
            float xR     = cx[gridCols] + sideWingWidth;

            bool IsPlatform(int col, int row)
            {
                if (row >= gridRows) return false;
                if (col < 0 || col >= gridCols) return true;
                if (row < 0) return true;
                return isEmpty[col, row];
            }

            bool HasCornerTR(int c, int r)
            {
                bool tr = IsPlatform(c, r);
                bool tl = IsPlatform(c - 1, r);
                bool bl = IsPlatform(c - 1, r - 1);
                bool br = IsPlatform(c, r - 1);
                return (tr && !tl && !br) || (!tr && tl && br && bl);
            }

            bool HasCornerTL(int c, int r)
            {
                bool tr = IsPlatform(c, r);
                bool tl = IsPlatform(c - 1, r);
                bool bl = IsPlatform(c - 1, r - 1);
                bool br = IsPlatform(c, r - 1);
                return (tl && !tr && !bl) || (!tl && tr && bl && br);
            }

            bool HasCornerBL(int c, int r)
            {
                bool tr = IsPlatform(c, r);
                bool tl = IsPlatform(c - 1, r);
                bool bl = IsPlatform(c - 1, r - 1);
                bool br = IsPlatform(c, r - 1);
                return (bl && !tl && !br) || (!bl && tl && br && tr);
            }

            bool HasCornerBR(int c, int r)
            {
                bool tr = IsPlatform(c, r);
                bool tl = IsPlatform(c - 1, r);
                bool bl = IsPlatform(c - 1, r - 1);
                bool br = IsPlatform(c, r - 1);
                return (br && !bl && !tr) || (!br && bl && tr && tl);
            }

            var verts    = new List<Vector3>();
            var uvs      = new List<Vector2>();
            var trisTop  = new List<int>();
            var trisWall = new List<int>();

            // ── 1. Empty cell tiles (top surfaces) ─────────────────────────
            for (int r = 0; r < gridRows; r++)
            for (int c = 0; c < gridCols; c++)
            {
                if (!isEmpty[c, r]) continue;

                // Center strip
                AddTop(verts, uvs, trisTop, cx[c] + R, cx[c+1] - R, cz[r], cz[r+1], yT);

                // Left side strip
                AddTop(verts, uvs, trisTop, cx[c], cx[c] + R, cz[r] + R, cz[r+1] - R, yT);

                // Right side strip
                AddTop(verts, uvs, trisTop, cx[c+1] - R, cx[c+1], cz[r] + R, cz[r+1] - R, yT);

                // BL corner square (if not rounded)
                if (!HasCornerTR(c, r))
                    AddTop(verts, uvs, trisTop, cx[c], cx[c] + R, cz[r], cz[r] + R, yT);

                // BR corner square (if not rounded)
                if (!HasCornerTL(c + 1, r))
                    AddTop(verts, uvs, trisTop, cx[c+1] - R, cx[c+1], cz[r], cz[r] + R, yT);

                // TL corner square (if not rounded)
                if (!HasCornerBR(c, r + 1))
                    AddTop(verts, uvs, trisTop, cx[c], cx[c] + R, cz[r+1] - R, cz[r+1], yT);

                // TR corner square (if not rounded)
                if (!HasCornerBL(c + 1, r + 1))
                    AddTop(verts, uvs, trisTop, cx[c+1] - R, cx[c+1], cz[r+1] - R, cz[r+1], yT);
            }

            // ── 2. Wing tops (rounded corners in XZ) ───────────────────────
            // Left wing: 3 rects + 2 corner fans
            AddTop(verts, uvs, trisTop, xL,   cx[0], zBack+R, zFront-R, yT); // full-width centre strip
            AddTop(verts, uvs, trisTop, xL+R, cx[0], zBack,   zBack+R,  yT); // back inner strip
            float leftWingFrontRightX = cx[0] - (IsPlatform(0, gridRows - 1) ? 0f : R);
            AddTop(verts, uvs, trisTop, xL+R, leftWingFrontRightX, zFront-R,zFront,   yT); // front inner strip
            AddCornerFan(verts, uvs, trisTop, xL+R, zBack+R, xL+R, zBack+R,  R, S, 180f, yT); // back-left
            AddCornerFan(verts, uvs, trisTop, xL+R, zFront-R, xL+R, zFront-R, R, S,  90f, yT); // front-left

            // Right wing: 3 rects + 2 corner fans (rect 1 goes full width to xR like left wing)
            AddTop(verts, uvs, trisTop, cx[gridCols], xR,   zBack+R, zFront-R, yT);
            AddTop(verts, uvs, trisTop, cx[gridCols], xR-R, zBack,   zBack+R,  yT);
            float rightWingFrontLeftX = cx[gridCols] + (IsPlatform(gridCols - 1, gridRows - 1) ? 0f : R);
            AddTop(verts, uvs, trisTop, rightWingFrontLeftX, xR-R, zFront-R,zFront,   yT); // front inner strip
            AddCornerFan(verts, uvs, trisTop, xR-R, zBack+R, xR-R, zBack+R,  R, S, 270f, yT); // back-right
            AddCornerFan(verts, uvs, trisTop, xR-R, zFront-R, xR-R, zFront-R, R, S,   0f, yT); // front-right

            // Back wing: grid-width strip (no corners, handled above)
            AddTop(verts, uvs, trisTop, cx[0], cx[gridCols], zBack, cz[0], yT);

            // ── 3. Straight walls ──────────────────────────────────────────
            AddWallX(verts, uvs, trisWall, xL, zBack+R, zFront-R, yT, yB, false); // left straight
            AddWallX(verts, uvs, trisWall, xR, zBack+R, zFront-R, yT, yB, true);  // right straight
            AddWallZ(verts, uvs, trisWall, xL+R, xR-R, zBack, yT, yB, false);     // back straight

            // Grid X-walls (vertical walls at constant x = cx[c])
            for (int c = 0; c <= gridCols; c++)
            for (int r = 0; r < gridRows; r++)
            {
                bool tl = IsPlatform(c - 1, r);
                bool tr = IsPlatform(c, r);
                if (tl != tr)
                {
                    bool normalRight = tl && !tr; // platform on left, hole on right -> faces right (+X)
                    float zStart = cz[r];
                    if (HasCornerTL(c, r) || HasCornerTR(c, r))
                        zStart += R;

                    float zEnd = cz[r+1];
                    if (HasCornerBL(c, r+1) || HasCornerBR(c, r+1))
                        zEnd -= R;

                    if (zStart < zEnd)
                        AddWallX(verts, uvs, trisWall, cx[c], zStart, zEnd, yT, yB, normalRight);
                }
            }

            // Grid and Wing Z-walls (horizontal walls)
            for (int r = 0; r <= gridRows; r++)
            for (int c = -1; c <= gridCols; c++)
            {
                bool bl = IsPlatform(c, r - 1);
                bool tr = IsPlatform(c, r);
                if (bl != tr)
                {
                    bool normalFwd = bl && !tr; // platform below, hole above -> faces north (+Z)
                    float xStart = (c == -1) ? xL + R : cx[c];
                    if (c > -1)
                    {
                        if (HasCornerTR(c, r) || HasCornerBR(c, r))
                            xStart += R;
                    }

                    float xEnd = (c == gridCols) ? xR - R : cx[c+1];
                    if (c < gridCols)
                    {
                        if (HasCornerTL(c+1, r) || HasCornerBL(c+1, r))
                            xEnd -= R;
                    }

                    if (xStart < xEnd)
                        AddWallZ(verts, uvs, trisWall, xStart, xEnd, cz[r], yT, yB, normalFwd);
                }
            }

            // ── 4. Curved Corners & Fans ───────────────────────────────────
            // Outer wing curved walls
            AddCurvedWall(verts, uvs, trisWall, xL+R, zBack+R,  R, S, 180f, yT, yB, false); // back-left
            AddCurvedWall(verts, uvs, trisWall, xR-R, zBack+R,  R, S, 270f, yT, yB, false); // back-right
            AddCurvedWall(verts, uvs, trisWall, xL+R, zFront-R, R, S,  90f, yT, yB, false); // front-left
            AddCurvedWall(verts, uvs, trisWall, xR-R, zFront-R, R, S,   0f, yT, yB, false); // front-right

            // Grid vertices curved corners
            for (int c = 0; c <= gridCols; c++)
            for (int r = 0; r <= gridRows; r++)
            {
                bool tr = IsPlatform(c, r);
                bool tl = IsPlatform(c - 1, r);
                bool bl = IsPlatform(c - 1, r - 1);
                bool br = IsPlatform(c, r - 1);

                // TR quadrant corner
                if ((tr && !tl && !br) || (!tr && tl && br && bl))
                {
                    bool inward = !tr;
                    float fx = inward ? cx[c] : cx[c] + R;
                    float fz = inward ? cz[r] : cz[r] + R;
                    AddCurvedWall(verts, uvs, trisWall, cx[c] + R, cz[r] + R, R, S, 180f, yT, yB, inward);
                    AddCornerFan(verts, uvs, trisTop, fx, fz, cx[c] + R, cz[r] + R, R, S, 180f, yT);
                }

                // TL quadrant corner
                if ((tl && !tr && !bl) || (!tl && tr && bl && br))
                {
                    bool inward = !tl;
                    float fx = inward ? cx[c] : cx[c] - R;
                    float fz = inward ? cz[r] : cz[r] + R;
                    AddCurvedWall(verts, uvs, trisWall, cx[c] - R, cz[r] + R, R, S, 270f, yT, yB, inward);
                    AddCornerFan(verts, uvs, trisTop, fx, fz, cx[c] - R, cz[r] + R, R, S, 270f, yT);
                }

                // BL quadrant corner
                if ((bl && !tl && !br) || (!bl && tl && br && tr))
                {
                    bool inward = !bl;
                    float fx = inward ? cx[c] : cx[c] - R;
                    float fz = inward ? cz[r] : cz[r] - R;
                    AddCurvedWall(verts, uvs, trisWall, cx[c] - R, cz[r] - R, R, S, 0f, yT, yB, inward);
                    AddCornerFan(verts, uvs, trisTop, fx, fz, cx[c] - R, cz[r] - R, R, S, 0f, yT);
                }

                // BR quadrant corner
                if ((br && !bl && !tr) || (!br && bl && tr && tl))
                {
                    bool inward = !br;
                    float fx = inward ? cx[c] : cx[c] + R;
                    float fz = inward ? cz[r] : cz[r] - R;
                    AddCurvedWall(verts, uvs, trisWall, cx[c] + R, cz[r] - R, R, S, 90f, yT, yB, inward);
                    AddCornerFan(verts, uvs, trisTop, fx, fz, cx[c] + R, cz[r] - R, R, S, 90f, yT);
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
        /// Triangle fan filling a rounded corner on the top surface.
        /// Center (cx, cz) is the inset corner point. Arc sweeps 90° CCW from startDeg.
        /// Winding gives +Y normal (visible from above).
        /// </summary>
        static void AddCornerFan(List<Vector3> v, List<Vector2> u, List<int> t,
            float fx, float fz, float cx, float cz, float R, int S, float startDeg, float yT)
        {
            int ci = v.Count;
            v.Add(new Vector3(fx, yT, fz));
            u.Add(new Vector2(fx, fz));

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
                int idxA = ci + 1 + k;
                int idxB = ci + 2 + k;
                Vector3 pA = v[idxA];
                Vector3 pB = v[idxB];
                
                // Calculate 2D cross product from fan center (fx, fz) to determine CW winding
                float cross = (pA.x - fx) * (pB.z - fz) - (pA.z - fz) * (pB.x - fx);
                if (cross < 0f)
                {
                    t.Add(ci);
                    t.Add(idxA);
                    t.Add(idxB);
                }
                else
                {
                    t.Add(ci);
                    t.Add(idxB);
                    t.Add(idxA);
                }
            }
        }

        /// <summary>
        /// Curved vertical wall following a 90° arc in XZ, extruded from yT to yB.
        /// Center (cx, cz), arc sweeps CCW from startDeg. Outward or inward normals.
        /// </summary>
        static void AddCurvedWall(List<Vector3> v, List<Vector2> u, List<int> t,
            float cx, float cz, float R, int S, float startDeg, float yT, float yB, bool normalInward)
        {
            var arc = new Vector2[S + 1];
            for (int k = 0; k <= S; k++)
            {
                float a = (startDeg + k * 90f / S) * Mathf.Deg2Rad;
                arc[k] = new Vector2(cx + R * Mathf.Cos(a), cz + R * Mathf.Sin(a));
            }

            for (int k = 0; k < S; k++)
            {
                if (normalInward)
                {
                    AddQuad(v, u, t,
                        arc[k+1].x, yB, arc[k+1].y,
                        arc[k+1].x, yT, arc[k+1].y,
                        arc[k].x,   yT, arc[k].y,
                        arc[k].x,   yB, arc[k].y);
                }
                else
                {
                    AddQuad(v, u, t,
                        arc[k].x,   yB, arc[k].y,
                        arc[k].x,   yT, arc[k].y,
                        arc[k+1].x, yT, arc[k+1].y,
                        arc[k+1].x, yB, arc[k+1].y);
                }
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
