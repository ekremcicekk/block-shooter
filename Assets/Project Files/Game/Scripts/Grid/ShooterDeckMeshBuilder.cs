using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Generates a raised tile for every EMPTY grid cell.
    /// Cells that contain a shooter block or door are left flat (no tile).
    /// Submesh 0 = top face, Submesh 1 = side walls.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ShooterDeckMeshBuilder : MonoBehaviour
    {
        public int   gridCols   = 4;
        public int   gridRows   = 2;
        public float cellSize   = 1.2f;
        [Tooltip("Height the tile rises from the ground")]
        public float tileHeight = 0.15f;
        [Tooltip("Gap between tile edge and cell boundary")]
        public float tilePad    = 0.04f;

        private MeshFilter _mf;
        private void Awake() => _mf = GetComponent<MeshFilter>();

        /// <summary>
        /// isEmpty[col, row] = true  → that cell is empty, gets a raised tile.
        /// isEmpty[col, row] = false → cell has a block/door, no tile generated.
        /// </summary>
        public void BuildMesh(bool[,] isEmpty)
        {
            if (_mf == null) _mf = GetComponent<MeshFilter>();

            float hw  = (gridCols - 1) * cellSize * 0.5f;
            float hd  = (gridRows - 1) * cellSize * 0.5f;
            float hs  = cellSize * 0.5f - tilePad;
            float yT  = 0f;
            float yB  = -tileHeight;

            var verts  = new List<Vector3>();
            var uvs    = new List<Vector2>();
            var trisTop  = new List<int>();
            var trisSide = new List<int>();

            for (int r = 0; r < gridRows; r++)
            for (int c = 0; c < gridCols; c++)
            {
                if (!isEmpty[c, r]) continue;

                float cx = -hw + c * cellSize;
                float cz = -hd + r * cellSize;

                AddTop (verts, uvs, trisTop,  cx, cz, hs, yT);
                AddSides(verts, uvs, trisSide, cx, cz, hs, yT, yB);
            }

            var mesh = new Mesh { name = "ShooterDeck" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trisTop,  0);
            mesh.SetTriangles(trisSide, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _mf.sharedMesh = mesh;
        }

        // ── Geometry helpers ──────────────────────────────────────────────────

        private static void AddTop(List<Vector3> v, List<Vector2> u, List<int> t,
            float cx, float cz, float hs, float y)
        {
            int b = v.Count;
            v.Add(new Vector3(cx-hs, y, cz-hs));
            v.Add(new Vector3(cx+hs, y, cz-hs));
            v.Add(new Vector3(cx+hs, y, cz+hs));
            v.Add(new Vector3(cx-hs, y, cz+hs));
            u.Add(new Vector2(0,0)); u.Add(new Vector2(1,0));
            u.Add(new Vector2(1,1)); u.Add(new Vector2(0,1));
            // +Y normal winding
            t.Add(b); t.Add(b+2); t.Add(b+1);
            t.Add(b); t.Add(b+3); t.Add(b+2);
        }

        private static void AddSides(List<Vector3> v, List<Vector2> u, List<int> t,
            float cx, float cz, float hs, float yT, float yB)
        {
            // Front  (normal −Z)
            Quad(v,u,t, cx-hs,yT,cz-hs,  cx+hs,yT,cz-hs,  cx+hs,yB,cz-hs,  cx-hs,yB,cz-hs);
            // Back   (normal +Z)
            Quad(v,u,t, cx+hs,yT,cz+hs,  cx-hs,yT,cz+hs,  cx-hs,yB,cz+hs,  cx+hs,yB,cz+hs);
            // Left   (normal −X)
            Quad(v,u,t, cx-hs,yT,cz+hs,  cx-hs,yT,cz-hs,  cx-hs,yB,cz-hs,  cx-hs,yB,cz+hs);
            // Right  (normal +X)
            Quad(v,u,t, cx+hs,yT,cz-hs,  cx+hs,yT,cz+hs,  cx+hs,yB,cz+hs,  cx+hs,yB,cz-hs);
        }

        // Vertices: TL, TR, BR, BL (from outside / normal direction)
        private static void Quad(List<Vector3> v, List<Vector2> u, List<int> t,
            float x0,float y0,float z0,
            float x1,float y1,float z1,
            float x2,float y2,float z2,
            float x3,float y3,float z3)
        {
            int b = v.Count;
            v.Add(new Vector3(x0,y0,z0)); v.Add(new Vector3(x1,y1,z1));
            v.Add(new Vector3(x2,y2,z2)); v.Add(new Vector3(x3,y3,z3));
            u.Add(new Vector2(0,1)); u.Add(new Vector2(1,1));
            u.Add(new Vector2(1,0)); u.Add(new Vector2(0,0));
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
                    var empty = new bool[b.gridCols, b.gridRows];
                    for (int c = 0; c < b.gridCols; c++)
                    for (int r = 0; r < b.gridRows; r++)
                        empty[c, r] = true;
                    b._mf = b.GetComponent<MeshFilter>();
                    b.BuildMesh(empty);
                    UnityEditor.EditorUtility.SetDirty(b.gameObject);
                }
            }
        }
#endif
    }
}
