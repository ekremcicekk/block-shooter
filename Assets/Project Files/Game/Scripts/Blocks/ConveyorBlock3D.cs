using System;
using UnityEngine;
using DG.Tweening;

namespace BlockShooter
{
    /// <summary>
    /// A single 3D colored block sitting on the conveyor track.
    /// Destroyed when hit by a matching-color projectile.
    /// </summary>
    public class ConveyorBlock3D : MonoBehaviour
    {
        [Header("Visuals")]
        public MeshRenderer blockRenderer;
        public ParticleSystem destroyParticle;

        private BlockColorType _colorType;
        private bool _isDestroyed;
        private static readonly int ColorProp = Shader.PropertyToID("_BaseColor");

        public BlockColorType ColorType => _colorType;
        public bool IsDestroyed => _isDestroyed;

        public event Action<ConveyorBlock3D> OnDestroyed;

        public void Initialize(BlockColorType colorType, Color color)
        {
            _colorType = colorType;
            _isDestroyed = false;

            if (blockRenderer != null)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(ColorProp, color);
                blockRenderer.SetPropertyBlock(mpb);
            }
        }

        public void TakeHit()
        {
            if (_isDestroyed) return;
            DestroyBlock();
        }

        private void DestroyBlock()
        {
            _isDestroyed = true;

            ScoreManager.Instance?.AddBlockDestroyed();

            if (destroyParticle != null)
            {
                var fx = Instantiate(destroyParticle, transform.position, Quaternion.identity);
                var main = fx.main;
                main.startColor = GameManager.Instance.config.GetColor(_colorType);
                fx.Play();
                Destroy(fx.gameObject, 2f);
            }

            OnDestroyed?.Invoke(this);

            transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack)
                .OnComplete(() => gameObject.SetActive(false));
        }

        private void OnDisable()
        {
            DOTween.Kill(transform);
        }
    }
}
