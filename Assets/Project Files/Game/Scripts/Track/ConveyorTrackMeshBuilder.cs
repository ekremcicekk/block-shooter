using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    /// <summary>
    /// Generates a smooth procedural mesh along the spline.
    /// Replaces segment-tiling for the track visual — no corner gaps.
    /// Add this component to the same GameObject as SplineContainer.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ConveyorTrackMeshBuilder : MonoBehaviour
    {
        [Header("Track Shape")]
        public float trackWidth    = 2.8f;
        public float trackThickness = 0.06f;
        public float railHeight    = 0.12f;
        public float railWidth     = 0.08f;

        [Header("Quality")]
        [Range(60, 600)] public int resolution = 240;

        private SplineContainer _spline;
        private MeshFilter _meshFilter;

        private void Awake()
        {
            _spline     = GetComponent<SplineContainer>();
            _meshFilter = GetComponent<MeshFilter>();
        }

        private void Start() => BuildMesh();

        // ── Mesh Generation ───────────────────────────────────────────────────

        public void BuildMesh()
        {
            if (_spline == null) _spline = GetComponent<SplineContainer>();
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();

            _meshFilter.mesh = GenerateMesh();
        }

        private Mesh GenerateMesh()
        {
            int n = resolution;
            var pts   = new Vector3[n + 1];
            var right = new Vector3[n + 1];

            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n;
                _spline.Spline.Evaluate(t, out var pos, out var tan, out var up);

                Vector3 fwd   = transform.TransformDirection((Vector3)tan).normalized;
                Vector3 upDir = ((Vector3)up == Vector3.zero) ? Vector3.up : ((Vector3)up).normalized;

                pts[i]   = transform.TransformPoint(pos);
                right[i] = Vector3.Cross(upDir, fwd).normalized;
            }

            var verts = new List<Vector3>();
            var uvs   = new List<Vector2>();
            var tris  = new List<int>();

            float hw = trackWidth * 0.5f;

            for (int i = 0; i < n; i++)
            {
                float u0 = (float)i / n * 8f;       // tile UV along length
                float u1 = (float)(i + 1) / n * 8f;

                Vector3 lA = pts[i]   - right[i]   * hw;
                Vector3 rA = pts[i]   + right[i]   * hw;
                Vector3 lB = pts[i+1] - right[i+1] * hw;
                Vector3 rB = pts[i+1] + right[i+1] * hw;

                // ── Top surface ──────────────────────────────────────────
                AddQuad(verts, uvs, tris,
                    lA, rA, lB, rB,
                    new Vector2(0, u0), new Vector2(1, u0),
                    new Vector2(0, u1), new Vector2(1, u1));

                // ── Bottom surface ───────────────────────────────────────
                Vector3 down = Vector3.down * trackThickness;
                AddQuad(verts, uvs, tris,
                    rA + down, lA + down, rB + down, lB + down,
                    new Vector2(1, u0), new Vector2(0, u0),
                    new Vector2(1, u1), new Vector2(0, u1));

                // ── Left side ────────────────────────────────────────────
                AddQuad(verts, uvs, tris,
                    lA + down, lA, lB + down, lB,
                    new Vector2(0, u0), new Vector2(0, u0),
                    new Vector2(0, u1), new Vector2(0, u1));

                // ── Right side ───────────────────────────────────────────
                AddQuad(verts, uvs, tris,
                    rA, rA + down, rB, rB + down,
                    new Vector2(1, u0), new Vector2(1, u0),
                    new Vector2(1, u1), new Vector2(1, u1));

                // ── Left rail ────────────────────────────────────────────
                if (railHeight > 0f)
                {
                    Vector3 rl = right[i]   * railWidth;
                    Vector3 rl1 = right[i+1] * railWidth;
                    Vector3 up0 = Vector3.up * railHeight;

                    // Left rail outer face
                    AddQuad(verts, uvs, tris,
                        lA - rl,        lA,        lB - rl1,      lB,
                        new Vector2(0, u0), new Vector2(0.1f, u0),
                        new Vector2(0, u1), new Vector2(0.1f, u1));
                    // Left rail top
                    AddQuad(verts, uvs, tris,
                        lA - rl + up0, lA + up0,  lB - rl1 + up0, lB + up0,
                        new Vector2(0, u0), new Vector2(0.1f, u0),
                        new Vector2(0, u1), new Vector2(0.1f, u1));
                    // Left rail inner face
                    AddQuad(verts, uvs, tris,
                        lA + up0,        lA - rl + up0, lB + up0,       lB - rl1 + up0,
                        new Vector2(0.1f, u0), new Vector2(0, u0),
                        new Vector2(0.1f, u1), new Vector2(0, u1));

                    // Right rail outer face
                    Vector3 rr  = right[i]   * railWidth;
                    Vector3 rr1 = right[i+1] * railWidth;
                    AddQuad(verts, uvs, tris,
                        rA,        rA + rr,        rB,        rB + rr1,
                        new Vector2(0.9f, u0), new Vector2(1, u0),
                        new Vector2(0.9f, u1), new Vector2(1, u1));
                    AddQuad(verts, uvs, tris,
                        rA + up0,  rA + rr + up0,  rB + up0,  rB + rr1 + up0,
                        new Vector2(0.9f, u0), new Vector2(1, u0),
                        new Vector2(0.9f, u1), new Vector2(1, u1));
                    AddQuad(verts, uvs, tris,
                        rA + rr + up0, rA + up0,     rB + rr1 + up0, rB + up0,
                        new Vector2(1, u0), new Vector2(0.9f, u0),
                        new Vector2(1, u1), new Vector2(0.9f, u1));
                }
            }

            var mesh = new Mesh { name = "ConveyorTrack_Generated" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(
            List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            int start = verts.Count;
            verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
            uvs.Add(uv0);  uvs.Add(uv1);  uvs.Add(uv2);  uvs.Add(uv3);
            tris.Add(start);     tris.Add(start + 2); tris.Add(start + 1);
            tris.Add(start + 1); tris.Add(start + 2); tris.Add(start + 3);
        }

        // ── Editor Helper ─────────────────────────────────────────────────────
#if UNITY_EDITOR
        [UnityEditor.CustomEditor(typeof(ConveyorTrackMeshBuilder))]
        public class Editor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();
                UnityEditor.EditorGUILayout.Space(6);
                if (GUILayout.Button("Rebuild Mesh", GUILayout.Height(32)))
                {
                    var t = (ConveyorTrackMeshBuilder)target;
                    t.BuildMesh();
                    UnityEditor.EditorUtility.SetDirty(t);
                }
            }
        }
#endif
    }
}
