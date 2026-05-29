using UnityEngine;

namespace BlockShooter
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class Projectile : MonoBehaviour
    {
        [Header("Visuals")]
        public MeshRenderer ballRenderer;
        public TrailRenderer trail;
        public ParticleSystem hitParticle;

        private BlockColorType _colorType;
        private bool _active;
        private ProjectilePool _pool;
        private Rigidbody _rb;
        private float _speed;
        private ConveyorBlock3D _target; // homing target

        private static readonly int ColorProp = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.isKinematic = false;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;

            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.12f;
        }

        public void Launch(BlockColorType colorType, float speed, ProjectilePool pool, Vector3 direction,
            ConveyorBlock3D target = null)
        {
            _colorType = colorType;
            _pool      = pool;
            _speed     = speed;
            _target    = target;
            _active    = true;

            Color c = GameManager.Instance.config.GetColor(colorType);
            if (ballRenderer != null)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(ColorProp, c);
                ballRenderer.SetPropertyBlock(mpb);
            }
            if (trail != null) { trail.Clear(); trail.startColor = c; }

            _rb.linearVelocity = direction.normalized * speed;
            Invoke(nameof(ReturnToPool), 4f);
        }

        private void Update()
        {
            if (!_active || _target == null || _target.IsDestroyed) return;

            // Steer toward moving target each frame — guarantees hit regardless of conveyor speed
            Vector3 dir = (_target.transform.position - transform.position).normalized;
            _rb.linearVelocity = dir * _speed;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_active) return;
            if (!other.TryGetComponent<ConveyorBlock3D>(out var block)) return;
            if (block.ColorType != _colorType) return;

            _active = false;
            _rb.linearVelocity = Vector3.zero;
            CancelInvoke(nameof(ReturnToPool));

            if (hitParticle != null)
            {
                hitParticle.transform.SetParent(null);
                hitParticle.Play();
            }

            block.TakeHit();
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            _active = false;
            _target = null;
            _rb.linearVelocity = Vector3.zero;
            _pool?.Return(this);
        }

        private void OnDisable()
        {
            _active = false;
            _target = null;
            CancelInvoke(nameof(ReturnToPool));
            if (_rb != null) _rb.linearVelocity = Vector3.zero;
        }
    }
}
