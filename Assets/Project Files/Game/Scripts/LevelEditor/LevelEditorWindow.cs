#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BlockShooter.Editor
{
    public class LevelEditorWindow : EditorWindow
    {
        private int _levelIndex = 1;
        private string _levelName = "Level_01";
        private LevelDifficulty _difficulty = LevelDifficulty.Normal;
        private float _conveyorSpeedMult = 1f;
        private LevelGoalType _goalType = LevelGoalType.ClearAllBlocks;
        private int _goalAmount = 0;
        private int _gridCols = 4;
        private int _gridRows = 2;
        private int _conveyorRowCount = 10;
        private int _conveyorColCount = 5;

        private GridCellType[,] _cellTypes;
        private BlockColorType[,] _cellColors;
        private int[,] _cellShotCounts;
        private int[,] _cellDoorCounts;
        private BlockColorType[,] _conveyorGrid;

        private bool _gridInitialized;
        private Vector2 _scrollPos;

        private readonly Color[] _colorMap = new Color[]
        {
            Color.gray,                              // None
            new Color(0.9f, 0.2f, 0.2f),            // Red
            new Color(0.2f, 0.5f, 0.9f),            // Blue
            new Color(0.2f, 0.8f, 0.3f),            // Green
            new Color(1f, 0.85f, 0.1f),             // Yellow
            new Color(0.6f, 0.2f, 0.9f),            // Purple
            new Color(1f, 0.55f, 0.1f)              // Orange
        };

        [MenuItem("BlockShooter/Level Editor")]
        public static void Open()
        {
            GetWindow<LevelEditorWindow>("Level Editor").Show();
        }

        private void OnEnable()
        {
            InitializeGrids();
        }

        private void InitializeGrids()
        {
            _cellTypes = new GridCellType[_gridCols, _gridRows];
            _cellColors = new BlockColorType[_gridCols, _gridRows];
            _cellShotCounts = new int[_gridCols, _gridRows];
            _cellDoorCounts = new int[_gridCols, _gridRows];
            _conveyorGrid = new BlockColorType[_conveyorColCount, _conveyorRowCount];

            for (int c = 0; c < _gridCols; c++)
                for (int r = 0; r < _gridRows; r++)
                {
                    _cellTypes[c, r] = GridCellType.ShooterBlock;
                    _cellColors[c, r] = BlockColorType.Red;
                    _cellShotCounts[c, r] = 100;
                    _cellDoorCounts[c, r] = 3;
                }

            for (int c = 0; c < _conveyorColCount; c++)
                for (int r = 0; r < _conveyorRowCount; r++)
                    _conveyorGrid[c, r] = BlockColorType.None;

            _gridInitialized = true;
        }

        private void OnGUI()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawLevelSettings();
            EditorGUILayout.Space(10);

            DrawGridSettings();
            EditorGUILayout.Space(10);

            DrawShooterGrid();
            EditorGUILayout.Space(10);

            DrawConveyorGrid();
            EditorGUILayout.Space(10);

            DrawExportButton();

            GUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField("Block Shooter - Level Editor", headerStyle, GUILayout.Height(30));
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        private void DrawLevelSettings()
        {
            EditorGUILayout.LabelField("Level Settings", EditorStyles.boldLabel);
            _levelIndex = EditorGUILayout.IntField("Level Index", _levelIndex);
            _levelName = EditorGUILayout.TextField("Level Name", _levelName);
            _difficulty = (LevelDifficulty)EditorGUILayout.EnumPopup("Difficulty", _difficulty);
            _conveyorSpeedMult = EditorGUILayout.Slider("Conveyor Speed Multiplier", _conveyorSpeedMult, 0.5f, 3f);
            _goalType = (LevelGoalType)EditorGUILayout.EnumPopup("Goal Type", _goalType);
            if (_goalType != LevelGoalType.ClearAllBlocks)
                _goalAmount = EditorGUILayout.IntField("Goal Amount", _goalAmount);
        }

        private void DrawGridSettings()
        {
            EditorGUILayout.LabelField("Grid Size", EditorStyles.boldLabel);

            int newCols = EditorGUILayout.IntSlider("Grid Columns", _gridCols, 1, 6);
            int newRows = EditorGUILayout.IntSlider("Grid Rows", _gridRows, 1, 3);
            int newConvCols = EditorGUILayout.IntSlider("Conveyor Columns", _conveyorColCount, 1, 8);
            int newConvRows = EditorGUILayout.IntSlider("Conveyor Rows", _conveyorRowCount, 1, 30);

            if (newCols != _gridCols || newRows != _gridRows ||
                newConvCols != _conveyorColCount || newConvRows != _conveyorRowCount)
            {
                _gridCols = newCols;
                _gridRows = newRows;
                _conveyorColCount = newConvCols;
                _conveyorRowCount = newConvRows;
                InitializeGrids();
            }
        }

        private void DrawShooterGrid()
        {
            if (!_gridInitialized) return;
            EditorGUILayout.LabelField("Shooter Grid (Bottom Area)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Click cells to cycle: ShooterBlock → Door → Empty → ShooterBlock", MessageType.Info);

            float cellSize = 55f;

            for (int r = _gridRows - 1; r >= 0; r--)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(10);
                for (int c = 0; c < _gridCols; c++)
                {
                    DrawShooterCell(c, r, cellSize);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }
        }

        private void DrawShooterCell(int col, int row, float size)
        {
            GridCellType cellType = _cellTypes[col, row];
            BlockColorType color = _cellColors[col, row];

            Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));

            Color bgColor = cellType switch
            {
                GridCellType.Empty => new Color(0.2f, 0.2f, 0.2f),
                GridCellType.Door => new Color(0.5f, 0.35f, 0.1f),
                _ => _colorMap[(int)color]
            };

            EditorGUI.DrawRect(rect, bgColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), Color.black);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), Color.black);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }, fontSize = 9 };

            string label = cellType switch
            {
                GridCellType.Empty => "EMPTY",
                GridCellType.Door => $"DOOR\nx{_cellDoorCounts[col, row]}",
                _ => $"{color.ToString().Substring(0, 3)}\n{_cellShotCounts[col, row]}"
            };

            GUI.Label(rect, label, labelStyle);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 0)
                    CycleCell(col, row);
                else if (Event.current.button == 1)
                    ShowCellContextMenu(col, row);

                Event.current.Use();
                Repaint();
            }
        }

        private void CycleCell(int col, int row)
        {
            _cellTypes[col, row] = _cellTypes[col, row] switch
            {
                GridCellType.ShooterBlock => GridCellType.Door,
                GridCellType.Door => GridCellType.Empty,
                _ => GridCellType.ShooterBlock
            };
        }

        private void ShowCellContextMenu(int col, int row)
        {
            GenericMenu menu = new GenericMenu();
            if (_cellTypes[col, row] == GridCellType.ShooterBlock)
            {
                foreach (BlockColorType ct in System.Enum.GetValues(typeof(BlockColorType)))
                {
                    if (ct == BlockColorType.None) continue;
                    var capturedColor = ct;
                    menu.AddItem(new GUIContent($"Set Color/{ct}"), _cellColors[col, row] == ct,
                        () => { _cellColors[col, row] = capturedColor; Repaint(); });
                }
                menu.AddSeparator("");
                for (int shots = 50; shots <= 200; shots += 50)
                {
                    var capturedShots = shots;
                    menu.AddItem(new GUIContent($"Set Shots/{shots}"), _cellShotCounts[col, row] == shots,
                        () => { _cellShotCounts[col, row] = capturedShots; Repaint(); });
                }
            }
            else if (_cellTypes[col, row] == GridCellType.Door)
            {
                for (int count = 1; count <= 5; count++)
                {
                    var capturedCount = count;
                    menu.AddItem(new GUIContent($"Door Count/{count}"), _cellDoorCounts[col, row] == count,
                        () => { _cellDoorCounts[col, row] = capturedCount; Repaint(); });
                }
            }
            menu.ShowAsContext();
        }

        private void DrawConveyorGrid()
        {
            if (!_gridInitialized) return;
            EditorGUILayout.LabelField("Conveyor Belt (Top Area) — Right-click for color menu", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Rows scroll from right to left. Top row spawns first.", MessageType.Info);

            float cellSize = 42f;

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            for (int c = 0; c < _conveyorColCount; c++)
                GUILayout.Label($"Col {c + 1}", GUILayout.Width(cellSize));
            GUILayout.EndHorizontal();

            for (int r = 0; r < _conveyorRowCount; r++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(10);
                for (int c = 0; c < _conveyorColCount; c++)
                {
                    DrawConveyorCell(c, r, cellSize);
                }
                GUILayout.Label($" Row {r + 1}", GUILayout.Width(50));
                GUILayout.EndHorizontal();
                GUILayout.Space(1);
            }
        }

        private void DrawConveyorCell(int col, int row, float size)
        {
            BlockColorType color = _conveyorGrid[col, row];
            Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));

            Color bg = color == BlockColorType.None ? new Color(0.15f, 0.15f, 0.15f) : _colorMap[(int)color];
            EditorGUI.DrawRect(rect, bg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), Color.black);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), Color.black);

            if (color != BlockColorType.None)
            {
                GUIStyle s = new GUIStyle(EditorStyles.miniLabel)
                { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }, fontSize = 8 };
                GUI.Label(rect, color.ToString().Substring(0, 1), s);
            }

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 0)
                    CycleConveyorColor(col, row);
                else
                    ShowConveyorContextMenu(col, row);
                Event.current.Use();
                Repaint();
            }
        }

        private void CycleConveyorColor(int col, int row)
        {
            int next = ((int)_conveyorGrid[col, row] + 1) % System.Enum.GetValues(typeof(BlockColorType)).Length;
            _conveyorGrid[col, row] = (BlockColorType)next;
        }

        private void ShowConveyorContextMenu(int col, int row)
        {
            GenericMenu menu = new GenericMenu();
            foreach (BlockColorType ct in System.Enum.GetValues(typeof(BlockColorType)))
            {
                var captured = ct;
                menu.AddItem(new GUIContent(ct == BlockColorType.None ? "Empty" : ct.ToString()),
                    _conveyorGrid[col, row] == ct,
                    () => { _conveyorGrid[col, row] = captured; Repaint(); });
            }
            menu.ShowAsContext();
        }

        private void DrawExportButton()
        {
            GUILayout.Space(10);
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
            if (GUILayout.Button("Export Level as ScriptableObject", GUILayout.Height(40)))
                ExportLevel();
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Fill Random Conveyor", GUILayout.Height(25)))
                FillRandomConveyor();

            if (GUILayout.Button("Reset All", GUILayout.Height(25)))
                InitializeGrids();
        }

        private void FillRandomConveyor()
        {
            BlockColorType[] colors = { BlockColorType.Red, BlockColorType.Blue, BlockColorType.Green,
                                         BlockColorType.Yellow, BlockColorType.Purple };
            for (int r = 0; r < _conveyorRowCount; r++)
                for (int c = 0; c < _conveyorColCount; c++)
                    _conveyorGrid[c, r] = colors[Random.Range(0, colors.Length)];
            Repaint();
        }

        private static void EnsureDirectory(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private void ExportLevel()
        {
            LevelData asset = CreateInstance<LevelData>();
            asset.levelIndex = _levelIndex;
            asset.levelName = _levelName;
            asset.difficulty = _difficulty;
            asset.conveyorSpeedMultiplier = _conveyorSpeedMult;
            asset.goalType = _goalType;
            asset.goalAmount = _goalAmount;

            // Build grid cells
            asset.gridCells = new List<GridCellData>();
            for (int c = 0; c < _gridCols; c++)
            {
                for (int r = 0; r < _gridRows; r++)
                {
                    if (_cellTypes[c, r] == GridCellType.Empty) continue;
                    asset.gridCells.Add(new GridCellData
                    {
                        column = c,
                        row = r,
                        cellType = _cellTypes[c, r],
                        color = _cellColors[c, r],
                        customShotCount = _cellShotCounts[c, r],
                        doorBlockCount = _cellDoorCounts[c, r]
                    });
                }
            }

            // Build conveyor rows
            asset.conveyorRows = new List<ConveyorRowData>();
            for (int r = 0; r < _conveyorRowCount; r++)
            {
                var row = new ConveyorRowData { columns = new List<BlockColorType>() };
                for (int c = 0; c < _conveyorColCount; c++)
                    row.columns.Add(_conveyorGrid[c, r]);
                asset.conveyorRows.Add(row);
            }

            // Collect available colors
            asset.availableColors = new List<BlockColorType>();
            foreach (GridCellData cell in asset.gridCells)
                if (cell.cellType == GridCellType.ShooterBlock && !asset.availableColors.Contains(cell.color))
                    asset.availableColors.Add(cell.color);

            string path = $"Assets/Project Files/Game/ScriptableObjects/Levels/{_levelName}.asset";
            EnsureDirectory("Assets/Project Files/Game/ScriptableObjects/Levels");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;

            EditorUtility.DisplayDialog("Level Exported!",
                $"Level saved to:\n{path}", "OK");
        }
    }
}
#endif
