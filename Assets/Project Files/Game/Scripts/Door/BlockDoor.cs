using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

namespace BlockShooter
{
    public class BlockDoor : MonoBehaviour
    {
        [Header("Visuals")]
        public SpriteRenderer doorRenderer;
        public SpriteRenderer questionMarkRenderer;
        public TextMeshPro countText;
        public ParticleSystem spawnParticle;

        [Header("Exit Point")]
        public Transform exitPoint;

        [Header("Door Config")]
        public int blockCount = 5;
        public List<BlockColorType> spawnColors = new();

        private int _remainingBlocks;
        private List<BlockColorType> _availableColors;
        private bool _isOpen = true;

        /// <summary>Called by ShooterGrid.Initialize() when scanning pre-placed doors.</summary>
        public void Initialize()
        {
            _remainingBlocks = blockCount;
            _availableColors = spawnColors;
            UpdateCountText();
            CheckFrontPositionLoop();
        }

        private void CheckFrontPositionLoop()
        {
            InvokeRepeating(nameof(TrySpawnBlock), 0.5f, 0.8f);
        }

        private void TrySpawnBlock()
        {
            if (!_isOpen || _remainingBlocks <= 0 || !GameManager.Instance.IsPlaying) return;
            if (!IsFrontPositionEmpty()) return;

            SpawnBlock();
        }

        private bool IsFrontPositionEmpty()
        {
            // Check if exit point position has no ShooterBlock (3D physics)
            var hits = Physics.OverlapSphere(exitPoint != null ? exitPoint.position : transform.position, 0.4f);
            foreach (var h in hits)
                if (h.GetComponent<ShooterBlock>() != null) return false;
            return true;
        }

        private void SpawnBlock()
        {
            if (_availableColors == null || _availableColors.Count == 0) return;

            BlockColorType randomColor = _availableColors[Random.Range(0, _availableColors.Count)];
            ShooterGrid.Instance?.AddBlock(exitPoint.position, randomColor);

            if (spawnParticle != null) spawnParticle.Play();

            _remainingBlocks--;
            UpdateCountText();

            transform.DOPunchScale(Vector3.one * 0.1f, 0.2f, 3, 0.5f);

            if (_remainingBlocks <= 0)
                CloseDoor();
        }

        private void CloseDoor()
        {
            _isOpen = false;
            CancelInvoke(nameof(TrySpawnBlock));

            transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack).OnComplete(() =>
                gameObject.SetActive(false));
        }

        private void UpdateCountText()
        {
            if (countText != null)
                countText.text = $"x{_remainingBlocks}";
        }

        private void OnDisable()
        {
            DOTween.Kill(transform);
            CancelInvoke(nameof(TrySpawnBlock));
        }
    }
}
