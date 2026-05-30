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
    ///
    /// FreePick booster: temporarily makes ALL InGrid blocks accessible.
    /// </summary>
    public class ShooterGrid : MonoBehaviour
    {
        public static ShooterGrid Instance { get; private set; }

        [Header("Prefabs")]
        [Tooltip("Prefab used only when spawning blocks dynamically at runtime (e.g. from BlockDoor).")]
        public ShooterBlock shooterBlockPrefab;

        private GameConfig _config;

        private readonly List<ShooterBlock> _activeBlocks = new();
        private readonly Dictionary<int, List<ShooterBlock>> _columns = new();

        private bool _freePickActive;

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
                block.transform.DOScale(Vector3.one, 0.3f).SetDelay(delay).SetEase(Ease.OutBack);
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
            foreach (var block in list)
            {
                if (block == null) continue;
                if (block.State == ShooterBlock.BlockState.Depleted) continue;
                if (block.State == ShooterBlock.BlockState.InSlot ||
                    block.State == ShooterBlock.BlockState.MovingToSlot) continue;

                if (_freePickActive)
                    block.SetAccessible(true);
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

            float cellSize = _config != null ? _config.gridCellSize : 1.2f;
            Vector3 origin = transform.position;
            int col = Mathf.RoundToInt((position.x - origin.x) / cellSize);
            int row = Mathf.RoundToInt((position.z - origin.z) / cellSize);

            ShooterBlock block = Instantiate(shooterBlockPrefab, position, Quaternion.identity, transform);
            block.Initialize(colorType, _config != null ? _config.defaultShotCount : 3, col, row);
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
            bool anyLeft = _activeBlocks.Count > 0;
            if (!anyLeft && (SlotSystem.Instance == null || SlotSystem.Instance.GetSlottedBlocks().Count == 0))
                GameManager.Instance?.TriggerFail();
        }
    }
}
