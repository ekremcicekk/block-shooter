#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter.Editor
{
    public struct LevelValidationResult
    {
        public Dictionary<BlockColorType, int> gridShots;
        public Dictionary<BlockColorType, int> conveyorTargets;
        public List<string> tunnelWarnings;
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
            GridDirection[,] tunnelDirections,
            List<TunnelSequenceItem>[,] tunnelSequences,
            List<LevelConveyorGroup> groups,
            List<BranchPathData> branches,
            (BlockColorType t, Color c, string n)[] activeColors)
        {
            var gridShots = new Dictionary<BlockColorType, int>();
            var conveyorTargets = new Dictionary<BlockColorType, int>();
            var tunnelWarnings = new List<string>();

            foreach (var entry in activeColors)
            {
                gridShots[entry.t] = 0;
                conveyorTargets[entry.t] = 0;
            }

            // 1. Calculate Grid Shots and validate tunnels
            if (type != null)
            {
                for (int c = 0; c < gridCols; c++)
                {
                    for (int r = 0; r < gridRows; r++)
                    {
                        if (c >= type.GetLength(0) || r >= type.GetLength(1)) continue;
                        if (type[c, r] == GridCellType.Empty) continue;

                        BlockColorType cellColor = color[c, r];

                        if (type[c, r] == GridCellType.ShooterBlock ||
                            type[c, r] == GridCellType.MysteryShooter ||
                            type[c, r] == GridCellType.FreezeShooter)
                        {
                            if (gridShots.ContainsKey(cellColor))
                                gridShots[cellColor] += Mathf.Max(0, shots[c, r]);
                        }
                        else if (type[c, r] == GridCellType.Door) // Door is Tunnel
                        {
                            // Sum sequence items' counts for grid shots
                            var sequence = tunnelSequences != null && c < tunnelSequences.GetLength(0) && r < tunnelSequences.GetLength(1) 
                                ? tunnelSequences[c, r] 
                                : null;

                            if (sequence != null && sequence.Count > 0)
                            {
                                foreach (var seqItem in sequence)
                                {
                                    if (gridShots.ContainsKey(seqItem.color))
                                    {
                                        gridShots[seqItem.color] += seqItem.count;
                                    }
                                }
                            }
                            else
                            {
                                // Fallback to legacy behavior if sequence is null
                                if (gridShots.ContainsKey(cellColor))
                                    gridShots[cellColor] += Mathf.Max(0, doors[c, r]);
                            }

                            // Validate Tunnel boundaries and direction
                            if (tunnelDirections != null && c < tunnelDirections.GetLength(0) && r < tunnelDirections.GetLength(1))
                            {
                                GridDirection dir = tunnelDirections[c, r];
                                bool isInvalid = false;
                                string invalidReason = "";

                                // Check grid boundaries based on looking direction
                                if (dir == GridDirection.Down && r == 0)
                                {
                                    isInvalid = true;
                                    invalidReason = "faces the front-most lane (conveyor/slots direction)";
                                }
                                else if (dir == GridDirection.Up && r == gridRows - 1)
                                {
                                    isInvalid = true;
                                    invalidReason = "faces out of the back grid boundary";
                                }
                                else if (dir == GridDirection.Left && c == 0)
                                {
                                    isInvalid = true;
                                    invalidReason = "faces out of the left grid boundary";
                                }
                                else if (dir == GridDirection.Right && c == gridCols - 1)
                                {
                                    isInvalid = true;
                                    invalidReason = "faces out of the right grid boundary";
                                }

                                // Check if the cell directly in front is empty (static wall)
                                if (!isInvalid)
                                {
                                    int frontC = c;
                                    int frontR = r;
                                    if (dir == GridDirection.Down) frontR--;
                                    else if (dir == GridDirection.Up) frontR++;
                                    else if (dir == GridDirection.Left) frontC--;
                                    else if (dir == GridDirection.Right) frontC++;

                                    if (frontC >= 0 && frontC < gridCols && frontR >= 0 && frontR < gridRows)
                                    {
                                        if (type[frontC, frontR] == GridCellType.Empty)
                                        {
                                            isInvalid = true;
                                            invalidReason = $"faces a static wall (empty cell) at ({frontC}, {frontR})";
                                        }
                                    }
                                }

                                if (isInvalid)
                                {
                                    tunnelWarnings.Add($"Tunnel at cell ({c}, {r}) is invalid: {invalidReason}.");
                                }
                            }
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
                conveyorTargets = conveyorTargets,
                tunnelWarnings = tunnelWarnings
            };
        }
    }
}
#endif
