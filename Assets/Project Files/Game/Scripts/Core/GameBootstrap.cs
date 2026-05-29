using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Entry point - ensures all required singletons and systems are initialized in correct order.
    /// Attach this to a single Bootstrap GameObject in the scene.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Required References")]
        public GameManager gameManager;
        public LevelManager levelManager;
        public ScoreManager scoreManager;
        public ProjectilePool projectilePool;
        public FireRange fireRange;
        public BoosterManager boosterManager;

        private void Awake()
        {
            ValidateReferences();
        }

        private void ValidateReferences()
        {
#if UNITY_EDITOR
            if (gameManager == null) Debug.LogError("[Bootstrap] GameManager reference missing!");
            if (levelManager == null) Debug.LogError("[Bootstrap] LevelManager reference missing!");
            if (projectilePool == null) Debug.LogError("[Bootstrap] ProjectilePool reference missing!");
            if (fireRange == null) Debug.LogError("[Bootstrap] FireRange reference missing!");
#endif
        }
    }
}
