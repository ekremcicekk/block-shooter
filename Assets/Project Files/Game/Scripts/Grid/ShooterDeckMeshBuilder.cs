using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Procedural mesh for the shooter grid platform.
    /// Two submeshes: 0 = top/deck surface, 1 = side walls.
    ///
    /// Cross-section (side view, blocks at Y=0):
    ///
    ///        wing     recess (grid)    wing
    ///   +---------+                +---------+   ← Y = +recessDepth (border/wing top)
    ///   |         +----------------+         |   ← Y = 0           (recess floor = block level)
    ///   |         |    (blocks)    |         |
    ///   |         |                |         |
    ///   +---------+----------------+---------+   ← Y = -deckHeight (bottom)
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ShooterDeckMeshBuilder : MonoBehaviour
    {
        [Header("Grid Dimensions")]
        public int   gridCols    = 4;
        public int   gridRows    = 2;
        public float cellSize    = 1.2f;

        [Header("Platform Size")]
        [Tooltip("Extra platform width on each side of the grid")]
        public float wingWidth   = 0.8f;
        [Tooltip("Extra platform depth in front of the grid (toward player)")]
        public float frontBorder = 0.3f;
        [Tooltip("Extra platform depth behind the grid")]
        public float backBorder  = 0.3f;
        [Tooltip("Total platform height below block level (Y=0)")]
        public float deckHeight  = 0.3f;

        [Header("Recess")]
        [Tooltip("How far wings/border rise ABOVE the block level (Y=0). Creates tray appearance.")]
        public float recessDepth = 0.05f;
        [Tooltip("Horizontal gap between grid edge and inner recess wall")]
        public float recessPad   = 0.12f;

        private MeshFilter _meshFilter;

        private void Awake() => _meshFilter = GetComponent<MeshFilter>();
        private void Start() => BuildMesh();

        public void BuildMesh()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            _meshFilter.mesh = Generate();
        }

        private Mesh Generate()
        {
            // Grid area half-extents (edge to edge)
            float gHW = gridCols * cellSize * 0.5f;
            float gHD = gridRows * cellSize * 0.5f;

            // X key values
            float xI =  gHW + recessPad;          // inner recess wall
            float xO =  xI  + wingWidth;           // outer platform edge

            // Z key values (−Z = toward player / front)
            float zFI = -(gHD + recessPad);        // front inner recess wall
            float zBI =  gHD + recessPad;          // back  inner recess wall
            float zF  =  zFI - frontBorder;        // front outer edge
            float zB  =  zBI + backBorder;         // back  outer edge

            // Y key values
            float yT  =  recessDepth;              // wing/border top (above blocks)
            float yR  =  0f;                       // recess floor = block level
            float yB  = -deckHeight;               // platform bottom

            var verts    = new List<Vector3>();
            var uvs      = new List<Vector2>();
            var trisDeck = new List<int>();
            var trisWall = new List<int>();

            // ── Submesh 0: deck top faces (normal = +Y) ──────────────────────
            HFlat(verts, uvs, trisDeck, -xO, -xI, zF,  zB,  yT);  // left wing
            HFlat(verts, uvs, trisDeck,  xI,  xO, zF,  zB,  yT);  // right wing
            HFlat(verts, uvs, trisDeck, -xI,  xI, zF,  zFI, yT);  // front border
            HFlat(verts, uvs, trisDeck, -xI,  xI, zBI, zB,  yT);  // back border
            HFlat(verts, uvs, trisDeck, -xI,  xI, zFI, zBI, yR);  // recess floor (block level)

            // ── Submesh 1: vertical wall faces ───────────────────────────────
            // Outer walls (full height yB→yT)
            VWall(verts, uvs, trisWall,
                new Vector3(-xO,yB,zB), new Vector3(-xO,yT,zB),
                new Vector3(-xO,yT,zF), new Vector3(-xO,yB,zF));  // left outer  (normal −X)

            VWall(verts, uvs, trisWall,
                new Vector3(xO,yB,zF), new Vector3(xO,yT,zF),
                new Vector3(xO,yT,zB), new Vector3(xO,yB,zB));    // right outer (normal +X)

            VWall(verts, uvs, trisWall,
                new Vector3(xO,yB,zF), new Vector3(xO,yT,zF),
                new Vector3(-xO,yT,zF), new Vector3(-xO,yB,zF));  // front outer (normal −Z)

            VWall(verts, uvs, trisWall,
                new Vector3(-xO,yB,zB), new Vector3(-xO,yT,zB),
                new Vector3(xO,yT,zB),  new Vector3(xO,yB,zB));   // back outer  (normal +Z)

            // Inner recess walls (yR→yT only)
            VWall(verts, uvs, trisWall,
                new Vector3(-xI,yR,zFI), new Vector3(-xI,yT,zFI),
                new Vector3(-xI,yT,zBI), new Vector3(-xI,yR,zBI)); // left inner  (normal +X)

            VWall(verts, uvs, trisWall,
                new Vector3(xI,yR,zBI), new Vector3(xI,yT,zBI),
                new Vector3(xI,yT,zFI), new Vector3(xI,yR,zFI));  // right inner (normal −X)

            VWall(verts, uvs, trisWall,
                new Vector3(xI,yR,zFI), new Vector3(xI,yT,zFI),
                new Vector3(-xI,yT,zFI), new Vector3(-xI,yR,zFI)); // front inner (normal +Z)

            VWall(verts, uvs, trisWall,
                new Vector3(-xI,yR,zBI), new Vector3(-xI,yT,zBI),
                new Vector3(xI,yT,zBI),  new Vector3(xI,yR,zBI)); // back inner  (normal −Z)

            var mesh = new Mesh { name = "ShooterDeck" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trisDeck, 0);
            mesh.SetTriangles(trisWall, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Horizontal quad at height y, +Y normal.
        // Vertices: (x1,y,z1),(x2,y,z1),(x2,y,z2),(x1,y,z2)
        private static void HFlat(List<Vector3> v, List<Vector2> u, List<int> t,
            float x1, float x2, float z1, float z2, float y)
        {
            int b = v.Count;
            v.Add(new Vector3(x1, y, z1));
            v.Add(new Vector3(x2, y, z1));
            v.Add(new Vector3(x2, y, z2));
            v.Add(new Vector3(x1, y, z2));
            u.Add(new Vector2(x1, z1)); u.Add(new Vector2(x2, z1));
            u.Add(new Vector2(x2, z2)); u.Add(new Vector2(x1, z2));
            t.Add(b); t.Add(b+2); t.Add(b+1);
            t.Add(b); t.Add(b+3); t.Add(b+2);
        }

        // Vertical quad. Pass corners so that bl→tl→tr→br is counterclockwise
        // when viewed from the OUTSIDE (normal direction).
        // bl=bottom-left, tl=top-left, tr=top-right, br=bottom-right (from outside view).
        private static void VWall(List<Vector3> v, List<Vector2> u, List<int> t,
            Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br)
        {
            int b = v.Count;
            v.Add(bl); v.Add(tl); v.Add(tr); v.Add(br);
            // UV: U along horizontal, V along vertical (0=bottom, 1=top)
            float w = Vector3.Distance(bl, br);
            float h = Vector3.Distance(bl, tl);
            u.Add(new Vector2(0, 0));    u.Add(new Vector2(0, h));
            u.Add(new Vector2(w, h));    u.Add(new Vector2(w, 0));
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
                if (GUILayout.Button("Rebuild Mesh", GUILayout.Height(34)))
                {
                    var b = (ShooterDeckMeshBuilder)target;
                    b._meshFilter = b.GetComponent<MeshFilter>();
                    b.BuildMesh();
                    UnityEditor.EditorUtility.SetDirty(b.gameObject);
                }
            }
        }
#endif
    }
}
