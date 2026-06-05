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
        private bool _isVisible = true;
        private float _launchY;

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

            if (hitParticle != null)
            {
                hitParticle.gameObject.SetActive(false);
            }
        }

        public void Launch(BlockColorType colorType, float speed, ProjectilePool pool, Vector3 direction,
            ConveyorBlock3D target = null, bool isVisible = true)
        {
            _colorType = colorType;
            _pool      = pool;
            _speed     = speed;
            _target    = target;
            _active    = true;
            _isVisible = isVisible;
            _launchY   = transform.position.y;

            ApplyColor(colorType);

            if (ballRenderer != null) ballRenderer.enabled = isVisible;
            if (trail != null)
            {
                trail.enabled = isVisible;
                if (!isVisible) trail.Clear();
            }

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

            Vector3 targetPos = _target.transform.position + Vector3.up * 0.3f;

            Vector3 toTarget = targetPos - transform.position;
            float   dist     = toTarget.magnitude;
            float   step     = _speed * Time.deltaTime;

            // Arrived (or would overshoot this frame)
            if (step >= dist || dist < 0.1f)
            {
                transform.position = targetPos;
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

            if (_isVisible) PlayHitFX();
            _target.TakeHit();
            _target = null;
            ReturnToPool();
        }

        private void PlayHitFX()
        {
            if (hitParticle == null || _target == null) return;
            
            Vector3 spawnPos = _target.transform.position;
            spawnPos.y = 0.6f;
            var fx = Instantiate(hitParticle, spawnPos, Quaternion.identity);
            fx.gameObject.SetActive(true);
            fx.Play();
            Destroy(fx.gameObject, fx.main.duration + fx.main.startLifetime.constantMax);
        }

        private void ApplyColor(BlockColorType colorType)
        {
            if (trail != null)
            {
                trail.Clear();
            }
        }

        private void ReturnToPool()
        {
            _active = false;
            // Release the claim so another shooter can target this block if the
            // projectile timed out or was cancelled before landing.
            if (_target != null && !_target.IsDestroyed)
                _target.SetTargeted(false);
            _target = null;
            CancelInvoke(nameof(ReturnToPool));
            _pool?.Return(this);
        }

        private void OnDisable()
        {
            _active = false;
            if (_target != null && !_target.IsDestroyed)
                _target.SetTargeted(false);
            _target = null;
            CancelInvoke(nameof(ReturnToPool));
        }
    }
}
