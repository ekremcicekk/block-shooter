#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter.Editor
{
    public struct LevelValidationResult
    {
        public Dictionary<BlockColorType, int> gridShots;
        public Dictionary<BlockColorType, int> conveyorTargets;
    }

    public static class LevelEditorValidation
    {
        public static LevelValidationResult Validate(
            int gridCols,
            int gridRows,
            GridCellType[,] type,
            BlockColorType[,] color,
            int[,] shots,
            int[,] doors,
            List<LevelConveyorGroup> groups,
            List<BranchPathData> branches,
            (BlockColorType t, Color c, string n)[] activeColors)
        {
            var gridShots = new Dictionary<BlockColorType, int>();
            var conveyorTargets = new Dictionary<BlockColorType, int>();

            foreach (var entry in activeColors)
            {
                gridShots[entry.t] = 0;
                conveyorTargets[entry.t] = 0;
            }

            // 1. Calculate Grid Shots
            if (type != null)
            {
                for (int c = 0; c < gridCols; c++)
                {
                    for (int r = 0; r < gridRows; r++)
                    {
                        if (c >= type.GetLength(0) || r >= type.GetLength(1)) continue;
                        if (type[c, r] == GridCellType.Empty) continue;

                        BlockColorType cellColor = color[c, r];
                        if (!gridShots.ContainsKey(cellColor)) continue;

                        if (type[c, r] == GridCellType.ShooterBlock ||
                            type[c, r] == GridCellType.MysteryShooter ||
                            type[c, r] == GridCellType.FreezeShooter)
                        {
                            gridShots[cellColor] += Mathf.Max(0, shots[c, r]);
                        }
                        else if (type[c, r] == GridCellType.Door)
                        {
                            gridShots[cellColor] += Mathf.Max(0, doors[c, r]) * 100; // Doors spawn 100-shot blocks
                        }
                    }
                }
            }

            // 2. Calculate Conveyor Targets
            if (groups != null)
            {
                foreach (var g in groups)
                {
                    if (g != null && conveyorTargets.ContainsKey(g.color))
                    {
                        conveyorTargets[g.color] += g.rowCount * g.laneCount;
                    }
                }
            }

            if (branches != null)
            {
                foreach (var b in branches)
                {
                    if (b == null || b.groups == null) continue;
                    foreach (var g in b.groups)
                    {
                        if (g != null && conveyorTargets.ContainsKey(g.color))
                        {
                            conveyorTargets[g.color] += g.rowCount * 5; // Branch lanes default to 5 in hierarchy
                        }
                    }
                }
            }

            return new LevelValidationResult
            {
                gridShots = gridShots,
                conveyorTargets = conveyorTargets
            };
        }
    }
}
#endif
