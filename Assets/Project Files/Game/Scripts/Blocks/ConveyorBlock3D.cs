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
        public bool IsTargeted  { get; private set; }

        public void SetTargeted(bool v) => IsTargeted = v;

        // Position within the BlockGroup — used by FireRange to target in row/lane order
        public int RowIndex  { get; private set; }
        public int LaneIndex { get; private set; }

        public void SetGroupIndex(int row, int lane) { RowIndex = row; LaneIndex = lane; }

        public event Action<ConveyorBlock3D> OnDestroyed;

        private void Awake()
        {
            // FireRange uses OnTriggerEnter — block needs a Collider + kinematic Rigidbody
            if (GetComponent<Collider>() == null)
            {
                var col = gameObject.AddComponent<BoxCollider>();
                col.size = Vector3.one * 0.9f;
            }
            if (GetComponent<Rigidbody>() == null)
            {
                var rb = gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity  = false;
            }
        }

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
            Debug.Log($"[Block:{_colorType}] HIT → Row:{RowIndex} Lane:{LaneIndex}");
            DestroyBlock();
        }

        /// <summary>Externally triggered destruction (e.g. Bomb booster).</summary>
        public void TriggerDestroy()
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
