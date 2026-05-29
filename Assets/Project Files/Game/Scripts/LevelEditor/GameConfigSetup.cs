#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BlockShooter.Editor
{
    public static class GameConfigSetup
    {
        [MenuItem("BlockShooter/Create GameConfig Asset", false, 2)]
        public static void CreateGameConfig()
        {
            const string path = "Assets/Project Files/Game/ScriptableObjects/Config/GameConfig.asset";

            var existing = AssetDatabase.LoadAssetAtPath<GameConfig>(path);
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Zaten Var", "GameConfig zaten mevcut:\n" + path, "Tamam");
                Selection.activeObject = existing;
                return;
            }

            var config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = config;
            EditorUtility.FocusProjectWindow();

            EditorUtility.DisplayDialog("GameConfig Oluşturuldu",
                "GameConfig oluşturuldu:\n" + path +
                "\n\nŞimdi GameManager'a bu asset'i ata!", "Tamam");
        }

        [MenuItem("BlockShooter/Create Sample Level Data", false, 3)]
        public static void CreateSampleLevel()
        {
            const string path = "Assets/Project Files/Game/ScriptableObjects/Levels/Level_01.asset";

            var level = ScriptableObject.CreateInstance<LevelData>();
            level.levelIndex = 1;
            level.levelName = "Level_01";
            level.difficulty = LevelDifficulty.Easy;
            level.conveyorSpeedMultiplier = 1f;
            level.goalType = LevelGoalType.ClearAllBlocks;

            // Grid: 4x2 grid, 2 colors
            level.gridCells = new System.Collections.Generic.List<GridCellData>
            {
                new GridCellData { column = 0, row = 0, cellType = GridCellType.ShooterBlock, color = BlockColorType.Red,  customShotCount = 100 },
                new GridCellData { column = 1, row = 0, cellType = GridCellType.ShooterBlock, color = BlockColorType.Blue, customShotCount = 100 },
                new GridCellData { column = 2, row = 0, cellType = GridCellType.ShooterBlock, color = BlockColorType.Red,  customShotCount = 100 },
                new GridCellData { column = 3, row = 0, cellType = GridCellType.ShooterBlock, color = BlockColorType.Blue, customShotCount = 100 },
                new GridCellData { column = 1, row = 1, cellType = GridCellType.ShooterBlock, color = BlockColorType.Green, customShotCount = 100 },
                new GridCellData { column = 2, row = 1, cellType = GridCellType.ShooterBlock, color = BlockColorType.Green, customShotCount = 100 },
            };

            // Conveyor: 20 rows × 5 columns — 2 color groups
            level.conveyorRows = new System.Collections.Generic.List<ConveyorRowData>();
            for (int r = 0; r < 20; r++)
            {
                var row = new ConveyorRowData
                {
                    columns = new System.Collections.Generic.List<BlockColorType>
                    {
                        BlockColorType.Red, BlockColorType.Red, BlockColorType.Red,
                        BlockColorType.Red, BlockColorType.Red
                    }
                };
                level.conveyorRows.Add(row);
            }
            for (int r = 0; r < 20; r++)
            {
                var row = new ConveyorRowData
                {
                    columns = new System.Collections.Generic.List<BlockColorType>
                    {
                        BlockColorType.Blue, BlockColorType.Blue, BlockColorType.Blue,
                        BlockColorType.Blue, BlockColorType.Blue
                    }
                };
                level.conveyorRows.Add(row);
            }

            level.availableColors = new System.Collections.Generic.List<BlockColorType>
                { BlockColorType.Red, BlockColorType.Blue, BlockColorType.Green };

            AssetDatabase.CreateAsset(level, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = level;
            EditorUtility.FocusProjectWindow();
            EditorUtility.DisplayDialog("Sample Level Oluşturuldu", path, "Tamam");
        }
    }
}
#endif
