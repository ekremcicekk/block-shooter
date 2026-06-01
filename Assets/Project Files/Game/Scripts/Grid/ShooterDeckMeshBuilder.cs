using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Procedural mesh for the shooter grid platform.
    /// Submesh 0 = top/deck surface, Submesh 1 = side walls.
    ///
    /// Top-down shape (with bevel corners, front open):
    ///
    ///   bv/         \bv
    ///   /   WING     \
    /// (-xO)          (xO)       ← side walls
    ///   \    RECESS  /
    ///    +----------+           ← front open (no wall toward player)
    ///
    /// Y levels:
    ///   yT =  recessDepth  → wing/border tops (above blocks)
    ///   yR =  0            → recess floor (block base level)
    ///   yB = -deckHeight   → platform bottom
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
        public float wingWidth   = 0.8f;
        public float frontBorder = 0.3f;
        public float backBorder  = 0.3f;
        public float deckHeight  = 0.3f;

        [Header("Recess")]
        [Tooltip("How much wings/border rise above the block level (Y=0). Creates tray appearance.")]
        public float recessDepth = 0.05f;
        [Tooltip("Gap between grid edge and inner recess wall.")]
        public float recessPad   = 0.12f;

        [Header("Shape")]
        [Tooltip("Corner chamfer size on outer wall corners.")]
        public float bevelSize   = 0.15f;
        [Tooltip("Leave the player-facing side of the deck open (no front wall).")]
        public bool  openFront   = true;

        private MeshFilter _meshFilter;

        private void Awake() => _meshFilter = GetComponent<MeshFilter>();
        private void Start() => BuildMesh();

        public void BuildMesh()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            _meshFilter.sharedMesh = Generate();
        }

        private Mesh Generate()
        {
            float gHW = gridCols * cellSize * 0.5f;
            float gHD = gridRows * cellSize * 0.5f;
            float bv  = Mathf.Min(bevelSize, Mathf.Min(wingWidth, backBorder) * 0.9f);

            // Key XZ coordinates
            float xI  =  gHW + recessPad;
            float xO  =  xI  + wingWidth;
            float zFI = -(gHD + recessPad);
            float zBI =  gHD + recessPad;
            float zF  =  zFI - frontBorder;
            float zB  =  zBI + backBorder;

            // Key Y levels
            float yT  =  recessDepth;
            float yR  =  0f;
            float yB  = -deckHeight;

            var verts    = new List<Vector3>();
            var uvs      = new List<Vector2>();
            var trisDeck = new List<int>();
            var trisWall = new List<int>();

            // ── Submesh 0: top/deck faces (normal = +Y) ──────────────────────
            // Wings
            HFlat(verts, uvs, trisDeck, -xO, -xI, zF, zB, yT);
            HFlat(verts, uvs, trisDeck,  xI,  xO, zF, zB, yT);
            // Borders around recess
            HFlat(verts, uvs, trisDeck, -xI,  xI, zF,  zFI, yT);
            HFlat(verts, uvs, trisDeck, -xI,  xI, zBI, zB,  yT);
            // Recess floor = block level (covers empty cells)
            HFlat(verts, uvs, trisDeck, -xI,  xI, zFI, zBI, yR);

            // ── Submesh 1: outer straight walls ──────────────────────────────
            // Left outer wall (shortened for corner bevels)
            VWall(verts, uvs, trisWall,
                new Vector3(-xO,yB,zB-bv), new Vector3(-xO,yT,zB-bv),
                new Vector3(-xO,yT,zF+bv), new Vector3(-xO,yB,zF+bv));

            // Right outer wall
            VWall(verts, uvs, trisWall,
                new Vector3(xO,yB,zF+bv), new Vector3(xO,yT,zF+bv),
                new Vector3(xO,yT,zB-bv), new Vector3(xO,yB,zB-bv));

            // Back outer wall (shortened for corner bevels)
            VWall(verts, uvs, trisWall,
                new Vector3(-xO+bv,yB,zB), new Vector3(-xO+bv,yT,zB),
                new Vector3( xO-bv,yT,zB), new Vector3( xO-bv,yB,zB));

            // Front outer wall (only if closed)
            if (!openFront)
            {
                VWall(verts, uvs, trisWall,
                    new Vector3( xO-bv,yB,zF), new Vector3( xO-bv,yT,zF),
                    new Vector3(-xO+bv,yT,zF), new Vector3(-xO+bv,yB,zF));
            }

            // ── Corner bevel wall faces (diagonal chamfers) ───────────────────
            // Back-left corner
            VWall(verts, uvs, trisWall,
                new Vector3(-xO,   yB, zB-bv), new Vector3(-xO,   yT, zB-bv),
                new Vector3(-xO+bv,yT, zB   ), new Vector3(-xO+bv,yB, zB   ));

            // Back-right corner
            VWall(verts, uvs, trisWall,
                new Vector3(xO-bv, yB, zB   ), new Vector3(xO-bv, yT, zB   ),
                new Vector3(xO,    yT, zB-bv), new Vector3(xO,    yB, zB-bv));

            // Front-left corner (end-cap of left wall, visible when front is open)
            VWall(verts, uvs, trisWall,
                new Vector3(-xO,   yB, zF+bv), new Vector3(-xO,   yT, zF+bv),
                new Vector3(-xO+bv,yT, zF   ), new Vector3(-xO+bv,yB, zF   ));

            // Front-right corner
            VWall(verts, uvs, trisWall,
                new Vector3(xO-bv, yB, zF   ), new Vector3(xO-bv, yT, zF   ),
                new Vector3(xO,    yT, zF+bv), new Vector3(xO,    yB, zF+bv));

            // ── Inner recess walls (yR → yT) ─────────────────────────────────
            // Left inner wall (faces +X into recess)
            VWall(verts, uvs, trisWall,
                new Vector3(-xI,yR,zFI), new Vector3(-xI,yT,zFI),
                new Vector3(-xI,yT,zBI), new Vector3(-xI,yR,zBI));

            // Right inner wall
            VWall(verts, uvs, trisWall,
                new Vector3(xI,yR,zBI), new Vector3(xI,yT,zBI),
                new Vector3(xI,yT,zFI), new Vector3(xI,yR,zFI));

            // Back inner wall
            VWall(verts, uvs, trisWall,
                new Vector3(-xI,yR,zBI), new Vector3(-xI,yT,zBI),
                new Vector3( xI,yT,zBI), new Vector3( xI,yR,zBI));

            // Front inner wall (only if closed)
            if (!openFront)
            {
                VWall(verts, uvs, trisWall,
                    new Vector3( xI,yR,zFI), new Vector3( xI,yT,zFI),
                    new Vector3(-xI,yT,zFI), new Vector3(-xI,yR,zFI));
            }

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

        // Horizontal quad at height y. Normal = +Y.
        private static void HFlat(List<Vector3> v, List<Vector2> u, List<int> t,
            float x1, float x2, float z1, float z2, float y)
        {
            int b = v.Count;
            v.Add(new Vector3(x1, y, z1)); v.Add(new Vector3(x2, y, z1));
            v.Add(new Vector3(x2, y, z2)); v.Add(new Vector3(x1, y, z2));
            u.Add(new Vector2(x1, z1)); u.Add(new Vector2(x2, z1));
            u.Add(new Vector2(x2, z2)); u.Add(new Vector2(x1, z2));
            t.Add(b); t.Add(b+2); t.Add(b+1);
            t.Add(b); t.Add(b+3); t.Add(b+2);
        }

        // Vertical quad. bl/tl/tr/br = bottom-left/top-left/top-right/bottom-right
        // when viewed from OUTSIDE (normal points outward).
        private static void VWall(List<Vector3> v, List<Vector2> u, List<int> t,
            Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br)
        {
            int b = v.Count;
            v.Add(bl); v.Add(tl); v.Add(tr); v.Add(br);
            float w = Vector3.Distance(bl, br);
            float h = Vector3.Distance(bl, tl);
            u.Add(new Vector2(0, 0));  u.Add(new Vector2(0, h));
            u.Add(new Vector2(w, h));  u.Add(new Vector2(w, 0));
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
