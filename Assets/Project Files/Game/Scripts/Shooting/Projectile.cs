using UnityEngine;

namespace BlockShooter
{
    // Projectile moves via pure transform (no physics velocity) for frame-rate-independent
    // reliable homing. Rigidbody is kept kinematic for smooth interpolated rendering only.
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
        private float _speed;
        private ConveyorBlock3D _target;

        private Rigidbody _rb;
        private static readonly int ColorProp = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity    = false;
            _rb.isKinematic   = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.constraints   = RigidbodyConstraints.FreezeRotation;

            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius    = 0.12f;
        }

        public void Launch(BlockColorType colorType, float speed, ProjectilePool pool, Vector3 direction,
            ConveyorBlock3D target = null)
        {
            _colorType = colorType;
            _pool      = pool;
            _speed     = speed;
            _target    = target;
            _active    = true;

            ApplyColor(colorType);

            // Face initial direction
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized);

            CancelInvoke(nameof(ReturnToPool));
            Invoke(nameof(ReturnToPool), 5f); // safety timeout
        }

        private void Update()
        {
            if (!_active) return;

            // Target gone before arrival
            if (_target == null || _target.IsDestroyed)
            {
                ReturnToPool();
                return;
            }

            Vector3 toTarget = _target.transform.position - transform.position;
            float   dist     = toTarget.magnitude;
            float   step     = _speed * Time.deltaTime;

            // Arrived (or would overshoot this frame)
            if (step >= dist || dist < 0.1f)
            {
                transform.position = _target.transform.position;
                HandleHit();
                return;
            }

            // Pure transform movement — frame-rate-independent, no physics forces
            transform.position += toTarget.normalized * step;
            transform.rotation  = Quaternion.LookRotation(toTarget.normalized);
        }

        private void HandleHit()
        {
            if (!_active) return;
            _active = false;
            CancelInvoke(nameof(ReturnToPool));

            PlayHitFX();
            _target.TakeHit();
            _target = null;
            ReturnToPool();
        }

        private void PlayHitFX()
        {
            if (hitParticle == null) return;
            hitParticle.transform.SetParent(null);
            hitParticle.Play();
        }

        private void ApplyColor(BlockColorType colorType)
        {
            var config = GameManager.Instance?.config;
            var mat    = config?.GetMaterial(colorType);

            if (ballRenderer != null)
            {
                if (mat != null)
                {
                    ballRenderer.sharedMaterial = mat;
                    ballRenderer.SetPropertyBlock(null);
                }
                else
                {
                    Color c = config?.GetColor(colorType) ?? Color.white;
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor(ColorProp, c);
                    ballRenderer.SetPropertyBlock(mpb);
                }
            }

            if (trail != null)
            {
                Color c = config?.GetColor(colorType) ?? Color.white;
                trail.Clear();
                trail.startColor = c;
            }
        }

        private void ReturnToPool()
        {
            _active = false;
            _target = null;
            CancelInvoke(nameof(ReturnToPool));
            _pool?.Return(this);
        }

        private void OnDisable()
        {
            _active = false;
            _target = null;
            CancelInvoke(nameof(ReturnToPool));
        }
    }
}
