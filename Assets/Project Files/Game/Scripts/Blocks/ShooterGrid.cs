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
        [Tooltip("Prefab used only when spawning blocks dynamically at runtime (e.g. from Tunnel).")]
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
        /// Scans pre-placed ShooterBlock and Tunnel children and activates them.
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

            foreach (var tunnel in GetComponentsInChildren<Tunnel>(true))
                tunnel.Initialize();

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

            // 2. Mark active doors (tunnels)
            var tunnels = GetComponentsInChildren<Tunnel>(false);
            foreach (var tunnel in tunnels)
            {
                if (tunnel.col >= 0 && tunnel.col < cols && tunnel.row >= 0 && tunnel.row < rows)
                {
                    isBlocked[tunnel.col, tunnel.row] = true;
                }

                // If the tunnel has blocks remaining to spawn, treat the target cell in front of it as blocked.
                if (tunnel.HasBlocksRemaining)
                {
                    int spawnCol = tunnel.col;
                    int spawnRow = tunnel.row;
                    switch (tunnel.direction)
                    {
                        case GridDirection.Down: spawnRow--; break;
                        case GridDirection.Up: spawnRow++; break;
                        case GridDirection.Left: spawnCol--; break;
                        case GridDirection.Right: spawnCol++; break;
                    }
                    if (spawnCol >= 0 && spawnCol < cols && spawnRow >= 0 && spawnRow < rows)
                    {
                        isBlocked[spawnCol, spawnRow] = true;
                    }
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

        // ── Dynamic block spawn (from Tunnel) ──────────────────────────────

        public void AddBlock(Vector3 position, BlockColorType colorType, int shotCount, int col, int row, Vector3 startPosition)
        {
            if (shooterBlockPrefab == null) return;

            ShooterBlock block = Instantiate(shooterBlockPrefab, startPosition, Quaternion.identity, transform);
            block.Initialize(colorType, shotCount, col, row);
            RegisterBlock(block);
            block.SetAccessible(false, triggerUnlockAnimation: false);

            block.transform.localScale = Vector3.zero;
            block.transform.DOMove(position, 0.35f).SetEase(Ease.OutQuad);
            block.transform.DOScale(Vector3.one * 0.8f, 0.35f).SetEase(Ease.OutBack).OnComplete(() =>
            {
                RefreshAllAccessibility();
            });
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
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

            // Defer until all in-flight projectiles land to avoid a race where
            // Deplete() fires before the last projectile reaches its target.
            if (ProjectilePool.Instance != null && ProjectilePool.Instance.ActiveCount > 0) return;

            GameManager.Instance.CheckFailCondition();
        }

        // Called by ProjectilePool when all in-flight projectiles have landed.
        public void NotifyAllProjectilesLanded()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
            GameManager.Instance.CheckFailCondition();
        }
    }
}
