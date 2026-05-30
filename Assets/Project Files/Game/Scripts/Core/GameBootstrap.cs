using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Entry point — ensures all required scene-level singletons are present.
    /// FireRange, ShooterGrid, and SlotSystem live inside the level prefab and are
    /// validated by LevelRoot.Initialize() instead.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Required Scene References")]
        public GameManager    gameManager;
        public LevelManager   levelManager;
        public ScoreManager   scoreManager;
        public ProjectilePool projectilePool;
        public BoosterManager boosterManager;

        private void Awake()
        {
#if UNITY_EDITOR
            if (gameManager    == null) Debug.LogError("[Bootstrap] GameManager reference missing!");
            if (levelManager   == null) Debug.LogError("[Bootstrap] LevelManager reference missing!");
            if (projectilePool == null) Debug.LogError("[Bootstrap] ProjectilePool reference missing!");
#endif
        }
    }
}
