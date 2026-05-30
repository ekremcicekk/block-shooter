using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlockShooter
{
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Levels")]
        public LevelData[] levels;

        [Header("Scene References")]
        public ShooterGrid shooterGrid;

        [Header("Track Spawn Point")]
        [Tooltip("Where the level track prefab is instantiated")]
        public Transform trackSpawnParent;
        [Tooltip("Local position offset applied to the instantiated track")]
        public Vector3 trackSpawnOffset = new Vector3(0f, 0f, 6f);

        private LevelData _currentLevel;
        private GameObject _activeTrackInstance;

        public LevelData CurrentLevel => _currentLevel;
        public int CurrentLevelIndex => SaveManager.CurrentLevel;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            LoadCurrentLevel();
        }

        public void LoadCurrentLevel()
        {
            int raw = SaveManager.CurrentLevel - 1;
            int index = levels.Length > 0 ? raw % levels.Length : 0;
            _currentLevel = levels[Mathf.Clamp(index, 0, levels.Length - 1)];
            ApplyLevel(_currentLevel);
        }

        private void ApplyLevel(LevelData data)
        {
            // Destroy previous track
            if (_activeTrackInstance != null)
                Destroy(_activeTrackInstance);

            // Instantiate this level's track prefab
            if (data.trackPrefab != null)
            {
                Transform parent = trackSpawnParent != null ? trackSpawnParent : transform;
                _activeTrackInstance = Instantiate(data.trackPrefab, parent);
                _activeTrackInstance.transform.localPosition = trackSpawnOffset;

                // Apply speed multiplier
                var pathCtrl = _activeTrackInstance.GetComponent<ConveyorPathController>();
                if (pathCtrl != null)
                    pathCtrl.speed *= data.conveyorSpeedMultiplier;
            }
            else
            {
                Debug.LogWarning($"[LevelManager] Level '{data.levelName}' has no trackPrefab assigned!");
            }

            // Setup shooter grid
            if (shooterGrid != null)
                shooterGrid.Initialize(data);
        }

        public void RestartLevel()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void LoadNextLevel()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void LoadMainMenu()
        {
            SceneManager.LoadScene(0);
        }
    }
}
