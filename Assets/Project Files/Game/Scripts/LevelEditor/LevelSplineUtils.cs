#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace BlockShooter.Editor
{
    public static class LevelSplineUtils
    {
        public static float CalculateSplineLength(
            List<Vector3> knots,
            List<Vector3> tangentsIn,
            List<Vector3> tangentsOut,
            List<TangentMode> tangentModes,
            bool isClosed)
        {
            if (knots.Count < 2) return 0f;
            
            var tempSpline = new Spline();
            for (int i = 0; i < knots.Count; i++)
            {
                var k = knots[i];
                var tanIn  = i < tangentsIn.Count ? (float3)tangentsIn[i] : float3.zero;
                var tanOut = i < tangentsOut.Count ? (float3)tangentsOut[i] : float3.zero;
                tempSpline.Add(new BezierKnot((float3)k, tanIn, tanOut));
            }
            tempSpline.Closed = isClosed;
            for (int i = 0; i < tempSpline.Count; i++)
            {
                var mode = i < tangentModes.Count ? tangentModes[i] : TangentMode.AutoSmooth;
                tempSpline.SetTangentMode(i, mode);
            }
            return SplineUtility.CalculateLength(tempSpline, Matrix4x4.identity);
        }

        public static void MakeSymmetric(
            List<Vector3> knots,
            List<Vector3> tangentsIn,
            List<Vector3> tangentsOut,
            List<TangentMode> tangentModes)
        {
            if (knots.Count < 3) return;

            int N = knots.Count;
            
            // Index 0 is always a center point (locked at x = 0)
            knots[0] = new Vector3(0f, knots[0].y, knots[0].z);

            if (N % 2 == 0)
            {
                int mid = N / 2;
                // Middle knot is also on the axis of symmetry (x = 0)
                knots[mid] = new Vector3(0f, knots[mid].y, knots[mid].z);

                // Mirror left-side knots (indices N - i) to right-side knots (indices i)
                for (int i = 1; i < mid; i++)
                {
                    int opp = N - i;
                    knots[i] = new Vector3(-knots[opp].x, knots[opp].y, knots[opp].z);
                    tangentsIn[i] = new Vector3(-tangentsOut[opp].x, tangentsOut[opp].y, tangentsOut[opp].z);
                    tangentsOut[i] = new Vector3(-tangentsIn[opp].x, tangentsIn[opp].y, tangentsIn[opp].z);
                    tangentModes[i] = tangentModes[opp];
                }
            }
            else
            {
                // Odd number of knots: index 0 is center, pair the rest symmetrically
                int limit = (N - 1) / 2;
                for (int i = 1; i <= limit; i++)
                {
                    int opp = N - i;
                    knots[i] = new Vector3(-knots[opp].x, knots[opp].y, knots[opp].z);
                    tangentsIn[i] = new Vector3(-tangentsOut[opp].x, tangentsOut[opp].y, tangentsOut[opp].z);
                    tangentsOut[i] = new Vector3(-tangentsIn[opp].x, tangentsIn[opp].y, tangentsIn[opp].z);
                    tangentModes[i] = tangentModes[opp];
                }
            }
        }

        public static void FlipHorizontally(
            List<Vector3> knots,
            List<Vector3> tangentsIn,
            List<Vector3> tangentsOut,
            List<TangentMode> tangentModes)
        {
            // 1. Flip X coordinates of all knots
            for (int i = 0; i < knots.Count; i++)
            {
                knots[i] = new Vector3(-knots[i].x, knots[i].y, knots[i].z);
            }

            // 2. Reverse knots to preserve conveyor flow direction (clockwise flow)
            knots.Reverse();

            // 3. Reverse and swap tangents (In becomes -Out mirrored, Out becomes -In mirrored)
            var oldIn = new List<Vector3>(tangentsIn);
            var oldOut = new List<Vector3>(tangentsOut);
            
            int N = knots.Count;
            for (int i = 0; i < N; i++)
            {
                int opp = N - 1 - i;
                
                // Mirror X and negate the vector due to reverse flow direction:
                // mirror_oldOut = (-x, y, z)
                // -mirror_oldOut = (x, -y, -z)
                Vector3 origOut = opp < oldOut.Count ? oldOut[opp] : Vector3.zero;
                Vector3 origIn = opp < oldIn.Count ? oldIn[opp] : Vector3.zero;

                if (i < tangentsIn.Count) 
                    tangentsIn[i] = new Vector3(-origOut.x, origOut.y, origOut.z);
                if (i < tangentsOut.Count) 
                    tangentsOut[i] = new Vector3(-origIn.x, origIn.y, origIn.z);
            }

            // 4. Reverse tangent modes
            tangentModes.Reverse();

            // 5. Rotate all right by 1 to restore index 0 to the original first knot position
            RotateRight(knots);
            RotateRight(tangentsIn);
            RotateRight(tangentsOut);
            RotateRight(tangentModes);
        }

        private static void RotateRight<T>(List<T> list)
        {
            if (list.Count <= 1) return;
            T last = list[list.Count - 1];
            for (int i = list.Count - 1; i > 0; i--)
            {
                list[i] = list[i - 1];
            }
            list[0] = last;
        }

        public static Vector3 GetMouseGroundHit(Vector2 mousePos)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
            if (Mathf.Abs(ray.direction.y) < 0.0001f) return Vector3.positiveInfinity;
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) return Vector3.positiveInfinity;
            return ray.origin + ray.direction * t;
        }

        public static int FindInsertIndex(Vector3 pos, List<Vector3> knots)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < knots.Count; i++)
            {
                int nxt = (i + 1) % knots.Count;
                Vector3 mid = (knots[i] + knots[nxt]) * .5f;
                float d = Vector3.Distance(pos, mid);
                if (d < bestDist) { bestDist = d; best = nxt; }
            }
            return best;
        }

        public static Vector3[] GetWireCubeVerts(Vector3 center, Vector3 size)
        {
            float hx = size.x * .5f, hz = size.z * .5f;
            return new[]
            {
                center + new Vector3(-hx, 0, -hz),
                center + new Vector3(+hx, 0, -hz),
                center + new Vector3(+hx, 0, +hz),
                center + new Vector3(-hx, 0, +hz),
            };
        }

        public static BranchPathData CreateMirroredBranch(BranchPathData source)
        {
            return new BranchPathData
            {
                branchName = $"{source.branchName}_Mirrored",
                mergeT = 1.0f - source.mergeT,
                connectFromLeft = !source.connectFromLeft,
                
                splineKnots = source.splineKnots.Select(k => new Vector3(-k.x, k.y, k.z)).ToList(),
                splineTangentsIn = source.splineTangentsIn.Select(t => new Vector3(-t.x, t.y, t.z)).ToList(),
                splineTangentsOut = source.splineTangentsOut.Select(t => new Vector3(-t.x, t.y, t.z)).ToList(),
                splineTangentModes = new List<int>(source.splineTangentModes),
                
                groups = source.groups.Select(g => new LevelConveyorGroup
                {
                    color = g.color,
                    rowCount = g.rowCount,
                    laneCount = g.laneCount
                }).ToList()
            };
        }
    }
}
#endif
