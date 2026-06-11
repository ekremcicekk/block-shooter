#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter.Editor
{
    public static class LevelSplinePresets
    {
        public static void GeneratePreset(
            int presetIndex,
            float hw,
            float fz,
            float d,
            out List<Vector3> knots,
            out List<Vector3> tangentsIn,
            out List<Vector3> tangentsOut,
            out List<TangentMode> tangentModes)
        {
            knots = new List<Vector3>();
            tangentsIn = new List<Vector3>();
            tangentsOut = new List<Vector3>();
            tangentModes = new List<TangentMode>();

            switch (presetIndex)
            {
                case 0: // Oval (Perfect Ellipse)
                    {
                        float b = d * 0.5f;
                        float k = 0.5522847f; // Bezier curve constant for circle/ellipse
                        
                        knots.Add(new Vector3(0f, 0f, fz));
                        knots.Add(new Vector3(+hw, 0f, fz + b));
                        knots.Add(new Vector3(0f, 0f, fz + d));
                        knots.Add(new Vector3(-hw, 0f, fz + b));

                        // Knot 0 (Bottom Center)
                        tangentsIn.Add(new Vector3(-hw * k, 0f, 0f));
                        tangentsOut.Add(new Vector3(hw * k, 0f, 0f));
                        tangentModes.Add(TangentMode.Mirrored);

                        // Knot 1 (Right Side)
                        tangentsIn.Add(new Vector3(0f, 0f, -b * k));
                        tangentsOut.Add(new Vector3(0f, 0f, b * k));
                        tangentModes.Add(TangentMode.Mirrored);

                        // Knot 2 (Top Center)
                        tangentsIn.Add(new Vector3(hw * k, 0f, 0f));
                        tangentsOut.Add(new Vector3(-hw * k, 0f, 0f));
                        tangentModes.Add(TangentMode.Mirrored);

                        // Knot 3 (Left Side)
                        tangentsIn.Add(new Vector3(0f, 0f, b * k));
                        tangentsOut.Add(new Vector3(0f, 0f, -b * k));
                        tangentModes.Add(TangentMode.Mirrored);
                    }
                    break;

                case 1: // Wide Capsule / Stadium (Straight parallel sides + perfect circular caps)
                    {
                        float r = Mathf.Min(hw, d * 0.5f);
                        float k = 0.5522847f;
                        float straightHeight = d - 2 * r;

                        knots.Add(new Vector3(0f, 0f, fz)); // 0: Bottom Center
                        knots.Add(new Vector3(+hw, 0f, fz + r)); // 1: Bottom Right Cap End / Straight Start
                        knots.Add(new Vector3(+hw, 0f, fz + d - r)); // 2: Straight End / Top Right Cap Start
                        knots.Add(new Vector3(0f, 0f, fz + d)); // 3: Top Center
                        knots.Add(new Vector3(-hw, 0f, fz + d - r)); // 4: Top Left Cap End / Straight Start
                        knots.Add(new Vector3(-hw, 0f, fz + r)); // 5: Straight End / Bottom Left Cap Start

                        // Knot 0 (Bottom Center)
                        tangentsIn.Add(new Vector3(-hw * k, 0f, 0f));
                        tangentsOut.Add(new Vector3(hw * k, 0f, 0f));
                        tangentModes.Add(TangentMode.Mirrored);

                        // Knot 1 (Bottom Right)
                        tangentsIn.Add(new Vector3(0f, 0f, -r * k));
                        tangentsOut.Add(new Vector3(0f, 0f, straightHeight * 0.33f)); // Points straight up
                        tangentModes.Add(TangentMode.Broken);

                        // Knot 2 (Top Right)
                        tangentsIn.Add(new Vector3(0f, 0f, -straightHeight * 0.33f)); // Points straight down
                        tangentsOut.Add(new Vector3(0f, 0f, r * k));
                        tangentModes.Add(TangentMode.Broken);

                        // Knot 3 (Top Center)
                        tangentsIn.Add(new Vector3(hw * k, 0f, 0f));
                        tangentsOut.Add(new Vector3(-hw * k, 0f, 0f));
                        tangentModes.Add(TangentMode.Mirrored);

                        // Knot 4 (Top Left)
                        tangentsIn.Add(new Vector3(0f, 0f, r * k));
                        tangentsOut.Add(new Vector3(0f, 0f, -straightHeight * 0.33f)); // Points straight down
                        tangentModes.Add(TangentMode.Broken);

                        // Knot 5 (Bottom Left)
                        tangentsIn.Add(new Vector3(0f, 0f, straightHeight * 0.33f)); // Points straight up
                        tangentsOut.Add(new Vector3(0f, 0f, -r * k));
                        tangentModes.Add(TangentMode.Broken);
                    }
                    break;

                case 2: // Wavy Loop (Capsule loop with elegant waves on parallel sides)
                    {
                        knots.Add(new Vector3(0f, 0f, fz));
                        knots.Add(new Vector3(+hw * 0.8f, 0f, fz + d * 0.2f));
                        knots.Add(new Vector3(+hw * 1.2f, 0f, fz + d * 0.5f));
                        knots.Add(new Vector3(+hw * 0.8f, 0f, fz + d * 0.8f));
                        knots.Add(new Vector3(0f, 0f, fz + d));
                        knots.Add(new Vector3(-hw * 0.8f, 0f, fz + d * 0.8f));
                        knots.Add(new Vector3(-hw * 1.2f, 0f, fz + d * 0.5f));
                        knots.Add(new Vector3(-hw * 0.8f, 0f, fz + d * 0.2f));

                        for (int i = 0; i < 8; i++)
                        {
                            tangentsIn.Add(Vector3.zero);
                            tangentsOut.Add(Vector3.zero);
                            tangentModes.Add(TangentMode.AutoSmooth);
                        }
                    }
                    break;

                case 3: // Heart Loop
                    {
                        knots.Add(new Vector3(0f, 0f, fz));
                        knots.Add(new Vector3(+hw * 0.9f, 0f, fz + d * 0.35f));
                        knots.Add(new Vector3(+hw * 0.7f, 0f, fz + d * 0.85f));
                        knots.Add(new Vector3(0f, 0f, fz + d * 0.65f));
                        knots.Add(new Vector3(-hw * 0.7f, 0f, fz + d * 0.85f));
                        knots.Add(new Vector3(-hw * 0.9f, 0f, fz + d * 0.35f));

                        for (int i = 0; i < 6; i++)
                        {
                            tangentsIn.Add(Vector3.zero);
                            tangentsOut.Add(Vector3.zero);
                            tangentModes.Add(TangentMode.AutoSmooth);
                        }
                    }
                    break;
            }
        }
    }
}
#endif
