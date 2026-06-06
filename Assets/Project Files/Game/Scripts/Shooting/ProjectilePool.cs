using UnityEngine;

namespace BlockShooter
{
    public class ProjectilePool : MonoBehaviour
    {
        public static ProjectilePool Instance { get; private set; }

        [Header("Settings")]
        public Projectile projectilePrefab;
        public int poolSize = 40;

        private ObjectPool<Projectile> _pool;

        /// <summary>
        /// Number of projectiles currently in flight (launched but not yet returned to pool).
        /// </summary>
        public int ActiveCount { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (projectilePrefab != null)
                _pool = new ObjectPool<Projectile>(projectilePrefab, poolSize, transform);
        }

        public Projectile Get(Vector3 position)
        {
            if (_pool == null)
            {
                Debug.LogWarning("[ProjectilePool] Projectile prefab not assigned!");
                return null;
            }
            ActiveCount++;
            return _pool.Get(position);
        }

        public void Return(Projectile projectile)
        {
            if (ActiveCount > 0) ActiveCount--;
            _pool?.Return(projectile);

            // If no more projectiles are in flight, give the depletion check another chance.
            // This resolves the race condition where the last shooter fires its last shot and
            // Deplete() is called while the projectile is still mid-air.
            if (ActiveCount == 0)
            {
                ShooterGrid.Instance?.NotifyAllProjectilesLanded();
            }
        }
    }
}
