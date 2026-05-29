using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;

namespace BlockShooter
{
    public class ShooterBlock : MonoBehaviour
    {
        [Header("Visuals")]
        public MeshRenderer blockRenderer;
        public MeshRenderer glowRenderer;
        public TextMeshPro shotCountText;
        public ParticleSystem muzzleFlash;
        public ParticleSystem depletedParticle;

        [Header("Shoot Point")]
        public Transform shootPoint;

        private BlockColorType _colorType;
        private int _shotCount;
        private bool _isShooting;
        private bool _isDepleted;
        private Coroutine _shootCoroutine;
        private bool _isRainbowMode;

        private static readonly int ColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionProp = Shader.PropertyToID("_EmissionColor");

        public BlockColorType ColorType => _colorType;
        public bool IsDepleted => _isDepleted;
        public int GridColumn { get; private set; }
        public int GridRow { get; private set; }

        public void Initialize(BlockColorType colorType, int shotCount, int col, int row)
        {
            _colorType = colorType;
            _shotCount = shotCount;
            _isDepleted = false;
            _isShooting = false;
            GridColumn = col;
            GridRow = row;

            ApplyColor();
            UpdateShotCountUI();
        }

        private void ApplyColor()
        {
            Color c = GameManager.Instance.config.GetColor(_colorType);
            if (blockRenderer != null)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(ColorProp, c);
                blockRenderer.SetPropertyBlock(mpb);
            }
            if (glowRenderer != null)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(ColorProp, new Color(c.r, c.g, c.b, 0.4f));
                mpb.SetColor(EmissionProp, c * 0.6f);
                glowRenderer.SetPropertyBlock(mpb);
            }
        }

        private void Update()
        {
            if (!GameManager.Instance.IsPlaying || _isDepleted) return;

            bool hasTarget = _isRainbowMode
                ? FireRange.Instance != null && FireRange.Instance.BlocksInRange.Count > 0
                : FireRange.Instance != null && FireRange.Instance.HasTargetFor(_colorType);

            if (hasTarget && !_isShooting) StartShooting();
            else if (!hasTarget && _isShooting) StopShooting();
        }

        private void StartShooting()
        {
            _isShooting = true;
            _shootCoroutine = StartCoroutine(ShootRoutine());
        }

        private void StopShooting()
        {
            _isShooting = false;
            if (_shootCoroutine != null) { StopCoroutine(_shootCoroutine); _shootCoroutine = null; }
        }

        private IEnumerator ShootRoutine()
        {
            float fireRate = GameManager.Instance.config.fireRate;
            while (_isShooting && !_isDepleted)
            {
                FireProjectile();
                yield return new WaitForSeconds(fireRate);
            }
        }

        private void FireProjectile()
        {
            if (ProjectilePool.Instance == null) return;

            BlockColorType targetColor = _isRainbowMode ? GetAnyActiveColor() : _colorType;

            // Find closest target for direction
            ConveyorBlock3D target = FireRange.Instance?.GetClosestTarget(targetColor,
                shootPoint != null ? shootPoint.position : transform.position);

            Vector3 spawnPos = shootPoint != null ? shootPoint.position : transform.position + Vector3.up * 0.3f;
            Vector3 dir = target != null
                ? (target.transform.position - spawnPos).normalized
                : Vector3.forward;

            Projectile proj = ProjectilePool.Instance.Get(spawnPos);
            proj.Launch(targetColor, GameManager.Instance.config.projectileSpeed, ProjectilePool.Instance, dir);

            if (muzzleFlash != null) muzzleFlash.Play();
            transform.DOPunchScale(Vector3.one * 0.08f, 0.1f, 1, 0.5f);

            _shotCount--;
            UpdateShotCountUI();
            if (_shotCount <= 0) Deplete();
        }

        private BlockColorType GetAnyActiveColor()
        {
            foreach (var b in FireRange.Instance.BlocksInRange)
                return b.ColorType;
            return _colorType;
        }

        private void Deplete()
        {
            _isDepleted = true;
            StopShooting();
            if (depletedParticle != null) depletedParticle.Play();

            if (blockRenderer != null)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(ColorProp, Color.gray);
                blockRenderer.SetPropertyBlock(mpb);
            }
            if (glowRenderer != null) glowRenderer.gameObject.SetActive(false);
            if (shotCountText != null) shotCountText.text = "0";

            ShooterGrid.Instance?.OnBlockDepleted(this);
        }

        private void UpdateShotCountUI()
        {
            if (shotCountText != null)
                shotCountText.text = _shotCount.ToString();
        }

        public void SetRainbowMode(bool active)
        {
            _isRainbowMode = active;
            if (!active) ApplyColor();
            else if (blockRenderer != null)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(ColorProp, Color.white);
                blockRenderer.SetPropertyBlock(mpb);
            }
        }

        public void RefillShots(int amount)
        {
            _shotCount += amount;
            if (_isDepleted && _shotCount > 0) { _isDepleted = false; ApplyColor(); }
            UpdateShotCountUI();
        }

        private void OnDisable()
        {
            DOTween.Kill(transform);
            StopShooting();
        }
    }
}
