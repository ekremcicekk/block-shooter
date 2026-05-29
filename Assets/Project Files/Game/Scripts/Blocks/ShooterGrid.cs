using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace BlockShooter
{
    public class ShooterGrid : MonoBehaviour
    {
        public static ShooterGrid Instance { get; private set; }

        [Header("Prefabs")]
        public ShooterBlock shooterBlockPrefab;
        public BlockDoor doorPrefab;

        [Header("Layout")]
        public Transform gridParent;
        public Vector2 gridOrigin = new Vector2(-1.65f, -3.5f);

        private GameConfig _config;
        private readonly List<ShooterBlock> _activeBlocks = new();
        private LevelData _levelData;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Initialize(LevelData data)
        {
            _levelData = data;
            _config = GameManager.Instance.config;
            if (gridParent == null) gridParent = transform;
            ClearGrid();
            BuildGrid(data);
        }

        private void ClearGrid()
        {
            foreach (Transform child in gridParent)
                Destroy(child.gameObject);
            _activeBlocks.Clear();
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
                    _activeBlocks.Add(block);

                    block.transform.localScale = Vector3.zero;
                    block.transform.DOScale(Vector3.one, 0.3f).SetDelay(cell.column * 0.05f + cell.row * 0.1f).SetEase(Ease.OutBack);
                }
                else if (cell.cellType == GridCellType.Door && doorPrefab != null)
                {
                    BlockDoor door = Instantiate(doorPrefab, pos, Quaternion.identity, gridParent);
                    door.Initialize(cell.doorBlockCount, data.availableColors, _config, pos);
                }
            }
        }

        private Vector3 GetWorldPosition(int col, int row)
        {
            float x = gridOrigin.x + col * _config.gridCellSize;
            float z = gridOrigin.y + row * _config.gridCellSize;
            return new Vector3(x, 0f, z);
        }

        public void OnBlockDepleted(ShooterBlock block)
        {
            _activeBlocks.Remove(block);
            CheckAllDepleted();
        }

        private void CheckAllDepleted()
        {
            if (_activeBlocks.Count == 0)
                GameManager.Instance?.TriggerFail();
        }

        public List<ShooterBlock> GetActiveBlocks() => _activeBlocks;

        public void SetRainbowMode(bool active)
        {
            foreach (var b in _activeBlocks)
                b.SetRainbowMode(active);
        }

        public void RefillAllShots(int amount)
        {
            foreach (var b in _activeBlocks)
                b.RefillShots(amount);
        }

        public void AddBlock(Vector3 position, BlockColorType colorType)
        {
            ShooterBlock block = Instantiate(shooterBlockPrefab, position, Quaternion.identity, gridParent);
            int shots = _config.defaultShotCount;
            int col = Mathf.RoundToInt((position.x - gridOrigin.x) / _config.gridCellSize);
            int row = Mathf.RoundToInt((position.z - gridOrigin.y) / _config.gridCellSize);
            block.Initialize(colorType, shots, col, row);
            _activeBlocks.Add(block);

            block.transform.localScale = Vector3.zero;
            block.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
        }
    }
}
