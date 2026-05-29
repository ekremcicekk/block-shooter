using System;
using UnityEngine;
using TMPro;
using DG.Tweening;

namespace BlockShooter
{
    public class ConveyorBlock : MonoBehaviour
    {
        [Header("Visuals")]
        public SpriteRenderer blockRenderer;
        public ParticleSystem destroyParticle;
        public TextMeshPro countText;

        private BlockColorType _colorType;
        private int _health = 1;
        private bool _isDestroyed;

        public BlockColorType ColorType => _colorType;
        public bool IsDestroyed => _isDestroyed;

        public event Action<ConveyorBlock> OnDestroyed;

        public void Initialize(BlockColorType colorType, int health = 1)
        {
            _colorType = colorType;
            _health = health;
            _isDestroyed = false;

            if (blockRenderer != null)
                blockRenderer.color = GameManager.Instance.config.GetColor(colorType);

            if (countText != null)
                countText.gameObject.SetActive(health > 1);

            UpdateCountText();
        }

        public void TakeHit()
        {
            if (_isDestroyed) return;

            _health--;
            UpdateCountText();

            transform.DOPunchScale(Vector3.one * 0.25f, 0.15f, 3, 0.5f);

            if (_health <= 0)
                Destroy();
        }

        private void Destroy()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;

            ScoreManager.Instance?.AddBlockDestroyed();

            if (destroyParticle != null)
            {
                destroyParticle.transform.SetParent(null);
                destroyParticle.Play();
            }

            OnDestroyed?.Invoke(this);
            gameObject.SetActive(false);
        }

        private void UpdateCountText()
        {
            if (countText != null && _health > 1)
                countText.text = _health.ToString();
        }

        private void OnDisable()
        {
            DOTween.Kill(transform);
        }
    }
}
