using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace BlockShooter
{
    /// <summary>
    /// Manages the grid of ShooterBlocks below the conveyor track.
    ///
    /// Accessibility rule (per column):
    ///   The front-most block (lowest GridRow) that is still InGrid is accessible.
    ///   When it leaves (slotted or depleted), the next one in the column becomes accessible.
    ///
    /// FreePick booster: temporarily makes ALL InGrid blocks accessible.
    /// </summary>
    public class ShooterGrid : MonoBehaviour
    {
        public static ShooterGrid Instance { get; private set; }

        [Header("Prefabs")]
        public ShooterBlock shooterBlockPrefab;
        public BlockDoor    doorPrefab;

        [Header("Layout")]
        public Transform  gridParent;
        public Vector2    gridOrigin = new Vector2(-1.65f, -3.5f);

        private GameConfig  _config;
        private LevelData   _levelData;

        // All living blocks (InGrid or InSlot)
        private readonly List<ShooterBlock> _activeBlocks = new();
        // Per-column ordered lists (row 0 = front)
        private readonly Dictionary<int, List<ShooterBlock>> _columns = new();

        private bool _freePickActive;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Init ──────────────────────────────────────────────────────────────

        public void Initialize(LevelData data)
        {
            _levelData = data;
            _config    = GameManager.Instance.config;
            if (gridParent == null) gridParent = transform;
            ClearGrid();
            BuildGrid(data);
        }

        private void ClearGrid()
        {
            foreach (Transform child in gridParent) Destroy(child.gameObject);
            _activeBlocks.Clear();
            _columns.Clear();
        }

        private void BuildGrid(LevelData data)
        {
            foreach (var cell in data.gridCells)
            {
                Vector3 pos = GetWorldPosition(cell.column, cell.row);

                if (cell.cellType == GridCellType.ShooterBlock)
                {
                    ShooterBlock block = Instantiate(shooterBlockPrefab, pos, Quaternion.identity, gridParent);
                    int shots = cell.customShotCount > 0 ? cell.customShotCount : _config.defaultShotCount;
                    block.Initialize(cell.color, shots, cell.column, cell.row);
                    RegisterBlock(block);

                    block.transform.localScale = Vector3.zero;
                    block.transform.DOScale(Vector3.one, 0.3f)
                        .SetDelay(cell.column * 0.05f + cell.row * 0.1f)
                        .SetEase(Ease.OutBack);
                }
                else if (cell.cellType == GridCellType.Door && doorPrefab != null)
                {
                    BlockDoor door = Instantiate(doorPrefab, pos, Quaternion.identity, gridParent);
                    door.Initialize(cell.doorBlockCount, data.availableColors, _config, pos);
                }
            }

            RefreshAllAccessibility();
        }

        // ── Accessibility ─────────────────────────────────────────────────────

        /// <summary>
        /// Called by ShooterBlock when it leaves the grid (tapped → moving to slot).
        /// </summary>
        public void OnBlockLeftGrid(ShooterBlock block)
        {
            RefreshColumnAccessibility(block.GridColumn);
        }

        /// <summary>
        /// Called when a block is fully depleted (from slot or grid).
        /// </summary>
        public void OnBlockDepleted(ShooterBlock block)
        {
            _activeBlocks.Remove(block);
            RemoveFromColumn(block);
            RefreshColumnAccessibility(block.GridColumn);
            CheckAllDepleted();
        }

        /// <summary>FreePick booster: all InGrid blocks become selectable.</summary>
        public void SetFreePickMode(bool active)
        {
            _freePickActive = active;
            RefreshAllAccessibility();
        }

        private void RefreshAllAccessibility()
        {
            foreach (var col in _columns.Keys)
                RefreshColumnAccessibility(col);
        }

        private void RefreshColumnAccessibility(int col)
        {
            if (!_columns.TryGetValue(col, out var list)) return;

            bool frontFound = false;
            // list is ordered front-to-back (ascending row index)
            foreach (var block in list)
            {
                if (block == null || block.IsDepleted || block.IsInSlot) continue;
                if (block.State != ShooterBlock.BlockState.InGrid) continue;

                if (_freePickActive)
                {
                    block.SetAccessible(true);
                }
                else
                {
                    block.SetAccessible(!frontFound);
                    frontFound = true;
                }
            }
        }

        // ── Column registry ───────────────────────────────────────────────────

        private void RegisterBlock(ShooterBlock block)
        {
            _activeBlocks.Add(block);

            if (!_columns.TryGetValue(block.GridColumn, out var list))
            {
                list = new List<ShooterBlock>();
                _columns[block.GridColumn] = list;
            }

            // Insert in row order (ascending = front first)
            int insertAt = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                if (block.GridRow < list[i].GridRow) { insertAt = i; break; }
            }
            list.Insert(insertAt, block);
        }

        private void RemoveFromColumn(ShooterBlock block)
        {
            if (_columns.TryGetValue(block.GridColumn, out var list))
                list.Remove(block);
        }

        // ── Door / dynamic add ────────────────────────────────────────────────

        public void AddBlock(Vector3 position, BlockColorType colorType)
        {
            ShooterBlock block = Instantiate(shooterBlockPrefab, position, Quaternion.identity, gridParent);
            int col = Mathf.RoundToInt((position.x - gridOrigin.x) / _config.gridCellSize);
            int row = Mathf.RoundToInt((position.z - gridOrigin.y) / _config.gridCellSize);
            block.Initialize(colorType, _config.defaultShotCount, col, row);
            RegisterBlock(block);
            RefreshColumnAccessibility(col);

            block.transform.localScale = Vector3.zero;
            block.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
        }

        // ── Queries ───────────────────────────────────────────────────────────

        public List<ShooterBlock> GetActiveBlocks() => _activeBlocks;

        public void SetRainbowMode(bool active)
        {
            foreach (var b in _activeBlocks) b.SetRainbowMode(active);
        }

        public void RefillAllShots(int amount)
        {
            foreach (var b in _activeBlocks) b.RefillShots(amount);
        }

        // ── Win/Fail ──────────────────────────────────────────────────────────

        private void CheckAllDepleted()
        {
            // Fail only if there are no blocks left in grid AND none in slots
            bool anyLeft = _activeBlocks.Count > 0;
            if (!anyLeft && (SlotSystem.Instance == null || SlotSystem.Instance.GetSlottedBlocks().Count == 0))
                GameManager.Instance?.TriggerFail();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Vector3 GetWorldPosition(int col, int row)
        {
            float x = gridOrigin.x + col * _config.gridCellSize;
            float z = gridOrigin.y + row * _config.gridCellSize;
            return new Vector3(x, 0f, z);
        }
    }
}
