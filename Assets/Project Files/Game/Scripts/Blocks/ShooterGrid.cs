using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace BlockShooter
{
    /// <summary>
    /// Manages the grid of ShooterBlocks pre-placed as children by the Level Editor.
    ///
    /// Accessibility rule (per column):
    ///   The front-most block (lowest GridRow) that is still InGrid is accessible.
    ///   When it leaves (slotted or depleted), the next one in the column becomes accessible.
    /// </summary>
    public class ShooterGrid : MonoBehaviour
    {
        public static ShooterGrid Instance { get; private set; }

        [Header("Prefabs")]
        [Tooltip("Prefab used only when spawning blocks dynamically at runtime (e.g. from BlockDoor).")]
        public ShooterBlock shooterBlockPrefab;

        private GameConfig _config;
        private LevelRoot  _levelRoot;

        private readonly List<ShooterBlock> _activeBlocks = new();
        private readonly Dictionary<int, List<ShooterBlock>> _columns = new();


        private void Awake()
        {
            if (!Application.isPlaying) return;
            if (Instance != null && Instance != this) Destroy(Instance.gameObject);
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Scans pre-placed ShooterBlock and BlockDoor children and activates them.
        /// Called by LevelRoot.Initialize().
        /// </summary>
        public void Initialize()
        {
            _config = GameManager.Instance.config;
            _levelRoot = GetComponentInParent<LevelRoot>();
            _activeBlocks.Clear();
            _columns.Clear();

            var blocks = GetComponentsInChildren<ShooterBlock>(true);
            for (int i = 0; i < blocks.Length; i++)
            {
                var block = blocks[i];
                block.gameObject.SetActive(true);
                block.Initialize();
                RegisterBlock(block);

                block.transform.localScale = Vector3.zero;
                float delay = block.GridColumn * 0.05f + block.GridRow * 0.1f;
                block.transform.DOScale(Vector3.one * 0.8f, 0.3f).SetDelay(delay).SetEase(Ease.OutBack);
            }

            foreach (var door in GetComponentsInChildren<BlockDoor>(true))
                door.Initialize();

            RefreshAllAccessibility();
        }

        // ── Accessibility ─────────────────────────────────────────────────────

        public void OnBlockLeftGrid(ShooterBlock block)
        {
            RefreshColumnAccessibility(block.GridColumn);
        }

        public void OnBlockDepleted(ShooterBlock block)
        {
            _activeBlocks.Remove(block);
            RemoveFromColumn(block);
            RefreshColumnAccessibility(block.GridColumn);
            CheckAllDepleted();
        }


        public void RefreshAllAccessibility()
        {
            var deck = GetComponentInChildren<ShooterDeckMeshBuilder>();
            if (deck == null && _levelRoot != null)
                deck = _levelRoot.GetComponentInChildren<ShooterDeckMeshBuilder>();
            if (deck == null)
                deck = FindFirstObjectByType<ShooterDeckMeshBuilder>();

            int cols = deck != null ? deck.gridCols : 4;
            int rows = deck != null ? deck.gridRows : 2;

            bool[,] isBlocked = new bool[cols, rows];

            // 1. Mark static walls
            if (_levelRoot != null)
            {
                foreach (var cell in _levelRoot.cells)
                {
                    if (cell.type == GridCellType.Empty)
                    {
                        if (cell.col >= 0 && cell.col < cols && cell.row >= 0 && cell.row < rows)
                        {
                            isBlocked[cell.col, cell.row] = true;
                        }
                    }
                }
            }

            // 2. Mark active doors
            float cellSize = deck != null ? deck.cellSize : 1.2f;
            Vector3 origin = transform.position;
            var doors = GetComponentsInChildren<BlockDoor>(false);
            foreach (var door in doors)
            {
                int col = Mathf.RoundToInt((door.transform.position.x - origin.x) / cellSize);
                int row = Mathf.RoundToInt((door.transform.position.z - origin.z) / cellSize);
                if (col >= 0 && col < cols && row >= 0 && row < rows)
                {
                    isBlocked[col, row] = true;
                }
            }

            // 3. Mark active blocks still in grid
            foreach (var block in _activeBlocks)
            {
                if (block != null && block.State == ShooterBlock.BlockState.InGrid)
                {
                    if (block.GridColumn >= 0 && block.GridColumn < cols && block.GridRow >= 0 && block.GridRow < rows)
                    {
                        isBlocked[block.GridColumn, block.GridRow] = true;
                    }
                }
            }

            // 4. Reveal mystery blocks that have a clear path to the front (slots)
            foreach (var block in _activeBlocks)
            {
                if (block == null) continue;
                if (block.State == ShooterBlock.BlockState.Depleted) continue;
                if (block.State == ShooterBlock.BlockState.InSlot ||
                    block.State == ShooterBlock.BlockState.MovingToSlot) continue;

                if (block.isMystery)
                {
                    if (HasPathToFront(block.GridColumn, block.GridRow, isBlocked, cols, rows))
                    {
                        var feat = block.GetComponent<MysteryBlockFeature>();
                        if (feat != null)
                        {
                            feat.Reveal();
                        }
                    }
                }
            }

            // 5. Update accessibility for each block using BFS pathfinding
            foreach (var block in _activeBlocks)
            {
                if (block == null) continue;
                if (block.State == ShooterBlock.BlockState.Depleted) continue;
                if (block.State == ShooterBlock.BlockState.InSlot ||
                    block.State == ShooterBlock.BlockState.MovingToSlot) continue;

                bool pathExists = HasPathToFront(block.GridColumn, block.GridRow, isBlocked, cols, rows);
                block.SetAccessible(pathExists);
            }
        }



        private void RefreshColumnAccessibility(int col)
        {
            RefreshAllAccessibility();
        }

        private bool HasPathToFront(int startCol, int startRow, bool[,] baseBlocked, int cols, int rows)
        {
            if (startCol < 0 || startCol >= cols || startRow < 0 || startRow >= rows) return false;
            if (startRow == rows - 1) return true;

            bool originalVal = baseBlocked[startCol, startRow];
            baseBlocked[startCol, startRow] = false;

            var queue = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int>();

            var startNode = new Vector2Int(startCol, startRow);
            queue.Enqueue(startNode);
            visited.Add(startNode);

            bool reachedFront = false;

            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();

                if (curr.y == rows - 1)
                {
                    reachedFront = true;
                    break;
                }

                Vector2Int[] neighbors = {
                    new Vector2Int(curr.x, curr.y + 1), // Front
                    new Vector2Int(curr.x - 1, curr.y), // Left
                    new Vector2Int(curr.x + 1, curr.y), // Right
                    new Vector2Int(curr.x, curr.y - 1)  // Back
                };

                foreach (var next in neighbors)
                {
                    if (next.x >= 0 && next.x < cols && next.y >= 0 && next.y < rows)
                    {
                        if (!visited.Contains(next) && !baseBlocked[next.x, next.y])
                        {
                            visited.Add(next);
                            queue.Enqueue(next);
                        }
                    }
                }
            }

            baseBlocked[startCol, startRow] = originalVal;
            return reachedFront;
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

            int insertAt = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                if (block.GridRow > list[i].GridRow) { insertAt = i; break; }
            }
            list.Insert(insertAt, block);
        }

        private void RemoveFromColumn(ShooterBlock block)
        {
            if (_columns.TryGetValue(block.GridColumn, out var list))
                list.Remove(block);
        }

        // ── Dynamic block spawn (from BlockDoor) ──────────────────────────────

        public void AddBlock(Vector3 position, BlockColorType colorType)
        {
            if (shooterBlockPrefab == null) return;

            const float cellSize = 1.2f;
            Vector3 origin = transform.position;
            int col = Mathf.RoundToInt((position.x - origin.x) / cellSize);
            int row = Mathf.RoundToInt((position.z - origin.z) / cellSize);

            ShooterBlock block = Instantiate(shooterBlockPrefab, position, Quaternion.identity, transform);
            block.Initialize(colorType, 100, col, row);
            RegisterBlock(block);
            RefreshColumnAccessibility(col);

            block.transform.localScale = Vector3.zero;
            block.transform.DOScale(Vector3.one * 0.8f, 0.35f).SetEase(Ease.OutBack);
        }

        // ── Queries ───────────────────────────────────────────────────────────

        public List<ShooterBlock> GetActiveBlocks() => _activeBlocks;

        public bool HasLockedBlocks()
        {
            foreach (var b in _activeBlocks)
            {
                if (b != null && b.State == ShooterBlock.BlockState.InGrid && !b.IsAccessible)
                {
                    return true;
                }
            }
            return false;
        }

        public void RefillAllShots(int amount)
        {
            foreach (var b in _activeBlocks) b.RefillShots(amount);
        }

        // ── Win/Fail ──────────────────────────────────────────────────────────

        private void CheckAllDepleted()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

            // If there are projectiles still in flight, defer this check.
            // When the last projectile lands (ReturnToPool), NotifyAllProjectilesLanded will be called,
            // which will re-trigger this check — resolving the race condition.
            if (ProjectilePool.Instance != null && ProjectilePool.Instance.ActiveCount > 0) return;

            bool anyLeft = _activeBlocks.Count > 0;
            if (!anyLeft && (SlotSystem.Instance == null || SlotSystem.Instance.GetSlottedBlocks().Count == 0))
            {
                bool eligibleForRevive = SlotSystem.Instance != null && 
                                         SlotSystem.Instance.MaxSlots <= SlotSystem.Instance.InitialSlotsCount && 
                                         UIManager.Instance != null && 
                                         !UIManager.Instance.HasRevivedThisLevel;

                if (eligibleForRevive)
                {
                    // Freeze conveyor while revival popup is shown
                    if (ConveyorController.Instance != null)
                    {
                        ConveyorController.Instance.IsFrozen = true;
                    }
                    UIManager.Instance.ShowKeepPlayingPanel();
                }
                else
                {
                    GameManager.Instance?.TriggerFail();
                }
            }
        }

        /// <summary>
        /// Called by ProjectilePool when all in-flight projectiles have landed.
        /// This is the deferred re-check for the depletion state, resolving the race condition
        /// where Deplete() fires before the last projectile reaches its target.
        /// </summary>
        public void NotifyAllProjectilesLanded()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
            CheckAllDepleted();
        }
    }
}
