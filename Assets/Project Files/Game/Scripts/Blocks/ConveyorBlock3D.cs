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

        private BlockColorType _colorType;
        private bool _isDestroyed;
        private static readonly int ColorProp = Shader.PropertyToID("_BaseColor");

        public BlockColorType ColorType      => _colorType;
        public bool IsDestroyed              => _isDestroyed;
        public bool IsTargeted               { get; private set; }
        public bool HasEnteredFireRange      { get; private set; }

        public void SetTargeted(bool v)      => IsTargeted = v;
        public void MarkEnteredFireRange()   => HasEnteredFireRange = true;

        [HideInInspector] public Vector3 transitionOffset = Vector3.zero;
        [HideInInspector] public Quaternion transitionRotOffset = Quaternion.identity;

        // Serialized so the Level Editor can bake row/lane into the prefab hierarchy.
        [SerializeField] private int _rowIndex;
        [SerializeField] private int _laneIndex;
        public int RowIndex  => _rowIndex;
        public int LaneIndex => _laneIndex;

        public void SetGroupIndex(int row, int lane) { _rowIndex = row; _laneIndex = lane; }

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
            _colorType   = colorType;
            _isDestroyed = false;

            if (blockRenderer != null)
            {
                var mat = GameManager.Instance?.config?.GetMaterial(colorType);
                if (mat != null)
                {
                    blockRenderer.sharedMaterial = mat;
                    blockRenderer.SetPropertyBlock(null);
                }
                else
                {
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor(ColorProp, color);
                    blockRenderer.SetPropertyBlock(mpb);
                }
            }
        }

        public void TakeHit()
        {
            if (_isDestroyed) return;
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

            // Decrement freeze counts of active frozen blocks in grid (any color)
            if (ShooterGrid.Instance != null)
            {
                var activeBlocks = ShooterGrid.Instance.GetActiveBlocks();
                for (int i = 0; i < activeBlocks.Count; i++)
                {
                    var sb = activeBlocks[i];
                    if (sb != null && sb.TryGetComponent<FreezeBlockFeature>(out var f))
                    {
                        if (f.isFrozen)
                        {
                            f.DecrementCount();
                        }
                    }
                }
            }

            OnDestroyed?.Invoke(this);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.CheckWinCondition();
                GameManager.Instance.CheckFailCondition($"BlockDestroyed({_colorType})");
            }

            transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack)
                .OnComplete(() => gameObject.SetActive(false));
        }

        private void Update()
        {
            if (!_isDestroyed && !HasEnteredFireRange && FireRange.Instance != null)
            {
                if (FireRange.Instance.GetBounds().Contains(transform.position))
                {
                    FireRange.Instance.RegisterBlockManually(this);
                }
            }
        }

        private void OnDisable()
        {
            DOTween.Kill(transform);
        }
    }
}
