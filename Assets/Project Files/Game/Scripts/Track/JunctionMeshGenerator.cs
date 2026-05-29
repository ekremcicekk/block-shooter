using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    /// <summary>
    /// Generates a T-junction mesh where a FeederPath meets the main ConveyorPath.
    /// Placed at a ConnectionPoint. Auto-orients to match both path tangents.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class JunctionMeshGenerator : MonoBehaviour
    {
        [Header("References")]
        public SplineContainer mainSpline;
        public SplineContainer feederSpline;
        [Range(0f, 1f)] public float mainSplineT = 0.5f;

        [Header("Dimensions")]
        public float trackWidth = 2.5f;
        public float wallHeight = 0.28f;
        public float wallThickness = 0.08f;
        public float junctionLength = 1.2f;

        [Header("Material")]
        public Material junctionMaterial;

        private MeshFilter _meshFilter;

        private void Awake() => _meshFilter = GetComponent<MeshFilter>();
        private void Start() => BuildJunction();

        public void BuildJunction()
        {
            if (mainSpline == null) return;
            _meshFilter = GetComponent<MeshFilter>();
            _meshFilter.sharedMesh = GenerateJunctionMesh();

            var mr = GetComponent<MeshRenderer>();
            if (mr != null && junctionMaterial != null)
                mr.sharedMaterial = junctionMaterial;
        }

        private Mesh GenerateJunctionMesh()
        {
            // Get main path direction at junction
            mainSpline.Spline.Evaluate(mainSplineT, out var mainPos, out var mainTangent, out var mainUp);
            Vector3 fwd = transform.InverseTransformDirection(((Vector3)mainTangent).normalized);
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, fwd).normalized;

            float hw = trackWidth * 0.5f;
            float jl = junctionLength;

            // Build a T-shape flat floor + outer walls
            // Main axis: fwd direction
            // Feeder axis: -right (coming from the side)

            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            var uvs = new System.Collections.Generic.List<Vector2>();

            // --- Main pass-through section (rectangle along fwd) ---
            AddFlatRect(verts, tris, uvs,
                -fwd * jl - right * hw,
                -fwd * jl + right * hw,
                fwd * jl + right * hw,
                fwd * jl - right * hw);

            // --- Feeder stem (rectangle along -right) ---
            AddFlatRect(verts, tris, uvs,
                -right * jl - fwd * hw,
                -right * jl + fwd * hw,
                Vector3.zero + fwd * hw,
                Vector3.zero - fwd * hw);

            // Outer walls along main direction
            AddWallStrip(verts, tris, uvs, -fwd * jl - right * hw, fwd * jl - right * hw, up, wallHeight, wallThickness, right);
            AddWallStrip(verts, tris, uvs, -fwd * jl + right * hw, fwd * jl + right * hw, up, wallHeight, wallThickness, -right);

            // Outer wall along feeder stem
            AddWallStrip(verts, tris, uvs, -right * jl - fwd * hw, -right * hw - fwd * hw, up, wallHeight, wallThickness, fwd);
            AddWallStrip(verts, tris, uvs, -right * jl + fwd * hw, -right * hw + fwd * hw, up, wallHeight, wallThickness, -fwd);

            var mesh = new Mesh { name = "TJunction" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void AddFlatRect(System.Collections.Generic.List<Vector3> v,
            System.Collections.Generic.List<int> t, System.Collections.Generic.List<Vector2> u,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            u.Add(Vector2.zero); u.Add(Vector2.right); u.Add(Vector2.one); u.Add(Vector2.up);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        private void AddWallStrip(System.Collections.Generic.List<Vector3> v,
            System.Collections.Generic.List<int> t, System.Collections.Generic.List<Vector2> u,
            Vector3 start, Vector3 end, Vector3 up, float height, float thickness, Vector3 outDir)
        {
            int i = v.Count;
            v.Add(start + outDir * thickness);
            v.Add(end + outDir * thickness);
            v.Add(end + outDir * thickness + up * height);
            v.Add(start + outDir * thickness + up * height);
            u.Add(Vector2.zero); u.Add(Vector2.right); u.Add(Vector2.one); u.Add(Vector2.up);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) BuildJunction();
        }

        private void OnDrawGizmosSelected()
        {
            if (mainSpline == null) return;
            mainSpline.Spline.Evaluate(mainSplineT, out var pos, out _, out _);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(mainSpline.transform.TransformPoint(pos), 0.15f);
        }
#endif
    }
}
