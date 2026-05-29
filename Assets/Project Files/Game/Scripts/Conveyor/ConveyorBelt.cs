using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    public class ConveyorBelt : MonoBehaviour
    {
        [Header("References")]
        public ConveyorBlock3D conveyorBlockPrefab;
        public Transform blockParent;

        [Header("Layout")]
        public Transform spawnPoint;
        public Transform despawnPoint;
        public float laneSpacing = 1.1f;

        [Header("Runtime")]
        public bool IsFrozen { get; private set; }

        private GameConfig _config;
        private LevelData _levelData;
        private float _speed;
        private int _currentRowIndex;
        private float _spawnTimer;
        private float _spawnInterval;

        private readonly List<ConveyorBlock3D> _activeBlocks = new();
        private readonly List<ConveyorBlock3D> _blockPool = new();

        private int _totalBlocksToSpawn;
        private int _blocksDestroyed;

        public static ConveyorBelt Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Initialize(LevelData data)
        {
            _levelData = data;
            _config = GameManager.Instance.config;
            _speed = _config.conveyorSpeed * data.conveyorSpeedMultiplier;
            _currentRowIndex = 0;
            _activeBlocks.Clear();

            _totalBlocksToSpawn = CountTotalBlocks(data);
            _blocksDestroyed = 0;

            // Calculate spawn interval based on block spacing and speed
            _spawnInterval = _config.blockSpacing / _speed * 0.8f;

            StartCoroutine(SpawnRoutine());
        }

        private int CountTotalBlocks(LevelData data)
        {
            int total = 0;
            foreach (var row in data.conveyorRows)
                total += row.columns.Count;
            return total;
        }

        private IEnumerator SpawnRoutine()
        {
            while (_currentRowIndex < _levelData.conveyorRows.Count)
            {
                if (!IsFrozen && GameManager.Instance.IsPlaying)
                {
                    SpawnRow(_levelData.conveyorRows[_currentRowIndex]);
                    _currentRowIndex++;
                }
                yield return new WaitForSeconds(_spawnInterval * _config.columnCount);
            }
        }

        private void SpawnRow(ConveyorRowData rowData)
        {
            for (int i = 0; i < rowData.columns.Count && i < _config.columnCount; i++)
            {
                BlockColorType color = rowData.columns[i];
                if (color == BlockColorType.None) continue;

                float xOffset = (i - (_config.columnCount - 1) * 0.5f) * laneSpacing;
                Vector3 spawnPos = spawnPoint.position + new Vector3(xOffset, 0, 0);

                ConveyorBlock3D block = GetPooledBlock();
                block.transform.position = spawnPos;
                block.gameObject.SetActive(true);
                block.Initialize(color, GameManager.Instance.config.GetColor(color));
                block.OnDestroyed += HandleBlockDestroyed;
                _activeBlocks.Add(block);
            }
        }

        private ConveyorBlock3D GetPooledBlock()
        {
            foreach (var b in _blockPool)
            {
                if (!b.gameObject.activeSelf)
                {
                    _blockPool.Remove(b);
                    return b;
                }
            }
            ConveyorBlock3D newBlock = Instantiate(conveyorBlockPrefab, blockParent);
            return newBlock;
        }

        private void Update()
        {
            if (IsFrozen || !GameManager.Instance.IsPlaying) return;

            MoveBlocks();
            CheckDespawn();
        }

        private void MoveBlocks()
        {
            float delta = _speed * Time.deltaTime;
            foreach (var block in _activeBlocks)
            {
                if (block == null || !block.gameObject.activeSelf) continue;
                block.transform.Translate(Vector3.left * delta);
            }
        }

        private void CheckDespawn()
        {
            for (int i = _activeBlocks.Count - 1; i >= 0; i--)
            {
                var block = _activeBlocks[i];
                if (block == null || !block.gameObject.activeSelf) { _activeBlocks.RemoveAt(i); continue; }
                if (block.transform.position.x < despawnPoint.position.x)
                {
                    block.gameObject.SetActive(false);
                    _blockPool.Add(block);
                    _activeBlocks.RemoveAt(i);

                    // Blocks passing through counts as a loss condition
                    CheckLoseCondition();
                }
            }
        }

        private void HandleBlockDestroyed(ConveyorBlock3D block)
        {
            block.OnDestroyed -= HandleBlockDestroyed;
            _activeBlocks.Remove(block);
            _blockPool.Add(block);
            _blocksDestroyed++;
            CheckWinCondition();
        }

        private void CheckWinCondition()
        {
            if (_levelData.goalType == LevelGoalType.ClearAllBlocks)
            {
                if (_currentRowIndex >= _levelData.conveyorRows.Count && _activeBlocks.Count == 0)
                    GameManager.Instance?.TriggerWin();
            }
            else if (_levelData.goalType == LevelGoalType.ClearCount)
            {
                if (_blocksDestroyed >= _levelData.goalAmount)
                    GameManager.Instance?.TriggerWin();
            }
        }

        private void CheckLoseCondition()
        {
            // If blocks pass through without being destroyed, it's a fail
            // Optional: implement lives system
        }

        public void SetFrozen(bool frozen)
        {
            IsFrozen = frozen;
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            _speed = _config.conveyorSpeed * _levelData.conveyorSpeedMultiplier * multiplier;
        }

        public void BombColumn(int columnIndex)
        {
            for (int i = _activeBlocks.Count - 1; i >= 0; i--)
            {
                var block = _activeBlocks[i];
                if (block == null) continue;
                float xOffset = (columnIndex - (_config.columnCount - 1) * 0.5f) * laneSpacing;
                float targetX = spawnPoint.position.x + xOffset;
                if (Mathf.Abs(block.transform.position.x - targetX) < laneSpacing * 0.5f)
                    block.TakeHit();
            }
        }

        public void DestroyAllInRange()
        {
            var blocks = FireRange.Instance?.BlocksInRange;
            if (blocks == null) return;
            var toDestroy = new List<ConveyorBlock3D>(blocks);
            foreach (var b in toDestroy)
                b.TakeHit();
        }
    }
}
