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
                Debug.LogWarning("[ProjectilePool] Prefab atanmamış!");
                return null;
            }
            return _pool.Get(position);
        }

        public void Return(Projectile projectile)
        {
            _pool?.Return(projectile);
        }
    }
}
