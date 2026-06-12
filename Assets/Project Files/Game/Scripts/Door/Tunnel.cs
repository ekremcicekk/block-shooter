using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

namespace BlockShooter
{
    public class Tunnel : MonoBehaviour
    {
        [Header("Visuals")]
        public MeshRenderer tunnelMesh;
        public TextMeshPro countText;
        public ParticleSystem spawnParticle;

        [Header("Tunnel Config")]
        public GridDirection direction = GridDirection.Down;
        public List<TunnelSequenceItem> spawnSequence = new();
        public int totalBlockCount = 5;

        [HideInInspector]
        public int col, row;

        public bool HasBlocksRemaining => _isActive && _remainingBlocks > 0;

        private int _remainingBlocks;
        private List<TunnelSequenceItem> _runtimeSequence = new();
        private bool _isActive = true;
        private float _cellSize = 1.2f;

        private float _checkCooldown = 0.1f; // Check space every 100ms for near-instant spawning
        private float _checkTimer = 0f;

        public void Initialize()
        {
            // Deep copy sequence list for runtime manipulation
            _runtimeSequence = new List<TunnelSequenceItem>();
            if (spawnSequence != null)
            {
                foreach (var item in spawnSequence)
                {
                    _runtimeSequence.Add(new TunnelSequenceItem 
                    { 
                        color = item.color, 
                        count = item.count 
                    });
                }
            }

            // Remaining blocks count is the number of items in the sequence!
            _remainingBlocks = _runtimeSequence.Count;

            UpdateRemainingVisuals();
        }

        private void Update()
        {
            if (!_isActive || _remainingBlocks <= 0 || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

            _checkTimer += Time.deltaTime;
            if (_checkTimer >= _checkCooldown)
            {
                _checkTimer = 0f;
                if (IsFrontPositionEmpty())
                {
                    SpawnBlock();
                }
            }
        }

        private bool IsFrontPositionEmpty()
        {
            // Calculate front position based on direction and cell size
            Vector3 offset = Vector3.zero;
            switch (direction)
            {
                case GridDirection.Down: offset = new Vector3(0, 0, -_cellSize); break;
                case GridDirection.Up: offset = new Vector3(0, 0, _cellSize); break;
                case GridDirection.Left: offset = new Vector3(-_cellSize, 0, 0); break;
                case GridDirection.Right: offset = new Vector3(_cellSize, 0, 0); break;
            }
            Vector3 frontPosition = transform.position + offset;

            // Check if there is any ShooterBlock at the front position (radius 0.4f)
            var hits = Physics.OverlapSphere(frontPosition, 0.4f);
            foreach (var h in hits)
            {
                if (h.GetComponent<ShooterBlock>() != null)
                {
                    return false;
                }
            }
            return true;
        }

        private void SpawnBlock()
        {
            if (_runtimeSequence == null || _runtimeSequence.Count == 0) return;

            // Take the first item in the sequence
            var currentItem = _runtimeSequence[0];
            BlockColorType colorToSpawn = currentItem.color;
            int shotCountToSpawn = currentItem.count; // item.count represents the shotCount!

            // Remove it from the sequence (one block spawned per sequence item)
            _runtimeSequence.RemoveAt(0);
            _remainingBlocks = _runtimeSequence.Count;

            // Calculate target position and target col/row
            int spawnCol = col;
            int spawnRow = row;
            Vector3 offset = Vector3.zero;
            switch (direction)
            {
                case GridDirection.Down: spawnRow--; offset = new Vector3(0, 0, -_cellSize); break;
                case GridDirection.Up: spawnRow++; offset = new Vector3(0, 0, _cellSize); break;
                case GridDirection.Left: spawnCol--; offset = new Vector3(-_cellSize, 0, 0); break;
                case GridDirection.Right: spawnCol++; offset = new Vector3(_cellSize, 0, 0); break;
            }
            Vector3 spawnPosition = transform.position + offset;

            // Spawn the block via ShooterGrid, starting from the tunnel's transform position
            if (ShooterGrid.Instance != null)
            {
                ShooterGrid.Instance.AddBlock(spawnPosition, colorToSpawn, shotCountToSpawn, spawnCol, spawnRow, transform.position);
            }

            if (spawnParticle != null)
            {
                spawnParticle.Play();
            }

            UpdateRemainingVisuals();

            // Punch scale visual feedback
            transform.DOPunchScale(Vector3.one * 0.1f, 0.2f, 3, 0.5f);

            if (_remainingBlocks <= 0)
            {
                StopSpawning();
            }
        }

        private void StopSpawning()
        {
            _isActive = false;
        }

        private void UpdateRemainingVisuals()
        {
            // Update Text (listed block count)
            if (countText != null)
            {
                countText.text = $"{Mathf.Max(0, _remainingBlocks)}";
            }

            // Update Materials
            if (tunnelMesh != null && tunnelMesh.sharedMaterials.Length > 0)
            {
                Material nextColorMat = null;

                if (_remainingBlocks > 0 && _runtimeSequence.Count > 0)
                {
                    BlockColorType nextColor = _runtimeSequence[0].color;
                    if (GameManager.Instance != null && GameManager.Instance.config != null)
                    {
                        nextColorMat = GameManager.Instance.config.GetMaterial(nextColor);
                    }
                }

                // Update Element 0 material
                if (nextColorMat != null)
                {
                    var mats = tunnelMesh.sharedMaterials;
                    mats[0] = nextColorMat;
                    tunnelMesh.sharedMaterials = mats;
                }
            }
        }

        private void OnDisable()
        {
            DOTween.Kill(transform);
        }
    }
}
